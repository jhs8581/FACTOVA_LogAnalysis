using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Services; // for WorkLogType
using WpfMessageBox = System.Windows.MessageBox;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfExpander = System.Windows.Controls.Expander;

namespace FACTOVA_LogAnalysis
{
    public partial class MainWindow
    {

        // Red business management handlers
        private void LoadRedBusinessList()
        {
            try
            {
                _redBusinessListManager.LoadFromFile();
                _workLogService.AddLog("?? 빨간색 비즈니스 목록 로드 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"Failed to load red business list: {ex.Message}", WorkLogType.Error);
                _redBusinessListManager.LoadSampleItems();
            }
        }

        private void AddRedBusinessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _redBusinessListManager.AddItem();
                var newItem = _redBusinessListManager.Items.LastOrDefault();
                if (newItem != null)
                {
                    var dg = FindName("redBusinessDataGrid") as DataGrid;
                    if (dg != null)
                    {
                        dg.SelectedItem = newItem;
                        dg.ScrollIntoView(newItem);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error adding item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteRedBusinessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dg = FindName("redBusinessDataGrid") as DataGrid;
                if (dg?.SelectedItem is Models.RedBusinessItem selectedItem)
                {
                    var result = WpfMessageBox.Show($"Are you sure you want to delete the item '{selectedItem.BusinessName}'?", "Delete Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        _redBusinessListManager.RemoveItem(selectedItem);
                    }
                }
                else
                {
                    WpfMessageBox.Show("Please select an item to delete.", "Notification", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Error deleting item: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveRedBusinessListButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _redBusinessListManager.SaveToFile();
                WpfMessageBox.Show("Red business list has been saved.", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Error saving red business list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadRedBusinessListButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadRedBusinessList();
                WpfMessageBox.Show("Red business list has been loaded.", "Load Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Error loading red business list: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RedBusinessDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                _workLogService.AddLog("빨간색 비즈니스 항목이 수정됨", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RedBusinessDataGrid_CellEditEnding error: {ex.Message}");
            }
        }


        // Submit / loading
        private async void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 진행 시간 표시 시작
                StartLoadProgress();
                
                // UI 비활성화
                SetControlsEnabled(false);
                
                _workLogService.AddLog("?? UI 잠금 - 로딩 시작", WorkLogType.Info);

                DateTime selectedDate = GetSelectedDate();
                string searchText = searchTextBox.Text;
                string searchMode = GetSelectedSearchMode();
                var (fromTime, toTime) = GetTimeRange();

                if (fromTime > toTime)
                {
                    _workLogService.AddLog("시작 시간이 종료 시간보다 늦습니다. 시간 범위를 확인해주세요.", WorkLogType.Warning);
                    SetControlsEnabled(true);
                    StopLoadProgress(); // 오류 시에도 중지
                    return;
                }

                bool loadText = LoadTextCheckBox?.IsChecked == true;
                bool loadDataGrid = LoadDataGridCheckBox?.IsChecked == true;
                // LoadExecTimeCheckBox는 현재 XAML에 없으므로 기본값 false 사용
                bool loadExecTime = false;

                if (!loadText && !loadDataGrid && !loadExecTime)
                {
                    _workLogService.AddLog("로드할 옵션을 하나 이상 선택해주세요", WorkLogType.Warning);
                    SetControlsEnabled(true);
                    StopLoadProgress(); // 오류 시에도 중지
                    return;
                }

                _workLogService.AddLog($"로드 시작: Text={loadText}, DataGrid={loadDataGrid}, exec.Time={loadExecTime}, 시간범위={fromTime:hh\\:mm}~{toTime:hh\\:mm}", WorkLogType.Info);

                // DataGrid 로딩 (완전히 백그라운드에서 실행)
                if (loadDataGrid)
                {
                    _workLogService.AddLog("DataGrid ?α? ?ε? ??...", WorkLogType.Info);
                    
                    // ?????? ???????? ??????? ?ε?
                    Dictionary<string, List<LogLineItem>>? allData = null;
                    
                    await Task.Run(async () =>
                    {
                        // ?????? ???????? ???? ?б?
                        allData = await _logLoadingService.LoadAllDataGridData(selectedDate, searchText, searchMode, fromTime, toTime);
                    });
                    
                    // UI ??????? ?????? ???ε?
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (allData != null)
                        {
                            // DataGridManager?? ?????? ???ε? ???
                            BindDataToDataGrids(allData);
                            
                            // 통합 로그 데이터도 로딩
                            _ = LoadUnifiedLogData(selectedDate, searchText, searchMode, fromTime, toTime);
                        }
                        
                        _workLogService.AddLog("? DataGrid ?ε? ???", WorkLogType.Success);
                        
                        if (!loadText)
                        {
                            LogTabControl.SelectedIndex = 1;
                        }
                    });
                }

                // UI 활성화
                SetControlsEnabled(true);
                _workLogService.AddLog("?? UI 잠금 해제", WorkLogType.Success);
                
                // 진행 시간 표시 중지
                StopLoadProgress();

                // 나머지는 백그라운드에서
                if (loadText)
                {
                    _workLogService.AddLog("텍스트 로그 로드 중... (백그라운드)", WorkLogType.Info);
                    _ = Task.Run(async () =>
                    {
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            await LoadIndividualLogFilesUsingService(selectedDate, searchText, searchMode, fromTime, toTime);
                            LogTabControl.SelectedIndex = 0;
                            _workLogService.AddLog("? 텍스트 로그 로드 완료", WorkLogType.Success);
                        });
                    });
                }

