using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Services;

namespace FACTOVA_LogAnalysis
{
    public partial class MainWindow
    {
        #region Search / Filter Management (moved)

        private void InitializeMultiSelectComboBoxes()
        {
            try
            {
                // DATA 비즈니스 필터 콤보박스
                var dataBusinessComboBox = FindName("DataBusinessFilterComboBox") as System.Windows.Controls.ComboBox;
                if (dataBusinessComboBox != null)
                {
                    dataBusinessComboBox.ItemsSource = _dataBusinessFilterItems;
                }

                // Also bind the Tab-mode DATA combo box when present
                var dataBusinessComboBoxTab = FindName("DataBusinessFilterComboBox_Tab") as System.Windows.Controls.ComboBox;
                if (dataBusinessComboBoxTab != null)
                {
                    dataBusinessComboBoxTab.ItemsSource = _dataBusinessFilterItems;
                }

                // EVENT MsgId 필터 콤보박스
                var eventMsgIdComboBox = FindName("EventMsgIdFilterComboBox") as System.Windows.Controls.ComboBox;
                if (eventMsgIdComboBox != null)
                {
                    eventMsgIdComboBox.ItemsSource = _eventMsgIdFilterItems;
                }

                // Also bind the Tab-mode EVENT combo box when present
                var eventMsgIdComboBoxTab = FindName("EventMsgIdFilterComboBox_Tab") as System.Windows.Controls.ComboBox;
                if (eventMsgIdComboBoxTab != null)
                {
                    eventMsgIdComboBoxTab.ItemsSource = _eventMsgIdFilterItems;
                }

                // EXCEPTION 비즈니스 필터 콤보박스
                var exceptionBusinessComboBox = FindName("ExceptionBusinessFilterComboBox") as System.Windows.Controls.ComboBox;
                if (exceptionBusinessComboBox != null)
                {
                    exceptionBusinessComboBox.ItemsSource = _exceptionBusinessFilterItems;
                }

                // Also bind the Tab-mode EXCEPTION combo box when present
                var exceptionBusinessComboBoxTab = FindName("ExceptionBusinessFilterComboBox_Tab") as System.Windows.Controls.ComboBox;
                if (exceptionBusinessComboBoxTab != null)
                {
                    exceptionBusinessComboBoxTab.ItemsSource = _exceptionBusinessFilterItems;
                }

                _workLogService.AddLog("? 멀티선택 콜보박스 초기화 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 멀티선택 콜보박스 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void OpenFindDialog(System.Windows.Controls.TextBox textBox)
        {
            // SearchFilterManager로 위임
            _searchFilterManager.OpenFindDialog(this, textBox, textBox.SelectedText);
        }

        public void FindDialogClosed()
        {
            // SearchFilterManager로 위임
            _searchFilterManager.CloseFindDialog();
        }

        public void FindAndHighlightAll(string searchText, bool caseSensitive, object target)
        {
            // SearchFilterManager로 위임
            _searchFilterManager.SearchAndHighlight(searchText, caseSensitive, target);
        }

        public void HighlightNext()
        {
            // SearchFilterManager로 위임
            _searchFilterManager.FindNext();
        }

        public void HighlightPrevious()
        {
            // SearchFilterManager로 위임
            _searchFilterManager.FindPrevious();
        }

        private Dictionary<string, (DataGrid dataGrid, ObservableCollection<LogLineItem> items)> GetAllDataGridsWithCollections()
        {
            var result = new Dictionary<string, (DataGrid, ObservableCollection<LogLineItem>)>();

            try
            {
                // DataGridManager에서 컬렉션 정보 가져오기
                var dataGrid = FindName("dataLogDataGrid") as DataGrid;
                var eventGrid = FindName("eventLogDataGrid") as DataGrid;
                var debugGrid = FindName("debugLogDataGrid") as DataGrid;
                var exceptionGrid = FindName("exceptionLogDataGrid") as DataGrid;

                if (dataGrid != null)
                    result["DATA"] = (dataGrid, _dataGridManager.DataLogLines);
                if (eventGrid != null)
                    result["EVENT"] = (eventGrid, _dataGridManager.EventLogLines);
                if (debugGrid != null)
                    result["DEBUG"] = (debugGrid, _dataGridManager.DebugLogLines);
                if (exceptionGrid != null)
                    result["EXCEPTION"] = (exceptionGrid, _dataGridManager.ExceptionLogLines);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DataGrid 정보 수집 오류: {ex.Message}", WorkLogType.Error);
            }

            return result;
        }

        private void GlobalKeywordFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.TextBox textBox)
                {
                    string keyword = textBox.Text?.Trim() ?? "";

                    // SearchFilterManager를 통한 글로벌 키워드 필터 적용
                    var dataGrids = GetAllDataGridsWithCollections();
                    _searchFilterManager.ApplyGlobalKeywordFilter(keyword, dataGrids);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 글로벌 키워드 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearAllFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 기존 멀티선택 필터들 초기화
                foreach (var item in _dataBusinessFilterItems) item.IsSelected = false;
                foreach (var item in _eventMsgIdFilterItems) item.IsSelected = false;
                foreach (var item in _exceptionBusinessFilterItems) item.IsSelected = false;

                // Time 필터들 초기화 (새로 추가)
                var timeFilters = new[] {
                    "DataTimeFilterTextBox", "EventTimeFilterTextBox", 
                    "DebugTimeFilterTextBox", "DebugTimeFilterTextBox_Tab", 
                    "ExceptionTimeFilterTextBox"
                };

                foreach (var filterName in timeFilters)
                {
                    var textBox = FindName(filterName) as System.Windows.Controls.TextBox;
                    if (textBox != null) textBox.Text = "";
                }

                // Content 필터들 초기화
                var contentFilters = new[] {
                    "DataContentFilterTextBox", "EventContentFilterTextBox", 
                    "DebugContentFilterTextBox", "DebugContentFilterTextBox_Tab", 
                    "ExceptionContentFilterTextBox"
                };

                foreach (var filterName in contentFilters)
                {
                    var textBox = FindName(filterName) as System.Windows.Controls.TextBox;
                    if (textBox != null) textBox.Text = "";
                }

                // 콤보박스 텍스트 업데이트
                UpdateComboBoxTexts();

                // 각 DataGrid의 모든 필터 적용 (빈 조건으로 = 전체 표시)
                ApplyIndividualFilters("DATA");
                ApplyIndividualFilters("EVENT");
                ApplyIndividualFilters("DEBUG");
                ApplyIndividualFilters("EXCEPTION");

                _workLogService.AddLog("✅ 모든 개별 필터 초기화 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ 전체 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void UpdateBusinessNameFilters()
        {
            try
            {
                // DATA 비즈니스명 필터 업데이트
                var dataBusinessNames = _dataGridManager.GetBusinessNamesForLogType("DATA");
                _dataBusinessFilterItems.Clear();

                foreach (var name in dataBusinessNames.OrderBy(n => n))
                {
                    _dataBusinessFilterItems.Add(new FilterItem(name));
                }

                _workLogService.AddLog($"DATA 비즈니스명 필터: {dataBusinessNames.Count}개 항목 로드", WorkLogType.Info);

                // EVENT MsgId 필터 업데이트 - ⚡ 숫자 정렬 적용!
                var eventMsgIds = _dataGridManager.GetMsgIdsForLogType("EVENT");
                _eventMsgIdFilterItems.Clear();

                // ⚡ 숫자 정렬: "1100" → "2000" → "5001"
                var sortedMsgIds = eventMsgIds
                    .OrderBy(m => int.TryParse(m, out int num) ? num : int.MaxValue) // 숫자 우선
                    .ThenBy(m => m); // 숫자가 아닌 경우 문자열 정렬

                foreach (var msgId in sortedMsgIds)
                {
                    _eventMsgIdFilterItems.Add(new FilterItem(msgId));
                }

                _workLogService.AddLog($"EVENT MsgId 필터: {eventMsgIds.Count}개 항목 로드", WorkLogType.Info);

                // EXCEPTION 비즈니스명 필터 업데이트
                var exceptionBusinessNames = _dataGridManager.GetBusinessNamesForLogType("EXCEPTION");
                _exceptionBusinessFilterItems.Clear();

                foreach (var name in exceptionBusinessNames.OrderBy(n => n))
                {
                    _exceptionBusinessFilterItems.Add(new FilterItem(name));
                }

                _workLogService.AddLog($"EXCEPTION 비즈니스명 필터: {exceptionBusinessNames.Count}개 항목 로드", WorkLogType.Info);

                // 콤보박스 텍스트 업데이트
                UpdateComboBoxTexts();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 필터링 항목 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void UpdateComboBoxTexts()
        {
            try
            {
                // DATA combo boxes (four-panel and tab)
                var dataBusinessComboBox = FindName("DataBusinessFilterComboBox") as System.Windows.Controls.ComboBox;
                var dataBusinessComboBoxTab = FindName("DataBusinessFilterComboBox_Tab") as System.Windows.Controls.ComboBox;
                var selectedDataItems = _dataBusinessFilterItems.Where(x => x.IsSelected).Select(x => x.Value).ToList();
                var dataText = selectedDataItems.Count == 0 ? "선택하세요" : string.Join(", ", selectedDataItems);
                if (dataBusinessComboBox != null) dataBusinessComboBox.Text = dataText;
                if (dataBusinessComboBoxTab != null) dataBusinessComboBoxTab.Text = dataText;

                // EVENT combo boxes (four-panel and tab)
                var eventMsgIdComboBox = FindName("EventMsgIdFilterComboBox") as System.Windows.Controls.ComboBox;
                var eventMsgIdComboBoxTab = FindName("EventMsgIdFilterComboBox_Tab") as System.Windows.Controls.ComboBox;
                var selectedEventItems = _eventMsgIdFilterItems.Where(x => x.IsSelected).Select(x => x.Value).ToList();
                var eventText = selectedEventItems.Count == 0 ? "선택하세요" : string.Join(", ", selectedEventItems);
                if (eventMsgIdComboBox != null) eventMsgIdComboBox.Text = eventText;
                if (eventMsgIdComboBoxTab != null) eventMsgIdComboBoxTab.Text = eventText;

                // EXCEPTION combo boxes (four-panel and tab)
                var exceptionBusinessComboBox = FindName("ExceptionBusinessFilterComboBox") as System.Windows.Controls.ComboBox;
                var exceptionBusinessComboBoxTab = FindName("ExceptionBusinessFilterComboBox_Tab") as System.Windows.Controls.ComboBox;
                var selectedExceptionItems = _exceptionBusinessFilterItems.Where(x => x.IsSelected).Select(x => x.Value).ToList();
                var exceptionText = selectedExceptionItems.Count == 0 ? "선택하세요" : string.Join(", ", selectedExceptionItems);
                if (exceptionBusinessComboBox != null) exceptionBusinessComboBox.Text = exceptionText;
                if (exceptionBusinessComboBoxTab != null) exceptionBusinessComboBoxTab.Text = exceptionText;
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 콤보박스 텍스트 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ApplyMultiSelectFilter(string logType)
        {
            // 이 메서드는 이제 ApplyIndividualFilters로 대체됩니다
            // 호환성을 위해 새로운 메서드로 포워딩
            ApplyIndividualFilters(logType);
        }

        // Individual handlers that simply call ApplyIndividualFilters and update combo text
        private void BusinessCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("DATA");
                UpdateComboBoxTexts();

                // keep the parent ComboBox dropdown open so user can select multiple items
                if (sender is DependencyObject dob)
                {
                    var parentCombo = FindVisualParent<System.Windows.Controls.ComboBox>(dob);
                    if (parentCombo != null) parentCombo.IsDropDownOpen = true;
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"BusinessCheckBox_Checked error: {ex.Message}", WorkLogType.Error);
            }
        }

        private void BusinessCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("DATA");
                UpdateComboBoxTexts();
                if (sender is DependencyObject dob)
                {
                    var parentCombo = FindVisualParent<System.Windows.Controls.ComboBox>(dob);
                    if (parentCombo != null) parentCombo.IsDropDownOpen = true;
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"BusinessCheckBox_Unchecked error: {ex.Message}", WorkLogType.Error);
            }
        }

        private void MsgIdCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("EVENT");
                UpdateComboBoxTexts();
                if (sender is DependencyObject dob)
                {
                    var parentCombo = FindVisualParent<System.Windows.Controls.ComboBox>(dob);
                    if (parentCombo != null) parentCombo.IsDropDownOpen = true;
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"MsgIdCheckBox_Checked error: {ex.Message}", WorkLogType.Error);
            }
        }

        private void MsgIdCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("EVENT");
                UpdateComboBoxTexts();
                if (sender is DependencyObject dob)
                {
                    var parentCombo = FindVisualParent<System.Windows.Controls.ComboBox>(dob);
                    if (parentCombo != null) parentCombo.IsDropDownOpen = true;
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"MsgIdCheckBox_Unchecked error: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ExceptionBusinessCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("EXCEPTION");
                UpdateComboBoxTexts();
                if (sender is DependencyObject dob)
                {
                    var parentCombo = FindVisualParent<System.Windows.Controls.ComboBox>(dob);
                    if (parentCombo != null) parentCombo.IsDropDownOpen = true;
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"ExceptionBusinessCheckBox_Checked error: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ExceptionBusinessCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("EXCEPTION");
                UpdateComboBoxTexts();
                if (sender is DependencyObject dob)
                {
                    var parentCombo = FindVisualParent<System.Windows.Controls.ComboBox>(dob);
                    if (parentCombo != null) parentCombo.IsDropDownOpen = true;
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"ExceptionBusinessCheckBox_Unchecked error: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearDataFilterButton_Click(object sender, RoutedEventArgs e)
        {
            // 이 메서드는 이제 ClearDataBusinessFilterButton_Click으로 대체됩니다
            ClearDataBusinessFilterButton_Click(sender, e);
        }

        private void ClearExceptionFilterButton_Click(object sender, RoutedEventArgs e)
        {
            // 이 메서드는 이제 ClearExceptionBusinessFilterButton_Click으로 대체됩니다
            ClearExceptionBusinessFilterButton_Click(sender, e);
        }

        private void ClearEventFilterButton_Click(object sender, RoutedEventArgs e)
        {
            // 이 메서드는 이제 ClearEventMsgIdFilterButton_Click으로 대체됩니다
            ClearEventMsgIdFilterButton_Click(sender, e);
        }

        #endregion

        #region Individual Content Filter Handlers

        // DATA Time Filter (새로 추가)
        private void DataTimeFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("DATA");
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ DATA Time 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearDataTimeFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("DataTimeFilterTextBox") as System.Windows.Controls.TextBox;
                if (textBox != null) textBox.Text = "";
                ApplyIndividualFilters("DATA");
                _workLogService.AddLog("DATA Time 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ DATA Time 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        // DATA Content Filter
        private void DataContentFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("DATA");
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DATA Content 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearDataContentFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("DataContentFilterTextBox") as System.Windows.Controls.TextBox;
                if (textBox != null) textBox.Text = "";
                ApplyIndividualFilters("DATA");
                _workLogService.AddLog("DATA Content 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DATA Content 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearDataBusinessFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var item in _dataBusinessFilterItems)
                {
                    item.IsSelected = false;
                }
                UpdateComboBoxTexts();
                ApplyIndividualFilters("DATA");
                _workLogService.AddLog("DATA Business 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DATA Business 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        // EVENT Content Filter
        private void EventContentFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("EVENT");
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ EVENT Content 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void EventTimeFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("EVENT");
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ EVENT Time 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearEventTimeFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("EventTimeFilterTextBox") as System.Windows.Controls.TextBox;
                if (textBox != null) textBox.Text = "";
                ApplyIndividualFilters("EVENT");
                _workLogService.AddLog("EVENT Time 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ EVENT Time 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearEventContentFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("EventContentFilterTextBox") as System.Windows.Controls.TextBox;
                if (textBox != null) textBox.Text = "";
                ApplyIndividualFilters("EVENT");
                _workLogService.AddLog("EVENT Content 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? EVENT Content 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearEventMsgIdFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var item in _eventMsgIdFilterItems)
                {
                    item.IsSelected = false;
                }
                UpdateComboBoxTexts();
                ApplyIndividualFilters("EVENT");
                _workLogService.AddLog("EVENT MsgId 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? EVENT MsgId 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        // DEBUG Content Filter
        private void DebugContentFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("DEBUG");
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ DEBUG Content 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void DebugTimeFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("DEBUG");
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ DEBUG Time 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearDebugTimeFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("DebugTimeFilterTextBox") as System.Windows.Controls.TextBox;
                var textBoxTab = FindName("DebugTimeFilterTextBox_Tab") as System.Windows.Controls.TextBox;
                if (textBox != null) textBox.Text = "";
                if (textBoxTab != null) textBoxTab.Text = "";
                ApplyIndividualFilters("DEBUG");
                _workLogService.AddLog("DEBUG Time 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ DEBUG Time 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearDebugFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Time 필터 초기화
                var timeTextBox = FindName("DebugTimeFilterTextBox") as System.Windows.Controls.TextBox;
                var timeTextBoxTab = FindName("DebugTimeFilterTextBox_Tab") as System.Windows.Controls.TextBox;
                if (timeTextBox != null) timeTextBox.Text = "";
                if (timeTextBoxTab != null) timeTextBoxTab.Text = "";

                // Content 필터 초기화
                var textBox = FindName("DebugContentFilterTextBox") as System.Windows.Controls.TextBox;
                var textBoxTab = FindName("DebugContentFilterTextBox_Tab") as System.Windows.Controls.TextBox;
                if (textBox != null) textBox.Text = "";
                if (textBoxTab != null) textBoxTab.Text = "";

                ApplyIndividualFilters("DEBUG");
                _workLogService.AddLog("DEBUG 모든 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ DEBUG 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        // EXCEPTION Content Filter
        private void ExceptionContentFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("EXCEPTION");
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ EXCEPTION Content 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ExceptionTimeFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ApplyIndividualFilters("EXCEPTION");
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ EXCEPTION Time 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearExceptionTimeFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("ExceptionTimeFilterTextBox") as System.Windows.Controls.TextBox;
                if (textBox != null) textBox.Text = "";
                ApplyIndividualFilters("EXCEPTION");
                _workLogService.AddLog("EXCEPTION Time 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"❌ EXCEPTION Time 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearExceptionContentFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("ExceptionContentFilterTextBox") as System.Windows.Controls.TextBox;
                if (textBox != null) textBox.Text = "";
                ApplyIndividualFilters("EXCEPTION");
                _workLogService.AddLog("EXCEPTION Content 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? EXCEPTION Content 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ClearExceptionBusinessFilterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var item in _exceptionBusinessFilterItems)
                {
                    item.IsSelected = false;
                }
                UpdateComboBoxTexts();
                ApplyIndividualFilters("EXCEPTION");
                _workLogService.AddLog("EXCEPTION Business 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? EXCEPTION Business 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion

        #region Combined Filter Logic (AND Conditions)

        /// <summary>
        /// 특정 로그 타입에 대해 모든 개별 필터들을 AND 조건으로 적용
        /// </summary>
        private void ApplyIndividualFilters(string logType)
        {
            try
            {
                DataGrid? targetGrid = null;
                ObservableCollection<LogLineItem>? sourceCollection = null;

                // DataGrid와 소스 컬렉션 가져오기
                switch (logType)
                {
                    case "DATA":
                        targetGrid = FindName("dataLogDataGrid") as DataGrid;
                        sourceCollection = _dataGridManager.DataLogLines;
                        break;
                    case "EVENT":
                        targetGrid = FindName("eventLogDataGrid") as DataGrid;
                        sourceCollection = _dataGridManager.EventLogLines;
                        break;
                    case "DEBUG":
                        targetGrid = FindName("debugLogDataGrid") as DataGrid;
                        sourceCollection = _dataGridManager.DebugLogLines;
                        break;
                    case "EXCEPTION":
                        targetGrid = FindName("exceptionLogDataGrid") as DataGrid;
                        sourceCollection = _dataGridManager.ExceptionLogLines;
                        break;
                }

                if (targetGrid == null || sourceCollection == null)
                    return;

                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(targetGrid.ItemsSource);
                if (view == null) return;

                // 필터 조건들 수집
                var filterConditions = GetFilterConditions(logType);

                // AND 조건으로 필터 적용
                view.Filter = item =>
                {
                    if (item is not LogLineItem logItem) return false;

                    // 모든 조건이 true여야 함 (AND 조건)
                    foreach (var condition in filterConditions)
                    {
                        if (!condition(logItem))
                            return false;
                    }
                    return true;
                };

                view.Refresh();

                // 필터 결과 상태 로그
                var filteredCount = view.Cast<object>().Count();
                var totalCount = sourceCollection.Count;
                _workLogService.AddLog($"{logType} 필터 적용 완료: {filteredCount}/{totalCount} 항목", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} 필터 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 특정 로그 타입에 대한 모든 필터 조건들을 반환
        /// </summary>
        private List<Func<LogLineItem, bool>> GetFilterConditions(string logType)
        {
            var conditions = new List<Func<LogLineItem, bool>>();

            switch (logType)
            {
                case "DATA":
                    // Business Name 필터
                    var selectedBusinessNames = _dataBusinessFilterItems.Where(x => x.IsSelected).Select(x => x.Value).ToList();
                    if (selectedBusinessNames.Count > 0)
                    {
                        conditions.Add(item => selectedBusinessNames.Contains(item.BusinessName ?? ""));
                    }

                    // Time 필터 (새로 추가)
                    var dataTimeFilter = (FindName("DataTimeFilterTextBox") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    if (!string.IsNullOrEmpty(dataTimeFilter))
                    {
                        conditions.Add(item => (item.Timestamp ?? "").IndexOf(dataTimeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    // Content 필터
                    var dataContentFilter = (FindName("DataContentFilterTextBox") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    if (!string.IsNullOrEmpty(dataContentFilter))
                    {
                        conditions.Add(item => (item.Content ?? "").IndexOf(dataContentFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    break;

                case "EVENT":
                    // MsgId 필터
                    var selectedMsgIds = _eventMsgIdFilterItems.Where(x => x.IsSelected).Select(x => x.Value).ToList();
                    if (selectedMsgIds.Count > 0)
                    {
                        conditions.Add(item => selectedMsgIds.Contains(item.MsgId ?? ""));
                    }

                    // Time 필터 (새로 추가)
                    var eventTimeFilter = (FindName("EventTimeFilterTextBox") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    if (!string.IsNullOrEmpty(eventTimeFilter))
                    {
                        conditions.Add(item => (item.Timestamp ?? "").IndexOf(eventTimeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    // Content 필터
                    var eventContentFilter = (FindName("EventContentFilterTextBox") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    if (!string.IsNullOrEmpty(eventContentFilter))
                    {
                        conditions.Add(item => (item.Content ?? "").IndexOf(eventContentFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    break;

                case "DEBUG":
                    // Time 필터 (새로 추가)
                    var debugTimeFilter = (FindName("DebugTimeFilterTextBox") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    // Tab 모드에서도 확인
                    if (string.IsNullOrEmpty(debugTimeFilter))
                    {
                        debugTimeFilter = (FindName("DebugTimeFilterTextBox_Tab") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    }
                    if (!string.IsNullOrEmpty(debugTimeFilter))
                    {
                        conditions.Add(item => (item.Timestamp ?? "").IndexOf(debugTimeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    // Content 필터
                    var debugContentFilter = (FindName("DebugContentFilterTextBox") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    // Tab 모드에서도 확인
                    if (string.IsNullOrEmpty(debugContentFilter))
                    {
                        debugContentFilter = (FindName("DebugContentFilterTextBox_Tab") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    }
                    if (!string.IsNullOrEmpty(debugContentFilter))
                    {
                        conditions.Add(item => (item.Content ?? "").IndexOf(debugContentFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    break;

                case "EXCEPTION":
                    // Business Name 필터
                    var selectedExceptionBusinessNames = _exceptionBusinessFilterItems.Where(x => x.IsSelected).Select(x => x.Value).ToList();
                    if (selectedExceptionBusinessNames.Count > 0)
                    {
                        conditions.Add(item => selectedExceptionBusinessNames.Contains(item.BusinessName ?? ""));
                    }

                    // Time 필터 (새로 추가)
                    var exceptionTimeFilter = (FindName("ExceptionTimeFilterTextBox") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    if (!string.IsNullOrEmpty(exceptionTimeFilter))
                    {
                        conditions.Add(item => (item.Timestamp ?? "").IndexOf(exceptionTimeFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }

                    // Content 필터
                    var exceptionContentFilter = (FindName("ExceptionContentFilterTextBox") as System.Windows.Controls.TextBox)?.Text?.Trim();
                    if (!string.IsNullOrEmpty(exceptionContentFilter))
                    {
                        conditions.Add(item => (item.Content ?? "").IndexOf(exceptionContentFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    break;
            }

            return conditions;
        }

        #endregion
    }
}
