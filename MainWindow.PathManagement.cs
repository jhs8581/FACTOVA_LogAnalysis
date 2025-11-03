using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using FACTOVA_LogAnalysis.Services;
using WpfMessageBox = System.Windows.MessageBox;

namespace FACTOVA_LogAnalysis
{
    public partial class MainWindow
    {
        #region Path Management (moved)

        private void RestoreOrSetDefaultLogFolderPath()
        {
            try
            {
                _workLogService.AddLog("Setting log folder path...", WorkLogType.Info);

                string savedPath = _appSettings.LogFolderPath;

                if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                {
                    _logFolderPath = savedPath;
                    _logFileManager.LogFolderPath = savedPath;
                    _eventHandlerManager.LogFolderPath = savedPath; // EventHandlerManager에도 설정
                    LogFolderPathTextBox.Text = savedPath;
                    _workLogService.AddLog($"Restored saved log path: {savedPath}", WorkLogType.Success);
                }
                else
                {
                    SetDefaultLogFolderPath();
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"Failed to restore log path: {ex.Message}", WorkLogType.Error);
                WpfMessageBox.Show($"Error restoring log folder path: {ex.Message}\nUsing default path.");
                SetDefaultLogFolderPath();
            }
        }

        private void SetDefaultLogFolderPath()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _logFolderPath = Path.Combine(userProfile, "Documents", "FactovaMES", "SFC", "Logs");
            _logFileManager.LogFolderPath = _logFolderPath;
            _eventHandlerManager.LogFolderPath = _logFolderPath; // EventHandlerManager에도 설정
            LogFolderPathTextBox.Text = _logFolderPath;
            _workLogService.AddLog($"Default log path set: {_logFolderPath}", WorkLogType.Info);
        }

        private void ChangeFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select log folder.";
                dialog.SelectedPath = _logFolderPath;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _logFolderPath = dialog.SelectedPath;
                    _logFileManager.LogFolderPath = _logFolderPath;
                    _eventHandlerManager.LogFolderPath = _logFolderPath; // EventHandlerManager에도 설정
                    LogFolderPathTextBox.Text = _logFolderPath;

                    _workLogService.AddLog($"Log folder changed: {_logFolderPath}", WorkLogType.Success);
                    SaveSelectedLogFolderPath(_logFolderPath);