                if (loadExecTime)
                {
                    _workLogService.AddLog("exec.Time 분석 로드 중... (백그라운드)", WorkLogType.Info);
                    _ = Task.Run(async () =>
                    {
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            await LoadExecTimeAnalysis(selectedDate, searchText, searchMode, fromTime, toTime);
                            if (!loadText && !loadDataGrid)
                                LogTabControl.SelectedIndex = 2;
                            _workLogService.AddLog("? exec.Time 분석 로드 완료", WorkLogType.Success);
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"?? 로드 중 오류 발생: {ex.Message}", WorkLogType.Error);
                SetControlsEnabled(true);
                StopLoadProgress(); // 예외 발생 시에도 중지
            }
        }

        /// <summary>
        /// 모든 주요 컨트롤의 활성화/비활성화 상태 설정
        /// </summary>
        /// <param name="enabled">true=활성화, false=비활성화</param>
        private void SetControlsEnabled(bool enabled)
        {
            try
            {
                // Ribbon Expander 전체 비활성화/활성화
                var ribbonExpander = FindName("RibbonExpander") as WpfExpander;
                if (ribbonExpander != null)
                    ribbonExpander.IsEnabled = enabled;

                // Load 버튼
                var submitButton = FindName("SubmitButton") as WpfButton;
                if (submitButton != null)
                    submitButton.IsEnabled = enabled;

                // 날짜 선택 콤보박스
                var dateComboBox = FindName("dateComboBox") as WpfComboBox;
                if (dateComboBox != null)
                    dateComboBox.IsEnabled = enabled;

                // 시간 프리셋 콤보박스
                var timePresetComboBox = FindName("timePresetComboBox") as WpfComboBox;
                if (timePresetComboBox != null)
                    timePresetComboBox.IsEnabled = enabled;

                // 시간 입력 텍스트박스
                var fromTimeTextBox = FindName("fromTimeTextBox") as WpfTextBox;
                if (fromTimeTextBox != null)
                    fromTimeTextBox.IsEnabled = enabled;

                var toTimeTextBox = FindName("toTimeTextBox") as WpfTextBox;
                if (toTimeTextBox != null)
                    toTimeTextBox.IsEnabled = enabled;

                // 검색 텍스트박스
                var searchTextBox = FindName("searchTextBox") as WpfTextBox;
                if (searchTextBox != null)
                    searchTextBox.IsEnabled = enabled;

                // 검색 모드 콤보박스
                var searchModeComboBox = FindName("searchModeComboBox") as WpfComboBox;
                if (searchModeComboBox != null)
                    searchModeComboBox.IsEnabled = enabled;

                // Load Options 체크박스들
                var loadTextCheckBox = FindName("LoadTextCheckBox") as WpfCheckBox;
                if (loadTextCheckBox != null)
                    loadTextCheckBox.IsEnabled = enabled;

                var loadDataGridCheckBox = FindName("LoadDataGridCheckBox") as WpfCheckBox;
                if (loadDataGridCheckBox != null)
                    loadDataGridCheckBox.IsEnabled = enabled;

                // exec.Time 관련 버튼들
                var applyExecTimeFilterButton = FindName("ApplyExecTimeFilterButton") as WpfButton;
                if (applyExecTimeFilterButton != null)
                    applyExecTimeFilterButton.IsEnabled = enabled;

                var clearExecTimeFilterButton = FindName("ClearExecTimeFilterButton") as WpfButton;
                if (clearExecTimeFilterButton != null)
                    clearExecTimeFilterButton.IsEnabled = enabled;

                // 폴더 관련 버튼들
                var openFolderButton = FindName("OpenFolderButton") as WpfButton;
                if (openFolderButton != null)
                    openFolderButton.IsEnabled = enabled;

                var changeFolderButton = FindName("ChangeFolderButton") as WpfButton;
                if (changeFolderButton != null)
                    changeFolderButton.IsEnabled = enabled;

                var resetFolderPathButton = FindName("ResetFolderPathButton") as WpfButton;
                if (resetFolderPathButton != null)
                    resetFolderPathButton.IsEnabled = enabled;

                var openNotepadButton = FindName("OpenNotepadButton") as WpfButton;
                if (openNotepadButton != null)
                    openNotepadButton.IsEnabled = enabled;

                var openInVSCodeButton = FindName("OpenInVSCodeButton") as WpfButton;
                if (openInVSCodeButton != null)
                    openInVSCodeButton.IsEnabled = enabled;

                // DataGrid 탭의 버튼들
                var toggleViewModeButton = FindName("ToggleViewModeButton") as WpfButton;
                if (toggleViewModeButton != null)
                    toggleViewModeButton.IsEnabled = enabled;

                var toggleAllContentButton = FindName("ToggleAllContentButton") as WpfButton;
                if (toggleAllContentButton != null)
                    toggleAllContentButton.IsEnabled = enabled;

                var goToSameTimeButton = FindName("GoToSameTimeButton") as WpfButton;
                if (goToSameTimeButton != null)
                    goToSameTimeButton.IsEnabled = enabled;

                var clearAllFiltersButton = FindName("ClearAllFiltersButton") as WpfButton;
                if (clearAllFiltersButton != null)
                    clearAllFiltersButton.IsEnabled = enabled;

                var exportDataGridButton = FindName("ExportDataGridButton") as WpfButton;
                if (exportDataGridButton != null)
                    exportDataGridButton.IsEnabled = enabled;

                // Text 탭의 버튼들
                var toggleTextViewModeButton = FindName("ToggleTextViewModeButton") as WpfButton;
                if (toggleTextViewModeButton != null)
                    toggleTextViewModeButton.IsEnabled = enabled;

                var fontSizeDecreaseButton = FindName("FontSizeDecreaseButton") as WpfButton;
                if (fontSizeDecreaseButton != null)
                    fontSizeDecreaseButton.IsEnabled = enabled;

                var fontSizeIncreaseButton = FindName("FontSizeIncreaseButton") as WpfButton;
                if (fontSizeIncreaseButton != null)
                    fontSizeIncreaseButton.IsEnabled = enabled;

                var applyFontSizeButton = FindName("ApplyFontSizeButton") as WpfButton;
                if (applyFontSizeButton != null)
                    applyFontSizeButton.IsEnabled = enabled;

                // DataGrid Font 관련 버튼들
                var dataGridFontSizeDecreaseButton = FindName("DataGridFontSizeDecreaseButton") as WpfButton;
                if (dataGridFontSizeDecreaseButton != null)
                    dataGridFontSizeDecreaseButton.IsEnabled = enabled;

                var dataGridFontSizeIncreaseButton = FindName("DataGridFontSizeIncreaseButton") as WpfButton;
                if (dataGridFontSizeIncreaseButton != null)
                    dataGridFontSizeIncreaseButton.IsEnabled = enabled;

                var applyDataGridFontSizeButton = FindName("ApplyDataGridFontSizeButton") as WpfButton;
                if (applyDataGridFontSizeButton != null)
                    applyDataGridFontSizeButton.IsEnabled = enabled;

                var applyAllFontSizesButton = FindName("ApplyAllFontSizesButton") as WpfButton;
                if (applyAllFontSizesButton != null)
                    applyAllFontSizesButton.IsEnabled = enabled;

                // 필터 관련 컨트롤들
                var dataBusinessFilterComboBox = FindName("DataBusinessFilterComboBox") as WpfComboBox;
                if (dataBusinessFilterComboBox != null)
                    dataBusinessFilterComboBox.IsEnabled = enabled;

                var eventMsgIdFilterComboBox = FindName("EventMsgIdFilterComboBox") as WpfComboBox;
                if (eventMsgIdFilterComboBox != null)
                    eventMsgIdFilterComboBox.IsEnabled = enabled;

                var exceptionBusinessFilterComboBox = FindName("ExceptionBusinessFilterComboBox") as WpfComboBox;
                if (exceptionBusinessFilterComboBox != null)
                    exceptionBusinessFilterComboBox.IsEnabled = enabled;

                // 마우스 커서 변경
                this.Cursor = enabled ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Wait;
                
                // UI 업데이트 강제 적용
                this.UpdateLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetControlsEnabled 오류: {ex.Message}");
            }
        }

