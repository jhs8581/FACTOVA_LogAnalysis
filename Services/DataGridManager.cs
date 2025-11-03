using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Data;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Services;
using FACTOVA_LogAnalysis.Helpers;

namespace FACTOVA_LogAnalysis.Services
{
    /// <summary>
    /// DataGrid 관리 로직을 담당하는 서비스 클래스
    /// 4분할 DataGrid 초기화, 데이터 바인딩, 필터링 처리
    /// </summary>
    public class DataGridManager
    {
        #region Fields

        private readonly WorkLogService _workLogService;
        private readonly LogLoadingService _logLoadingService;
        private readonly RedBusinessListManager _redBusinessListManager;

        // 4분할 DataGrid용 컬렉션들
        private readonly ObservableCollection<LogLineItem> _dataLogLines;
        private readonly ObservableCollection<LogLineItem> _eventLogLines;
        private readonly ObservableCollection<LogLineItem> _debugLogLines;
        private readonly ObservableCollection<LogLineItem> _exceptionLogLines;

        // DataGrid 참조들
        private DataGrid? _dataLogDataGrid;
        private DataGrid? _eventLogDataGrid;
        private DataGrid? _debugLogDataGrid;
        private DataGrid? _exceptionLogDataGrid;
        // Tab DataGrid references (4-tab view)
        private DataGrid? _dataLogDataGrid_Tab;
        private DataGrid? _eventLogDataGrid_Tab;
        private DataGrid? _debugLogDataGrid_Tab;
        private DataGrid? _exceptionLogDataGrid_Tab;

        // 필터링 상태
        private string _currentLogLevelFilter = "ALL";
        private string _currentBusinessNameFilter = "ALL";  
        private string _currentKeywordFilter = "";

        #endregion

        #region Properties

        public ObservableCollection<LogLineItem> DataLogLines => _dataLogLines;
        public ObservableCollection<LogLineItem> EventLogLines => _eventLogLines;
        public ObservableCollection<LogLineItem> DebugLogLines => _debugLogLines;
        public ObservableCollection<LogLineItem> ExceptionLogLines => _exceptionLogLines;

        #endregion

        #region Constructor

