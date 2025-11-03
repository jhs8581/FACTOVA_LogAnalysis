using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FACTOVA_LogAnalysis.Services;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMessageBox = System.Windows.MessageBox;
using WpfClipboard = System.Windows.Clipboard;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace FACTOVA_LogAnalysis.Services
{
    /// <summary>
    /// 이벤트 핸들러 관리를 담당하는 서비스 클래스
    /// 파일 작업, 외부 프로그램 실행, 텍스트박스 이벤트 등을 관리
    /// </summary>
    public class EventHandlerManager
    {
        #region Fields

        private readonly WorkLogService _workLogService;
        private readonly SearchFilterManager _searchFilterManager;
        private string _logFolderPath = "";
        
        // 활성 텍스트박스 추적
        private WpfTextBox? _activeTextBox;

        #endregion

        #region Properties

        public string LogFolderPath 
        { 
            get => _logFolderPath; 
            set => _logFolderPath = value ?? "";
        }

        public WpfTextBox? ActiveTextBox => _activeTextBox;

        #endregion

        #region Constructor

        public EventHandlerManager(WorkLogService workLogService, SearchFilterManager searchFilterManager)
        {
            _workLogService = workLogService ?? throw new ArgumentNullException(nameof(workLogService));
            _searchFilterManager = searchFilterManager ?? throw new ArgumentNullException(nameof(searchFilterManager));
        }

        #endregion

        #region Public Methods - File Operations

        /// <summary>
        /// 폴더 열기
        /// </summary>
        public void OpenFolder()
        {
            try
            {
                if (Directory.Exists(_logFolderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = _logFolderPath,
                        UseShellExecute = true
                    });
                    _workLogService.AddLog($"폴더 열기: {_logFolderPath}", WorkLogType.Success);
                }
                else
                {
                    _workLogService.AddLog($"폴더가 존재하지 않음: {_logFolderPath}", WorkLogType.Warning);
                    WpfMessageBox.Show($"Folder does not exist: {_logFolderPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 폴더 열기 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 메모장 열기
        /// </summary>
        public void OpenNotepad()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    UseShellExecute = true
                });
                _workLogService.AddLog("메모장 열기", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 메모장 열기 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 워크로그 저장
        /// </summary>
        public void SaveWorkLog(WpfRichTextBox workLogTextBox)
        {
            try
            {
                var dialog = new WpfSaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"WorkLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    _workLogService.SaveLog(workLogTextBox, dialog.FileName);
                    _workLogService.AddLog($"워크로그 저장: {dialog.FileName}", WorkLogType.Success);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 워크로그 저장 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 워크로그 초기화
        /// </summary>
        public void ClearWorkLog(WpfRichTextBox workLogTextBox, WpfTextBox workLogLineNumberTextBox, TextBlock statusText)
        {
            try
            {
                _workLogService.ClearLog(workLogTextBox, workLogLineNumberTextBox, statusText);
                _workLogService.AddLog("워크로그 초기화", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 워크로그 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion

        #region Public Methods - TextBox Events

        /// <summary>
        /// 텍스트박스 키 다운 이벤트 처리
        /// </summary>
        public void HandleTextBoxKeyDown(object sender, WpfKeyEventArgs e, Window parentWindow)
        {
            try
            {
                if (sender is WpfTextBox textBox)
                {
                    _activeTextBox = textBox;
                    
                    // Ctrl+F: 검색 다이얼로그 열기
                    if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        _searchFilterManager.OpenFindDialog(parentWindow, textBox, textBox.SelectedText);
                        e.Handled = true;
                        _workLogService.AddLog("검색 다이얼로그 열기 (Ctrl+F)", WorkLogType.Info);
                    }
                    // Ctrl+A: 전체 선택
                    else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        textBox.SelectAll();
                        e.Handled = true;
                        _workLogService.AddLog("전체 텍스트 선택 (Ctrl+A)", WorkLogType.Info);
                    }
                    // Ctrl+C: 복사
                    else if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                    {
                        if (!string.IsNullOrEmpty(textBox.SelectedText))
                        {
                            WpfClipboard.SetText(textBox.SelectedText);
                            _workLogService.AddLog($"텍스트 복사: {textBox.SelectedText.Length}자", WorkLogType.Info);
                        }
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 텍스트박스 키 이벤트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 텍스트박스 포커스 이벤트 처리
        /// </summary>
        public void HandleTextBoxGotFocus(object sender)
        {
            try
            {
                if (sender is WpfTextBox textBox)
                {
                    _activeTextBox = textBox;
                    _workLogService.AddLog($"텍스트박스 포커스: {textBox.Name ?? "Unknown"}", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 텍스트박스 포커스 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 텍스트박스 스크롤 동기화 처리
        /// </summary>
        public void HandleTextBoxScrollChanged(object sender, ScrollChangedEventArgs e, Func<WpfTextBox, WpfTextBox?> getCorrespondingLineNumberTextBox)
        {
            try
            {
                if (sender is WpfTextBox textBox)
                {
                    var lineNumberTextBox = getCorrespondingLineNumberTextBox(textBox);
                    if (lineNumberTextBox != null)
                    {
                        // 스크롤 동기화 로직
                        SynchronizeScrollViews(textBox, lineNumberTextBox, e.VerticalOffset);
                    }
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 스크롤 동기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 텍스트박스 텍스트 변경 이벤트 처리
        /// </summary>
        public void HandleTextBoxTextChanged(object sender, Action<string> onTextChanged)
        {
            try
            {
                if (sender is WpfTextBox textBox)
                {
                    string text = textBox.Text ?? "";
                    onTextChanged?.Invoke(text);
                    
                    // 텍스트 길이에 따른 로그
                    if (text.Length > 10000)
                    {
                        _workLogService.AddLog($"대용량 텍스트 감지: {text.Length:N0}자", WorkLogType.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 텍스트 변경 이벤트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion

        #region Public Methods - Utility

        /// <summary>
        /// 텍스트에서 특정 라인으로 이동
        /// </summary>
        public void GoToLineInTextBox(WpfTextBox textBox, int lineNumber)
        {
            try
            {
                if (textBox == null) return;

                var lines = textBox.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                if (lineNumber > 0 && lineNumber <= lines.Length)
                {
                    // 해당 라인의 시작 위치 계산
                    int charIndex = 0;
                    for (int i = 0; i < lineNumber - 1; i++)
                    {
                        charIndex += lines[i].Length + Environment.NewLine.Length;
                    }
                    
                    textBox.Focus();
                    textBox.SelectionStart = charIndex;
                    textBox.SelectionLength = lines[lineNumber - 1].Length;
                    textBox.ScrollToLine(Math.Max(0, lineNumber - 5)); // 5줄 위에서 보이게
                    
                    _workLogService.AddLog($"라인 {lineNumber}로 이동 완료", WorkLogType.Success);
                }
                else
                {
                    _workLogService.AddLog($"잘못된 라인 번호: {lineNumber} (전체: {lines.Length}줄)", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 라인 이동 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 텍스트박스에서 검색
        /// </summary>
        public void FindInTextBox(WpfTextBox textBox, string searchText)
        {
            try
            {
                if (textBox == null || string.IsNullOrEmpty(searchText)) return;

                var content = textBox.Text;
                int index = content.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
                
                if (index >= 0)
                {
                    textBox.Focus();
                    textBox.SelectionStart = index;
                    textBox.SelectionLength = searchText.Length;
                    
                    // 해당 위치로 스크롤
                    int lineNumber = content.Substring(0, index).Split('\n').Length;
                    textBox.ScrollToLine(Math.Max(0, lineNumber - 5));
                    
                    _workLogService.AddLog($"검색 완료: '{searchText}' ({index}번째 문자)", WorkLogType.Success);
                }
                else
                {
                    _workLogService.AddLog($"? 검색 실패: '{searchText}'를 찾을 수 없음", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 텍스트 검색 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 클립보드에 텍스트 복사
        /// </summary>
        public void CopyToClipboard(string text, string description = "")
        {
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    WpfClipboard.SetText(text);
                    string desc = string.IsNullOrEmpty(description) ? "" : $" ({description})";
                    _workLogService.AddLog($"클립보드 복사{desc}: {text.Length:N0}자", WorkLogType.Success);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 클립보드 복사 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 상태 정보 반환
        /// </summary>
        public string GetStatusInfo()
        {
            try
            {
                return $"활성 텍스트박스: {_activeTextBox?.Name ?? "없음"}, 로그 폴더: {_logFolderPath}";
            }
            catch
            {
                return "상태 정보 확인 중...";
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 스크롤뷰 동기화
        /// </summary>
        private void SynchronizeScrollViews(WpfTextBox sourceTextBox, WpfTextBox targetTextBox, double verticalOffset)
        {
            try
            {
                // Prefer line-based synchronization for TextBox to keep exact row alignment
                try
                {
                    // Use TextBox methods to get first visible line index if available
                    int firstVisibleLine = -1;
                    int lastVisibleLine = -1;

                    // GetFirstVisibleLineIndex / GetLastVisibleLineIndex are available on TextBox
                    firstVisibleLine = sourceTextBox.GetFirstVisibleLineIndex();
                    lastVisibleLine = sourceTextBox.GetLastVisibleLineIndex();

                    if (firstVisibleLine >= 0)
                    {
                        // Scroll the target text box to the same first visible line
                        targetTextBox.ScrollToLine(firstVisibleLine);
                        return;
                    }
                }
                catch
                {
                    // If line-based methods fail for any reason, fall back to offset-based sync below
                }

                // ScrollViewer를 찾아 수직 오프셋으로 동기화 (기존 방식)
                var targetScrollViewer = FindScrollViewer(targetTextBox);
                if (targetScrollViewer != null)
                {
                    targetScrollViewer.ScrollToVerticalOffset(verticalOffset);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 스크롤 동기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// ScrollViewer 찾기
        /// </summary>
        private ScrollViewer? FindScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        #endregion
    }
}