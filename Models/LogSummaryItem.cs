using System;

namespace FACTOVA_LogAnalysis.Models
{
    // 로그 요약 데이터 클래스
    public class LogSummaryItem
    {
        public string Timestamp { get; set; } = "";
        public string BusinessName { get; set; } = "";
        public string ExecTime { get; set; } = "";
        public string SearchKeyword { get; set; } = ""; // DATA 로그에서 찾을 키워드
        public int LineNumber { get; set; } = 0; // DATA 로그에서의 줄 번호
    }
}