        private async Task LoadIndividualLogFilesUsingService(DateTime selectedDate, string searchText, string searchMode, TimeSpan fromTime = default, TimeSpan toTime = default)
        {
            LogTabControl.SelectedIndex = 5;

            var dataTextBox = FindName("dataLogTextBox") as System.Windows.Controls.TextBox;
            var eventTextBox = FindName("eventLogTextBox") as System.Windows.Controls.TextBox;
            var debugTextBox = FindName("debugLogTextBox") as System.Windows.Controls.TextBox;
            var exceptionTextBox = FindName("exceptionLogTextBox") as System.Windows.Controls.TextBox;

            var dataLineTextBox = FindName("dataLineNumberTextBox") as System.Windows.Controls.TextBox;
            var eventLineTextBox = FindName("eventLineNumberTextBox") as System.Windows.Controls.TextBox;
            var debugLineTextBox = FindName("debugLineNumberTextBox") as System.Windows.Controls.TextBox;
            var exceptionLineTextBox = FindName("exceptionLineNumberTextBox") as System.Windows.Controls.TextBox;

            if (dataTextBox != null && eventTextBox != null && debugTextBox != null && exceptionTextBox != null &&
                dataLineTextBox != null && eventLineTextBox != null && debugLineTextBox != null && exceptionLineTextBox != null)
            {
                if (fromTime == default && toTime == default)
                {
                    var timeRange = GetTimeRange();
                    fromTime = timeRange.fromTime;
                    toTime = timeRange.toTime;
                }

                await _logLoadingService.LoadIndividualLogFiles(selectedDate, searchText, searchMode,
                    dataTextBox, eventTextBox, debugTextBox, exceptionTextBox,
                    dataLineTextBox, eventLineTextBox, debugLineTextBox, exceptionLineTextBox,
                    fromTime, toTime);
            }
            else
            {
                _workLogService.AddLog("? TextBox 컨트롤을 찾을 수 없음", WorkLogType.Error);
            }
        }

