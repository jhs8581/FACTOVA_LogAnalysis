using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfMedia = System.Windows.Media;

namespace FACTOVA_LogAnalysis.Helpers
{
    /// <summary>
    /// FACTOVA Log Analysis 프로그램 도움말 다이얼로그
    /// </summary>
    public class HelpDialog : Window
    {
        public HelpDialog()
        {
            Title = "FACTOVA Log Analysis - 사용 도움말";
            Width = 800;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize; // 크기 조절 비활성화
            WindowStyle = WindowStyle.SingleBorderWindow; // 기본 윈도우 스타일 유지하되
            
            // 스크롤 가능한 RichTextBox 생성
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10)
            };
            
            var richTextBox = new System.Windows.Controls.RichTextBox
            {
                IsReadOnly = true,
                FontSize = 14,
                FontFamily = new WpfMedia.FontFamily("맑은 고딕"),
                Background = WpfMedia.Brushes.WhiteSmoke
            };
            
            // 도움말 내용 추가
            AddHelpContent(richTextBox);
            
            scrollViewer.Content = richTextBox;
            Content = scrollViewer;
        }
        
        private void AddHelpContent(System.Windows.Controls.RichTextBox rtb)
        {
            var doc = new FlowDocument();
            
            // 제목
            var titlePara = new Paragraph(new Run("\U0001F4DA FACTOVA Log Analysis 사용 가이드"))
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = WpfMedia.Brushes.DarkBlue,
                TextAlignment = TextAlignment.Center
            };
            doc.Blocks.Add(titlePara);
            
            doc.Blocks.Add(new Paragraph(new Run("===========================================================================")));
            
            // 1. 프로그램 개요
            AddSection(doc, "\U0001F4CC 프로그램 개요", 
                "FACTOVA Log Analysis는 SFC (Shop Floor Control) 시스템의 로그를 분석하는 도구입니다.\n" +
                "DATA, EVENT, DEBUG, EXCEPTION 4가지 로그 타입을 지원하며,\n" +
                "날짜/시간 범위 필터링, 검색어 필터링, 실행시간 분석 등의 기능을 제공합니다.");
            
            // 2. 폴더 설정
            AddSection(doc, "\U0001F4C1 폴더 설정", 
                "- 로그 폴더 경로: SFC 로그 파일이 있는 폴더를 지정합니다\n" +
                "- 폴더 열기: 현재 설정된 로그 폴더를 탐색기로 엽니다\n" +
                "- 폴더 변경: 다른 로그 폴더를 선택합니다\n" +
                "- 폴더 경로 리셋: 기본 로그 폴더로 되돌립니다\n" +
                "- Notepad 열기: 메모장으로 로그 폴더의 파일을 엽니다\n" +
                "- VS Code 열기: VS Code로 로그 폴더를 엽니다");
            
            // 3. 조회 조건
            AddSection(doc, "\U0001F50D 조회 조건", 
                "- 날짜 선택: 조회할 로그 날짜를 선택합니다\n" +
                "- 시간 범위: 조회할 시간 범위를 설정합니다 (예: 09:00 ~ 18:00)\n" +
                "  * 시간 프리셋: 종일, 오전, 오후 등 미리 정의된 시간 범위\n" +
                "  * 직접 입력: HH:MM 형식으로 시작/종료 시간 입력\n" +
                "- 검색어: 특정 문자열이 포함된 로그만 필터링\n" +
                "- 검색 범위: 로그 시작/끝/전체 중 검색 범위 선택\n" +
                "- 로드 옵션:\n" +
                "  * Text: 텍스트 형식으로 로그 로드\n" +
                "  * DataGrid: 표 형식으로 로그 로드");
            
            // 4. SFC Log (Text) 탭
            AddSection(doc, "\U0001F4C4 SFC Log (Text) 탭", 
                "텍스트 형식으로 로그를 표시합니다.\n\n" +
                "- 4분할/탭 전환: DATA, EVENT, DEBUG, EXCEPTION을 4분할 또는 탭으로 표시\n" +
                "- 폰트 크기 조절: 텍스트 크기를 증가/감소시킵니다\n" +
                "- 찾기 (Ctrl+F): 텍스트 내에서 검색어를 찾습니다\n" +
                "- 선택 복사 (Ctrl+C): 선택한 텍스트를 복사합니다");
            
            // 5. SFC Log (DataGrid) 탭
            AddSection(doc, "\U0001F4CA SFC Log (DataGrid) 탭", 
                "표 형식으로 로그를 표시합니다.\n\n" +
                "- 4분할/탭 전환: DATA, EVENT, DEBUG, EXCEPTION을 4분할 또는 탭으로 표시\n" +
                "- DataGrid 폰트 조절: 표의 글자 크기 조정\n" +
                "- 동일 시간 이동: 선택한 행과 같은 시간대의 다른 로그로 이동\n" +
                "- 콘텐츠 열기/닫기: Content 열을 펼치거나 접습니다\n" +
                "- 필터:\n" +
                "  * Business 필터: 비즈니스명으로 필터링\n" +
                "  * MsgId 필터: 메시지 ID로 필터링\n" +
                "- 내보내기: 현재 DataGrid를 Excel(xlsx) 파일로 저장");
            
            // 6. exec.Time Analysis 탭
            AddSection(doc, "\U000023F1 exec.Time Analysis 탭", 
                "실행 시간 분석 기능입니다.\n\n" +
                "- 임계값 설정: 특정 시간(초) 이상 걸린 작업만 표시\n" +
                "- 필터 적용: 설정한 임계값으로 필터링\n" +
                "- 필터 초기화: 필터를 제거하고 전체 데이터 표시");
            
            // 7. WorkLog 탭
            AddSection(doc, "\U0001F4DD WorkLog 탭", 
                "프로그램의 작업 로그를 표시합니다.\n\n" +
                "- 자동 스크롤: 새 로그가 추가될 때 자동으로 스크롤\n" +
                "- 로그 초기화: WorkLog 내용을 모두 삭제\n" +
                "- 로그 저장: WorkLog를 텍스트 파일로 저장");
            
            // 8. 단축키
            AddSection(doc, "\U00002328 단축키", 
                "- Ctrl + F: 텍스트 찾기\n" +
                "- Ctrl + C: 선택 복사\n" +
                "- F3: 다음 찾기\n" +
                "- Shift + F3: 이전 찾기");
            
            // 9. 주의사항
            AddSection(doc, "\U000026A0 주의사항", 
                "- 로그 파일 형식: 'LGE GMES_{타입}_{날짜}.log' 형식이어야 합니다\n" +
                "- 시간 형식: 로그 내 타임스탬프는 [dd-MM-yyyy HH:mm:ss] 형식이어야 합니다\n" +
                "- 대용량 파일: 매우 큰 로그 파일은 로딩 시간이 오래 걸릴 수 있습니다\n" +
                "- DataGrid 모드: 대용량 파일의 경우 Text 모드보다 메모리를 많이 사용합니다");
            
            // 푸터
            doc.Blocks.Add(new Paragraph(new Run("\n" + "===========================================================================")));
            var footerPara = new Paragraph(new Run("\U000000A9 2025 FACTOVA Log Analysis Tool"))
            {
                FontSize = 12,
                Foreground = WpfMedia.Brushes.Gray,
                TextAlignment = TextAlignment.Center
            };
            doc.Blocks.Add(footerPara);
            
            rtb.Document = doc;
        }
        
        private void AddSection(FlowDocument doc, string title, string content)
        {
            // 섹션 제목
            var titlePara = new Paragraph(new Run(title))
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = WpfMedia.Brushes.DarkGreen,
                Margin = new Thickness(0, 15, 0, 5)
            };
            doc.Blocks.Add(titlePara);
            
            // 섹션 내용
            var contentPara = new Paragraph(new Run(content))
            {
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10),
                LineHeight = 22
            };
            doc.Blocks.Add(contentPara);
        }
    }
}
