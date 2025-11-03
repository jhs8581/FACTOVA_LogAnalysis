using System.ComponentModel;

namespace FACTOVA_LogAnalysis.Models
{
    /// <summary>
    /// 멀티선택 필터 아이템을 위한 클래스
    /// 콤보박스의 체크박스 아이템을 나타냅니다.
    /// </summary>
    public class FilterItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// 필터 아이템의 값 (비즈니스명, MsgId 등)
        /// </summary>
        public string Value { get; set; } = "";

        /// <summary>
        /// 선택된 상태 여부
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        /// <summary>
        /// FilterItem 생성자
        /// </summary>
        /// <param name="value">필터 값</param>
        public FilterItem(string value)
        {
            Value = value ?? "";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return Value;
        }
    }
}