                    // Reconfigure date selector based on new folder
                    TryConfigureDateSelectorFromLogFolder();
                }
            }
        }

        private void SaveSelectedLogFolderPath(string path)
        {
            try
            {
                _appSettings.LogFolderPath = path;
                _appSettings.Save();
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Error saving log folder path: {ex.Message}");
            }
        }

        private void ResetFolderPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetDefaultLogFolderPath();
                SaveSelectedLogFolderPath(_logFolderPath);
                WpfMessageBox.Show("Log folder path has been reset to default.");

                // Reconfigure date selector after resetting
                TryConfigureDateSelectorFromLogFolder();
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Error resetting log folder path: {ex.Message}");
            }
        }

        // Configure date selector: if custom log folder set, switch to ComboBox bound to file dates
        private void TryConfigureDateSelectorFromLogFolder()
        {
            try
            {
                // If _appSettings has a custom path, prefer that; otherwise use current _logFolderPath
                var configured = !string.IsNullOrEmpty(_appSettings.LogFolderPath) ? _appSettings.LogFolderPath : _logFolderPath;

                // If no custom folder set -> show DatePicker
                if (string.IsNullOrWhiteSpace(configured))
                {
                    SetDateSelectorMode(useCombo: false);
                    return;
                }

                if (Directory.Exists(configured))
                {
                    _logFileManager.LogFolderPath = configured;
                    _logFolderPath = configured;

                    var dates = ExtractDatesFromLogFilenames(configured);
                    var combo = FindName("dateComboBox") as System.Windows.Controls.ComboBox;
                    var datePicker = FindName("dateSelector") as System.Windows.Controls.DatePicker;
                    if (combo != null)
                    {
                        combo.ItemsSource = dates;
                        if (dates.Any())
                        {
                            // select the first (most recent) date string
                            combo.SelectedItem = dates[0];

                            // also update DatePicker for consistency if present
                            if (datePicker != null)
                            {
                                if (DateTime.TryParse(dates[0], out DateTime parsed))
                                {
                                    datePicker.SelectedDate = parsed;
                                }
                            }
                        }
                    }

                    SetDateSelectorMode(useCombo: true);
                }
                else
                {
                    SetDateSelectorMode(useCombo: false);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 날짜 콤보 구성 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        private void SetDateSelectorMode(bool useCombo)
        {
            var datePicker = FindName("dateSelector") as System.Windows.Controls.DatePicker;
            var combo = FindName("dateComboBox") as System.Windows.Controls.ComboBox;

            if (useCombo)
            {
                if (datePicker != null) datePicker.Visibility = Visibility.Collapsed;
                if (combo != null) combo.Visibility = Visibility.Visible;
            }
            else
            {
                if (datePicker != null) datePicker.Visibility = Visibility.Visible;
                if (combo != null) combo.Visibility = Visibility.Collapsed;
            }
        }

        private List<string> ExtractDatesFromLogFilenames(string folder)
        {
            var results = new List<string>();
            try
            {
                // Enumerate files in folder and subfolders to support year/month subfolder layout (default path)
                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);

                foreach (var f in files)
                {
                    try
                    {
                        var name = Path.GetFileNameWithoutExtension(f);

                        // 1) Try parse date from filename
                        var date = TryParseDateFromString(name);
                        if (date != null)
                        {
                            var s = date.Value.ToString("yyyy-MM-dd");
                            if (!results.Contains(s)) results.Add(s);
                            continue;
                        }

                        // 2) Try infer from parent directory names (e.g., ...\2025\07\ or ...\2025\\07)
                        var dir = Path.GetDirectoryName(f);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            var parts = dir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            // Look for a sequence like yyyy then MM (or MM then dd)
                            for (int i = 0; i < parts.Length; i++)
                            {
                                var yearPart = parts[i];
                                if (yearPart.Length == 4 && int.TryParse(yearPart, out int y) && y >= 1900 && y <= 3000)
                                {
                                    // try to find a month segment after
                                    if (i + 1 < parts.Length)
                                    {
                                        var monthPart = parts[i + 1];
                                        if (monthPart.Length == 1 || monthPart.Length == 2)
                                        {
                                            if (int.TryParse(monthPart, out int m) && m >= 1 && m <= 12)
                                            {
                                                // If filename contains day, try to parse day; otherwise use first day of month
                                                int day = 1;
                                                var dayMatch = System.Text.RegularExpressions.Regex.Match(Path.GetFileNameWithoutExtension(f), "(\\d{1,2})");
                                                if (dayMatch.Success && int.TryParse(dayMatch.Value, out int d) && d >= 1 && d <= DateTime.DaysInMonth(y, m))
                                                    day = d;

                                                try
                                                {
                                                    var inferred = new DateTime(y, m, day);
                                                    var s = inferred.ToString("yyyy-MM-dd");
                                                    if (!results.Contains(s)) results.Add(s);
                                                    break;
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { /* ignore individual file parse errors */ }
                }

                // If no files found directly, also attempt to infer dates from top-level year/month folders
                if (!results.Any())
                {
                    var yearDirs = Directory.GetDirectories(folder);
                    foreach (var ydir in yearDirs)
                    {
                        var yname = Path.GetFileName(ydir);
                        if (yname.Length == 4 && int.TryParse(yname, out int yy))
                        {
                            var monthDirs = Directory.GetDirectories(ydir);
                            foreach (var mdir in monthDirs)
                            {
                                var mname = Path.GetFileName(mdir);
                                if (int.TryParse(mname, out int mm) && mm >= 1 && mm <= 12)
                                {
                                    // add first day of month (user can refine by filename selection)
                                    try
                                    {
                                        var inferred = new DateTime(yy, mm, 1);
                                        var s = inferred.ToString("yyyy-MM-dd");
                                        if (!results.Contains(s)) results.Add(s);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }

                results = results.OrderByDescending(x => x).ToList();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 파일명에서 날짜 추출 실패: {ex.Message}", WorkLogType.Error);
            }

            return results;
        }

        private DateTime? TryParseDateFromString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            var patterns = new[] { "yyyyMMdd", "yyyy-MM-dd", "yyyy_MM_dd", "yyyyMMddHHmmss", "yyyy-MM-dd_HH_mm_ss" };
            foreach (var p in patterns)
            {
                if (DateTime.TryParseExact(s, p, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime dt))
                    return dt.Date;
            }

            var m = System.Text.RegularExpressions.Regex.Match(s, "(\\d{4})[-_]?\\d{2}[-_]?\\d{2}");
            if (m.Success)
            {
                var candidate = m.Value.Replace('_', '-');
                if (DateTime.TryParse(candidate, out DateTime dt2)) return dt2.Date;
            }

            var mm = System.Text.RegularExpressions.Regex.Match(s, "(\\d{8})");
            if (mm.Success)
            {
                // Try both common 8-digit formats: yyyyMMdd and MMddyyyy
                var formats = new[] { "yyyyMMdd", "MMddyyyy" };
                if (DateTime.TryParseExact(mm.Value, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime dt3))
                    return dt3.Date;

                // As a last resort, try general parse (will handle locale-specific order if possible)
                if (DateTime.TryParse(mm.Value, out DateTime dt4))
                    return dt4.Date;
            }

            return null;
        }

        #endregion
    }
}
