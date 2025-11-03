using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows;
using FACTOVA_LogAnalysis.Services;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfColor = System.Windows.Media.Color;

namespace FACTOVA_LogAnalysis.Services
{
    /// <summary>
    /// 텍스트 포맷팅 및 색상 관리를 담당하는 서비스 클래스
    /// 폰트 크기, 색상 변경, 툴바 업데이트 처리
    /// </summary>
    public class TextFormattingManager
    {
        #region Fields

        private readonly WorkLogService _workLogService;
        private double _currentFontSize = 12.0;
        private WpfColor _currentTextColor = Colors.Black;
        
        // UI 요소 참조들
        private WpfComboBox? _textColorComboBox;
        private WpfComboBox? _fontSizeComboBox;

        #endregion

        #region Properties

        public double CurrentFontSize => _currentFontSize;
        public WpfColor CurrentTextColor => _currentTextColor;

        #endregion

        #region Constructor

        public TextFormattingManager(WorkLogService workLogService)
        {
            _workLogService = workLogService ?? throw new ArgumentNullException(nameof(workLogService));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 텍스트 포맷팅 매니저 초기화
        /// </summary>
        public void Initialize(WpfComboBox textColorComboBox, WpfComboBox? fontSizeComboBox)
        {
            _textColorComboBox = textColorComboBox;
            _fontSizeComboBox = fontSizeComboBox;
            
            // 기본값 설정
            SetDefaultValues();
            
            _workLogService.AddLog("? TextFormattingManager 초기화 완료", WorkLogType.Success);
        }

        /// <summary>
        /// 폰트 크기 증가
        /// </summary>
        public void IncreaseFontSize(params WpfRichTextBox[] richTextBoxes)
        {
            try
            {
                _currentFontSize = Math.Min(_currentFontSize + 1, 36); // 최대 36pt
                
                ApplyFontSizeToControls(richTextBoxes);
                UpdateFontSizeComboBox();
                
                _workLogService.AddLog($"폰트 크기 증가: {_currentFontSize}pt", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 폰트 크기 증가 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 폰트 크기 감소
        /// </summary>
        public void DecreaseFontSize(params WpfRichTextBox[] richTextBoxes)
        {
            try
            {
                _currentFontSize = Math.Max(_currentFontSize - 1, 8); // 최소 8pt
                
                ApplyFontSizeToControls(richTextBoxes);
                UpdateFontSizeComboBox();
                
                _workLogService.AddLog($"폰트 크기 감소: {_currentFontSize}pt", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 폰트 크기 감소 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 특정 폰트 크기로 설정
        /// </summary>
        public void SetFontSize(double fontSize, params WpfRichTextBox[] richTextBoxes)
        {
            try
            {
                _currentFontSize = Math.Max(8, Math.Min(fontSize, 36)); // 8~36pt 범위
                
                ApplyFontSizeToControls(richTextBoxes);
                UpdateFontSizeComboBox();
                
                _workLogService.AddLog($"폰트 크기 설정: {_currentFontSize}pt", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 폰트 크기 설정 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 텍스트 색상 변경
        /// </summary>
        public void SetTextColor(WpfColor color, params WpfRichTextBox[] richTextBoxes)
        {
            try
            {
                _currentTextColor = color;
                
                ApplyTextColorToControls(richTextBoxes);
                UpdateTextColorComboBox();
                
                _workLogService.AddLog($"텍스트 색상 변경: {color}", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 텍스트 색상 변경 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 미리 정의된 색상 적용
        /// </summary>
        public void ApplyPredefinedColor(string colorName, params WpfRichTextBox[] richTextBoxes)
        {
            try
            {
                WpfColor color = colorName.ToLower() switch
                {
                    "black" => Colors.Black,
                    "blue" => Colors.Blue,
                    "red" => Colors.Red,
                    "green" => Colors.Green,
                    "purple" => Colors.Purple,
                    "orange" => Colors.Orange,
                    "brown" => Colors.Brown,
                    "gray" => Colors.Gray,
                    _ => Colors.Black
                };
                
                SetTextColor(color, richTextBoxes);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 미리 정의된 색상 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 툴바에서 선택한 내용 업데이트
        /// </summary>
        public void UpdateToolbarFromSelection(WpfRichTextBox richTextBox)
        {
            try
            {
                if (richTextBox.Selection != null && !richTextBox.Selection.IsEmpty)
                {
                    // 선택된 텍스트의 포맷 정보를 툴바에 반영
                    var fontSizeProperty = richTextBox.Selection.GetPropertyValue(TextElement.FontSizeProperty);
                    if (fontSizeProperty != DependencyProperty.UnsetValue)
                    {
                        _currentFontSize = (double)fontSizeProperty;
                        UpdateFontSizeComboBox();
                    }

                    var foregroundProperty = richTextBox.Selection.GetPropertyValue(TextElement.ForegroundProperty);
                    if (foregroundProperty is SolidColorBrush brush)
                    {
                        _currentTextColor = brush.Color;
                        UpdateTextColorComboBox();
                    }
                }
                
                _workLogService.AddLog("툴바 상태 업데이트", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 툴바 업데이트 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 기본값으로 재설정
        /// </summary>
        public void ResetToDefaults(params WpfRichTextBox[] richTextBoxes)
        {
            try
            {
                _currentFontSize = 12.0;
                _currentTextColor = Colors.Black;
                
                ApplyFontSizeToControls(richTextBoxes);
                ApplyTextColorToControls(richTextBoxes);
                UpdateFontSizeComboBox();
                UpdateTextColorComboBox();
                
                _workLogService.AddLog("텍스트 포맷 기본값으로 재설정", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 기본값 재설정 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 기본값 설정
        /// </summary>
        private void SetDefaultValues()
        {
            _currentFontSize = 12.0;
            _currentTextColor = Colors.Black;
        }

        /// <summary>
        /// 폰트 크기를 컨트롤들에 적용
        /// </summary>
        private void ApplyFontSizeToControls(params WpfRichTextBox[] richTextBoxes)
        {
            foreach (var rtb in richTextBoxes)
            {
                if (rtb != null)
                {
                    if (rtb.Selection != null && !rtb.Selection.IsEmpty)
                    {
                        // 선택된 텍스트에만 적용
                        rtb.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, _currentFontSize);
                    }
                    else
                    {
                        // 전체 폰트 크기 변경
                        rtb.FontSize = _currentFontSize;
                    }
                }
            }
        }

        /// <summary>
        /// 텍스트 색상을 컨트롤들에 적용
        /// </summary>
        private void ApplyTextColorToControls(params WpfRichTextBox[] richTextBoxes)
        {
            var brush = new SolidColorBrush(_currentTextColor);
            
            foreach (var rtb in richTextBoxes)
            {
                if (rtb != null)
                {
                    if (rtb.Selection != null && !rtb.Selection.IsEmpty)
                    {
                        // 선택된 텍스트에만 적용
                        rtb.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, brush);
                    }
                    else
                    {
                        // 전체 텍스트 색상 변경
                        rtb.Foreground = brush;
                    }
                }
            }
        }

        /// <summary>
        /// 폰트 크기 콤보박스 업데이트
        /// </summary>
        private void UpdateFontSizeComboBox()
        {
            if (_fontSizeComboBox != null)
            {
                // 현재 폰트 크기에 해당하는 항목 선택
                for (int i = 0; i < _fontSizeComboBox.Items.Count; i++)
                {
                    if (_fontSizeComboBox.Items[i] is ComboBoxItem item && 
                        item.Content?.ToString() == _currentFontSize.ToString())
                    {
                        _fontSizeComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 텍스트 색상 콤보박스 업데이트
        /// </summary>
        private void UpdateTextColorComboBox()
        {
            if (_textColorComboBox != null)
            {
                // 현재 색상에 해당하는 항목 선택 (색상명으로 매칭)
                string colorName = GetColorName(_currentTextColor);
                
                for (int i = 0; i < _textColorComboBox.Items.Count; i++)
                {
                    if (_textColorComboBox.Items[i] is ComboBoxItem item && 
                        item.Content?.ToString()?.ToLower() == colorName.ToLower())
                    {
                        _textColorComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 색상을 색상명으로 변환
        /// </summary>
        private string GetColorName(WpfColor color)
        {
            if (color == Colors.Black) return "Black";
            if (color == Colors.Blue) return "Blue";
            if (color == Colors.Red) return "Red";
            if (color == Colors.Green) return "Green";
            if (color == Colors.Purple) return "Purple";
            if (color == Colors.Orange) return "Orange";
            if (color == Colors.Brown) return "Brown";
            if (color == Colors.Gray) return "Gray";
            
            return "Black"; // 기본값
        }

        #endregion
    }
}