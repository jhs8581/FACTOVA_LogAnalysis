using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using FACTOVA_LogAnalysis.Services;
using FACTOVA_LogAnalysis.Helpers;

namespace FACTOVA_LogAnalysis
{
    public partial class MainWindow
    {
        #region DataGrid Initialization (moved)

        private void InitializeDataGrids()
        {
            try
            {
                // DataGrid 참조 확인 및 DataGridManager에 전달
                var dataGrid = FindName("dataLogDataGrid") as DataGrid;
                var eventGrid = FindName("eventLogDataGrid") as DataGrid;
                var debugGrid = FindName("debugLogDataGrid") as DataGrid;
                var exceptionGrid = FindName("exceptionLogDataGrid") as DataGrid;

                if (dataGrid != null && eventGrid != null && debugGrid != null && exceptionGrid != null)
                {
                    // Ensure explicit grid-line styles are applied to these DataGrids
                    ApplyFullGridLines(dataGrid);
                    ApplyFullGridLines(eventGrid);
                    ApplyFullGridLines(debugGrid);
                    ApplyFullGridLines(exceptionGrid);

                    _dataGridManager.InitializeDataGrids(dataGrid, eventGrid, debugGrid, exceptionGrid);

                    // register focus / click handlers to track last selected DataGrid (in-memory)
                    var primaryGrids = new[] { dataGrid, eventGrid, debugGrid, exceptionGrid };
                    foreach (var dg in primaryGrids)
                    {
                        dg.GotFocus += DataGrid_GotFocus;
                        dg.PreviewMouseLeftButtonDown += DataGrid_PreviewMouseLeftButtonDown;
                    }

                    // Also bind the 4-tab DataGrids to the same underlying collections so both views show identical data
                    try
                    {
                        var dataGridTab = FindName("dataLogDataGrid_Tab") as DataGrid;
                        var eventGridTab = FindName("eventLogDataGrid_Tab") as DataGrid;
                        var debugGridTab = FindName("debugLogDataGrid_Tab") as DataGrid;
                        var exceptionGridTab = FindName("exceptionLogDataGrid_Tab") as DataGrid;

                        if (dataGridTab != null)
                        {
                            // Ensure basic properties similar to main grids
                            LogDataGridHelper.SetupDataGridFeatures(dataGridTab);
                            ApplyFullGridLines(dataGridTab);
                            dataGridTab.ItemsSource = _dataGridManager.DataLogLines;
                            dataGridTab.GotFocus += DataGrid_GotFocus;
                            dataGridTab.PreviewMouseLeftButtonDown += DataGrid_PreviewMouseLeftButtonDown;
                        }

                        if (eventGridTab != null)
                        {
                            LogDataGridHelper.SetupDataGridFeatures(eventGridTab);
                            ApplyFullGridLines(eventGridTab);
                            eventGridTab.ItemsSource = _dataGridManager.EventLogLines;
                            eventGridTab.GotFocus += DataGrid_GotFocus;
                            eventGridTab.PreviewMouseLeftButtonDown += DataGrid_PreviewMouseLeftButtonDown;
                        }

                        if (debugGridTab != null)
                        {
                            LogDataGridHelper.SetupDataGridFeatures(debugGridTab);
                            ApplyFullGridLines(debugGridTab);
                            debugGridTab.ItemsSource = _dataGridManager.DebugLogLines;
                            debugGridTab.GotFocus += DataGrid_GotFocus;
                            debugGridTab.PreviewMouseLeftButtonDown += DataGrid_PreviewMouseLeftButtonDown;
                        }

                        if (exceptionGridTab != null)
                        {
                            LogDataGridHelper.SetupDataGridFeatures(exceptionGridTab);
                            ApplyFullGridLines(exceptionGridTab);
                            exceptionGridTab.ItemsSource = _dataGridManager.ExceptionLogLines;
                            exceptionGridTab.GotFocus += DataGrid_GotFocus;
                            exceptionGridTab.PreviewMouseLeftButtonDown += DataGrid_PreviewMouseLeftButtonDown;
                        }

                        // Register tab DataGrids with manager so operations cover both views
                        _dataGridManager.RegisterTabDataGrids(dataGridTab, eventGridTab, debugGridTab, exceptionGridTab);
                    }
                    catch (Exception ex)
                    {
                        _workLogService.AddLog($"? 4-tab DataGrid 바인딩 오류: {ex.Message}", WorkLogType.Error);
                    }

                     // 콤보박스 업데이트 이벤트 구독
                     _dataGridManager.ComboBoxFiltersUpdated += UpdateBusinessNameFilters;

                    // 멀티선택 콤보박스 초기화
                    InitializeMultiSelectComboBoxes();

                    _workLogService.AddLog("? DataGridManager를 통한 초기화 완료", WorkLogType.Success);
                }
                else
                {
                    _workLogService.AddLog("일부 DataGrid를 찾을 수 없음", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? DataGrid 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        // Apply full grid lines and the cell/header styles from application resources to a DataGrid
        private void ApplyFullGridLines(DataGrid dg)
        {
            if (dg == null) return;
            try
            {
                dg.GridLinesVisibility = DataGridGridLinesVisibility.All;
                // Try apply styles from resources if available
                var cellStyle = TryFindResource("GridCellStyle") as Style ?? System.Windows.Application.Current.TryFindResource("GridCellStyle") as Style;
                var headerStyle = TryFindResource("GridHeaderStyle") as Style ?? System.Windows.Application.Current.TryFindResource("GridHeaderStyle") as Style;
                if (cellStyle != null) dg.CellStyle = cellStyle;
                if (headerStyle != null) dg.ColumnHeaderStyle = headerStyle;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyFullGridLines error: {ex.Message}");
            }
        }

        #endregion
    }
}
