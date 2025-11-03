using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;

namespace FACTOVA_LogAnalysis.Services
{
    public enum WorkLogType
    {
        Info,
        Success,
        Warning,
        Error,
        Debug
    }

    public class WorkLogService
    {
        private int _workLogLineCounter = 1;
        private readonly object _workLogLock = new object();

        public event Action<string, WorkLogType>? LogAdded;

        public void AddLog(string message, WorkLogType logType = WorkLogType.Info)
        {
            string cleaned = CleanMessage(message);
            LogAdded?.Invoke(cleaned, logType);
        }

        private static string CleanMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return string.Empty;

            int idx = 0;
            while (idx < message.Length && (message[idx] == '?' || char.IsWhiteSpace(message[idx]))) idx++;
            return message.Substring(idx);
        }

        private static string PrefixForLogType(WorkLogType type)
        {
            return type switch
            {
                WorkLogType.Info => "[INFO]",
                WorkLogType.Success => "[OK]",
                WorkLogType.Warning => "[WARN]",
                WorkLogType.Error => "[ERROR]",
                WorkLogType.Debug => "[DEBUG]",
                _ => string.Empty
            };
        }

        // Single, clear implementation using fully-qualified control types for safety
        public void AddToRichTextBox(global::System.Windows.Controls.RichTextBox workLogTextBox,
            global::System.Windows.Controls.TextBox workLogLineNumberTextBox,
            global::System.Windows.Controls.TextBlock statusText,
            global::System.Windows.Controls.CheckBox autoScrollCheckBox,
            string message,
            WorkLogType logType)
        {
            try
            {
                lock (_workLogLock)
                {
                    // ✅ 폰트 설정 (한글 지원 폰트)
                    if (workLogTextBox.FontFamily.Source != "맑은 고딕")
                    {
                        workLogTextBox.FontFamily = new System.Windows.Media.FontFamily("맑은 고딕, Malgun Gothic, Gulim, Arial Unicode MS, MS Gothic");
                    }

                    string prefix = PrefixForLogType(logType);
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    string logMessage = string.IsNullOrWhiteSpace(prefix)
                        ? $"[{timestamp}] {message}"
                        : $"[{timestamp}] {prefix} {message}";

                    var color = logType switch
                    {
                        WorkLogType.Info => Colors.Black,
                        WorkLogType.Success => Colors.Green,
                        WorkLogType.Warning => Colors.Orange,
                        WorkLogType.Error => Colors.Red,
                        WorkLogType.Debug => Colors.Gray,
                        _ => Colors.Black
                    };

                    Paragraph paragraph;
                    if (workLogTextBox.Document.Blocks.FirstBlock is Paragraph p)
                        paragraph = p;
                    else
                    {
                        paragraph = new Paragraph();
                        workLogTextBox.Document.Blocks.Add(paragraph);
                    }

                    if (_workLogLineCounter == 1 && paragraph.Inlines.Count > 0)
                        paragraph.Inlines.Clear();

                    var run = new Run(logMessage)
                    {
                        Foreground = new SolidColorBrush(color),
                        FontWeight = logType == WorkLogType.Error ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal
                    };

                    paragraph.Inlines.Add(run);
                    paragraph.Inlines.Add(new LineBreak());

                    UpdateLineNumbers(workLogLineNumberTextBox);

                    if (!string.IsNullOrEmpty(message))
                        statusText.Text = message.Length > 30 ? $"로그 {_workLogLineCounter}줄 | {message.Substring(0, 30)}..." : $"로그 {_workLogLineCounter}줄 | {message}";
                    else
                        statusText.Text = $"로그 {_workLogLineCounter}줄";

                    _workLogLineCounter++;

                    // Move caret and optionally scroll on UI thread
                    try
                    {
                        if (workLogTextBox.Dispatcher.CheckAccess())
                        {
                            workLogTextBox.CaretPosition = workLogTextBox.Document.ContentEnd;
                            if (autoScrollCheckBox != null && autoScrollCheckBox.IsChecked == true)
                                workLogTextBox.ScrollToEnd();
                        }
                        else
                        {
                            workLogTextBox.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                workLogTextBox.CaretPosition = workLogTextBox.Document.ContentEnd;
                                if (autoScrollCheckBox != null && autoScrollCheckBox.IsChecked == true)
                                    workLogTextBox.ScrollToEnd();
                            }));
                        }
                    }
                    catch { /* ignore UI thread exceptions */ }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddToRichTextBox 오류: {ex.Message}");
            }
        }

        private void UpdateLineNumbers(global::System.Windows.Controls.TextBox lineNumberTextBox)
        {
            try
            {
                var sb = new StringBuilder();
                for (int i = 1; i <= _workLogLineCounter; i++)
                {
                    sb.AppendLine(i.ToString());
                }
                lineNumberTextBox.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateLineNumbers 오류: {ex.Message}");
            }
        }

        public void ClearLog(global::System.Windows.Controls.RichTextBox workLogTextBox,
            global::System.Windows.Controls.TextBox workLogLineNumberTextBox,
            global::System.Windows.Controls.TextBlock statusText)
        {
            try
            {
                lock (_workLogLock)
                {
                    workLogTextBox.Document.Blocks.Clear();
                    var paragraph = new Paragraph();
                    paragraph.Inlines.Add(new Run("작업 로그가 여기에 표시됩니다...")
                    {
                        Foreground = System.Windows.Media.Brushes.Gray,
                        FontStyle = System.Windows.FontStyles.Italic
                    });
                    workLogTextBox.Document.Blocks.Add(paragraph);

                    workLogLineNumberTextBox.Text = string.Empty;
                    _workLogLineCounter = 1;
                    statusText.Text = "로그가 초기화되었습니다";
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"로그 초기화 중 오류 발생: {ex.Message}");
            }
        }

        public void SaveLog(global::System.Windows.Controls.RichTextBox workLogTextBox, string filePath)
        {
            try
            {
                var range = new TextRange(workLogTextBox.Document.ContentStart, workLogTextBox.Document.ContentEnd);
                File.WriteAllText(filePath, range.Text, Encoding.UTF8);
                AddLog($"작업 로그가 저장됨: {filePath}", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                AddLog($"로그 저장 실패: {ex.Message}", WorkLogType.Error);
                throw new Exception($"로그 저장 중 오류 발생: {ex.Message}");
            }
        }
    }
}
