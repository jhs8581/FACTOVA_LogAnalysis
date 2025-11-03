using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Services;

namespace FACTOVA_LogAnalysis.Helpers
{
    public static class UIHelpers
    {
        public static void SetRichTextContentWithLineNumbers(System.Windows.Controls.RichTextBox contentBox, System.Windows.Controls.TextBox lineNumberBox, 
            string content, RedBusinessListManager redBusinessManager)
        {
            if (string.IsNullOrEmpty(content))
            {
                contentBox.Document.Blocks.Clear();
                lineNumberBox.Text = "";
                return;
            }

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
            
            var lineNumbers = new StringBuilder();
            for (int i = 1; i <= lines.Length; i++)
            {
                lineNumbers.AppendLine(i.ToString());
            }
            lineNumberBox.Text = lineNumbers.ToString();

            contentBox.Document.Blocks.Clear();
            var paragraph = new Paragraph();
            
            // 빨간색 비즈니스 리스트에서 활성화된 항목들 가져오기
            var redBusinessNames = redBusinessManager.GetEnabledBusinessNames();
            
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    paragraph.Inlines.Add(new LineBreak());
                    continue;
                }

                var run = new Run(line);
                
                // 빨간색 비즈니스 리스트 항목들 체크 (우선순위 최상위)
                bool isRedBusiness = redBusinessNames.Any(businessName => 
                    line.Contains(businessName, System.StringComparison.OrdinalIgnoreCase));
                
                if (isRedBusiness)
                {
                    run.Foreground = System.Windows.Media.Brushes.Red;
                    run.FontWeight = FontWeights.Bold;
                }
                // 기존 특정 비즈니스명 빨간색으로 표시 (BR_SFC_RegisterStartEndJobBuffer, BR_SFC_CheckStartLotUI) - 이제 위에서 처리됨
                else if (line.Contains("BR_SFC_RegisterStartEndJobBuffer") || line.Contains("BR_SFC_CheckStartLotUI"))
                {
                    run.Foreground = System.Windows.Media.Brushes.Red;
                    run.FontWeight = FontWeights.Bold;
                }
                // EXCEPTION이 포함된 라인을 빨간색 (ERROR도 포함)
                else if (line.ToUpper().Contains("EXCEPTION"))
                {
                    run.Foreground = System.Windows.Media.Brushes.Red;
                    run.FontWeight = FontWeights.Bold;
                }
                // NOREAD 포함된 라인 빨간색으로 표시 (단, noread_flag는 제외)
                else if (line.ToUpper().Contains("NOREAD") && !line.ToUpper().Contains("NOREAD_FLAG"))
                {
                    // DATA 로그에서만 NOREAD 빨간색 처리 제외 (contentBox 이름으로 판단)
                    if (contentBox.Name != "dataLogTextBox")
                    {
                        run.Foreground = System.Windows.Media.Brushes.Red;
                        run.FontWeight = FontWeights.Bold;
                    }
                }
                // 비즈니스가 있는 초록색으로표시 (ExecuteService():[ 비즈니스명 ] 패턴)
                else if (line.Contains("ExecuteService():[") && line.Contains("]"))
                {
                    run.Foreground = System.Windows.Media.Brushes.Green;
                }
                
                paragraph.Inlines.Add(run);
                paragraph.Inlines.Add(new LineBreak());
            }
            
            contentBox.Document.Blocks.Add(paragraph);
        }

        public static void SetRichTextContent(System.Windows.Controls.RichTextBox contentBox, System.Windows.Controls.TextBox lineNumberBox, string content, System.Windows.Media.Color color)
        {
            contentBox.Document.Blocks.Clear();
            lineNumberBox.Text = "1";
            
            var paragraph = new Paragraph();
            var run = new Run(content)
            {
                Foreground = new SolidColorBrush(color)
            };
            paragraph.Inlines.Add(run);
            contentBox.Document.Blocks.Add(paragraph);
        }

        public static void ApplyTextColor(System.Windows.Controls.RichTextBox? activeTextBox, System.Windows.Media.Color color)
        {
            if (activeTextBox?.Selection != null && !activeTextBox.Selection.IsEmpty)
            {
                activeTextBox.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));
            }
        }

        public static void ApplyFontSize(System.Windows.Controls.RichTextBox? activeTextBox, System.Windows.Controls.TextBox fontSizeTextBox, double fontSize)
        {
            if (activeTextBox != null)
            {
                // 전체 RichTextBox의 폰트 크기 변경 (선택 영역 무시)
                activeTextBox.FontSize = fontSize;
                fontSizeTextBox.Text = fontSize.ToString("F0");
            }
        }

        public static System.Windows.Media.Color GetColorFromName(string colorName)
        {
            return colorName switch
            {
                "Black" => Colors.Black,
                "Red" => Colors.Red,
                "Green" => Colors.Green,
                "Blue" => Colors.Blue,
                "Orange" => Colors.Orange,
                "Purple" => Colors.Purple,
                "Brown" => Colors.Brown,
                "Gray" => Colors.Gray,
                _ => Colors.Black
            };
        }
    }
}