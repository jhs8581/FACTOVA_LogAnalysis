using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace FACTOVA_LogAnalysis
{
    public partial class FindDialog : Window
    {
        private readonly MainWindow _mainWindow;
        private object _searchTarget; // RichTextBox 또는 TextBox
        private bool _isTextBoxMode;

        // 생성자 통합
        public FindDialog(MainWindow owner, object target, string selectedText = "")
        {
            InitializeComponent();
            Owner = owner;
            _mainWindow = owner;
            _searchTarget = target;
            _isTextBoxMode = target is System.Windows.Controls.TextBox;

            this.Title = "Find"; // 기본 타이틀

            if (!string.IsNullOrEmpty(selectedText))
            {
                searchTextBox.Text = selectedText;
                searchTextBox.SelectAll();
            }
            else if (!string.IsNullOrEmpty(owner.LastSearchText))
            {
                searchTextBox.Text = owner.LastSearchText;
                searchTextBox.SelectAll();
            }
        }

        public void SetSelectedText(string selectedText)
        {
            if (!string.IsNullOrEmpty(selectedText))
            {
                searchTextBox.Text = selectedText;
                searchTextBox.SelectAll();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            searchTextBox.Focus();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _mainWindow.FindDialogClosed();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 텍스트 변경 시 아무것도 하지 않음. 'Find' 버튼을 눌러야 검색 시작.
        }

        private void SearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                FindButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void CaseCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // 옵션 변경 시 아무것도 하지 않음.
        }

        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            string searchText = searchTextBox.Text;
            bool caseSensitive = caseCheckBox.IsChecked == true;

            if (string.IsNullOrEmpty(searchText))
            {
                return;
            }

            _mainWindow.LastSearchText = searchText;
            _mainWindow.FindAndHighlightAll(searchText, caseSensitive, _searchTarget);

            // keep dialog focused after search
            try
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    try { Activate(); searchTextBox.Focus(); } catch { }
                }), DispatcherPriority.Input);
            }
            catch { }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.HighlightNext();

            // restore focus to the Next button so it stays selected when clicked
            try
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    try { Activate(); nextButton.Focus(); } catch { }
                }), DispatcherPriority.Input);
            }
            catch { }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.HighlightPrevious();

            // restore focus to the Prev button so it stays selected when clicked
            try
            {
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    try { Activate(); prevButton.Focus(); } catch { }
                }), DispatcherPriority.Input);
            }
            catch { }
        }

        public void UpdateStatus(int current, int total)
        {
            if (total > 0)
            {
                statusLabel.Content = $"{current + 1}/{total}";
            }
            else
            {
                statusLabel.Content = "0/0";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
