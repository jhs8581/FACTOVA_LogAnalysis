using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Services;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfDataGrid = System.Windows.Controls.DataGrid;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace FACTOVA_LogAnalysis
{
    /// <summary>
    /// 통합 로그 탭 관련 기능 (Unified Log Tab)
    /// 4개의 로그 파일(DATA, EVENT, DEBUG, EXCEPTION)을 시간순으로 통합하여 표시
    /// </summary>
    public partial class MainWindow : Window
    {
        // 통합 로그 데이터
        private ObservableCollection<LogLineItem> _unifiedLogLines = new ObservableCollection<LogLineItem>();
        private ObservableCollection<LogLineItem> _unfilteredUnifiedLogLines = new ObservableCollection<LogLineItem>();
        
        // 통합 Business/MsgId 필터 아이템
        private ObservableCollection<FilterItem> _unifiedBusinessMsgIdFilterItems = new ObservableCollection<FilterItem>();
        
        // 콘텐츠 컬럼 토글 상태
        private bool _isUnifiedContentExpanded = true;
        private DataGridLength _unifiedOriginalContentWidth = new DataGridLength(1, DataGridLengthUnitType.Star);

        /// <summary>
        /// 통합 로그 데이터를 로드하고 시간순으로 정렬
        /// </summary>
        public async Task LoadUnifiedLogData(DateTime selectedDate, string searchText, string searchMode, TimeSpan fromTime = default, TimeSpan toTime = default)
        {
            try
            {
                _workLogService.AddLog("=== 통합 로그 생성 중 (기존 데이터 활용) ===", WorkLogType.Info);
                
                // ? 1. 이미 로드된 DataGrid 데이터 합치기 (파일을 다시 읽지 않음!)
                var combinedList = new List<LogLineItem>();
                
                if (_dataGridManager.DataLogLines != null)
                {
                    _workLogService.AddLog($"DATA: {_dataGridManager.DataLogLines.Count}개", WorkLogType.Info);
                    combinedList.AddRange(_dataGridManager.DataLogLines);
                }
                
                if (_dataGridManager.EventLogLines != null)
                {
                    _workLogService.AddLog($"EVENT: {_dataGridManager.EventLogLines.Count}개", WorkLogType.Info);
                    combinedList.AddRange(_dataGridManager.EventLogLines);
                }
                
                if (_dataGridManager.DebugLogLines != null)
                {
                    _workLogService.AddLog($"DEBUG: {_dataGridManager.DebugLogLines.Count}개", WorkLogType.Info);
                    combinedList.AddRange(_dataGridManager.DebugLogLines);
                }
                
                if (_dataGridManager.ExceptionLogLines != null)
                {
                    _workLogService.AddLog($"EXCEPTION: {_dataGridManager.ExceptionLogLines.Count}개", WorkLogType.Info);
                    combinedList.AddRange(_dataGridManager.ExceptionLogLines);
                }
                
                _workLogService.AddLog($"총 {combinedList.Count}개 로그 합침", WorkLogType.Info);
                
                // ? 2. 시간순 정렬 (밀리초까지 정확하게)
                var sortedList = combinedList
                    .OrderBy(item => ParseTimestamp(item.Timestamp))
                    .ToList();
                
                // ?? 정렬 후 샘플 5개 출력 (밀리초 확인용)
                _workLogService.AddLog("=== 정렬 후 샘플 (처음 5개) ===", WorkLogType.Info);
                for (int i = 0; i < Math.Min(5, sortedList.Count); i++)
                {
                    var item = sortedList[i];
                    _workLogService.AddLog($"[샘플 {i + 1}] LogType={item.LogLevel}, Timestamp={item.Timestamp}, Business={item.BusinessName}", WorkLogType.Info);
                }
                
                // ? 3. 통합 로그 컬렉션에 추가
                await Dispatcher.InvokeAsync(() =>
                {
                    _unfilteredUnifiedLogLines.Clear();
                    _unifiedLogLines.Clear();
                    
                    foreach (var item in sortedList)
                    {
                        _unfilteredUnifiedLogLines.Add(item);
                        _unifiedLogLines.Add(item);
                    }
                    
                    // ? Red Business 하이라이트 적용
                    ApplyRedBrushToUnifiedLog(sortedList);
                    
                    // ? 4. DataGrid에 바인딩
                    var grid = FindName("unifiedLogDataGrid") as DataGrid;
                    if (grid != null)
                    {
                        grid.ItemsSource = _unifiedLogLines;
                        
                        // ? DataGrid CellStyle 적용 - TextColor 바인딩
                        ApplyTextColorStyleToUnifiedGrid(grid);
                    }
                    
                    // ? 5. 필터 초기화
                    UpdateUnifiedBusinessMsgIdFilters();
                    
                    _workLogService.AddLog($"? 통합 로그 생성 완료: {_unifiedLogLines.Count}개 행", WorkLogType.Success);
                });
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 통합 로그 로드 오류: {ex.Message}", WorkLogType.Error);
                _workLogService.AddLog($"StackTrace: {ex.StackTrace}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그에 RedBrush 적용
        /// </summary>
        private void ApplyRedBrushToUnifiedLog(List<LogLineItem> items)
        {
            try
            {
                int highlightCount = 0;

                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.BusinessName))
                        continue;

                    // RedBusinessListManager를 통해 매칭 확인
                    var matchedItem = _redBusinessListManager.Items
                        .FirstOrDefault(rb => rb.IsEnabled && 
                                             !string.IsNullOrWhiteSpace(rb.BusinessName) &&
                                             item.BusinessName.Contains(rb.BusinessName, StringComparison.OrdinalIgnoreCase));

                    if (matchedItem != null)
                    {
                        item.IsRedBusiness = true;
                        
                        // ? TextColor (Foreground)를 설정
                        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(matchedItem.Color);
                        item.TextColor = new System.Windows.Media.SolidColorBrush(color);
                        
                        highlightCount++;
                        
                        System.Diagnostics.Debug.WriteLine($"? 통합로그 색상 적용: {item.BusinessName} → {matchedItem.Color}");
                    }
                }

                if (highlightCount > 0)
                {
                    _workLogService.AddLog($"? 통합 로그에 {highlightCount}개 행 하이라이트 적용", WorkLogType.Info);
                }
                else
                {
                    _workLogService.AddLog("?? 통합 로그에 매칭되는 RedBusiness가 없습니다", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? RedBrush 적용 오류: {ex.Message}", WorkLogType.Error);
                System.Diagnostics.Debug.WriteLine($"ApplyRedBrushToUnifiedLog 오류: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Business/MsgId 필터 아이템 업데이트
        /// </summary>
        private void UpdateUnifiedBusinessMsgIdFilters()
        {
            try
            {
                var businessMsgIds = new HashSet<string>();
                
                foreach (var item in _unfilteredUnifiedLogLines)
                {
                    if (!string.IsNullOrWhiteSpace(item.BusinessName))
                        businessMsgIds.Add(item.BusinessName);
                    if (!string.IsNullOrWhiteSpace(item.MsgId))
                        businessMsgIds.Add(item.MsgId);
                }
                
                _unifiedBusinessMsgIdFilterItems.Clear();
                foreach (var name in businessMsgIds.OrderBy(x => x))
                {
                    _unifiedBusinessMsgIdFilterItems.Add(new FilterItem(name) { IsSelected = false });
                }
                
                var combo = FindName("UnifiedBusinessMsgIdFilterComboBox") as WpfComboBox;
                if (combo != null)
                {
                    combo.ItemsSource = _unifiedBusinessMsgIdFilterItems;
                }
                
                _workLogService.AddLog($"통합 Business/MsgId 필터: {businessMsgIds.Count}개 항목", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Business/MsgId 필터 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 타임스탬프 문자열을 DateTime으로 파싱 (밀리초 포함)
        /// </summary>
        private DateTime ParseTimestamp(string timestamp)
        {
            try
            {
                // yyyy-MM-dd HH:mm:ss.fff 형식 파싱
                if (DateTime.TryParseExact(timestamp, "yyyy-MM-dd HH:mm:ss.fff",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime result))
                {
                    return result;
                }

                // yyyy-MM-dd HH:mm:ss 형식 파싱
                if (DateTime.TryParseExact(timestamp, "yyyy-MM-dd HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime result2))
                {
                    return result2;
                }
                
                // HH:mm:ss.fff 형식 파싱
                if (DateTime.TryParseExact(timestamp, "HH:mm:ss.fff",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime result3))
                {
                    return result3;
                }

                // HH:mm:ss 형식 파싱
                if (DateTime.TryParseExact(timestamp, "HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime result4))
                {
                    return result4;
                }

                // 일반 파싱 시도
                if (DateTime.TryParse(timestamp, out DateTime result5))
                {
                    return result5;
                }

                // 파싱 실패 시 최소값 반환
                return DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// 통합 로그 LogType 필터 변경 이벤트
        /// </summary>
        private void UnifiedLogTypeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ApplyUnifiedLogFilters();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? LogType 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Business/MsgId 체크박스 체크 이벤트
        /// </summary>
        private void UnifiedBusinessMsgIdCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ApplyUnifiedLogFilters();
        }

        /// <summary>
        /// Business/MsgId 체크박스 해제 이벤트
        /// </summary>
        private void UnifiedBusinessMsgIdCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyUnifiedLogFilters();
        }

        /// <summary>
        /// Business/MsgId 필터 초기화
        /// </summary>
        private void ClearUnifiedBusinessMsgIdFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var item in _unifiedBusinessMsgIdFilterItems)
                {
                    item.IsSelected = false;
                }
                ApplyUnifiedLogFilters();
                _workLogService.AddLog("통합 Business/MsgId 필터 초기화", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Business/MsgId 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 Content 필터 변경 이벤트
        /// </summary>
        private void UnifiedContentFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyUnifiedLogFilters();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Content 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Content 필터 초기화
        /// </summary>
        private void ClearUnifiedContentFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var contentTextBox = FindName("UnifiedContentFilterTextBox") as WpfTextBox;
                if (contentTextBox != null)
                {
                    contentTextBox.Text = "";
                }
                _workLogService.AddLog("통합 Content 필터 초기화", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Content 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 필터 적용
        /// </summary>
        private void ApplyUnifiedLogFilters()
        {
            try
            {
                if (_isInitializing) return;

                var logTypeCombo = FindName("UnifiedLogTypeFilterComboBox") as WpfComboBox;
                var contentTextBox = FindName("UnifiedContentFilterTextBox") as WpfTextBox;

                string selectedLogType = (logTypeCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";
                string contentFilter = contentTextBox?.Text?.Trim() ?? "";
                
                // 선택된 Business/MsgId 필터
                var selectedBusinessMsgIds = _unifiedBusinessMsgIdFilterItems
                    .Where(x => x.IsSelected)
                    .Select(x => x.Value)
                    .ToList();

                _unifiedLogLines.Clear();

                foreach (var item in _unfilteredUnifiedLogLines)
                {
                    // LogType 필터
                    if (selectedLogType != "ALL" && item.LogLevel != selectedLogType)
                        continue;

                    // Business/MsgId 필터 (멀티셀렉트)
                    if (selectedBusinessMsgIds.Any())
                    {
                        bool matchBusiness = !string.IsNullOrWhiteSpace(item.BusinessName) && 
                                            selectedBusinessMsgIds.Any(filter => item.BusinessName.Contains(filter, StringComparison.OrdinalIgnoreCase));
                        bool matchMsgId = !string.IsNullOrWhiteSpace(item.MsgId) && 
                                         selectedBusinessMsgIds.Any(filter => item.MsgId.Contains(filter, StringComparison.OrdinalIgnoreCase));
                        
                        if (!matchBusiness && !matchMsgId)
                            continue;
                    }

                    // Content 필터
                    if (!string.IsNullOrEmpty(contentFilter))
                    {
                        if (item.Content?.Contains(contentFilter, StringComparison.OrdinalIgnoreCase) != true)
                            continue;
                    }

                    _unifiedLogLines.Add(item);
                }

                // 상태 업데이트
                var statusText = FindName("UnifiedLogStatusText") as WpfTextBlock;
                if (statusText != null)
                {
                    statusText.Text = $"통합 로그: {_unifiedLogLines.Count}개 행 (필터링됨)";
                }

                _workLogService.AddLog($"통합 로그 필터 적용: {_unifiedLogLines.Count}개 행 표시", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 통합 로그 필터 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 필터 전체 초기화
        /// </summary>
        private void ClearUnifiedFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logTypeCombo = FindName("UnifiedLogTypeFilterComboBox") as WpfComboBox;
                var contentTextBox = FindName("UnifiedContentFilterTextBox") as WpfTextBox;

                if (logTypeCombo != null)
                    logTypeCombo.SelectedIndex = 0; // "전체"

                if (contentTextBox != null)
                    contentTextBox.Text = "";
                
                // Business/MsgId 필터 초기화
                foreach (var item in _unifiedBusinessMsgIdFilterItems)
                {
                    item.IsSelected = false;
                }

                _workLogService.AddLog("? 통합 로그 전체 필터 초기화 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 통합 로그 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 폰트 크기 감소
        /// </summary>
        private void UnifiedLogFontSizeDecrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = FindName("unifiedLogDataGrid") as DataGrid;
                if (grid == null) return;

                double currentSize = grid.FontSize;
                double newSize = Math.Max(8, currentSize - 1); // 최소 8

                ApplyFontToDataGridInstance(grid, newSize);
                
                // ? TextBox 업데이트
                var fontSizeTextBox = FindName("UnifiedLogFontSizeTextBox") as System.Windows.Controls.TextBox;
                if (fontSizeTextBox != null)
                {
                    fontSizeTextBox.Text = newSize.ToString();
                }
                
                // AppSettings에 저장
                _appSettings.UnifiedLogFontSize = newSize;
                _appSettings.Save();

                _workLogService.AddLog($"? 통합 로그 폰트 크기 변경: {currentSize} → {newSize}", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 통합 로그 폰트 크기 감소 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 폰트 크기 증가
        /// </summary>
        private void UnifiedLogFontSizeIncrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = FindName("unifiedLogDataGrid") as DataGrid;
                if (grid == null) return;

                double currentSize = grid.FontSize;
                double newSize = Math.Min(32, currentSize + 1); // 최대 32

                ApplyFontToDataGridInstance(grid, newSize);
                
                // ? TextBox 업데이트
                var fontSizeTextBox = FindName("UnifiedLogFontSizeTextBox") as System.Windows.Controls.TextBox;
                if (fontSizeTextBox != null)
                {
                    fontSizeTextBox.Text = newSize.ToString();
                }
                
                // AppSettings에 저장
                _appSettings.UnifiedLogFontSize = newSize;
                _appSettings.Save();

                _workLogService.AddLog($"? 통합 로그 폰트 크기 변경: {currentSize} → {newSize}", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 통합 로그 폰트 크기 증가 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 콘텐츠 컬럼 토글 (완전히 숨김)
        /// </summary>
        private void ToggleUnifiedContentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = FindName("unifiedLogDataGrid") as WpfDataGrid;
                var button = sender as System.Windows.Controls.Button;

                if (grid == null) return;

                var contentColumn = grid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Content");
                if (contentColumn == null) return;

                if (_isUnifiedContentExpanded)
                {
                    // 콘텐츠 닫기 - 완전히 숨김
                    _unifiedOriginalContentWidth = contentColumn.Width;
                    contentColumn.Visibility = System.Windows.Visibility.Collapsed;
                    if (button != null) button.Content = "콘텐츠 열기";
                    _isUnifiedContentExpanded = false;
                    _workLogService.AddLog("통합 로그 콘텐츠 컬럼 닫기", WorkLogType.Info);
                }
                else
                {
                    // 콘텐츠 열기
                    contentColumn.Visibility = System.Windows.Visibility.Visible;
                    contentColumn.Width = _unifiedOriginalContentWidth;
                    if (button != null) button.Content = "콘텐츠 닫기";
                    _isUnifiedContentExpanded = true;
                    _workLogService.AddLog("통합 로그 콘텐츠 컬럼 열기", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 콘텐츠 토글 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 엑셀 다운로드
        /// </summary>
        private void ExportUnifiedLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = FindName("unifiedLogDataGrid") as WpfDataGrid;
                if (grid == null)
                {
                    _workLogService.AddLog("통합 로그 DataGrid를 찾을 수 없습니다.", WorkLogType.Warning);
                    return;
                }

                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var suggestedName = $"UnifiedLog_{timestamp}.xlsx";

                var sfd = new Microsoft.Win32.SaveFileDialog()
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    FileName = suggestedName,
                    DefaultExt = ".xlsx"
                };

                if (sfd.ShowDialog() != true) return;

                ExportDataGridToXlsx(grid, sfd.FileName);
                _workLogService.AddLog($"? 통합 로그를 XLSX로 저장했습니다: {sfd.FileName}", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 통합 로그 Export 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 DataGrid의 행이 로드될 때 RedBrush 적용
        /// </summary>
        private void UnifiedLogDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            try
            {
                if (e.Row.Item is LogLineItem logItem && logItem.IsRedBusiness)
                {
                    // IsRedBusiness가 true이면 해당 색상으로 배경 설정
                    e.Row.Background = logItem.TextColor;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UnifiedLogDataGrid_LoadingRow 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 통합 로그 DataGrid에 TextColor 바인딩 스타일 적용
        /// </summary>
        private void ApplyTextColorStyleToUnifiedGrid(DataGrid grid)
        {
            try
            {
                // ? CellStyle에 Background (배경색) 바인딩
                var cellStyle = new System.Windows.Style(typeof(DataGridCell));
                
                // ? TextColor가 null이 아닐 때만 Background 적용
                var trigger = new System.Windows.DataTrigger();
                trigger.Binding = new System.Windows.Data.Binding("IsRedBusiness");
                trigger.Value = true;
                
                // IsRedBusiness가 true일 때만 배경색 적용
                trigger.Setters.Add(new System.Windows.Setter(
                    DataGridCell.BackgroundProperty,
                    new System.Windows.Data.Binding("TextColor")
                ));
                
                cellStyle.Triggers.Add(trigger);
                grid.CellStyle = cellStyle;
                
                _workLogService.AddLog("? 통합 로그 DataGrid에 Background 색상 스타일 적용 완료 (조건부)", WorkLogType.Success);
                System.Diagnostics.Debug.WriteLine("? ApplyTextColorStyleToUnifiedGrid - Background 모드 (IsRedBusiness 조건)");
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? TextColor 스타일 적용 오류: {ex.Message}", WorkLogType.Error);
                System.Diagnostics.Debug.WriteLine($"ApplyTextColorStyleToUnifiedGrid 오류: {ex.StackTrace}");
            }
        }

        #region Unified Content Cell Height Management

        /// <summary>
        /// 통합 로그 Content Cell 높이 감소
        /// </summary>
        private void UnifiedContentCellHeightDecrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("UnifiedContentCellHeightTextBox") as System.Windows.Controls.TextBox;
                if (textBox == null || !double.TryParse(textBox.Text, out double height))
                {
                    _workLogService.AddLog("유효한 Content 높이 값을 입력하세요", WorkLogType.Warning);
                    return;
                }
                height = Math.Max(50, height - 10); // 최소 50
                textBox.Text = ((int)height).ToString();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"통합 로그 Content 높이 감소 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 Content Cell 높이 증가
        /// </summary>
        private void UnifiedContentCellHeightIncrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("UnifiedContentCellHeightTextBox") as System.Windows.Controls.TextBox;
                if (textBox == null || !double.TryParse(textBox.Text, out double height))
                {
                    _workLogService.AddLog("유효한 Content 높이 값을 입력하세요", WorkLogType.Warning);
                    return;
                }
                height = Math.Min(500, height + 10); // 최대 500
                textBox.Text = ((int)height).ToString();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"통합 로그 Content 높이 증가 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 통합 로그 Content Cell 높이 적용
        /// </summary>
        private void ApplyUnifiedContentCellHeight_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("UnifiedContentCellHeightTextBox") as System.Windows.Controls.TextBox;
                if (textBox == null || !double.TryParse(textBox.Text, out double height))
                {
                    _workLogService.AddLog("유효한 Content 높이 값을 입력하세요", WorkLogType.Warning);
                    return;
                }

                // 통합 로그 DataGrid 찾기
                var unifiedGrid = FindName("unifiedLogDataGrid") as DataGrid;
                if (unifiedGrid == null)
                {
                    _workLogService.AddLog("통합 로그 DataGrid를 찾을 수 없습니다", WorkLogType.Warning);
                    return;
                }

                // ContentCellStyle을 동적으로 생성
                var contentStyle = new System.Windows.Style(typeof(DataGridCell));
                contentStyle.Setters.Add(new System.Windows.Setter(DataGridCell.BorderBrushProperty, System.Windows.Media.Brushes.LightGray));
                contentStyle.Setters.Add(new System.Windows.Setter(DataGridCell.BorderThicknessProperty, new Thickness(0.5)));
                contentStyle.Setters.Add(new System.Windows.Setter(DataGridCell.PaddingProperty, new Thickness(0)));
                contentStyle.Setters.Add(new System.Windows.Setter(FrameworkElement.FocusVisualStyleProperty, null));

                var template = new ControlTemplate(typeof(DataGridCell));
                var factory = new FrameworkElementFactory(typeof(System.Windows.Controls.Border));
                factory.SetValue(System.Windows.Controls.Border.BorderBrushProperty, new TemplateBindingExtension(DataGridCell.BorderBrushProperty));
                factory.SetValue(System.Windows.Controls.Border.BorderThicknessProperty, new TemplateBindingExtension(DataGridCell.BorderThicknessProperty));
                factory.SetValue(System.Windows.Controls.Border.BackgroundProperty, System.Windows.Media.Brushes.White);

                var scrollViewerFactory = new FrameworkElementFactory(typeof(ScrollViewer));
                scrollViewerFactory.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
                scrollViewerFactory.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
                scrollViewerFactory.SetValue(FrameworkElement.MaxHeightProperty, height); // ✨ 사용자 지정 높이
                scrollViewerFactory.SetValue(UIElement.FocusableProperty, false);

                var textBoxFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBox));
                textBoxFactory.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("Content") { Mode = System.Windows.Data.BindingMode.OneWay });
                textBoxFactory.SetValue(System.Windows.Controls.TextBox.IsReadOnlyProperty, true);
                textBoxFactory.SetValue(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(0));
                textBoxFactory.SetValue(System.Windows.Controls.Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                textBoxFactory.SetValue(System.Windows.Controls.Control.ForegroundProperty, System.Windows.Media.Brushes.Black);
                textBoxFactory.SetValue(System.Windows.Controls.TextBox.TextWrappingProperty, TextWrapping.Wrap);
                textBoxFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2));
                textBoxFactory.SetValue(System.Windows.Controls.Control.PaddingProperty, new Thickness(0));
                textBoxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
                textBoxFactory.SetValue(System.Windows.Controls.Control.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas"));
                textBoxFactory.SetValue(System.Windows.Controls.TextBox.AcceptsReturnProperty, true);
                textBoxFactory.SetValue(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Arrow);
                textBoxFactory.SetValue(UIElement.FocusableProperty, true);
                textBoxFactory.SetValue(System.Windows.Controls.Control.IsTabStopProperty, false);
                textBoxFactory.SetValue(System.Windows.Controls.Primitives.TextBoxBase.SelectionBrushProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x99, 0xFF)));
                textBoxFactory.SetValue(System.Windows.Controls.Primitives.TextBoxBase.SelectionTextBrushProperty, System.Windows.Media.Brushes.White);
                textBoxFactory.SetValue(System.Windows.Controls.Primitives.TextBoxBase.SelectionOpacityProperty, 0.4);

                scrollViewerFactory.AppendChild(textBoxFactory);
                factory.AppendChild(scrollViewerFactory);
                template.VisualTree = factory;

                contentStyle.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.TemplateProperty, template));

                // Content 컬럼 찾기 및 적용
                var contentColumn = unifiedGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == "Content");
                if (contentColumn is DataGridTextColumn textCol)
                {
                    textCol.CellStyle = contentStyle;
                }
                
                unifiedGrid.Items.Refresh();
                unifiedGrid.UpdateLayout();

                // 설정 저장 (통합 로그 전용)
                try
                {
                    _appSettings.ContentCellMaxHeight = height;
                    _appSettings.Save();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save UnifiedContentCellMaxHeight: {ex.Message}");
                }

                _workLogService.AddLog($"통합 로그 Content 셀 높이 적용 완료: {height}px", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"통합 로그 Content 높이 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion
    }
}
