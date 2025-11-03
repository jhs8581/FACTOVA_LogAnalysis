using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FACTOVA_LogAnalysis.Services;

namespace FACTOVA_LogAnalysis
{
    public partial class MainWindow
    {
        #region Font Size Management (moved)

        private void ApplyFontSizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!double.TryParse(FontSizeTextBox.Text, out double size))
                {
                    _workLogService.AddLog("유효한 폰트 크기 값을 입력하세요", WorkLogType.Warning);
                    return;
                }

                var names = new[] { "dataLogTextBox", "eventLogTextBox", "debugLogTextBox", "exceptionLogTextBox", "execTimeTextBox" };
                foreach (var name in names)
                {
                    var tb = FindName(name) as System.Windows.Controls.TextBox;
                    if (tb != null)
                        tb.FontSize = size;
                }

                // workLog is a RichTextBox
                var rtb = FindName("workLogTextBox") as System.Windows.Controls.RichTextBox;
                if (rtb != null)
                    rtb.FontSize = size;

                // Save to settings
                try
                {
                    _appSettings.TextFontSize = size;
                    _appSettings.Save();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save TextFontSize: {ex.Message}");
                }

                _workLogService.AddLog($"텍스트 탭 폰트 크기 적용: {size}", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"텍스트 폰트 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void DataGridFontSizeDecrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!double.TryParse(DataGridFontSizeTextBox.Text, out double size))
                {
                    _workLogService.AddLog("유효한 DataGrid 폰트 크기 값을 입력하세요", WorkLogType.Warning);
                    return;
                }
                size = Math.Max(6, size - 1);
                DataGridFontSizeTextBox.Text = ((int)size).ToString();

                // Apply to the last selected DataGrid only
                var dg = GetLastSelectedDataGrid();
                if (dg != null)
                {
                    ApplyFontToDataGridInstance(dg, size);
                }
                else
                {
                    _workLogService.AddLog("적용할 DataGrid가 선택되지 않았습니다. 폰트를 적용하려면 그리드를 클릭하세요.", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"DataGrid 폰트 감소 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void DataGridFontSizeIncrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!double.TryParse(DataGridFontSizeTextBox.Text, out double size))
                {
                    _workLogService.AddLog("유효한 DataGrid 폰트 크기 값을 입력하세요", WorkLogType.Warning);
                    return;
                }
                size = Math.Min(72, size + 1);
                DataGridFontSizeTextBox.Text = ((int)size).ToString();

                // Apply to the last selected DataGrid only
                var dg = GetLastSelectedDataGrid();
                if (dg != null)
                {
                    ApplyFontToDataGridInstance(dg, size);
                }
                else
                {
                    _workLogService.AddLog("적용할 DataGrid가 선택되지 않았습니다. 폰트를 적용하려면 그리드를 클릭하세요.", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"DataGrid 폰트 증가 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ApplyColorButton_Click_1(object sender, RoutedEventArgs e)
        {
            // Color picker removed from UI; no action
        }

        private void ApplyDataGridFontSizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!double.TryParse(DataGridFontSizeTextBox.Text, out double size))
                {
                    _workLogService.AddLog("유효한 DataGrid 폰트 크기 값을 입력하세요", WorkLogType.Warning);
                    return;
                }

                // Apply only to the last selected DataGrid
                var target = GetLastSelectedDataGrid();
                if (target == null)
                {
                    _workLogService.AddLog("적용할 DataGrid가 선택되지 않았습니다. 먼저 원하는 DataGrid를 클릭하세요.", WorkLogType.Warning);
                    return;
                }

                ApplyFontToDataGridInstance(target, size);

                // Save to settings
                try
                {
                    _appSettings.DataGridFontSize = size;
                    _appSettings.Save();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save DataGridFontSize: {ex.Message}");
                }

                _workLogService.AddLog($"DataGrid 폰트 크기 적용 완료: {target.Name} -> {size}", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"DataGrid 폰트 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void ApplyAllFontSizesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (double.TryParse(FontSizeTextBox.Text, out double textSize))
                {
                    var names = new[] { "dataLogTextBox", "eventLogTextBox", "debugLogTextBox", "exceptionLogTextBox", "execTimeTextBox" };
                    foreach (var name in names)
                    {
                        var tb = FindName(name) as System.Windows.Controls.TextBox;
                        if (tb != null)
                            tb.FontSize = textSize;
                    }

                    // workLog is a RichTextBox
                    var rtb = FindName("workLogTextBox") as System.Windows.Controls.RichTextBox;
                    if (rtb != null)
                        rtb.FontSize = textSize;

                    // Save text size
                    _appSettings.TextFontSize = textSize;
                }

                if (double.TryParse(DataGridFontSizeTextBox.Text, out double dgSize))
                {
                    var gridNames = new[] {
                        "dataLogDataGrid", "eventLogDataGrid", "debugLogDataGrid", "exceptionLogDataGrid",
                        "dataLogDataGrid_Tab", "eventLogDataGrid_Tab", "debugLogDataGrid_Tab", "exceptionLogDataGrid_Tab"
                    };

                    foreach (var name in gridNames)
                    {
                        var dg = FindDataGridByName(name);
                        if (dg == null)
                        {
                            _workLogService.AddLog($"DataGrid을 찾을 수 없음(일괄): {name}", WorkLogType.Warning);
                            continue;
                        }

                        dg.FontSize = dgSize;

                        // Apply CellStyle/RowStyle and column ElementStyle similar to individual apply
                        var cellStyle = new Style(typeof(DataGridCell));
                        cellStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontSizeProperty, dgSize));
                        dg.CellStyle = cellStyle;

                        var rowStyle = new Style(typeof(DataGridRow));
                        rowStyle.Setters.Add(new Setter(System.Windows.Controls.Control.FontSizeProperty, dgSize));
                        dg.RowStyle = rowStyle;

                        foreach (var col in dg.Columns.OfType<DataGridTextColumn>())
                        {
                            var elementStyle = new Style(typeof(TextBlock));
                            elementStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, dgSize));
                            col.ElementStyle = elementStyle;
                        }

                        dg.Items.Refresh();
                        dg.UpdateLayout();

                        _workLogService.AddLog($"DataGrid에 폰트 일괄 적용 완료: {name} -> {dgSize}", WorkLogType.Info);
                    }

                    // Save data grid size
                    _appSettings.DataGridFontSize = dgSize;
                }

                // Persist both settings
                try
                {
                    _appSettings.Save();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save font sizes: {ex.Message}");
                }

                _workLogService.AddLog("모든 폰트 사이즈 일괄 적용 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"일괄 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion
    }
}
