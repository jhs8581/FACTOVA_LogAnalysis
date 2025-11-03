using System;
using System.Windows;
using System.Windows.Controls;
using FACTOVA_LogAnalysis.Services;
using WpfButton = System.Windows.Controls.Button;

namespace FACTOVA_LogAnalysis.Services
{
    /// <summary>
    /// UI 레이아웃 관리를 담당하는 서비스 클래스
    /// Compact/Full View 전환 및 레이아웃 상태 관리
    /// </summary>
    public class LayoutManager
    {
        #region Fields

        private readonly WorkLogService _workLogService;
        private bool _isCompactLayout = true;
        
        // UI 요소 참조들
        private RowDefinition? _bottomRowDefinition;
        private WpfButton? _toggleLayoutButton;

        #endregion

        #region Properties

        public bool IsCompactLayout => _isCompactLayout;

        #endregion

        #region Constructor

        public LayoutManager(WorkLogService workLogService)
        {
            _workLogService = workLogService ?? throw new ArgumentNullException(nameof(workLogService));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 레이아웃 매니저 초기화
        /// Accepts nulls so callers that don't have certain UI elements can still initialize the manager.
        /// </summary>
        public void Initialize(RowDefinition? bottomRowDefinition, WpfButton? toggleLayoutButton)
        {
            // Do not throw for null parameters - handle gracefully
            _bottomRowDefinition = bottomRowDefinition;
            _toggleLayoutButton = toggleLayoutButton;

            // 초기 상태를 Compact으로 설정 (methods internally check for nulls)
            SetCompactLayout();

            if (_bottomRowDefinition == null || _toggleLayoutButton == null)
            {
                _workLogService.AddLog("? LayoutManager 초기화: 일부 UI 요소가 없으므로 기본 동작으로 초기화함", WorkLogType.Warning);
            }
            else
            {
                _workLogService.AddLog("? LayoutManager 초기화 완료", WorkLogType.Success);
            }
        }

        /// <summary>
        /// 레이아웃 토글 (Compact ↔ Full)
        /// </summary>
        public void ToggleLayout()
        {
            try
            {
                if (_isCompactLayout)
                {
                    SetFullLayout();
                }
                else
                {
                    SetCompactLayout();
                }
                
                _workLogService.AddLog($"레이아웃 변경: {(_isCompactLayout ? "Compact" : "Full")} 모드", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 레이아웃 토글 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Compact 레이아웃으로 설정
        /// </summary>
        public void SetCompactLayout()
        {
            try
            {
                if (_bottomRowDefinition != null)
                {
                    _bottomRowDefinition.Height = new GridLength(0.3, GridUnitType.Star);
                }
                
                if (_toggleLayoutButton != null)
                {
                    _toggleLayoutButton.Content = "4-Panel View";
                }
                
                _isCompactLayout = true;
                _workLogService.AddLog("Compact 레이아웃 적용", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Compact 레이아웃 설정 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// Full 레이아웃으로 설정
        /// </summary>
        public void SetFullLayout()
        {
            try
            {
                if (_bottomRowDefinition != null)
                {
                    _bottomRowDefinition.Height = new GridLength(1, GridUnitType.Star);
                }
                
                if (_toggleLayoutButton != null)
                {
                    _toggleLayoutButton.Content = "Compact View";
                }
                
                _isCompactLayout = false;
                _workLogService.AddLog("??? Full 레이아웃 적용", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? Full 레이아웃 설정 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 레이아웃 상태 정보 반환
        /// </summary>
        public string GetLayoutStatus()
        {
            return $"현재 레이아웃: {(_isCompactLayout ? "Compact" : "Full")} 모드";
        }

        /// <summary>
        /// 특정 레이아웃으로 강제 설정
        /// </summary>
        public void ForceLayout(bool compact)
        {
            try
            {
                if (compact)
                {
                    SetCompactLayout();
                }
                else
                {
                    SetFullLayout();
                }
                
                _workLogService.AddLog($"강제 레이아웃 설정: {(compact ? "Compact" : "Full")} 모드", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 강제 레이아웃 설정 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion
    }
}