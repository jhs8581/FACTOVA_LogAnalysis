using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfColor = System.Windows.Media.Color;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfBrushes = System.Windows.Media.Brushes;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Services;
using FACTOVA_LogAnalysis.Helpers;
using System.Diagnostics;
using ClosedXML.Excel;

namespace FACTOVA_LogAnalysis
{
    public partial class MainWindow : Window
    {
        private static readonly Regex LogStartRegex = new Regex(@"^\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}\]", RegexOptions.Compiled | RegexOptions.Multiline);
        
        // 검색 다이얼로그 - SearchFilterManager로 관리되지만 참조 필요
        private FindDialog? _findDialog;
        
        // 기본 상태 관리 - 필요한 것만 유지
        public string LastSearchText { get; set; } = "";
        private bool _isInitializing = true;
        private bool _isTabSwitching = false;
        private string _logFolderPath = "";

        // 마지막으로 선택된(활성) DataGrid 이름 (단순 메모리)
        private string? _lastSelectedDataGridName;

        // 핵심 서비스 인스턴스들 - 기존
        private readonly RedBusinessListManager _redBusinessListManager;
        private readonly WorkLogService _workLogService;

        private readonly LogFileManager _logFileManager;
        private readonly LogSummaryService _logSummaryService;
        private readonly SearchService _searchService;

        // 새로운 서비스 인스턴스들 - 단계별 리팩토링 결과
        private readonly LogLoadingService _logLoadingService;        // 1단계: 로그 로딩
        private readonly DataGridManager _dataGridManager;            // 2단계: DataGrid 관리
        private readonly LayoutManager _layoutManager;               // 3단계: 레이아웃 관리
        private readonly TextFormattingManager _textFormattingManager; // 3단계: 텍스트 포맷팅
        private readonly SearchFilterManager _searchFilterManager;    // 4단계: 검색/필터
        private readonly EventHandlerManager _eventHandlerManager;    // 5단계: 이벤트 핸들러

        // 검색 관련 - 최소한 유지
        private List<TextRange> _matchRanges = new List<TextRange>();
        private List<int> _textBoxMatches = new List<int>();
        private int _currentMatchIndex = -1;

        // 로그 데이터 - DataGridManager에서 주로 관리
        private ObservableCollection<LogSummaryItem> _summaryItems = new ObservableCollection<LogSummaryItem>();
        private ObservableCollection<LogLineItem> _mainLogLines = new ObservableCollection<LogLineItem>();
        private bool _useDataGridMode = false;
        
        // 활성 TextBox 참조 - Format Toolbar에서 사용
        private System.Windows.Controls.TextBox? _activeTextBox;
        
        // Content 토글 상태 관리 - DataGridManager에서 관리
        private bool _isContentExpanded = true;
        private DataGridLength _originalContentWidth = new DataGridLength(1, DataGridLengthUnitType.Star);
        private bool _isAllContentExpanded = true;

        // 멀티선택 필터 컬렉션들
        private ObservableCollection<FilterItem> _dataBusinessFilterItems = new ObservableCollection<FilterItem>();
        private ObservableCollection<FilterItem> _eventMsgIdFilterItems = new ObservableCollection<FilterItem>();
        private ObservableCollection<FilterItem> _exceptionBusinessFilterItems = new ObservableCollection<FilterItem>();

        // 애플리케이션 설정
        private AppSettings _appSettings = new AppSettings();

        // 4분할/4탭 뷰 전환 상태
        private bool _isFourPanelMode = true;
        // Text tab view mode
        private bool _isTextFourPanelMode = true;

        // 프로그램 생존 확인용 독립적인 타이머
        private System.Windows.Threading.DispatcherTimer? _aliveTimer;

        // 로드 진행 시간 표시용 타이머 (System.Threading.Timer로 UI 스레드와 완전 분리)
        private System.Threading.Timer? _loadProgressTimer;
        private DateTime _loadStartTime;
        private volatile bool _isLoading = false;  // volatile로 스레드 안전성 보장

