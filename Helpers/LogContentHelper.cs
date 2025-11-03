using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Services;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfColor = System.Windows.Media.Color;
using WpfBrushes = System.Windows.Media.Brushes;

namespace FACTOVA_LogAnalysis.Helpers
{
    /// <summary>
    /// 메인 로그 파일 내용 표시 전용 Helper 클래스 (작업 로그와 완전 분리)
    /// </summary>
    public static class LogContentHelper
    {
        /// <summary>
        /// 메인 로그 파일용 행 번호와 내용을 설정합니다 (파일 로딩 시에만 사용)
        /// </summary>
        public static void SetLogFileContentWithLineNumbers(
            WpfRichTextBox contentBox, 
            WpfTextBox lineNumberBox, 
            string content, 
            RedBusinessListManager redBusinessManager)
        {
            if (string.IsNullOrEmpty(content))
            {
                contentBox.Document.Blocks.Clear();
                lineNumberBox.Text = "";
                System.Diagnostics.Debug.WriteLine("LogContentHelper - 빈 내용으로 초기화");
                return;
            }

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            // 메인 로그 파일용 행 번호 생성 (작업 로그와 독립적)
            GenerateMainLogLineNumbers(lineNumberBox, lines.Length);
            
            // 메인 로그 파일용 내용 설정 (작업 로그와 독립적)
            SetMainLogContent(contentBox, content, redBusinessManager);
            
            System.Diagnostics.Debug.WriteLine($"LogContentHelper - 메인 로그 설정 완료: {lines.Length}줄");
        }

        /// <summary>
        /// 메인 로그 파일용 행 번호만 생성 (작업 로그 격리)
        /// </summary>
        private static void GenerateMainLogLineNumbers(WpfTextBox lineNumberBox, int lineCount)
        {
            try
            {
                var lineNumbers = new StringBuilder();
                for (int i = 1; i <= lineCount; i++)
                {
                    lineNumbers.AppendLine(i.ToString());
                }
                
                // 마지막 빈 줄 제거
                string lineNumberText = lineNumbers.ToString().TrimEnd('\r', '\n');
                lineNumberBox.Text = lineNumberText;
                
                System.Diagnostics.Debug.WriteLine($"LogContentHelper - 메인 로그 행 번호 생성: 1-{lineCount}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogContentHelper - 행 번호 생성 오류: {ex.Message}");
                // 오류 시 기본 행 번호라도 설정
                lineNumberBox.Text = "1";
            }
        }

        /// <summary>
        /// 메인 로그 파일용 내용 설정 (작업 로그 격리)
        /// </summary>
        private static void SetMainLogContent(WpfRichTextBox contentBox, string content, RedBusinessListManager redBusinessManager)
        {
            try
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                
                contentBox.Document.Blocks.Clear();
                
                // 빨간색 비즈니스 리스트에서 활성화된 항목들 가져오기
                var redBusinessNames = redBusinessManager.GetEnabledBusinessNames();
                
                // 각 줄을 개별 Paragraph로 처리하는 대신, 하나의 Paragraph에 Run과 LineBreak 조합으로 처리
                var paragraph = new Paragraph();
                paragraph.Margin = new Thickness(0); // 여백 제거
                paragraph.LineHeight = 1.0; // 줄 간격 설정
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    
                    // 빈 줄도 처리 (공백 Run 추가)
                    if (string.IsNullOrEmpty(line))
                    {
                        var emptyRun = new Run(" "); // 공백 문자 추가하여 줄 유지
                        paragraph.Inlines.Add(emptyRun);
                    }
                    else
                    {
                        var run = new Run(line);
                        
                        // 색상 및 스타일 적용
                        ApplyLogLineFormatting(run, line, redBusinessNames, contentBox.Name);
                        
                        paragraph.Inlines.Add(run);
                    }
                    
                    // 마지막 줄이 아니면 LineBreak 추가
                    if (i < lines.Length - 1)
                    {
                        paragraph.Inlines.Add(new LineBreak());
                    }
                }
                
