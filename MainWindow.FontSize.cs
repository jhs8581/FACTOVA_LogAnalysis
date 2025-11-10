using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FACTOVA_LogAnalysis.Services;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;

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

        #region Content Cell Height Management

        /// <summary>
        /// Content Cell 높이 감소
        /// </summary>
        private void ContentCellHeightDecrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("ContentCellHeightTextBox") as System.Windows.Controls.TextBox;
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
                _workLogService.AddLog($"Content 높이 감소 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Content Cell 높이 증가
        /// </summary>
        private void ContentCellHeightIncrease_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("ContentCellHeightTextBox") as System.Windows.Controls.TextBox;
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
                _workLogService.AddLog($"Content 높이 증가 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Content Cell 높이 적용
        /// </summary>
        private void ApplyContentCellHeight_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textBox = FindName("ContentCellHeightTextBox") as System.Windows.Controls.TextBox;
                if (textBox == null || !double.TryParse(textBox.Text, out double height))
                {
                    _workLogService.AddLog("유효한 Content 높이 값을 입력하세요", WorkLogType.Warning);
                    return;
                }

                // ContentCellStyle을 동적으로 업데이트
                var contentStyle = new Style(typeof(DataGridCell));
                contentStyle.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, WpfBrushes.LightGray));
                contentStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0.5)));
                contentStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(0)));
                contentStyle.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));

                var template = new ControlTemplate(typeof(DataGridCell));
                var factory = new FrameworkElementFactory(typeof(Border));
                factory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(DataGridCell.BorderBrushProperty));
                factory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(DataGridCell.BorderThicknessProperty));
                factory.SetValue(Border.BackgroundProperty, WpfBrushes.White);

                var scrollViewerFactory = new FrameworkElementFactory(typeof(ScrollViewer));
                scrollViewerFactory.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
                scrollViewerFactory.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
                scrollViewerFactory.SetValue(FrameworkElement.MaxHeightProperty, height); // ✨ 사용자 지정 높이
                scrollViewerFactory.SetValue(UIElement.FocusableProperty, false);

                var textBoxFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBox));
                textBoxFactory.SetBinding(System.Windows.Controls.TextBox.TextProperty, new System.Windows.Data.Binding("Content") { Mode = System.Windows.Data.BindingMode.OneWay });
                textBoxFactory.SetValue(System.Windows.Controls.TextBox.IsReadOnlyProperty, true);
                textBoxFactory.SetValue(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(0));
                textBoxFactory.SetValue(System.Windows.Controls.Control.BackgroundProperty, WpfBrushes.Transparent);
                textBoxFactory.SetValue(System.Windows.Controls.Control.ForegroundProperty, WpfBrushes.Black);
                textBoxFactory.SetValue(System.Windows.Controls.TextBox.TextWrappingProperty, TextWrapping.Wrap);
                textBoxFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2));
                textBoxFactory.SetValue(System.Windows.Controls.Control.PaddingProperty, new Thickness(0));
                textBoxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
                textBoxFactory.SetValue(System.Windows.Controls.Control.FontFamilyProperty, new WpfFontFamily("Consolas"));
                textBoxFactory.SetValue(System.Windows.Controls.TextBox.AcceptsReturnProperty, true);
                textBoxFactory.SetValue(FrameworkElement.CursorProperty, System.Windows.Input.Cursors.Arrow);
                textBoxFactory.SetValue(UIElement.FocusableProperty, true);
                textBoxFactory.SetValue(System.Windows.Controls.Control.IsTabStopProperty, false);
                textBoxFactory.SetValue(System.Windows.Controls.Primitives.TextBoxBase.SelectionBrushProperty, new SolidColorBrush(WpfColor.FromRgb(0x33, 0x99, 0xFF)));
                textBoxFactory.SetValue(System.Windows.Controls.Primitives.TextBoxBase.SelectionTextBrushProperty, WpfBrushes.White);
                textBoxFactory.SetValue(System.Windows.Controls.Primitives.TextBoxBase.SelectionOpacityProperty, 0.4);

                scrollViewerFactory.AppendChild(textBoxFactory);
                factory.AppendChild(scrollViewerFactory);
                template.VisualTree = factory;

                contentStyle.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty, template));

                // 리소스에 추가
                if (Resources.Contains("ContentCellStyle"))
                {
                    Resources.Remove("ContentCellStyle");
                }
                Resources.Add("ContentCellStyle", contentStyle);

                // 모든 DataGrid에 적용
                var gridNames = new[] {
                    "dataLogDataGrid", "eventLogDataGrid", "debugLogDataGrid", "exceptionLogDataGrid",
                    "unifiedLogDataGrid", "execTimeDataGrid"
                };

                foreach (var name in gridNames)
                {
                    var dg = FindDataGridByName(name);
                    if (dg != null)
                    {
                        // Content 컬럼 찾기
                        var contentColumn = dg.Columns.FirstOrDefault(c => c.Header?.ToString() == "Content");
                        if (contentColumn is DataGridTextColumn textCol)
                        {
                            textCol.CellStyle = contentStyle;
                        }
                        
                        dg.Items.Refresh();
                        dg.UpdateLayout();
                    }
                }

                // 설정 저장
                try
                {
                    _appSettings.ContentCellMaxHeight = height;
                    _appSettings.Save();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save ContentCellMaxHeight: {ex.Message}");
                }

                _workLogService.AddLog($"Content 셀 높이 적용 완료: {height}px", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"Content 높이 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion

        #endregion
    }
}