        public DataGridManager(WorkLogService workLogService, LogLoadingService logLoadingService, RedBusinessListManager redBusinessListManager)
        {
            _workLogService = workLogService ?? throw new ArgumentNullException(nameof(workLogService));
            _logLoadingService = logLoadingService ?? throw new ArgumentNullException(nameof(logLoadingService));
            _redBusinessListManager = redBusinessListManager ?? throw new ArgumentNullException(nameof(redBusinessListManager));

            // 컬렉션 초기화
            _dataLogLines = new ObservableCollection<LogLineItem>();
            _eventLogLines = new ObservableCollection<LogLineItem>();
            _debugLogLines = new ObservableCollection<LogLineItem>();
            _exceptionLogLines = new ObservableCollection<LogLineItem>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// DataGrid들을 초기화하고 바인딩 설정
        /// </summary>
        public void InitializeDataGrids(DataGrid dataLogDataGrid, DataGrid eventLogDataGrid, 
            DataGrid debugLogDataGrid, DataGrid exceptionLogDataGrid)
        {
            try
            {
                // DataGrid 참조 저장
                _dataLogDataGrid = dataLogDataGrid;
                _eventLogDataGrid = eventLogDataGrid;
                _debugLogDataGrid = debugLogDataGrid;
                _exceptionLogDataGrid = exceptionLogDataGrid;

                // DataGrid 설정 먼저 적용 (빈 행 제거를 위한 설정)
                ConfigureDataGridProperties(_dataLogDataGrid);
                ConfigureDataGridProperties(_eventLogDataGrid);
                ConfigureDataGridProperties(_debugLogDataGrid);
                ConfigureDataGridProperties(_exceptionLogDataGrid);

                // ItemSource 바인딩 (설정 후에 바인딩)
                _dataLogDataGrid.ItemsSource = _dataLogLines;
                _eventLogDataGrid.ItemsSource = _eventLogLines;
                _debugLogDataGrid.ItemsSource = _debugLogLines;
                _exceptionLogDataGrid.ItemsSource = _exceptionLogLines;

                // LoadingRow 이벤트 핸들러 추가
                _dataLogDataGrid.LoadingRow += DataGrid_LoadingRow;
                _eventLogDataGrid.LoadingRow += DataGrid_LoadingRow;
                _exceptionLogDataGrid.LoadingRow += DataGrid_LoadingRow;

                // LogDataGridHelper 기능 설정
                LogDataGridHelper.SetupDataGridFeatures(_dataLogDataGrid);
                LogDataGridHelper.SetupDataGridFeatures(_eventLogDataGrid);
                LogDataGridHelper.SetupDataGridFeatures(_debugLogDataGrid);
                LogDataGridHelper.SetupDataGridFeatures(_exceptionLogDataGrid);

                _workLogService.AddLog("? DataGrid 초기화 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DataGrid 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// DataGrid 속성 설정 (빈 행 제거 포함)
        /// </summary>
        private void ConfigureDataGridProperties(DataGrid dataGrid)
        {
            if (dataGrid == null) return;

            try
            {
                // 빈 행 제거를 위한 핵심 설정
                dataGrid.CanUserAddRows = false;
                dataGrid.CanUserDeleteRows = false;
                dataGrid.CanUserResizeRows = false;
                
                // 추가적인 빈 행 제거 설정
                dataGrid.AutoGenerateColumns = false;
                dataGrid.ItemsSource = null; // 임시로 null 설정
                
                // 기타 설정
                dataGrid.SelectionMode = DataGridSelectionMode.Extended;
                dataGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
                // Ensure full grid lines and visible brushes (override previous horizontal-only setting)
                dataGrid.GridLinesVisibility = DataGridGridLinesVisibility.All;
                dataGrid.HorizontalGridLinesBrush = System.Windows.Media.Brushes.LightGray;
                dataGrid.VerticalGridLinesBrush = System.Windows.Media.Brushes.LightGray;
                dataGrid.BorderBrush = System.Windows.Media.Brushes.LightGray;
                dataGrid.BorderThickness = new System.Windows.Thickness(1);
                dataGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
                
                // Apply global cell/header styles if available to ensure consistent borders
                try
                {
                    var cellStyle = System.Windows.Application.Current.TryFindResource("GridCellStyle") as System.Windows.Style;
                    var headerStyle = System.Windows.Application.Current.TryFindResource("GridHeaderStyle") as System.Windows.Style;
                    if (cellStyle != null) dataGrid.CellStyle = cellStyle;
                    if (headerStyle != null) dataGrid.ColumnHeaderStyle = headerStyle;
                }
                catch { }

                // 성능 향상을 위한 가상화 설정
                dataGrid.EnableRowVirtualization = true;
                dataGrid.EnableColumnVirtualization = true;
                
                // 자동 크기 조정 비활성화 (성능 향상)
                dataGrid.CanUserReorderColumns = false;
                dataGrid.CanUserSortColumns = true;
                
                // 행 높이 관련 설정
                dataGrid.RowHeight = double.NaN; // 자동 크기
                dataGrid.MinRowHeight = 20;
                
                _workLogService.AddLog($"? DataGrid 속성 설정 완료: {dataGrid.Name}", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DataGrid 속성 설정 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 특정 로그 타입의 데이터를 로드
        /// </summary>
        public async void LoadDataGridByType(string logType, DateTime selectedDate, string searchText, string searchMode)
        {
            try
            {
                var targetCollection = GetCollectionByType(logType);
                var targetDataGrid = GetDataGridByType(logType);
                
                if (targetCollection == null || targetDataGrid == null)
                {
                    _workLogService.AddLog($"? 알 수 없는 로그 타입: {logType}", WorkLogType.Error);
                    return;
                }

                _workLogService.AddLog($"{logType} DataGrid 로드 시작", WorkLogType.Info);

                // 로그 데이터 로드
                var newLines = await _logLoadingService.LoadLogDataForDataGrid(logType, selectedDate, searchText, searchMode);
                
                // 데이터 바인딩을 임시로 해제
                targetDataGrid.ItemsSource = null;
                
                // 컬렉션 업데이트
                targetCollection.Clear();
                foreach (var line in newLines)
                {
                    line.LogLevel = logType;
                    targetCollection.Add(line);
                }

                // 빨간색 비즈니스 하이라이팅 적용
                ApplyRedBusinessHighlighting(targetCollection);
                
                // 데이터 바인딩 복원
                targetDataGrid.ItemsSource = targetCollection;

                _workLogService.AddLog($"? {logType}: {targetCollection.Count}개 로드 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} 로드 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 모든 DataGrid에 데이터를 병렬로 로드
        /// ?? UI 스레드 블로킹 방지: Task.Delay(1)로 주기적으로 제어권 반환
        /// </summary>
        public async Task LoadAllDataGridsButton_ClickAsync(DateTime selectedDate, string searchText, string searchMode, TimeSpan fromTime = default, TimeSpan toTime = default)
        {
            try
            {
                _workLogService.AddLog("모든 DataGrid 로드 시작", WorkLogType.Info);
                
                // 시간 범위 로깅
                if (fromTime != default || toTime != default)
                {
                    _workLogService.AddLog($"?? 시간 범위: {fromTime:hh\\:mm} ~ {toTime:hh\\:mm}", WorkLogType.Info);
                }
                
                // UI 스레드에 제어권 반환 (타이머가 동작할 시간 확보)
                await Task.Delay(1);
                
                // 모든 데이터를 병렬로 로드
                var allData = await _logLoadingService.LoadAllDataGridData(selectedDate, searchText, searchMode, fromTime, toTime);
                
                // UI 스레드에 제어권 반환
                await Task.Delay(1);
                
                // 각 컬렉션에 데이터를 할당 (한 번 바인딩 해제 후 재바인딩)
                int processedCount = 0;
                foreach (var kvp in allData)
                {
                    var collection = GetCollectionByType(kvp.Key);
                    var dataGrid = GetDataGridByType(kvp.Key);
                    
                    if (collection != null && dataGrid != null)
                    {
                        // 바인딩 해제를 임시로 수행
                        dataGrid.ItemsSource = null;
                        
                        // 컬렉션 업데이트
                        collection.Clear();
                        
                        // 데이터 추가 (100개마다 UI 업데이트 허용)
                        int addedCount = 0;
                        foreach (var item in kvp.Value)
                        {
                            item.LogLevel = kvp.Key;
                            collection.Add(item);
                            
                            addedCount++;
                            if (addedCount % 100 == 0)
                            {
                                // 100개마다 UI 스레드에 제어권 반환
                                await Task.Delay(1);
                            }
                        }

                        // 빨간색 비즈니스 하이라이팅 적용
                        ApplyRedBusinessHighlighting(collection);
                        
                        // 바인딩 복원
                        dataGrid.ItemsSource = collection;
                        
                        processedCount++;
                        _workLogService.AddLog($"?? {kvp.Key} 로드 완료: {kvp.Value.Count}개 행", WorkLogType.Info);
                        
                        // 각 DataGrid 처리 후 UI 업데이트 허용
                        await Task.Delay(10);
                    }
                }
                
                var totalCount = allData.Values.Sum(list => list.Count);
                string timeInfo = (fromTime != default || toTime != default) 
                    ? $" (시간범위: {fromTime:hh\\:mm}~{toTime:hh\\:mm})" 
                    : "";
                _workLogService.AddLog($"? 모든 DataGrid 로드 완료: 총 {totalCount}개 행{timeInfo}", WorkLogType.Success);
                
                // UI 스레드에 제어권 반환
                await Task.Delay(1);
                
                // 콤보박스 필터 옵션 업데이트
                UpdateComboBoxFilters();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DataGrid 로드 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 모든 DataGrid에 데이터를 병렬로 로드
        /// </summary>
        public void LoadAllDataGridsButton_Click(DateTime selectedDate, string searchText, string searchMode, TimeSpan fromTime = default, TimeSpan toTime = default)
        {
            // 비동기 작업이 진행되도록 호출하고, 결과를 기다리지 않음
            _ = LoadAllDataGridsButton_ClickAsync(selectedDate, searchText, searchMode, fromTime, toTime);
        }

        /// <summary>
        /// 콤보박스 필터 데이터 업데이트
        /// </summary>
        public void UpdateComboBoxFilters()
        {
            try
            {
                // 이벤트를 통해서 MainWindow에 콤보박스 업데이트 알림
                ComboBoxFiltersUpdated?.Invoke();
                _workLogService.AddLog("콤보박스 필터 업데이트 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 콤보박스 필터 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        // 콤보박스 업데이트 이벤트
        public event Action? ComboBoxFiltersUpdated;

        /// <summary>
        /// 모든 DataGrid에 키워드 필터 적용
        /// </summary>
        public void ApplyGlobalKeywordFilter(string keyword)
        {
            try
            {
                var collections = new Dictionary<string, (ObservableCollection<LogLineItem>, DataGrid?)>
                {
                    ["DATA"] = (_dataLogLines, _dataLogDataGrid),
                    ["EVENT"] = (_eventLogLines, _eventLogDataGrid),
                    ["DEBUG"] = (_debugLogLines, _debugLogDataGrid),
                    ["EXCEPTION"] = (_exceptionLogLines, _exceptionLogDataGrid)
                };

                foreach (var kvp in collections)
                {
                    var (collection, dataGrid) = kvp.Value;
                    if (dataGrid != null)
                    {
                        LogDataGridHelper.ApplyFilters(dataGrid, collection, "ALL", "ALL", keyword);
                    }
                }

                _currentKeywordFilter = keyword;
                var totalFiltered = GetTotalFilteredCount();
                _workLogService.AddLog($"글로벌 키워드 필터: '{keyword}' (총 {totalFiltered}개 행)", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 글로벌 필터 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 모든 필터 초기화
        /// </summary>
        public void ClearAllFilters()
        {
            try
            {
                var dataGrids = new[] { _dataLogDataGrid, _eventLogDataGrid, _debugLogDataGrid, _exceptionLogDataGrid };

                foreach (var dataGrid in dataGrids.Where(dg => dg != null))
                {
                    var view = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
                    if (view != null)
                    {
                        view.Filter = null;
                        view.Refresh();
                    }
                }

                _currentLogLevelFilter = "ALL";
                _currentBusinessNameFilter = "ALL";
                _currentKeywordFilter = "";
                
                _workLogService.AddLog("모든 필터 초기화됨", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Content 영역 토글
        /// </summary>
        public void ToggleAllContent(ref bool isContentExpanded)
        {
            try
            {
                isContentExpanded = !isContentExpanded;
                
                var dataGrids = new[] { _dataLogDataGrid, _eventLogDataGrid, _debugLogDataGrid, _exceptionLogDataGrid,
                                        _dataLogDataGrid_Tab, _eventLogDataGrid_Tab, _debugLogDataGrid_Tab, _exceptionLogDataGrid_Tab };

                foreach (var dataGrid in dataGrids.Where(dg => dg != null))
                {
                    var contentColumn = dataGrid.Columns.FirstOrDefault(col => col.Header?.ToString() == "Content");
                    if (contentColumn != null)
                    {
                        contentColumn.Visibility = isContentExpanded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                    }
                }
                
                _workLogService.AddLog($"모든 Content 영역: {(isContentExpanded ? "펼침" : "접음")}", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Content 토글 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 특정 라인으로 이동
        /// </summary>
        public void GoToLine(int targetLineNumber, string logType = "DATA")
        {
            try
            {
                var dataGrid = GetDataGridByType(logType);
                if (dataGrid != null)
                {
                    LogDataGridHelper.ScrollToLine(dataGrid, targetLineNumber);
                    _workLogService.AddLog($"{logType} DataGrid: 행 {targetLineNumber}로 이동 완료", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 라인 이동 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 키워드 검색 및 이동
        /// </summary>
        public void FindAndGoToKeyword(string keyword, string logType = "DATA")
        {
            try
            {
                var dataGrid = GetDataGridByType(logType);
                var collection = GetCollectionByType(logType);
                
                if (dataGrid != null && collection != null)
                {
                    LogDataGridHelper.FindAndScrollToKeyword(dataGrid, collection, keyword);
                    _workLogService.AddLog($"{logType}에서 '{keyword}' 검색 완료", WorkLogType.Success);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 키워드 검색 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 검색 결과 하이라이트
        /// </summary>
        public void HighlightSearchResults(string searchText)
        {
            try
            {
                var collections = new[] { _dataLogLines, _eventLogLines, _debugLogLines, _exceptionLogLines };
                var logTypes = new[] { "DATA", "EVENT", "DEBUG", "EXCEPTION" };

                for (int i = 0; i < collections.Length; i++)
                {
                    LogDataGridHelper.HighlightSearchResults(collections[i], searchText);
                    _workLogService.AddLog($"{logTypes[i]} 검색 하이라이트 적용", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 검색 하이라이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 하이라이트 초기화
        /// </summary>
        public void ClearHighlights()
        {
            try
            {
                var collections = new[] { _dataLogLines, _eventLogLines, _debugLogLines, _exceptionLogLines };
                
                foreach (var collection in collections)
                {
                    LogDataGridHelper.ClearHighlights(collection);
                }
                
                _workLogService.AddLog("모든 하이라이트 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 하이라이트 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 상태 정보 업데이트
        /// </summary>
        public string GetStatusInfo()
        {
            try
            {
                var totalCount = _dataLogLines.Count + _eventLogLines.Count + _debugLogLines.Count + _exceptionLogLines.Count;
                var filteredCount = GetTotalFilteredCount();
                
                return $"전체: {totalCount}개 행, 필터링됨: {filteredCount}개 행";
            }
            catch
            {
                return "상태 정보 확인 중...";
            }
        }

        /// <summary>
        /// 특정 DataGrid에 비즈니스명 필터 적용
        /// </summary>
        public void ApplyBusinessNameFilter(string logType, string businessName)
        {
            try
            {
                var dataGrid = GetDataGridByType(logType);
                var collection = GetCollectionByType(logType);
                
                if (dataGrid != null && collection != null)
                {
                    LogDataGridHelper.ApplyFilters(dataGrid, collection, "ALL", businessName, _currentKeywordFilter);
                    _workLogService.AddLog($"??? {logType} 비즈니스명 필터: '{businessName}' 적용", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} 비즈니스명 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 특정 DataGrid의 비즈니스명 목록 반환
        /// </summary>
        public List<string> GetBusinessNamesForLogType(string logType)
        {
            try
            {
                var collection = GetCollectionByType(logType);
                if (collection != null)
                {
                    var businessNames = LogDataGridHelper.GetUniqueBusinessNames(collection);
                    _workLogService.AddLog($"{logType} 비즈니스명: {businessNames.Count}개 항목", WorkLogType.Info);
                    return businessNames;
                }
                return new List<string>();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} 비즈니스명 조회 오류: {ex.Message}", WorkLogType.Error);
                return new List<string>();
            }
        }

        /// <summary>
        /// 특정 DataGrid의 필터 초기화
        /// </summary>
        public void ClearFiltersForLogType(string logType)
        {
            try
            {
                var dataGrid = GetDataGridByType(logType);
                if (dataGrid != null)
                {
                    var view = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
                    if (view != null)
                    {
                        view.Filter = null;
                        view.Refresh();
                    }
                    _workLogService.AddLog($"{logType} 필터 초기화", WorkLogType.Success);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 특정 DataGrid에 MsgId 필터 적용
        /// </summary>
        public void ApplyMsgIdFilter(string logType, string msgId)
        {
            try
            {
                var dataGrid = GetDataGridByType(logType);
                var collection = GetCollectionByType(logType);
                
                if (dataGrid != null && collection != null)
                {
                    LogDataGridHelper.ApplyMsgIdFilter(dataGrid, collection, msgId, _currentKeywordFilter);
                    _workLogService.AddLog($"{logType} MsgId 필터: '{msgId}' 적용", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} MsgId 필터 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 특정 DataGrid의 MsgId 목록 반환
        /// </summary>
        public List<string> GetMsgIdsForLogType(string logType)
        {
            try
            {
                var collection = GetCollectionByType(logType);
                if (collection != null)
                {
                    var msgIds = LogDataGridHelper.GetUniqueMsgIds(collection);
                    _workLogService.AddLog($"{logType} MsgId: {msgIds.Count}개 항목", WorkLogType.Info);
                    return msgIds;
                }
                return new List<string>();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} MsgId 조회 오류: {ex.Message}", WorkLogType.Error);
                return new List<string>();
            }
        }

        /// <summary>
        /// 멀티선택 필터 적용
        /// </summary>
        public void ApplyMultiSelectFilter(string logType, List<string> selectedItems)
        {
            try
            {
                var dataGrid = GetDataGridByType(logType);
                var collection = GetCollectionByType(logType);
                
                if (dataGrid != null && collection != null)
                {
                    var view = CollectionViewSource.GetDefaultView(collection);
                    
                    if (selectedItems == null || selectedItems.Count == 0)
                    {
                        // 선택된 항목이 없으면 모든 항목 표시
                        view.Filter = null;
                    }
                    else
                    {
                        view.Filter = item =>
                        {
                            if (item is not LogLineItem logItem) return false;
                            
                            // 키워드 필터 (기존 글로벌 키워드가 있다면 유지)
                            if (!string.IsNullOrWhiteSpace(_currentKeywordFilter))
                            {
                                bool containsKeyword = logItem.Content.Contains(_currentKeywordFilter, StringComparison.OrdinalIgnoreCase) ||
                                                      logItem.BusinessName.Contains(_currentKeywordFilter, StringComparison.OrdinalIgnoreCase) ||
                                                      logItem.MsgId.Contains(_currentKeywordFilter, StringComparison.OrdinalIgnoreCase);
                                if (!containsKeyword)
                                    return false;
                            }
                            
                            // 멀티선택 필터 적용
                            switch (logType)
                            {
                                case "DATA":
                                case "EXCEPTION":
                                    return selectedItems.Any(selected => 
                                        logItem.BusinessName.Contains(selected, StringComparison.OrdinalIgnoreCase));
                                case "EVENT":
                                    return selectedItems.Any(selected => 
                                        logItem.MsgId.Contains(selected, StringComparison.OrdinalIgnoreCase));
                                default:
                                    return true;
                            }
                        };
                    }
                    
                    view.Refresh();
                    
                    var filteredCount = view.Cast<object>().Count();
                    _workLogService.AddLog($"{logType} 멀티선택 필터 적용: {selectedItems.Count}개 조건, {filteredCount}개 행 표시", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} 멀티선택 필터 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Content 영역 토글
        /// </summary>
        public void ToggleAllTabContent(ref bool isContentExpanded)
        {
            try
            {
                isContentExpanded = !isContentExpanded;
                
                var dataGrids = new[] { _dataLogDataGrid_Tab, _eventLogDataGrid_Tab, _debugLogDataGrid_Tab, _exceptionLogDataGrid_Tab };

                foreach (var dataGrid in dataGrids.Where(dg => dg != null))
                {
                    var contentColumn = dataGrid.Columns.FirstOrDefault(col => col.Header?.ToString() == "Content");
                    if (contentColumn != null)
                    {
                        contentColumn.Visibility = isContentExpanded ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                      }
                }
                
                _workLogService.AddLog($"모든 Tab Content 영역: {(isContentExpanded ? "펼침" : "접음")}", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Tab Content 토글 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 특정 탭의 DataGrid 초기화
        /// </summary>
        public void InitializeTabDataGrid(DataGrid? dataLogTab, DataGrid? eventLogTab, DataGrid? debugLogTab, DataGrid? exceptionTab)
        {
            try
            {
                // Tab DataGrid 참조 저장
                _dataLogDataGrid_Tab = dataLogTab;
                _eventLogDataGrid_Tab = eventLogTab;
                _debugLogDataGrid_Tab = debugLogTab;
                _exceptionLogDataGrid_Tab = exceptionTab;

                // Tab DataGrid 설정 먼저 적용 (빈 행 제거를 위한 설정)
                ConfigureDataGridProperties(_dataLogDataGrid_Tab);
                ConfigureDataGridProperties(_eventLogDataGrid_Tab);
                ConfigureDataGridProperties(_debugLogDataGrid_Tab);
                ConfigureDataGridProperties(_exceptionLogDataGrid_Tab);

                // ItemSource 바인딩 (설정 후에 바인딩)
                _dataLogDataGrid_Tab.ItemsSource = _dataLogLines;
                _eventLogDataGrid_Tab.ItemsSource = _eventLogLines;
                _debugLogDataGrid_Tab.ItemsSource = _debugLogLines;
                _exceptionLogDataGrid_Tab.ItemsSource = _exceptionLogLines;

                // LoadingRow 이벤트 핸들러 추가
                _dataLogDataGrid_Tab.LoadingRow += DataGrid_LoadingRow;
                _exceptionLogDataGrid_Tab.LoadingRow += DataGrid_LoadingRow;

                // LogDataGridHelper 기능 설정
                LogDataGridHelper.SetupDataGridFeatures(_dataLogDataGrid_Tab);
                LogDataGridHelper.SetupDataGridFeatures(_eventLogDataGrid_Tab);
                LogDataGridHelper.SetupDataGridFeatures(_debugLogDataGrid_Tab);
                LogDataGridHelper.SetupDataGridFeatures(_exceptionLogDataGrid_Tab);

                _workLogService.AddLog("? Tab DataGrid 초기화 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Tab DataGrid 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Register the 4-tab DataGrids so manager operations cover both panel and tab views.
        /// </summary>
        public void RegisterTabDataGrids(DataGrid? dataLogTab, DataGrid? eventLogTab, DataGrid? debugLogTab, DataGrid? exceptionTab)
        {
            _dataLogDataGrid_Tab = dataLogTab;
            _eventLogDataGrid_Tab = eventLogTab;
            _debugLogDataGrid_Tab = debugLogTab;
            _exceptionLogDataGrid_Tab = exceptionTab;

            try
            {
                if (_dataLogDataGrid_Tab != null)
                {
                    ConfigureDataGridProperties(_dataLogDataGrid_Tab);
                    _dataLogDataGrid_Tab.ItemsSource = _dataLogLines;
                    LogDataGridHelper.SetupDataGridFeatures(_dataLogDataGrid_Tab);
                    // Row styling (e.g., red business) should also apply to tab grid
                    _dataLogDataGrid_Tab.LoadingRow += DataGrid_LoadingRow;
                }

                if (_eventLogDataGrid_Tab != null)
                {
                    ConfigureDataGridProperties(_eventLogDataGrid_Tab);
                    _eventLogDataGrid_Tab.ItemsSource = _eventLogLines;
                    LogDataGridHelper.SetupDataGridFeatures(_eventLogDataGrid_Tab);
                    // Row styling (e.g., red business) should also apply to tab grid
                    _eventLogDataGrid_Tab.LoadingRow += DataGrid_LoadingRow;
                }

                if (_debugLogDataGrid_Tab != null)
                {
                    ConfigureDataGridProperties(_debugLogDataGrid_Tab);
                    _debugLogDataGrid_Tab.ItemsSource = _debugLogLines;
                    LogDataGridHelper.SetupDataGridFeatures(_debugLogDataGrid_Tab);
                }

                if (_exceptionLogDataGrid_Tab != null)
                {
                    ConfigureDataGridProperties(_exceptionLogDataGrid_Tab);
                    _exceptionLogDataGrid_Tab.ItemsSource = _exceptionLogLines;
                    LogDataGridHelper.SetupDataGridFeatures(_exceptionLogDataGrid_Tab);
                    // Row styling (e.g., red business) should also apply to tab grid
                    _exceptionLogDataGrid_Tab.LoadingRow += DataGrid_LoadingRow;
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Tab DataGrid 등록 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 로그 타입에 따른 컬렉션 반환
        /// </summary>
        private ObservableCollection<LogLineItem>? GetCollectionByType(string logType)
        {
            return logType switch
            {
                "DATA" => _dataLogLines,
                "EVENT" => _eventLogLines,
                "DEBUG" => _debugLogLines,
                "EXCEPTION" => _exceptionLogLines,
                _ => null
            };
        }

        /// <summary>
        /// 로그 타입에 따른 DataGrid 반환
        /// </summary>
        private DataGrid? GetDataGridByType(string logType)
        {
            return logType switch
            {
                "DATA" => _dataLogDataGrid,
                "EVENT" => _eventLogDataGrid,
                "DEBUG" => _debugLogDataGrid,
                "EXCEPTION" => _exceptionLogDataGrid,
                _ => null
            };
        }

        /// <summary>
        /// 전체 필터링된 행 수 계산
        /// </summary>
        private int GetTotalFilteredCount()
        {
            try
            {
                var dataGrids = new[] { _dataLogDataGrid, _eventLogDataGrid, _debugLogDataGrid, _exceptionLogDataGrid };

                int totalCount = 0;
                foreach (var dataGrid in dataGrids.Where(dg => dg != null))
                {
                    var view = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);
                    totalCount += view?.Cast<object>().Count() ?? 0;
                }
                
                return totalCount;
            }
            catch
            {
                return _dataLogLines.Count + _eventLogLines.Count + _debugLogLines.Count + _exceptionLogLines.Count;
            }
        }

        /// <summary>
        /// DATA DataGrid 비즈니스명 필터 업데이트
        /// </summary>
        private void UpdateDataBusinessFilter()
        {
            try
            {
                var businessNames = LogDataGridHelper.GetUniqueBusinessNames(_dataLogLines);
                _workLogService.AddLog($"??? DATA 비즈니스명: {businessNames.Count}개 항목", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 비즈니스명 필터 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// DataGrid의 행(Row)이 로드될 때 호출되어 스타일을 지정하는 이벤트 핸들러
        /// </summary>
        private void DataGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            try
            {
                if (e.Row.DataContext is LogLineItem item)
                {
                    var redBusinessNamesColor= _redBusinessListManager.GetEnabledBusinessNamesColor();
                    var redBusinessNames = _redBusinessListManager.GetEnabledBusinessNames();

                    // Try to match BusinessName first (existing behavior)
                    int index = -1;
                    if (!string.IsNullOrEmpty(item.BusinessName))
                    {
                        index = redBusinessNames.FindIndex(name => string.Equals(name, item.BusinessName, System.StringComparison.OrdinalIgnoreCase));
                    }

                    // If not found and this is an EVENT row, try matching MsgId against the red list
                    if (index < 0 && !string.IsNullOrEmpty(item.MsgId))
                    {
                        index = redBusinessNames.FindIndex(name => string.Equals(name, item.MsgId, System.StringComparison.OrdinalIgnoreCase));
                    }

                    if (index >= 0)
                    {
                        var color = redBusinessNamesColor.ElementAtOrDefault(index) ?? "LightCoral";
                        e.Row.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
                    }
                    else
                    {
                        // AlternatingRowBackground를 위해 기본 색상으로 되돌림
                        e.Row.ClearValue(DataGridRow.BackgroundProperty);
                    }
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DataGrid 행 스타일링 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 빨간색 비즈니스 하이라이팅 적용
        /// </summary>
        private void ApplyRedBusinessHighlighting(ObservableCollection<LogLineItem> collection)
        {
            try
            {
                var redBusinessNames = _redBusinessListManager.GetEnabledBusinessNames();
                
                foreach (var item in collection)
                {
                    if (!string.IsNullOrEmpty(item.BusinessName) && redBusinessNames.Contains(item.BusinessName))
                    {
                        item.IsRedBusiness = true;
                        item.ApplyRedBusinessHighlight();
                    }
                }
                
                _workLogService.AddLog($"? 빨간색 비즈니스 하이라이팅 적용: {redBusinessNames.Count}개 항목", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 빨간색 비즈니스 하이라이팅 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion
    }
}