                contentBox.Document.Blocks.Add(paragraph);
                System.Diagnostics.Debug.WriteLine($"LogContentHelper - 메인 로그 내용 설정 완료: {lines.Length}줄");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogContentHelper - 내용 설정 오류: {ex.Message}");
                
                // 오류 시 기본 내용이라도 표시
                contentBox.Document.Blocks.Clear();
                var errorParagraph = new Paragraph();
                errorParagraph.Inlines.Add(new Run($"내용 표시 중 오류 발생: {ex.Message}")
                {
                    Foreground = WpfBrushes.Red
                });
                contentBox.Document.Blocks.Add(errorParagraph);
            }
        }

        /// <summary>
        /// 로그 라인별 색상 및 스타일 적용
        /// </summary>
        private static void ApplyLogLineFormatting(Run run, string line, System.Collections.Generic.List<string> redBusinessNames, string? containerName)
        {
            try
            {
                // 빨간색 비즈니스 리스트 항목들 체크 (우선순위 최상위)
                bool isRedBusiness = redBusinessNames.Any(businessName => 
                    line.Contains(businessName, StringComparison.OrdinalIgnoreCase));
                
                if (isRedBusiness)
                {
                    run.Foreground = WpfBrushes.Red;
                    run.FontWeight = FontWeights.Bold;
                }
                // 기존 특정 비즈니스명 빨간색으로 표시 (BR_SFC_RegisterStartEndJobBuffer, BR_SFC_CheckStartLotUI) - 이제 위에서 처리됨
                else if (line.Contains("BR_SFC_RegisterStartEndJobBuffer") || line.Contains("BR_SFC_CheckStartLotUI"))
                {
                    run.Foreground = WpfBrushes.Red;
                    run.FontWeight = FontWeights.Bold;
                }
                // EXCEPTION이 포함된 라인을 빨간색 (ERROR도 포함)
                else if (line.ToUpper().Contains("EXCEPTION"))
                {
                    run.Foreground = WpfBrushes.Red;
                    run.FontWeight = FontWeights.Bold;
                }
                // NOREAD 포함된 라인 빨간색으로 표시 (단, noread_flag는 제외)
                else if (line.ToUpper().Contains("NOREAD") && !line.ToUpper().Contains("NOREAD_FLAG"))
                {
                    // DATA 로그에서만 NOREAD 빨간색 처리 제외 (containerName으로 판단)
                    if (containerName != "dataLogTextBox")
                    {
                        run.Foreground = WpfBrushes.Red;
                        run.FontWeight = FontWeights.Bold;
                    }
                }
                // 비즈니스가 있는 초록색으로표시 (ExecuteService():[ 비즈니스명 ] 패턴)
                else if (line.Contains("ExecuteService():[") && line.Contains("]"))
                {
                    run.Foreground = WpfBrushes.Green;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogContentHelper - 포맷팅 적용 오류: {ex.Message}");
                // 오류 시 기본 스타일 유지
            }
        }

        /// <summary>
        /// 간단한 텍스트 내용 설정 (에러 메시지 등)
        /// </summary>
        public static void SetSimpleLogContent(WpfRichTextBox contentBox, WpfTextBox lineNumberBox, string content, WpfColor color)
        {
            try
            {
                contentBox.Document.Blocks.Clear();
                
                if (string.IsNullOrEmpty(content))
                {
                    lineNumberBox.Text = "";
                    return;
                }

                // 줄 수에 맞게 행 번호 생성
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                GenerateMainLogLineNumbers(lineNumberBox, lines.Length);
                
                var paragraph = new Paragraph();
                paragraph.Margin = new Thickness(0);
                var run = new Run(content)
                {
                    Foreground = new SolidColorBrush(color)
                };
                paragraph.Inlines.Add(run);
                contentBox.Document.Blocks.Add(paragraph);
                
                System.Diagnostics.Debug.WriteLine($"LogContentHelper - 간단한 내용 설정 완료: {lines.Length}줄");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogContentHelper - 간단한 내용 설정 오류: {ex.Message}");
            }
        }
    }
}