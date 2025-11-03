using System;

namespace FACTOVA_LogAnalysis.Models
{
    // 빨간색 비즈니스 리스트 아이템 클래스
    public class RedBusinessItem
    {
        public int Index { get; set; }
        public string BusinessName { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public string Color { get; set; } = "Red";
    }
}