        private string GetSelectedSearchMode()
        {
            if (searchModeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Tag?.ToString() ?? "Range";
            }
            return "Range";
        }

        private async Task LoadExecTimeAnalysis(DateTime selectedDate, string searchText, string searchMode, TimeSpan fromTime, TimeSpan toTime)
        {
            try
            {
                // For now reuse the general log loading routine. Future: implement exec-specific parsing.
                await LoadIndividualLogFilesUsingService(selectedDate, searchText, searchMode, fromTime, toTime);

                // Update UI status on main thread
                Dispatcher.BeginInvoke(() =>
                {
                    var execTimeStatusText = FindName("ExecTimeStatusText") as TextBlock;
                    if (execTimeStatusText != null)
                    {
                        execTimeStatusText.Text = "Analysis Loaded";
                    }
                    _workLogService.AddLog("? exec.Time 분석 로드 완료", WorkLogType.Success);
                });
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? exec.Time 분석 로드 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        // Event handlers delegating to managers
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            _eventHandlerManager.LogFolderPath = _logFolderPath;
            _eventHandlerManager.OpenFolder();
        }

        private void OpenNotepad_Click(object sender, RoutedEventArgs e)
        {
            _eventHandlerManager.OpenNotepad();
        }

        /// <summary>
        /// 도움말 버튼 클릭 이벤트
        /// </summary>
        private void ShowHelp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var helpDialog = new Helpers.HelpDialog
                {
                    Owner = this
                };
                helpDialog.ShowDialog();
                
                _workLogService.AddLog("?? 도움말 대화상자 표시", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"?? 도움말 표시 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void FontSizeDecrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!double.TryParse(FontSizeTextBox.Text, out double size))
                {
                    _workLogService.AddLog("유효한 폰트 크기 값을 입력하세요", WorkLogType.Warning);
                    return;
                }

                size = Math.Max(6, size - 1);
                FontSizeTextBox.Text = ((int)size).ToString();

                if (_activeTextBox != null)
                {
                    _activeTextBox.FontSize = size;
                }
                else
                {
                    _workLogService.AddLog("현재 TextBox가 활성화되지 않았습니다. 원하는 텍스트 박스를 클릭하세요.", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"Error decreasing font size: {ex.Message}", WorkLogType.Error);
            }
        }

        private void FontSizeIncrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!double.TryParse(FontSizeTextBox.Text, out double size))
                {
                    _workLogService.AddLog("유효한 폰트 크기 값을 입력하세요", WorkLogType.Warning);
                    return;
                }

