using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace FACTOVA_LogAnalysis.Services
{
    public class SearchService
    {
        private List<TextRange> _matchRanges = new List<TextRange>();
        private List<int> _textBoxMatches = new List<int>();
        private int _currentMatchIndex = -1;

        public event Action<int, int>? StatusUpdated;

        public void FindAndHighlightAll(string searchText, bool caseSensitive, object target)
        {
            _matchRanges.Clear();
            _textBoxMatches.Clear();
            _currentMatchIndex = -1;

            if (string.IsNullOrEmpty(searchText))
            {
                StatusUpdated?.Invoke(0, 0);
                return;
            }

            if (target is System.Windows.Controls.RichTextBox rtb)
            {
                FindInRichTextBox(rtb, searchText, caseSensitive);
            }
            else if (target is System.Windows.Controls.TextBox tb)
            {
                FindInTextBox(tb, searchText, caseSensitive);
            }

            int total = _matchRanges.Count + _textBoxMatches.Count;
            if (total > 0)
            {
                _currentMatchIndex = 0;
                HighlightCurrent(target, searchText);
            }
            
            StatusUpdated?.Invoke(_currentMatchIndex, total);
        }

        private void FindInRichTextBox(System.Windows.Controls.RichTextBox rtb, string searchText, bool caseSensitive)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            TextPointer current = rtb.Document.ContentStart;

            while (current != null && current.CompareTo(rtb.Document.ContentEnd) < 0)
            {
                if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = current.GetTextInRun(LogicalDirection.Forward);
                    int index = 0;
                    while (index < textRun.Length)
                    {
                        int foundIndex = textRun.IndexOf(searchText, index, comparison);
                        if (foundIndex == -1) break;

                        TextPointer start = current.GetPositionAtOffset(foundIndex);
                        TextPointer end = start.GetPositionAtOffset(searchText.Length);
                        _matchRanges.Add(new TextRange(start, end));

                        index = foundIndex + 1;
                    }
                }
                current = current.GetNextContextPosition(LogicalDirection.Forward);
            }
        }

        private void FindInTextBox(System.Windows.Controls.TextBox tb, string searchText, bool caseSensitive)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            string content = tb.Text;
            int index = 0;
            while (index < content.Length)
            {
                int foundIndex = content.IndexOf(searchText, index, comparison);
                if (foundIndex == -1) break;

                _textBoxMatches.Add(foundIndex);
                index = foundIndex + 1;
            }
        }

        public void HighlightNext(object target, string searchText)
        {
            int total = _matchRanges.Count + _textBoxMatches.Count;
            if (total == 0) return;

            _currentMatchIndex = (_currentMatchIndex + 1) % total;
            HighlightCurrent(target, searchText);
            StatusUpdated?.Invoke(_currentMatchIndex, total);
        }

        public void HighlightPrevious(object target, string searchText)
        {
            int total = _matchRanges.Count + _textBoxMatches.Count;
            if (total == 0) return;

            _currentMatchIndex--;
            if (_currentMatchIndex < 0)
            {
                _currentMatchIndex = total - 1;
            }
            HighlightCurrent(target, searchText);
            StatusUpdated?.Invoke(_currentMatchIndex, total);
        }

        private void HighlightCurrent(object target, string searchText)
        {
            if (_currentMatchIndex < 0) return;

            if (target is System.Windows.Controls.RichTextBox rtb && _matchRanges.Any())
            {
                var range = _matchRanges[_currentMatchIndex];
                rtb.Focus();
                rtb.Selection.Select(range.Start, range.End);
                rtb.BringIntoView(range.Start.GetCharacterRect(LogicalDirection.Forward));
            }
            else if (target is System.Windows.Controls.TextBox tb && _textBoxMatches.Any())
            {
                int start = _textBoxMatches[_currentMatchIndex];
                tb.Focus();
                tb.Select(start, searchText.Length);
                tb.ScrollToLine(tb.GetLineIndexFromCharacterIndex(start));
            }
        }

        public void ClearSearch()
        {
            _matchRanges.Clear();
            _textBoxMatches.Clear();
            _currentMatchIndex = -1;
        }
    }
}