        public MainWindow()
        {
            InitializeComponent();
            
            // ⚡ 성능 테스트 실행 (개발/디버깅용)
            // 100만 줄 테스트 (시간 오래 걸림)
            // PerformanceTest.RunLogParsingTest();
            
            // 빠른 테스트 (1만 줄)
            //PerformanceTest.RunQuickTest();
            
            // 애플리케이션 설정 로드
            _appSettings = AppSettings.Load();

            // =======================================================
            // 서비스 초기화 - 6단계 모듈화 완료된 아키텍처
            // =======================================================
            
            // 기존 핵심 서비스들
            _redBusinessListManager = new RedBusinessListManager();
            _workLogService = new WorkLogService();
            _logFileManager = new LogFileManager();
            _logSummaryService = new LogSummaryService();
            _searchService = new SearchService();
            
            // 1단계: 로그 로드 서비스
            _logLoadingService = new LogLoadingService(_logFileManager, _workLogService);
            
            // 2단계: DataGrid 관리 서비스  
            _dataGridManager = new DataGridManager(_workLogService, _logLoadingService, _redBusinessListManager);
            
            // 3단계: UI 관련 서비스들
            _layoutManager = new LayoutManager(_workLogService);
            _textFormattingManager = new TextFormattingManager(_workLogService);
            
            // 4단계: 검색/필터 관리 서비스 초기화
            _searchFilterManager = new SearchFilterManager(_workLogService, _searchService);
            
            // 5단계: 이벤트 핸들러 관리 서비스 초기화
            _eventHandlerManager = new EventHandlerManager(_workLogService, _searchFilterManager);
            
            // =======================================================
            
            SetupEventHandlers();

            _workLogService.AddLog("FACTOVA logs analysis tool started", WorkLogType.Success);
            
            InitializeSearchModeComboBox();
            InitializeTimeTextBoxes();
            InitializeUIManagers(); // UI 매니저들 초기화
            RestoreOrSetDefaultLogFolderPath();

            // After log folder restored, set up date control depending on folder
            TryConfigureDateSelectorFromLogFolder();

            // DataGrid 초기화 - DataGridManager 사용
            InitializeDataGrids();
            InitializeRedBusinessList();

            // Initialize multi-select combo boxes for filters and populate their items
            try
            {
                InitializeMultiSelectComboBoxes();
                UpdateBusinessNameFilters();
            }
            catch { }

            var redBusinessDataGrid = FindName("redBusinessDataGrid") as DataGrid;
            if (redBusinessDataGrid != null)
            {
                redBusinessDataGrid.ItemsSource = _redBusinessListManager.Items;
                redBusinessDataGrid.CellEditEnding += RedBusinessDataGrid_CellEditEnding;
            }

            LoadRedBusinessList();

            this.Closing += MainWindow_Closing;

            // TextColorComboBox.SelectedIndex = 1; // Text color control removed from UI; skip selection
            var execTimeFilterTextBox = FindName("execTimeFilterTextBox") as WpfTextBox;
            if (execTimeFilterTextBox != null)
            {
                execTimeFilterTextBox.Text = "0.5";
            }

            // Grid Splitter 위치 복원
            RestoreGridSplitterPositions();

            // Grid Splitter 위치 변경 이벤트 추가
            SetupGridSplitterEvents();

            // Apply saved font sizes from settings
            try
            {
                // Set toolbar textboxes to saved values
                if (FontSizeTextBox != null)
                    FontSizeTextBox.Text = _appSettings.TextFontSize.ToString();
                if (DataGridFontSizeTextBox != null)
                    DataGridFontSizeTextBox.Text = _appSettings.DataGridFontSize.ToString();
                
                // ✨ Content Cell Height TextBox 초기화
                var contentHeightTextBox = FindName("ContentCellHeightTextBox") as System.Windows.Controls.TextBox;
                if (contentHeightTextBox != null)
                    contentHeightTextBox.Text = _appSettings.ContentCellMaxHeight.ToString();
                
                // ✨ 통합 로그 Content Cell Height TextBox 초기화
                var unifiedContentHeightTextBox = FindName("UnifiedContentCellHeightTextBox") as System.Windows.Controls.TextBox;
                if (unifiedContentHeightTextBox != null)
                    unifiedContentHeightTextBox.Text = _appSettings.ContentCellMaxHeight.ToString();

                // Apply text font size to text boxes and work log
                var textNames = new[] { "dataLogTextBox", "eventLogTextBox", "debugLogTextBox", "exceptionLogTextBox", "execTimeTextBox" };
                foreach (var name in textNames)
                {
                    var tb = FindName(name) as System.Windows.Controls.TextBox;
                    if (tb != null)
                        tb.FontSize = _appSettings.TextFontSize;
                }

                var rtb = FindName("workLogTextBox") as System.Windows.Controls.RichTextBox;
                if (rtb != null)
                    rtb.FontSize = _appSettings.TextFontSize;

                // Apply DataGrid font sizes to known DataGrids
                var gridNames = new[] {
                    "dataLogDataGrid", "eventLogDataGrid", "debugLogDataGrid", "exceptionLogDataGrid"
                };

                foreach (var name in gridNames)
                {
                    var dg = FindDataGridByName(name);
                    if (dg == null)
                        continue;

                    ApplyFontToDataGridInstance(dg, _appSettings.DataGridFontSize);
                }

                // 💡 통합 로그 DataGrid 폰트 크기 적용
                var unifiedGrid = FindName("unifiedLogDataGrid") as DataGrid;
                if (unifiedGrid != null)
                {
                    ApplyFontToDataGridInstance(unifiedGrid, _appSettings.UnifiedLogFontSize);
                    
                    // 💡 TextBox도 초기화
                    var unifiedFontSizeTextBox = FindName("UnifiedLogFontSizeTextBox") as System.Windows.Controls.TextBox;
                    if (unifiedFontSizeTextBox != null)
                    {
                        unifiedFontSizeTextBox.Text = _appSettings.UnifiedLogFontSize.ToString();
                    }
                    
                    _workLogService.AddLog($"💡 통합 로그 폰트 크기 적용: {_appSettings.UnifiedLogFontSize}", WorkLogType.Info);
                }

                _workLogService.AddLog("💾 저장된 폰트 크기 및 Content 높이 적용 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ 저장된 폰트 적용 오류: {ex.Message}", WorkLogType.Error);
            }

            _workLogService.AddLog("? 6단계 모듈화 완료 - 모든 기능 초기화 완료", WorkLogType.Success);

            // Ensure the view mode button reflects the current layout at startup
            UpdateViewModeButtonText();
            // Ensure text tab toggle button matches DataGrid toggle behavior
            UpdateToggleTextButtonText();
            // Ensure GoToSameTime button visibility matches current view mode
            UpdateGoToSameTimeButtonVisibility();

            // Wire up tab selection changed to move shared DataGrids when in tab mode
            try
            {
                var dataGridTabPanel = FindName("DataGridTabPanel") as System.Windows.Controls.TabControl;
                if (dataGridTabPanel != null)
                {
                    dataGridTabPanel.SelectionChanged += DataGridTabPanel_SelectionChanged;
                }
            }
            catch { /* ignore */ }

            // Ensure single DataGrid is hosted correctly depending on initial view
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TryPlaceDataGridIntoActiveHost();
            }));

            // Restore saved view state (panel/tab modes and selected indices)
            RestoreViewState();

            // Load Options 체크박스 변경 이벤트 연결
            SetupLoadOptionsEventHandlers();

            _isInitializing = false;

            // 초기 탭 Visibility 설정 (_isInitializing = false 이후에 호출)
            UpdateTabVisibility();

            // 프로그램 생존 확인용 타이머 초기화 (가장 마지막에 추가)
            InitializeAliveTimer();
            
            // 로드 진행 시간 타이머 초기화
            InitializeLoadProgressTimer();
        }

        /// <summary>
        /// 프로그램 생존 확인용 독립적인 타이머 초기화
        /// 윈도우 타이틀에 현재 시간을 표시하여 프로그램이 살아있는지 확인
        /// </summary>
        private void InitializeAliveTimer()
        {
            try
            {
                _aliveTimer = new System.Windows.Threading.DispatcherTimer();
                _aliveTimer.Interval = TimeSpan.FromSeconds(1); // 1초마다 갱신
                _aliveTimer.Tick += AliveTimer_Tick;
                _aliveTimer.Start();
                
                _workLogService.AddLog("?? 프로그램 생존 확인 타이머 시작 (1초 간격)", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 타이머 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 타이머 틱 이벤트 - 윈도우 타이틀에 현재 시간 표시
        /// 이 메서드는 다른 어떤 이벤트와도 독립적으로 1초마다 실행됩니다
        /// </summary>
        private void AliveTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // 윈도우 타이틀에 현재 시간 표시
                string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                this.Title = $"FACTOVA Log Analysis - {currentTime}";
            }
            catch
            {
                // 타이머는 절대 죽으면 안되므로 예외는 무시
                // 예외 발생 시에도 계속 동작하도록 보장
            }
        }

        /// <summary>
        /// Load Options 체크박스 이벤트 핸들러 설정
        /// </summary>
        private void SetupLoadOptionsEventHandlers()
        {
            try
            {
                var loadTextCheckBox = FindName("LoadTextCheckBox") as System.Windows.Controls.CheckBox;
                if (loadTextCheckBox != null)
                    loadTextCheckBox.Checked += LoadOptions_Changed;
                if (loadTextCheckBox != null)
                    loadTextCheckBox.Unchecked += LoadOptions_Changed;

                var loadDataGridCheckBox = FindName("LoadDataGridCheckBox") as System.Windows.Controls.CheckBox;
                if (loadDataGridCheckBox != null)
                    loadDataGridCheckBox.Checked += LoadOptions_Changed;
                if (loadDataGridCheckBox != null)
                    loadDataGridCheckBox.Unchecked += LoadOptions_Changed;

                var loadExecTimeCheckBox = FindName("LoadExecTimeCheckBox") as System.Windows.Controls.CheckBox;
                if (loadExecTimeCheckBox != null)
                    loadExecTimeCheckBox.Checked += LoadOptions_Changed;
                if (loadExecTimeCheckBox != null)
                    loadExecTimeCheckBox.Unchecked += LoadOptions_Changed;
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Load Options 이벤트 핸들러 설정 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Load Options 체크박스 상태 변경 이벤트 핸들러
        /// </summary>
        private void LoadOptions_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isInitializing) return; // 초기화 중에는 저장하지 않음

                // 즉시 설정 저장
                var loadTextCheckBox = FindName("LoadTextCheckBox") as System.Windows.Controls.CheckBox;
                if (loadTextCheckBox != null)
                    _appSettings.LoadTextChecked = loadTextCheckBox.IsChecked ?? true;

                var loadDataGridCheckBox = FindName("LoadDataGridCheckBox") as System.Windows.Controls.CheckBox;
                if (loadDataGridCheckBox != null)
                    _appSettings.LoadDataGridChecked = loadDataGridCheckBox.IsChecked ?? true;

                var loadExecTimeCheckBox = FindName("LoadExecTimeCheckBox") as System.Windows.Controls.CheckBox;
                if (loadExecTimeCheckBox != null)
                    _appSettings.LoadExecTimeChecked = loadExecTimeCheckBox.IsChecked ?? false;

                _appSettings.Save();

                if (sender is System.Windows.Controls.CheckBox cb)
                {
                    string optionName = cb.Content?.ToString() ?? "알 수 없음";
                    string state = cb.IsChecked == true ? "체크됨" : "체크 해제됨";
                    _workLogService.AddLog($"Load Options 설정 저장: {optionName} -> {state}", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Load Options 설정 저장 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void RestoreViewState()
        {
            try
            {
                // Restore main view mode
                _isFourPanelMode = _appSettings.IsFourPanelMode;

                // DataGrid 탭 전환 시 항상 DATA 탭(0번)으로 시작
                var dataTab = FindName("DataGridTabPanel") as System.Windows.Controls.TabControl;
                if (dataTab != null)
                {
                    // 4-panel에서 탭으로 전환할 때 항상 0번(DATA) 탭으로 시작
                    dataTab.SelectedIndex = 0;
                }

                // Restore text view mode and tab index
                _isTextFourPanelMode = _appSettings.IsTextFourPanelMode;
                var textTab = FindName("TextTabPanel") as System.Windows.Controls.TabControl;
                if (textTab != null)
                    textTab.SelectedIndex = Math.Max(0, Math.Min(3, _appSettings.TextTabIndex));

                // Restore Load Options 체크박스 상태
                var loadTextCheckBox = FindName("LoadTextCheckBox") as System.Windows.Controls.CheckBox;
                if (loadTextCheckBox != null)
                    loadTextCheckBox.IsChecked = _appSettings.LoadTextChecked;

                var loadDataGridCheckBox = FindName("LoadDataGridCheckBox") as System.Windows.Controls.CheckBox;
                if (loadDataGridCheckBox != null)
                    loadDataGridCheckBox.IsChecked = _appSettings.LoadDataGridChecked;

                var loadExecTimeCheckBox = FindName("LoadExecTimeCheckBox") as System.Windows.Controls.CheckBox;
                if (loadExecTimeCheckBox != null)
                    loadExecTimeCheckBox.IsChecked = _appSettings.LoadExecTimeChecked;

                // Apply UI placements
                TryPlaceDataGridIntoActiveHost();
                TryPlaceTextIntoActiveHost();
                UpdateViewModeButtonText();
                UpdateToggleTextButtonText();
                UpdateGoToSameTimeButtonVisibility();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"[ERROR] RestoreViewState 실패: {ex.Message}", WorkLogType.Error);
            }
        }

        private void SaveViewState()
        {
            try
            {
                _appSettings.IsFourPanelMode = _isFourPanelMode;
                var dataTab = FindName("DataGridTabPanel") as System.Windows.Controls.TabControl;
                if (dataTab != null)
                    _appSettings.DataGridTabIndex = dataTab.SelectedIndex >= 0 ? dataTab.SelectedIndex : 0;

                _appSettings.IsTextFourPanelMode = _isTextFourPanelMode;
                var textTab = FindName("TextTabPanel") as System.Windows.Controls.TabControl;
                if (textTab != null)
                    _appSettings.TextTabIndex = textTab.SelectedIndex >= 0 ? textTab.SelectedIndex : 0;

                // Save Load Options 체크박스 상태
                var loadTextCheckBox = FindName("LoadTextCheckBox") as System.Windows.Controls.CheckBox;
                if (loadTextCheckBox != null)
                    _appSettings.LoadTextChecked = loadTextCheckBox.IsChecked ?? true;

                var loadDataGridCheckBox = FindName("LoadDataGridCheckBox") as System.Windows.Controls.CheckBox;
                if (loadDataGridCheckBox != null)
                    _appSettings.LoadDataGridChecked = loadDataGridCheckBox.IsChecked ?? true;

                var loadExecTimeCheckBox = FindName("LoadExecTimeCheckBox") as System.Windows.Controls.CheckBox;
                if (loadExecTimeCheckBox != null)
                    _appSettings.LoadExecTimeChecked = loadExecTimeCheckBox.IsChecked ?? false;

                _appSettings.Save();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? SaveViewState 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void InitializeRedBusinessList()
        {
            var colorColumn = (FindName("ColorColumn") as DataGridComboBoxColumn);
            if (colorColumn != null)
            {
                colorColumn.ItemsSource = new List<string> {
                    "Red","Green","Blue","Orange","Purple","Brown","Gray","Black","White","Cyan",
                    "Magenta","Yellow","Lime","Maroon","Navy","Olive","Teal","Silver","Gold","Indigo"
                };
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 타이머 정리
                if (_aliveTimer != null)
                {
                    _aliveTimer.Stop();
                    _aliveTimer.Tick -= AliveTimer_Tick;
                    _aliveTimer = null;
                    _workLogService.AddLog("?? 프로그램 생존 확인 타이머 중지", WorkLogType.Info);
                }

                // 로드 진행 타이머 정리 (System.Threading.Timer)
                if (_loadProgressTimer != null)
                {
                    _loadProgressTimer.Dispose();  // System.Threading.Timer는 Dispose() 사용
                    _loadProgressTimer = null;
                    _workLogService.AddLog("?? 로드 진행 타이머 중지", WorkLogType.Info);
                }

                // Save view mode/tab indices and Load Options
                SaveViewState();
                
                 // GridSplitter 위치 저장
                 SaveGridSplitterPositions();
                 
                 // Save current font sizes into settings
                 try
                 {
                     if (double.TryParse(FontSizeTextBox.Text, out double textSize))
                     {
                         _appSettings.TextFontSize = textSize;
                     }
                     if (double.TryParse(DataGridFontSizeTextBox.Text, out double dgSize))
                     {
                         _appSettings.DataGridFontSize = dgSize;
                     }

                     // ? 통합 로그 폰트 크기 저장
                     var unifiedGrid = FindName("unifiedLogDataGrid") as DataGrid;
                     if (unifiedGrid != null)
                     {
                         _appSettings.UnifiedLogFontSize = unifiedGrid.FontSize;
                     }

                     // Load Options 상태도 한번 더 저장 (보험용)
                     var loadTextCheckBox = FindName("LoadTextCheckBox") as System.Windows.Controls.CheckBox;
                     if (loadTextCheckBox != null)
                         _appSettings.LoadTextChecked = loadTextCheckBox.IsChecked ?? true;

                     var loadDataGridCheckBox = FindName("LoadDataGridCheckBox") as System.Windows.Controls.CheckBox;
                     if (loadDataGridCheckBox != null)
                         _appSettings.LoadDataGridChecked = loadDataGridCheckBox.IsChecked ?? true;

                     var loadExecTimeCheckBox = FindName("LoadExecTimeCheckBox") as System.Windows.Controls.CheckBox;
                     if (loadExecTimeCheckBox != null)
                         _appSettings.LoadExecTimeChecked = loadExecTimeCheckBox.IsChecked ?? false;

                     _appSettings.Save();
                    
                     _workLogService.AddLog("? 통합 로그 폰트 크기 저장 완료", WorkLogType.Info);
                 }
                 catch (Exception ex)
                 {
                     System.Diagnostics.Debug.WriteLine($"Failed to save font settings on close: {ex.Message}");
                 }
                
                // 창 닫기 시 정리 작업
                _findDialog?.Close();
                _workLogService.AddLog("Application is closing", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow_Closing error: {ex.Message}");
            }
        }

        private void SetupEventHandlers()
        {
            // ❌ WorkLog UI가 제거되어 LogAdded 이벤트 핸들러 제거
            // WorkLogService는 Debug 출력만 수행
            
            _searchService.StatusUpdated += (current, total) => 
            {
                _findDialog?.UpdateStatus(current, total);
            };
        }

        // Path and date selection helpers moved to partials (MainWindow.PathManagement.cs and MainWindow.MiscHandlers.cs).
        // Duplicate implementations removed from this file.

        private void InitializeSearchModeComboBox()
        {
            searchModeComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// 시간 입력 TextBox 초기화 및 이벤트 설정
        /// </summary>
        private void InitializeTimeTextBoxes()
        {
            // 시간 입력 검증 이벤트 추가
            fromTimeTextBox.TextChanged += TimeTextBox_TextChanged;
            toTimeTextBox.TextChanged += TimeTextBox_TextChanged;
            
            // 기본값 설정
            fromTimeTextBox.Text = "00:00";
            toTimeTextBox.Text = "23:59";
            
            // 시간 프리셋 콤보박스 초기값 설정
            var timePreset = FindName("timePresetComboBox") as System.Windows.Controls.ComboBox;
            if (timePreset != null && timePreset.Items.Count > 0)
            {
                timePreset.SelectedIndex = 0; // 종일 선택
            }
        }

        /// <summary>
        /// 시간 프리셋 콤보박스 선택 이벤트
        /// </summary>
        private void TimePresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var comboBox = sender as System.Windows.Controls.ComboBox;
                var selectedItem = comboBox?.SelectedItem as System.Windows.Controls.ComboBoxItem;
                
                if (selectedItem?.Tag is string timeRange)
                {
                    var parts = timeRange.Split('~');
                    if (parts.Length == 2)
                    {
                        fromTimeTextBox.Text = parts[0];
                        toTimeTextBox.Text = parts[1];
                        
                        var content = selectedItem.Content?.ToString() ?? "시간 설정";
                        _workLogService.AddLog($"시간 프리셋 적용: {content} ({timeRange})", WorkLogType.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"시간 프리셋 설정 오류: {ex.Message}", WorkLogType.Error);
            }
        }
        /// <summary>
        /// 시간 입력 형식 검증
        /// </summary>
        private void TimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                string text = textBox.Text;
                
                // 빈 텍스트는 허용
                if (string.IsNullOrEmpty(text))
                    return;
                
                // HH:MM 형식 검증
                if (!IsValidTimeFormat(text))
                {
                    // 잘못된 형식인 경우 배경색을 연한 빨간색으로 표시
                    textBox.Background = new SolidColorBrush(Colors.LightPink);
                }
                else
                {
                    // 올바른 형식인 경우 기본 배경색으로 복원
                    textBox.Background = System.Windows.SystemColors.WindowBrush;
                }
            }
        }

        /// <summary>
        /// HH:MM 형식인지 검증
        /// </summary>
        private bool IsValidTimeFormat(string timeText)
        {
            if (string.IsNullOrWhiteSpace(timeText))
                return false;
                
            var parts = timeText.Split(':');
            if (parts.Length != 2)
                return false;
                
            if (int.TryParse(parts[0], out int hours) && int.TryParse(parts[1], out int minutes))
            {
                return hours >= 0 && hours <= 23 && minutes >= 0 && minutes <= 59;
            }
            
            return false;
        }

        /// <summary>
        /// 시간 범위 가져오기
        /// </summary>
        private (TimeSpan fromTime, TimeSpan toTime) GetTimeRange()
        {
            TimeSpan fromTime = TimeSpan.Zero;
            TimeSpan toTime = new TimeSpan(23, 59, 59);
            
            try
            {
                if (IsValidTimeFormat(fromTimeTextBox.Text))
                {
                    var parts = fromTimeTextBox.Text.Split(':');
                    fromTime = new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), 0);
                }
                
                if (IsValidTimeFormat(toTimeTextBox.Text))
                {
                    var parts = toTimeTextBox.Text.Split(':');
                    toTime = new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), 59); // 해당 분의 마지막 초까지 포함
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"시간 범위 파싱 오류: {ex.Message}", WorkLogType.Warning);
            }
            
            return (fromTime, toTime);
        }

        private void InitializeUIManagers()
        {
            try
            {
                var bottomRowDef = FindName("BottomRowDefinition") as RowDefinition;
                var toggleLayoutBtn = FindName("ToggleLayoutButton") as System.Windows.Controls.Button;

                _layoutManager.Initialize(bottomRowDef, toggleLayoutBtn);
                
                var fontSizeCombo = FindName("FontSizeComboBox") as System.Windows.Controls.ComboBox;
                _textFormattingManager.Initialize(null, fontSizeCombo);
                
                InitializeTimeTextBoxes();
                
                _workLogService.AddLog("? UI 매니저 초기화 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? UI 매니저 초기화 실패: {ex.Message}", WorkLogType.Error);
            }
        }

        public string? LastSelectedDataGridName => _lastSelectedDataGridName;

        private DataGrid? GetLastSelectedDataGrid()
        {
            if (string.IsNullOrEmpty(_lastSelectedDataGridName)) return null;
            return FindDataGridByName(_lastSelectedDataGridName);
        }

        private void DataGrid_GotFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is DataGrid dg)
            {
                _lastSelectedDataGridName = dg.Name;
                if (!_isInitializing)
                    _workLogService.AddLog($"Active DataGrid set: {dg.Name}", WorkLogType.Info);
            }
        }

        private void DataGrid_PreviewMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid dg)
            {
                _lastSelectedDataGridName = dg.Name;
            }
        }

        private void FindTextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_activeTextBox != null)
                {
                    OpenFindDialog(_activeTextBox);
                }
                else
                {
                    var dataTextBox = FindName("dataLogTextBox") as System.Windows.Controls.TextBox;
                    if (dataTextBox != null)
                    {
                        dataTextBox.Focus();
                        OpenFindDialog(dataTextBox);
                    }
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 텍스트 검색 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_activeTextBox != null && !string.IsNullOrEmpty(_activeTextBox.SelectedText))
                {
                    System.Windows.Clipboard.SetText(_activeTextBox.SelectedText);
                    _workLogService.AddLog($"선택된 텍스트가 클립보드에 복사됨 ({_activeTextBox.SelectedText.Length}자)", WorkLogType.Success);
                }
                else
                {
                    _workLogService.AddLog("복사할 텍스트를 선택하세요", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 텍스트 복사 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ApplyExecTimeFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var execTimeFilterTextBox = FindName("execTimeFilterTextBox") as WpfTextBox;
                string filterValue = execTimeFilterTextBox?.Text?.Trim() ?? "0.5";

                if (double.TryParse(filterValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double threshold))
                {
                    _workLogService.AddLog($"exec.Time 필터 적용: {threshold}초 이상", WorkLogType.Info);
                    var execTimeStatusText = FindName("ExecTimeStatusText") as TextBlock;
                    if (execTimeStatusText != null)
                    {
                        execTimeStatusText.Text = $"Filter Applied: >{threshold}s";
                    }

                    // Ensure execTimeDataGrid is bound to the same collection as dataLogDataGrid
                    var execGrid = FindName("execTimeDataGrid") as DataGrid;
                    if (execGrid != null)
                    {
                        try
                        {
                            // Use DataGridManager's DATA collection
                            if (_dataGridManager != null)
                            {
                                execGrid.ItemsSource = _dataGridManager.DataLogLines;

                                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(execGrid.ItemsSource);
                                if (view != null)
                                {
                                    view.Filter = item =>
                                    {
                                        if (item is not FACTOVA_LogAnalysis.Models.LogLineItem li) return false;

                                        if (string.IsNullOrWhiteSpace(li.ExecTime)) return false;

                                        // Try parse as double (seconds)
                                        if (double.TryParse(li.ExecTime, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v))
                                        {
                                            return v > threshold;
                                        }

                                        // Fallback: try TimeSpan parse
                                        if (TimeSpan.TryParse(li.ExecTime, out TimeSpan ts))
                                        {
                                            return ts.TotalSeconds > threshold;
                                        }

                                        return false;
                                    };

                                    view.Refresh();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _workLogService.AddLog($"? exec.Time DataGrid 필터 적용 오류: {ex.Message}", WorkLogType.Error);
                        }
                    }
                }
                else
                {
                    _workLogService.AddLog("? 유효하지 않은 exec.Time 값입니다", WorkLogType.Error);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? exec.Time 필터 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearExecTimeFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var execTimeFilterTextBox = FindName("execTimeFilterTextBox") as WpfTextBox;
                if (execTimeFilterTextBox != null)
                {
                    execTimeFilterTextBox.Text = "0.5";
                }

                var execGrid = FindName("execTimeDataGrid") as DataGrid;
                if (execGrid != null)
                {
                    var view = System.Windows.Data.CollectionViewSource.GetDefaultView(execGrid.ItemsSource);
                    if (view != null)
                    {
                        view.Filter = null;
                        view.Refresh();
                    }

                    // Optionally clear ItemsSource so grid shows nothing until user applies filter/loads data
                    // execGrid.ItemsSource = null;
                }

                var execTimeStatusText = FindName("ExecTimeStatusText") as TextBlock;
                if (execTimeStatusText != null)
                {
                    execTimeStatusText.Text = "exec.Time Analysis Ready";
                }

                _workLogService.AddLog("exec.Time 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? exec.Time 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void LoadExecTimeAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime selectedDate = GetSelectedDate();
                string searchText = searchTextBox.Text;
                string searchMode = GetSelectedSearchMode();
                
                var execTimeFilterTextBox = FindName("execTimeFilterTextBox") as WpfTextBox;
                string filterValue = execTimeFilterTextBox?.Text?.Trim() ?? "0.5";
                
                _workLogService.AddLog($"exec.Duration 분석 로드 시작: {selectedDate:yyyy-MM-dd}, 임계값: {filterValue}초", WorkLogType.Info);

                try
                {
                    if (LogTabControl != null && LogTabControl.Items.Count > 5)
                        LogTabControl.SelectedIndex = 5;
                    else if (LogTabControl != null)
                        LogTabControl.SelectedIndex = Math.Max(0, LogTabControl.Items.Count - 1);
                }
                catch { }

                Task.Run(async () =>
                {
                    await LoadIndividualLogFilesUsingService(selectedDate, searchText, searchMode);
                    
                    Dispatcher.BeginInvoke(() =>
                    {
                        var execTimeStatusText = FindName("ExecTimeStatusText") as TextBlock;
                        if (execTimeStatusText != null) execTimeStatusText.Text = "Analysis Loaded";
                        _workLogService.AddLog("? exec.Time 분석 로드 완료", WorkLogType.Success);
                    });
                });
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? exec.Time 분석 로드 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ToggleViewModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isFourPanelMode = !_isFourPanelMode;

            var fourPanel = this.FindName("DataGridFourPanel") as UIElement;
            var tabPanel = this.FindName("DataGridTabPanel") as UIElement;
            if (fourPanel != null && tabPanel != null)
            {
                fourPanel.Visibility = _isFourPanelMode ? Visibility.Visible : Visibility.Collapsed;
                tabPanel.Visibility = _isFourPanelMode ? Visibility.Collapsed : Visibility.Visible;
            }

            // 탭 모드로 전환할 때 항상 DATA 탭(인덱스 0)으로 설정
            if (!_isFourPanelMode)
            {
                var tab = this.FindName("DataGridTabPanel") as System.Windows.Controls.TabControl;
                if (tab != null)
                {
                    tab.SelectedIndex = 0;
                }
            }

            TryPlaceDataGridIntoActiveHost();

            UpdateViewModeButtonText();
            UpdateGoToSameTimeButtonVisibility();
        }

        private void GoToSameTimeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var active = GetFocusedOrActiveDataGrid();
                if (active == null)
                {
                    _workLogService.AddLog("활성 DataGrid가 없습니다.", WorkLogType.Warning);
                    return;
                }

                try
                {
                    var focused = Keyboard.FocusedElement?.GetType().Name ?? "null";
                    _workLogService.AddLog($"[DBG] Active DataGrid: {active.Name}, IsKeyboardFocusWithin={active.IsKeyboardFocusWithin}, IsFocused={active.IsFocused}, KeyboardFocusedElement={focused}", WorkLogType.Info);
                }
                catch { }

                object? selected = active.SelectedItem ?? active.CurrentItem;
                if (selected == null && active.SelectedCells != null && active.SelectedCells.Count > 0) selected = active.SelectedCells[0].Item;
                if (selected == null)
                {
                    _workLogService.AddLog("먼저 기준이 될 행을 선택하세요.", WorkLogType.Warning);
                    return;
                }

                var timeProp = selected.GetType().GetProperty("Timestamp");
                if (timeProp == null)
                {
                    _workLogService.AddLog("선택된 행에 'Timestamp' 속성이 없습니다.", WorkLogType.Error);
                    return;
                }

                var timeValue = timeProp.GetValue(selected)?.ToString();
                if (string.IsNullOrEmpty(timeValue))
                {
                    _workLogService.AddLog("선택된 행의 Time값이 비어있습니다.", WorkLogType.Warning);
                    return;
                }

                if (!TryParseTimestamp(timeValue, out DateTime targetTime))
                {
                    _workLogService.AddLog($"Time 파싱 실패: {timeValue}", WorkLogType.Error);
                    return;
                }

                var candidateGrids = new[] { "dataLogDataGrid", "eventLogDataGrid", "debugLogDataGrid", "exceptionLogDataGrid" };
                var moved = new List<string>();

                foreach (var name in candidateGrids)
                {
                    var dg = FindDataGridByName(name);
                    if (dg == null || dg == active) continue;

                    int foundIndex = -1;
                    for (int i = 0; i < dg.Items.Count; i++)
                    {
                        var item = dg.Items[i];
                        var p = item.GetType().GetProperty("Timestamp");
                        if (p == null) continue;
                        var v = p.GetValue(item)?.ToString();
                        if (string.IsNullOrEmpty(v)) continue;
                        if (TryParseTimestamp(v, out DateTime t))
                        {
                            if (t >= targetTime)
                            {
                                foundIndex = i;
                                break;
                            }
                        }
                    }

                    if (foundIndex >= 0)
                    {
                        ScrollDataGridToRow(dg, foundIndex);
                        moved.Add(dg.Name);
                    }
                }

                if (moved.Count > 0)
                    _workLogService.AddLog($"다음 그리드에서 해당 시간으로 이동: {string.Join(", ", moved)} -> {targetTime}", WorkLogType.Success);
                else
                    _workLogService.AddLog("다른 그리드에서 일치하는 시간을 찾지 못했습니다.", WorkLogType.Warning);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? GoToSameTime 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ScrollDataGridToRow(DataGrid dg, int index)
        {
            if (dg == null || index < 0 || index >= dg.Items.Count) return;
            dg.SelectedIndex = index;
            dg.UpdateLayout();
            dg.ScrollIntoView(dg.Items[index]);
            var row = dg.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow;
            row?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        private void OpenInVSCodeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = _logFolderPath;
                if (string.IsNullOrWhiteSpace(folder))
                {
                    var tb = FindName("LogFolderPathTextBox") as System.Windows.Controls.TextBox;
                    folder = tb?.Text ?? "";
                }

                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    _workLogService.AddLog("로그 폴더 경로가 유효하지 않습니다.", WorkLogType.Warning);
                    return;
                }

                try
                {
                    var psi = new ProcessStartInfo("code", $"\"{folder}\"") { UseShellExecute = true };
                    Process.Start(psi);
                    _workLogService.AddLog($"? VS Code 실행: {folder}", WorkLogType.Success);
                     return;
                }
                catch { }

                var candidates = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\Microsoft VS Code\\Code.exe"),
                    @"C:\\Program Files\\Microsoft VS Code\\Code.exe",
                    @"C:\\Program Files (x86)\\Microsoft VS Code\\Code.exe"
                };

                foreach (var exe in candidates)
                {
                    try
                    {
                        if (File.Exists(exe))
                        {
                            Process.Start(new ProcessStartInfo(exe, $"\"{folder}\"") { UseShellExecute = true });
                            _workLogService.AddLog($"? VS Code 실행: {exe} {folder}", WorkLogType.Success);
                            return;
                        }
                    }
                    catch { }
                }

                _workLogService.AddLog("VS Code 실행에 실패했습니다. 대신 탐색기로 폴더를 엽니다.", WorkLogType.Warning);
                Process.Start(new ProcessStartInfo("explorer", $"\"{folder}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? VS Code 열기 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private bool TryParseTimestamp(string text, out DateTime result)
        {
            string[] formats = { "yyyy-MM-dd HH:mm:ss", "MM-dd-yyyy HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "HH:mm:ss" };
            if (DateTime.TryParseExact(text, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out result))
                return true;

            return DateTime.TryParse(text, out result);
        }

        private DataGrid? FindDataGridByName(string name)
        {
            var dg = this.FindName(name) as DataGrid;
            if (dg != null)
                return dg;

            return FindVisualChild<DataGrid>(this, name);
        }

        private T? FindVisualChild<T>(DependencyObject parent, string? name = null) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild)
                {
                    if (string.IsNullOrEmpty(name))
                        return tChild;

                    if (child is FrameworkElement fe && fe.Name == name)
                        return tChild;
                }

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject? current = child;
            while (current != null)
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private DataGrid? GetFocusedOrActiveDataGrid()
        {
            // First, try to find focused DataGrid
            var focusedElement = Keyboard.FocusedElement as DependencyObject;
            if (focusedElement != null)
            {
                var dg = FindVisualParent<DataGrid>(focusedElement);
                if (dg != null) return dg;
            }

            // If in tab mode, try to get the DataGrid from the active tab's GroupBox
            var tabPanel = FindName("DataGridTabPanel") as System.Windows.Controls.TabControl;
            if (tabPanel != null && tabPanel.Visibility == Visibility.Visible)
            {
                // Get the ContentControl host for the active tab
                var hostName = tabPanel.SelectedIndex switch
                {
                    0 => "DataGridHost_DATA_Tab",
                    1 => "DataGridHost_EVENT_Tab",
                    2 => "DataGridHost_DEBUG_Tab",
                    3 => "DataGridHost_EXCEPTION_Tab",
                    _ => null
                };

                if (hostName != null)
                {
                    var host = FindName(hostName) as ContentControl;
                    if (host?.Content is System.Windows.Controls.GroupBox groupBox)
                    {
                        // Find DataGrid inside the GroupBox
                        var dg = FindVisualChild<DataGrid>(groupBox);
                        if (dg != null) return dg;
                    }
                }
            }

            // If in 4-panel mode, try to find focused or last selected DataGrid
            var fourPanel = FindName("DataGridFourPanel") as UIElement;
            if (fourPanel != null && fourPanel.Visibility == Visibility.Visible)
            {
                var names = new[] { "dataLogDataGrid", "eventLogDataGrid", "debugLogDataGrid", "exceptionLogDataGrid" };
                
                // First try to find one with keyboard focus
                foreach (var name in names)
                {
                    var dg = FindDataGridByName(name);
                    if (dg != null && (dg.IsKeyboardFocusWithin || dg.IsFocused))
                        return dg;
                }

                // Then try last selected
                var last = GetLastSelectedDataGrid();
                if (last != null)
                    return last;

                // Finally, default to DATA grid
                return FindDataGridByName("dataLogDataGrid");
            }

            // Fallback: return last selected or null
            return GetLastSelectedDataGrid();
        }

        private void ApplyFontToDataGridInstance(DataGrid dg, double size)
        {
            if (dg == null) return;

            dg.FontSize = size;

            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontSizeProperty, size));
            dg.CellStyle = cellStyle;

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontSizeProperty, size));
            dg.RowStyle = rowStyle;

            foreach (var col in dg.Columns.OfType<DataGridTextColumn>())
            {
                var elementStyle = new Style(typeof(TextBlock));
                elementStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, size));
                col.ElementStyle = elementStyle;
            }

            dg.Items.Refresh();
            dg.UpdateLayout();

            _workLogService.AddLog($"DataGrid에 폰트 적용 완료: {dg.Name} -> {size}", WorkLogType.Info);
        }

        private void RemoveElementFromParent(FrameworkElement element)
        {
            if (element == null) return;

            var logicalParent = System.Windows.LogicalTreeHelper.GetParent(element) as FrameworkElement;
            if (logicalParent != null)
            {
                if (logicalParent is ContentControl lc)
                {
                    if (lc.Content == element)
                    {
                        lc.Content = null;
                        return;
                    }
                }

                if (logicalParent is System.Windows.Controls.Panel lpanel)
                {
                    if (lpanel.Children.Contains(element))
                    {
                        lpanel.Children.Remove(element);
                        return;
                    }
                }

                if (logicalParent is ItemsControl itemsControl)
                {
                    if (itemsControl.Items.Contains(element))
                    {
                        itemsControl.Items.Remove(element);
                        return;
                    }
                }
            }

            DependencyObject parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is System.Windows.Controls.Panel panel)
                {
                    if (panel.Children.Contains(element))
                    {
                        panel.Children.Remove(element);
                        return;
                    }
                }

                if (parent is ContentControl contentControl)
                {
                    if (contentControl.Content == element)
                    {
                        contentControl.Content = null;
                        return;
                    }
                }

                if (parent is Decorator decorator)
                {
                    if (decorator.Child == element)
                    {
                        decorator.Child = null;
                        return;
                    }
                }

                if (parent is System.Windows.Controls.GroupBox gb)
                {
                    if (gb.Content == element)
                    {
                        gb.Content = null;
                        return;
                    }
                }

                if (parent is ContentPresenter cp)
                {
                    var templatedParent = cp.TemplatedParent as ContentControl;
                    if (templatedParent != null && templatedParent.Content == element)
                    {
                        templatedParent.Content = null;
                        return;
                    }
                }

                if (parent is ItemsControl ic)
                {
                    if (ic.Items.Contains(element))
                    {
                        ic.Items.Remove(element);
                        return;
                    }
                }

                parent = VisualTreeHelper.GetParent(parent);
            }
        }

        private void UpdateViewModeButtonText()
        {
            try
            {
                var button = FindName("ToggleViewModeButton") as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.Content = _isFourPanelMode ? "탭으로 전환" : "4분할 전환";
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? ViewMode 버튼 텍스트 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void UpdateGoToSameTimeButtonVisibility()
        {
            try
            {
                var goBtn = FindName("GoToSameTimeButton") as System.Windows.Controls.Button;
                if (goBtn != null)
                {
                    goBtn.Visibility = _isFourPanelMode ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? GoToSameTime 버튼 가시성 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        // end of class helper methods

        private void DataGridTabPanel_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isFourPanelMode) return;
                if (_isInitializing) return;
                TryPlaceDataGridIntoActiveHost();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 탭 선택 변경 처리 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void TryPlaceDataGridIntoActiveHost()
        {
            try
            {
                // Define mapping of GroupBoxes to their hosts
                var groupBoxMappings = new[] {
                    new { GroupBoxName = "DataGroupBox_FourPanel", TabHost = "DataGridHost_DATA_Tab", GridColumn = 0, GridRow = 0 },
                    new { GroupBoxName = "EventGroupBox_FourPanel", TabHost = "DataGridHost_EVENT_Tab", GridColumn = 2, GridRow = 0 },
                    new { GroupBoxName = "DebugGroupBox_FourPanel", TabHost = "DataGridHost_DEBUG_Tab", GridColumn = 0, GridRow = 2 },
                    new { GroupBoxName = "ExceptionGroupBox_FourPanel", TabHost = "DataGridHost_EXCEPTION_Tab", GridColumn = 2, GridRow = 2 }
                };

                var tab = FindName("DataGridTabPanel") as System.Windows.Controls.TabControl;
                int selectedTabIndex = tab != null && tab.SelectedIndex >= 0 ? tab.SelectedIndex : 0;

                foreach (var map in groupBoxMappings)
                {
                    var groupBox = FindName(map.GroupBoxName) as System.Windows.Controls.GroupBox;
                    if (groupBox == null) continue;

                    // Remove from any existing parent
                    RemoveElementFromParent(groupBox);

                    if (_isFourPanelMode)
                    {
                        // Place in 4-panel grid
                        var fourPanel = FindName("DataGridFourPanel") as System.Windows.Controls.Grid;
                        if (fourPanel != null)
                        {
                            groupBox.SetValue(System.Windows.Controls.Grid.ColumnProperty, map.GridColumn);
                            groupBox.SetValue(System.Windows.Controls.Grid.RowProperty, map.GridRow);
                            groupBox.Margin = new Thickness(5);
                            
                            if (!fourPanel.Children.Contains(groupBox))
                            {
                                fourPanel.Children.Add(groupBox);
                            }
                        }
                    }
                    else
                    {
                        // Place in active tab's ContentControl host
                        var host = FindName(map.TabHost) as ContentControl;
                        if (host != null)
                        {
                            // Determine if this GroupBox should be in the active tab
                            bool placeHere = (selectedTabIndex switch
                            {
                                0 => map.TabHost == "DataGridHost_DATA_Tab",
                                1 => map.TabHost == "DataGridHost_EVENT_Tab",
                                2 => map.TabHost == "DataGridHost_DEBUG_Tab",
                                3 => map.TabHost == "DataGridHost_EXCEPTION_Tab",
                                _ => false
                            });

                            if (placeHere)
                            {
                                groupBox.Margin = new Thickness(0);
                                host.Content = groupBox;
                            }
                            else
                            {
                                // Clear host if this GroupBox was there but is no longer active
                                if (host.Content == groupBox)
                                    host.Content = null;
                            }
                        }
                    }
                }

                // Update panel visibility
                var fourPanelContainer = FindName("DataGridFourPanel") as UIElement;
                var tabPanelContainer = FindName("DataGridTabPanel") as UIElement;
                if (fourPanelContainer != null && tabPanelContainer != null)
                {
                    fourPanelContainer.Visibility = _isFourPanelMode ? Visibility.Visible : Visibility.Collapsed;
                    tabPanelContainer.Visibility = _isFourPanelMode ? Visibility.Collapsed : Visibility.Visible;
                }

                _workLogService.AddLog($"? 뷰 모드 전환: {(_isFourPanelMode ? "4분할" : "탭")} 뷰", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 그룹박스 호스팅 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ToggleTextViewModeButton_Click(object sender, RoutedEventArgs e)
        {
            _isTextFourPanelMode = !_isTextFourPanelMode;
            TryPlaceTextIntoActiveHost();
            UpdateToggleTextButtonText();
        }

        private void UpdateToggleTextButtonText()
        {
            try
            {
                var button = FindName("ToggleTextViewModeButton") as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.Content = _isTextFourPanelMode ? "탭으로 전환" : "4분할 전환";
                    button.ToolTip = "Toggle between 4-panel and 4-tab view";
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? ToggleText 버튼 텍스트 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void TryPlaceTextIntoActiveHost()
        {
            try
            {
                var mappings = new[] {
                    new { ContentName = "dataLogTextBox", LineName = "dataLineNumberTextBox", FourGroup = "DataGroupBox", TabHost = "TextHost_DATA_Tab" },
                    new { ContentName = "eventLogTextBox", LineName = "eventLineNumberTextBox", FourGroup = "EventGroupBox", TabHost = "TextHost_EVENT_Tab" },
                    new { ContentName = "debugLogTextBox", LineName = "debugLineNumberTextBox", FourGroup = "DebugGroupBox", TabHost = "TextHost_DEBUG_Tab" },
                    new { ContentName = "exceptionLogTextBox", LineName = "exceptionLineNumberTextBox", FourGroup = "ExceptionGroupBox", TabHost = "TextHost_EXCEPTION_Tab" }
                };

                foreach (var map in mappings)
                {
                    var contentTb = FindName(map.ContentName) as System.Windows.Controls.TextBox;
                    var lineTb = FindName(map.LineName) as System.Windows.Controls.TextBox;
                    if (contentTb == null || lineTb == null) continue;

                    RemoveElementFromParent(contentTb);
                    RemoveElementFromParent(lineTb);

                    if (_isTextFourPanelMode)
                    {
                        var group = FindName(map.FourGroup) as System.Windows.Controls.GroupBox;
                        if (group != null)
                        {
                            var grid = new Grid();
                            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });
                            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                            lineTb.SetValue(Grid.ColumnProperty, 0);
                            contentTb.SetValue(Grid.ColumnProperty, 1);
                            grid.Children.Add(lineTb);
                            grid.Children.Add(contentTb);
                            group.Content = grid;
                        }
                    }
                    else
                    {
                        var host = FindName(map.TabHost) as ContentControl;
                        if (host != null)
                        {
                            var grid = new Grid();
                            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });
                            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
                            lineTb.SetValue(Grid.ColumnProperty, 0);
                            contentTb.SetValue(Grid.ColumnProperty, 1);
                            grid.Children.Add(lineTb);
                            grid.Children.Add(contentTb);
                            host.Content = grid;
                        }
                    }
                }

                var textFour = FindName("TextFourPanel") as UIElement;
                var textTab = FindName("TextTabPanel") as UIElement;
                if (textFour != null && textTab != null)
                {
                    textFour.Visibility = _isTextFourPanelMode ? Visibility.Visible : Visibility.Collapsed;
                    textTab.Visibility = _isTextFourPanelMode ? Visibility.Collapsed : Visibility.Visible;
                }
            }
            catch { }
        }

        // Clipboard tab button handlers (9 slots)
        private System.Windows.Controls.TextBox? GetFocusedClipboardBox()
        {
            var focused = Keyboard.FocusedElement as System.Windows.Controls.TextBox;
            if (focused != null && !string.IsNullOrEmpty(focused.Name) && focused.Name.StartsWith("ClipboardBox"))
                return focused;

            for (int i = 1; i <= 9; i++)
            {
                var tb = FindName($"ClipboardBox{i}") as System.Windows.Controls.TextBox;
                if (tb != null && tb.IsFocused) return tb;
            }

            return null;
        }

        private void ClipboardPasteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tb = GetFocusedClipboardBox();
                if (tb == null)
                {
                    _workLogService.AddLog("먼저 삽입할 Clipboard 슬롯을 선택하세요.", WorkLogType.Warning);
                    return;
                }

                if (System.Windows.Clipboard.ContainsText())
                {
                    tb.Text = System.Windows.Clipboard.GetText();
                    _workLogService.AddLog("📋 클립보드 내용을 선택된 슬롯에 붙여넣음", WorkLogType.Success);
                }
                else
                {
                    _workLogService.AddLog("클립보드에 텍스트가 없습니다.", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ ClipboardPaste 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClipboardCopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tb = GetFocusedClipboardBox();
                if (tb != null)
                {
                    System.Windows.Clipboard.SetText(tb.Text ?? string.Empty);
                    _workLogService.AddLog("📋 선택된 슬롯 텍스트를 클립보드에 복사함", WorkLogType.Success);
                    return;
                }

                // fallback: copy first non-empty slot
                for (int i = 1; i <= 9; i++)
                {
                    var t = FindName($"ClipboardBox{i}") as System.Windows.Controls.TextBox;
                    if (t != null && !string.IsNullOrEmpty(t.Text))
                    {
                        System.Windows.Clipboard.SetText(t.Text);
                        _workLogService.AddLog($"📋 슬롯{i} 텍스트를 클립보드에 복사함", WorkLogType.Success);
                        return;
                    }
                }

                _workLogService.AddLog("복사할 Clipboard 슬롯을 선택하거나 비어있지 않은 슬롯을 만드세요.", WorkLogType.Warning);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ ClipboardCopy 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClipboardInsertButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? source = null;
                var cb = GetFocusedClipboardBox();
                if (cb != null && !string.IsNullOrEmpty(cb.Text))
                {
                    source = cb.Text;
                }
                else
                {
                    for (int i = 1; i <= 9; i++)
                    {
                        var t = FindName($"ClipboardBox{i}") as System.Windows.Controls.TextBox;
                        if (t != null && !string.IsNullOrEmpty(t.Text))
                        {
                            source = t.Text;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(source))
                {
                    _workLogService.AddLog("삽입할 텍스트가 없습니다. 먼저 슬롯에 텍스트를 넣으세요.", WorkLogType.Warning);
                    return;
                }

                var target = _activeTextBox;
                if (target == null || target.IsReadOnly)
                {
                    _workLogService.AddLog("활성 편집 대상이 없습니다(또는 읽기 전용). 클립보드로 복사합니다.", WorkLogType.Warning);
                    System.Windows.Clipboard.SetText(source);
                    return;
                }

                int insertPos = target.CaretIndex;
                target.Text = target.Text.Insert(insertPos, source);
                target.CaretIndex = insertPos + source.Length;
                _workLogService.AddLog("✂️ 슬롯 텍스트를 활성 편집 영역에 삽입함", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ ClipboardInsert 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClipboardClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                for (int i = 1; i <= 9; i++)
                {
                    var t = FindName($"ClipboardBox{i}") as System.Windows.Controls.TextBox;
                    if (t != null) t.Text = string.Empty;
                }
                _workLogService.AddLog("? 모든 Clipboard 슬롯을 초기화함", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? ClipboardClear 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// DataGrid Content 셀의 TextBox 드래그 방지 및 복사 지원
        /// </summary>
        private void ContentTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.TextBox textBox && textBox.IsReadOnly)
                {
                    // 이미 선택된 텍스트가 있으면 복사 가능하도록 포커스 유지
                    if (!string.IsNullOrEmpty(textBox.SelectedText))
                    {
                        return; // 기존 선택 유지
                    }

                    // 새로운 클릭은 전체 텍스트를 선택하지 않고 클릭 위치로 캐럿 이동
                    textBox.Focus();
                    e.Handled = false; // 기본 동작 허용
                }
            }
            catch
            {
                // 예외 무시
            }
        }

        // Export DataGrid to CSV (Excel)

        private void ExportDataGridButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dg = GetFocusedOrActiveDataGrid();
                if (dg == null)
                {
                    _workLogService.AddLog("Export할 DataGrid를 찾을 수 없습니다.", WorkLogType.Warning);
                    return;
                }

                // Suggest a filename that already includes a timestamp so user sees it in the dialog.
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var suggestedName = dg.Name + "_" + timestamp + ".xlsx";

                var sfd = new Microsoft.Win32.SaveFileDialog()
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    FileName = suggestedName,
                    DefaultExt = ".xlsx"
                };

                if (sfd.ShowDialog() != true) return;

                // Use exactly the path/name the user entered in the dialog (respect user edits).
                var finalName = sfd.FileName;
                
                ExportDataGridToXlsx(dg, finalName);
                _workLogService.AddLog($"? DataGrid '{dg.Name}'를 XLSX로 저장했습니다: {finalName}", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Export 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ExportDataGridToXlsx(DataGrid dg, string filePath)
        {
            if (dg == null) return;

            var columns = dg.Columns.Where(c => c.Visibility == Visibility.Visible).ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Export");
            
            // headers
            for (int c = 0; c < columns.Count; c++)
            {
                ws.Cell(1, c + 1).Value = columns[c].Header?.ToString() ?? string.Empty;
            }

            int row = 2;
            foreach (var item in dg.Items)
            {
                if (item == System.Windows.Data.CollectionView.NewItemPlaceholder) continue;

                for (int c = 0; c < columns.Count; c++)
                {
                    var col = columns[c];
                    string text = string.Empty;
                    if (col is DataGridBoundColumn boundCol)
                    {
                        var binding = boundCol.Binding as System.Windows.Data.Binding;
                        if (binding != null)
                        {
                            var prop = item.GetType().GetProperty(binding.Path.Path);
                            if (prop != null)
                            {
                                var val = prop.GetValue(item);
                                ws.Cell(row, c + 1).Value = val?.ToString() ?? string.Empty;
                                continue;
                            }
                        }
                        else
                        {
                            var cellContent = col.GetCellContent(item) as FrameworkElement;
                            if (cellContent is TextBlock tb)
                            {
                                ws.Cell(row, c + 1).Value = tb.Text;
                                continue;
                            }
                        }
                    }

                    var fallback = col.GetCellContent(item) as FrameworkElement;
                    if (fallback is TextBlock ftb)
                        ws.Cell(row, c + 1).Value = ftb.Text;
                    else
                        ws.Cell(row, c + 1).Value = string.Empty;
                }

                row++;
            }

            // Adjust columns
            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }
        // end export helpers
        private void DateComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                var combo = sender as System.Windows.Controls.ComboBox ?? FindName("dateComboBox") as System.Windows.Controls.ComboBox;
                var datePicker = FindName("dateSelector") as System.Windows.Controls.DatePicker;
                if (combo == null) return;

                if (combo.SelectedItem is string s && DateTime.TryParse(s, out DateTime dt))
                {
                    // keep DatePicker in sync
                    if (datePicker != null)
                        datePicker.SelectedDate = dt;

                    _workLogService.AddLog($"날짜 콤보 선택: {s}", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DateComboBox_SelectionChanged 오류: {ex.Message}", WorkLogType.Error);
            }
        }
    }
}