                size = Math.Min(72, size + 1);
                FontSizeTextBox.Text = ((int)size).ToString();

                if (_activeTextBox != null)
                {
                    _activeTextBox.FontSize = size;
                }
                else
                {
                    _workLogService.AddLog("현재 TextBox가 활성화되지 않았습니다. 원하는 텍스트 박스를 클릭하세요.", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"Error increasing font size: {ex.Message}", WorkLogType.Error);
            }
        }

        private void LogTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _eventHandlerManager.HandleTextBoxKeyDown(sender, e, this);
        }

        private void LogTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                _activeTextBox = tb;
            }
            _eventHandlerManager.HandleTextBoxGotFocus(sender);
        }

        private void TextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            _eventHandlerManager.HandleTextBoxScrollChanged(sender, e, textBox =>
            {
                var name = textBox.Name;
                if (name != null)
                {
                    string lineNumberBoxName = name.Replace("LogTextBox", "LineNumberTextBox");
                    return FindName(lineNumberBoxName) as System.Windows.Controls.TextBox;
                }
                return null;
            });
        }

        private void ClearWorkLogButton_Click(object sender, RoutedEventArgs e)
        {
            var workLogTextBox = FindName("workLogTextBox") as System.Windows.Controls.RichTextBox;
            var workLogLineNumberTextBox = FindName("workLogLineNumberTextBox") as WpfTextBox;
            var WorkLogStatusText = FindName("WorkLogStatusText") as TextBlock;
            
            if (workLogTextBox != null && workLogLineNumberTextBox != null && WorkLogStatusText != null)
            {
                _eventHandlerManager.ClearWorkLog(workLogTextBox, workLogLineNumberTextBox, WorkLogStatusText);
            }
        }

        private void SaveWorkLogButton_Click(object sender, RoutedEventArgs e)
        {
            var workLogTextBox = FindName("workLogTextBox") as System.Windows.Controls.RichTextBox;
            
            if (workLogTextBox != null)
            {
                _eventHandlerManager.SaveWorkLog(workLogTextBox);
            }
        }

        private void ToggleAllContentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _dataGridManager.ToggleAllContent(ref _isAllContentExpanded);
                UpdateToggleAllContentButtonText();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Content 토글 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void UpdateToggleAllContentButtonText()
        {
            try
            {
                var button = FindName("ToggleAllContentButton") as System.Windows.Controls.Button;
                if (button != null)
                {
                    button.Content = _isAllContentExpanded ? "콘텐츠 닫기" : "콘텐츠 열기";
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? ToggleAllContent 버튼 텍스트 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// dateComboBox에서 선택된 날짜를 반환합니다.
        /// </summary>
        /// <returns>선택된 날짜(DateTime). 선택이 없으면 오늘 날짜 반환.</returns>
        private DateTime GetSelectedDate()
        {
            var dateComboBox = FindName("dateComboBox") as System.Windows.Controls.ComboBox;
            if (dateComboBox != null && dateComboBox.SelectedItem != null)
            {
                // DatePicker, DateTime, string 등 다양한 경우를 처리
                if (dateComboBox.SelectedItem is DateTime dt)
                    return dt;
                if (dateComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem cbi)
                {
                    if (DateTime.TryParse(cbi.Content?.ToString(), out var parsed))
                        return parsed;
                }
                if (DateTime.TryParse(dateComboBox.SelectedItem.ToString(), out var parsed2))
                    return parsed2;
            }
            return DateTime.Today;
        }

        /// <summary>
        /// Load Options 체크박스에 따라 탭 Visibility 업데이트
        /// </summary>
        private void UpdateTabVisibility()
        {
            try
            {
                // LogTabControl이 null이면 무시
                if (LogTabControl == null)
                {
                    System.Diagnostics.Debug.WriteLine("UpdateTabVisibility: LogTabControl이 null입니다.");
                    return;
                }

                // 체크박스들이 null이면 무시
                if (LoadTextCheckBox == null || LoadDataGridCheckBox == null)
                {
                    System.Diagnostics.Debug.WriteLine("UpdateTabVisibility: 체크박스가 null입니다.");
                    return;
                }

                bool loadText = LoadTextCheckBox.IsChecked == true;
                bool loadDataGrid = LoadDataGridCheckBox.IsChecked == true;

                var textTab = FindName("LogAnalysisTab") as TabItem;
                var dataGridTab = FindName("DataGridLogTab") as TabItem;

                if (textTab != null)
                {
                    textTab.Visibility = loadText ? Visibility.Visible : Visibility.Collapsed;
                }

                if (dataGridTab != null)
                {
                    dataGridTab.Visibility = loadDataGrid ? Visibility.Visible : Visibility.Collapsed;
                }

                // 현재 선택된 탭이 숨겨졌는지 확인
                bool needToChangeTab = false;
                if (LogTabControl.SelectedItem is TabItem selectedTab)
                {
                    if (selectedTab.Visibility == Visibility.Collapsed)
                    {
                        needToChangeTab = true;
                        System.Diagnostics.Debug.WriteLine($"현재 선택된 탭 '{selectedTab.Header}'이 숨겨졌습니다. 다른 탭으로 전환합니다.");
                    }
                }
                else if (LogTabControl.SelectedItem == null)
                {
                    needToChangeTab = true;
                    System.Diagnostics.Debug.WriteLine("선택된 탭이 없습니다. 보이는 탭으로 전환합니다.");
                }

                // 보이는 첫 번째 탭으로 이동
                if (needToChangeTab)
                {
                    TabItem? firstVisibleTab = null;
                    foreach (var item in LogTabControl.Items)
                    {
                        if (item is TabItem tab && tab.Visibility == Visibility.Visible)
                        {
                            firstVisibleTab = tab;
                            break;
                        }
                    }

                    if (firstVisibleTab != null)
                    {
                        LogTabControl.SelectedItem = firstVisibleTab;
                        System.Diagnostics.Debug.WriteLine($"탭 전환: '{firstVisibleTab.Header}'로 이동");
                        _workLogService?.AddLog($"탭 전환: '{firstVisibleTab.Header}'로 이동", WorkLogType.Info);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("?? 보이는 탭을 찾을 수 없습니다!");
                    }
                }

                _workLogService?.AddLog($"탭 Visibility 업데이트: Text={loadText}, DataGrid={loadDataGrid}", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService?.AddLog($"?? 탭 Visibility 업데이트 오류: {ex.Message}", WorkLogType.Error);
                System.Diagnostics.Debug.WriteLine($"UpdateTabVisibility 오류: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// LoadTextCheckBox 체크 상태 변경 이벤트 핸들러
        /// </summary>
        private void LoadTextCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // LogTabControl이 초기화되지 않았으면 무시
            if (LogTabControl == null) return;
            UpdateTabVisibility();
        }

        /// <summary>
        /// LoadDataGridCheckBox 체크 상태 변경 이벤트 핸들러
        /// </summary>
        private void LoadDataGridCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            // LogTabControl이 초기화되지 않았으면 무시
            if (LogTabControl == null) return;
            UpdateTabVisibility();
        }

        /// <summary>
        /// 로드 진행 시간 타이머 초기화 (System.Threading.Timer 사용 - UI 스레드와 완전 분리)
        /// </summary>
        private void InitializeLoadProgressTimer()
        {
            try
            {
                // System.Threading.Timer 사용: 백그라운드 스레드에서 실행되어 UI와 완전 독립
                // 초기에는 중지 상태로 생성 (Timeout.Infinite)
                _loadProgressTimer = new System.Threading.Timer(
                    LoadProgressTimer_Tick,  // 콜백 메서드
                    null,                    // state 객체
                    Timeout.Infinite,        // dueTime: 즉시 시작하지 않음
                    Timeout.Infinite         // period: 주기 실행하지 않음
                );
                
                _workLogService.AddLog("?? 로드 진행 타이머 초기화 완료 (백그라운드 스레드)", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 로드 진행 타이머 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 로드 진행 시간 타이머 틱 이벤트 (백그라운드 스레드에서 실행)
        /// ?? 주의: UI 업데이트는 반드시 Dispatcher.BeginInvoke를 통해서만!
        /// </summary>
        private void LoadProgressTimer_Tick(object? state)
        {
            try
            {
                if (!_isLoading) return;

                var elapsed = DateTime.Now - _loadStartTime;
                
                // 백그라운드 스레드에서 UI 업데이트 → Dispatcher 필수
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var loadProgressTextBlock = FindName("LoadProgressTextBlock") as TextBlock;
                        if (loadProgressTextBlock != null)
                        {
                            loadProgressTextBlock.Text = $" {elapsed:mm\\:ss\\.f}";
                        }
                    }
                    catch
                    {
                        // UI 업데이트 실패는 무시
                    }
                }));
            }
            catch
            {
                // 타이머는 절대 죽으면 안되므로 모든 예외는 무시
            }
        }

        /// <summary>
        /// 로드 진행 시간 표시 시작 (System.Threading.Timer 사용)
        /// </summary>
        private void StartLoadProgress()
        {
            try
            {
                _isLoading = true;
                _loadStartTime = DateTime.Now;
                
                // UI 업데이트
                var loadProgressTextBlock = FindName("LoadProgressTextBlock") as TextBlock;
                if (loadProgressTextBlock != null)
                {
                    loadProgressTextBlock.Visibility = Visibility.Visible;
                    loadProgressTextBlock.Text = "?? 00:00.0";
                    loadProgressTextBlock.FontSize = 18;
                    loadProgressTextBlock.FontWeight = FontWeights.Bold;
                }

                // 타이머 시작: 즉시 시작(0ms), 50ms마다 반복 (더 자주 업데이트)
                _loadProgressTimer?.Change(0, 50);
                
                _workLogService.AddLog("▶? 로드 진행 타이머 시작 (백그라운드 스레드, 50ms 간격)", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 로드 진행 표시 시작 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 로드 진행 시간 표시 중지 (System.Threading.Timer 사용)
        /// </summary>
        private void StopLoadProgress()
        {
            try
            {
                _isLoading = false;
                
                // 타이머 중지: Timeout.Infinite 설정
                _loadProgressTimer?.Change(Timeout.Infinite, Timeout.Infinite);

                var elapsed = DateTime.Now - _loadStartTime;
                var loadProgressTextBlock = FindName("LoadProgressTextBlock") as TextBlock;
                if (loadProgressTextBlock != null)
                {
                    loadProgressTextBlock.Text = $"? 완료 ({elapsed:mm\\:ss\\.f})";
                }

                _workLogService.AddLog($"?? 로딩 완료 (소요 시간: {elapsed:mm\\:ss\\.f})", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 로드 진행 표시 중지 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 백그라운드에서 로드한 데이터를 DataGrid에 바인딩 (UI 스레드에서만 호출)
        /// </summary>
        private void BindDataToDataGrids(Dictionary<string, List<LogLineItem>> allData)
        {
            try
            {
                foreach (var kvp in allData)
                {
                    var logType = kvp.Key;
                    var items = kvp.Value;
                    
                    // DataGridManager를 통해 바인딩
                    if (logType == "DATA")
                    {
                        _dataGridManager.DataLogLines.Clear();
                        foreach (var item in items)
                        {
                            item.LogLevel = logType;
                            _dataGridManager.DataLogLines.Add(item);
                        }
                    }
                    else if (logType == "EVENT")
                    {
                        _dataGridManager.EventLogLines.Clear();
                        foreach (var item in items)
                        {
                            item.LogLevel = logType;
                            _dataGridManager.EventLogLines.Add(item);
                        }
                        
                        // ⚡ 바인딩 완료 후 BCR_ID, SPS_BOX_ID 추출 (별도 작업)
                        ExtractBarcodeFieldsAfterBinding(items);
                    }
                    else if (logType == "DEBUG")
                    {
                        _dataGridManager.DebugLogLines.Clear();
                        foreach (var item in items)
                        {
                            item.LogLevel = logType;
                            _dataGridManager.DebugLogLines.Add(item);
                        }
                    }
                    else if (logType == "EXCEPTION")
                    {
                        _dataGridManager.ExceptionLogLines.Clear();
                        foreach (var item in items)
                        {
                            item.LogLevel = logType;
                            _dataGridManager.ExceptionLogLines.Add(item);
                        }
                    }
                    
                    _workLogService.AddLog($"✅ {logType}: {items.Count}개 바인딩 완료", WorkLogType.Info);
                }
                
                // ⚡ 콤보박스 필터 업데이트 (필터 목록 갱신)
                _dataGridManager.UpdateComboBoxFilters();
                
                // ⚡ 모든 필터를 "ALL"로 초기화 (간헐적 필터 문제 방지)
                ClearAllFiltersAfterBinding();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ 데이터 바인딩 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// ⚡ 바인딩 후 모든 필터를 "ALL"로 초기화 (간헐적 필터 문제 방지)
        /// </summary>
        private void ClearAllFiltersAfterBinding()
        {
            try
            {
                // ⚡ 모든 필터 아이템 선택 해제
                foreach (var item in _dataBusinessFilterItems) item.IsSelected = false;
                foreach (var item in _eventMsgIdFilterItems) item.IsSelected = false;
                foreach (var item in _exceptionBusinessFilterItems) item.IsSelected = false;
                
                // DATA 비즈니스 필터
                var dataBusinessFilterComboBox = FindName("DataBusinessFilterComboBox") as WpfComboBox;
                if (dataBusinessFilterComboBox != null && dataBusinessFilterComboBox.Items.Count > 0)
                {
                    dataBusinessFilterComboBox.SelectedIndex = -1; // ⚡ 선택 해제 (선택하세요)
                    dataBusinessFilterComboBox.Text = "선택하세요";
                }
                
                // EVENT MsgId 필터
                var eventMsgIdFilterComboBox = FindName("EventMsgIdFilterComboBox") as WpfComboBox;
                if (eventMsgIdFilterComboBox != null && eventMsgIdFilterComboBox.Items.Count > 0)
                {
                    eventMsgIdFilterComboBox.SelectedIndex = -1; // ⚡ 선택 해제 (선택하세요)
                    eventMsgIdFilterComboBox.Text = "선택하세요";
                }
                
                // EXCEPTION 비즈니스 필터
                var exceptionBusinessFilterComboBox = FindName("ExceptionBusinessFilterComboBox") as WpfComboBox;
                if (exceptionBusinessFilterComboBox != null && exceptionBusinessFilterComboBox.Items.Count > 0)
                {
                    exceptionBusinessFilterComboBox.SelectedIndex = -1; // ⚡ 선택 해제 (선택하세요)
                    exceptionBusinessFilterComboBox.Text = "선택하세요";
                }
                
                // ⚡ CollectionView 필터 초기화 (모두 보이도록)
                var dataView = System.Windows.Data.CollectionViewSource.GetDefaultView(_dataGridManager.DataLogLines);
                if (dataView != null)
                {
                    dataView.Filter = null;
                    dataView.Refresh();
                }
                
                var eventView = System.Windows.Data.CollectionViewSource.GetDefaultView(_dataGridManager.EventLogLines);
                if (eventView != null)
                {
                    eventView.Filter = null;
                    eventView.Refresh();
                }
                
                var debugView = System.Windows.Data.CollectionViewSource.GetDefaultView(_dataGridManager.DebugLogLines);
                if (debugView != null)
                {
                    debugView.Filter = null;
                    debugView.Refresh();
                }
                
                var exceptionView = System.Windows.Data.CollectionViewSource.GetDefaultView(_dataGridManager.ExceptionLogLines);
                if (exceptionView != null)
                {
                    exceptionView.Filter = null;
                    exceptionView.Refresh();
                }
                
                _workLogService.AddLog("🔄 모든 필터 초기화 완료 (선택 해제)", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
                System.Diagnostics.Debug.WriteLine($"ClearAllFiltersAfterBinding error: {ex.Message}");
            }
        }

        /// <summary>
        /// ⚡ 바인딩 후 BCR_ID, SPS_BOX_ID, SENSOR1 추출
        /// </summary>
        private void ExtractBarcodeFieldsAfterBinding(List<LogLineItem> eventItems)
        {
            try
            {
                int extractedCount = 0;

                foreach (var item in eventItems)
                {
                    if (string.IsNullOrWhiteSpace(item.Content))
                        continue;

                    // Content에서 BCR_ID, SPS_BOX_ID, SENSOR1 추출
                    item.ExtractBarcodeFieldsFromOriginalContent(item.Content);

                    if (!string.IsNullOrWhiteSpace(item.BCR_ID) || 
                        !string.IsNullOrWhiteSpace(item.SPS_BOX_ID) || 
                        !string.IsNullOrWhiteSpace(item.BARCODE_LOT) ||
                        !string.IsNullOrWhiteSpace(item.SENSOR1))
                    {
                        extractedCount++;
                    }
                }
                
                _workLogService.AddLog(
                    $"⚡ BCR_ID/SPS_BOX_ID/SENSOR1 추출 완료: {extractedCount}/{eventItems.Count}개", 
                    WorkLogType.Success
                );
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ 바코드 필드 추출 오류: {ex.Message}", WorkLogType.Error);
                System.Diagnostics.Debug.WriteLine($"ExtractBarcodeFieldsAfterBinding error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
