using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using FACTOVA_LogAnalysis.Services;

// ✅ WPF UserControl 명시
using WpfUserControl = System.Windows.Controls.UserControl;

namespace FACTOVA_LogAnalysis.Views
{
    /// <summary>
    /// SettingsControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SettingsControl : WpfUserControl
    {
        private const string TNS_SETTING_KEY = "TnsFilePath";
        
        public event EventHandler? TnsPathChanged;

        public SettingsControl()
        {
            InitializeComponent();
            LoadSettings();
        }

        /// <summary>
        /// 설정을 로드합니다.
        /// </summary>
        private void LoadSettings()
        {
            try
            {   // 현재 버전 표시
                var currentVersion = GitHubUpdateService.GetCurrentVersion();
                CurrentVersionTextBlock.Text = $"v{currentVersion}";
                
                // 마지막 확인 시간 초기화
                LastCheckTextBlock.Text = "";
                
                // 🔥 Rate Limit 정보 표시
                UpdateRateLimitDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in LoadSettings:");
                System.Diagnostics.Debug.WriteLine($"   {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Rate Limit 정보를 UI에 표시
        /// </summary>
        private void UpdateRateLimitDisplay()
        {
            var rateLimitInfo = GitHubUpdateService.GetRateLimitInfo();
            
            if (rateLimitInfo != null)
            {
                var status = rateLimitInfo.GetStatusText();
                RateLimitTextBlock.Text = $"API 호출 제한: {status}";
                
                // 🔥 남은 횟수에 따라 색상 변경
                if (rateLimitInfo.Remaining <= 5)
                {
                    RateLimitTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(220, 53, 69)); // 빨강
                }
                else if (rateLimitInfo.Remaining <= 15)
                {
                    RateLimitTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(255, 193, 7)); // 노랑
                }
                else
                {
                    RateLimitTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(102, 102, 102)); // 회색
                }
            }
            else
            {
                RateLimitTextBlock.Text = "API 호출 제한: 정보 없음 (아직 API 호출 안 함)";
                RateLimitTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(102, 102, 102));
            }
        }
        
        /// <summary>
        /// 업데이트 상태를 패널에 표시
        /// </summary>
        private void ShowUpdateStatus(string icon, string title, string message, string borderColor, string bgColor)
        {
            UpdateStatusPanel.Visibility = Visibility.Visible;
            UpdateStatusIcon.Text = icon;
            UpdateStatusTitle.Text = title;
            UpdateStatusMessage.Text = message;
            
            UpdateStatusPanel.BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(borderColor));
            UpdateStatusPanel.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bgColor));
        }
        
        /// <summary>
        /// 업데이트 상태 패널 숨기기
        /// </summary>
        private void HideUpdateStatus()
        {
            UpdateStatusPanel.Visibility = Visibility.Collapsed;
        }
        
        /// <summary>
        /// 기본 TNS 파일 경로를 반환합니다.
        /// </summary>
        private string GetDefaultTnsPath()
        {
            string oracleHome = Environment.GetEnvironmentVariable("ORACLE_HOME") ?? "";
            
            if (!string.IsNullOrEmpty(oracleHome))
            {
                string tnsPath = Path.Combine(oracleHome, "network", "admin", "tnsnames.ora");
                if (File.Exists(tnsPath))
                    return tnsPath;
            }

            // 기본 경로들 시도
            string[] defaultPaths = new[]
            {
                @"C:\oracle\product\19c\dbhome_1\network\admin\tnsnames.ora",
                @"C:\oracle\product\18c\dbhome_1\network\admin\tnsnames.ora",
                @"C:\app\oracle\product\19c\dbhome_1\network\admin\tnsnames.ora",
                @"C:\app\oracle\product\18c\dbhome_1\network\admin\tnsnames.ora"
            };

            foreach (var path in defaultPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tnsnames.ora");
        }
        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckUpdateButton.IsEnabled = false;
                CheckUpdateButton.Content = "🔄 확인 중...";
                LastCheckTextBlock.Text = "업데이트 확인 중...";
                HideUpdateStatus(); // 이전 상태 숨기기

                System.Diagnostics.Debug.WriteLine("=== Manual Update Check Started (force refresh) ===");
                
                // 🔥 forceRefresh = true: 캐시 무시하고 강제로 API 호출
                var releaseInfo = await GitHubUpdateService.CheckForUpdatesAsync(forceRefresh: true);
                
                var now = DateTime.Now;
                LastCheckTextBlock.Text = $"마지막 확인: {now:yyyy-MM-dd HH:mm:ss}";

                System.Diagnostics.Debug.WriteLine($"=== Manual Update Check Result ===");
                System.Diagnostics.Debug.WriteLine($"   releaseInfo is null: {releaseInfo == null}");

                if (releaseInfo == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Update check returned null (should not happen)");
                    
                    ShowUpdateStatus(
                        "❌",
                        "업데이트 확인 실패",
                        "업데이트를 확인할 수 없습니다.\n\n" +
                        "가능한 원인:\n" +
                        "• 인터넷 연결 확인\n" +
                        "• 방화벽에서 GitHub API 접근 허용\n" +
                        "• GitHub 서비스 상태 확인",
                        "#DC3545",
                        "#F8D7DA");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"   Current: v{releaseInfo.CurrentVersion}");
                System.Diagnostics.Debug.WriteLine($"   Latest: v{releaseInfo.LatestVersion}");
                System.Diagnostics.Debug.WriteLine($"   HasUpdate: {releaseInfo.HasUpdate}");
                System.Diagnostics.Debug.WriteLine($"   ErrorMessage: {releaseInfo.ErrorMessage ?? "None"}");

                // 🔥 Rate Limit 정보 업데이트
                UpdateRateLimitDisplay();

                // 🔥 에러가 있는 경우
                if (!string.IsNullOrEmpty(releaseInfo.ErrorMessage))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Update check completed with error");
                    
                    ShowUpdateStatus(
                        "⚠️",
                        "업데이트 확인 중 문제 발생",
                        $"현재 버전: v{releaseInfo.CurrentVersion}\n" +
                        $"GitHub 릴리즈: v{releaseInfo.LatestVersion}\n\n" +
                        $"오류:\n{releaseInfo.ErrorMessage}\n\n" +
                        "현재 버전을 계속 사용할 수 있습니다.",
                        "#FFC107",
                        "#FFF3CD");
                    return;
                }

                if (releaseInfo.HasUpdate)
                {
                    System.Diagnostics.Debug.WriteLine("🎉 Update available!");
                    
                    ShowUpdateStatus(
                        "🎉",
                        "새로운 버전이 있습니다!",
                        $"현재 버전: v{releaseInfo.CurrentVersion}\n" +
                        $"최신 버전: v{releaseInfo.LatestVersion}\n" +
                        $"릴리즈 날짜: {releaseInfo.PublishedDate:yyyy-MM-dd}\n\n" +
                        $"아래 버튼을 클릭하여 업데이트하세요.",
                        "#0078D7",
                        "#D1E7FF");
                    
                    // UpdateNotificationWindow 표시
                    var updateWindow = new UpdateNotificationWindow(releaseInfo)
                    {
                        Owner = Window.GetWindow(this)
                    };
                    
                    updateWindow.ShowDialog();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✅ Already up to date");
                    
                    // 🔥 현재 버전이 더 높은지 확인
                    if (releaseInfo.CurrentVersion > releaseInfo.LatestVersion)
                    {
                        ShowUpdateStatus(
                            "💻",
                            "개발 버전 사용 중",
                            $"현재 버전: v{releaseInfo.CurrentVersion}\n" +
                            $"최신 릴리즈: v{releaseInfo.LatestVersion}\n" +
                            $"릴리즈 날짜: {releaseInfo.PublishedDate:yyyy-MM-dd}\n\n" +
                            $"💡 현재 버전이 릴리즈보다 높습니다.",
                            "#17A2B8",
                            "#D1ECF1");
                    }
                    else
                    {
                        ShowUpdateStatus(
                            "✅",
                            "최신 버전입니다",
                            $"현재 버전: v{releaseInfo.CurrentVersion}\n" +
                            $"릴리즈 날짜: {releaseInfo.PublishedDate:yyyy-MM-dd}\n\n" +
                            "최신 버전을 사용 중입니다.",
                            "#28A745",
                            "#D4EDDA");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Exception in CheckUpdateButton_Click:");
                System.Diagnostics.Debug.WriteLine($"   Type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"   Message: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
                
                LastCheckTextBlock.Text = "확인 실패";
                
                ShowUpdateStatus(
                    "❌",
                    "업데이트 확인 오류",
                    $"오류: {ex.GetType().Name}\n" +
                    $"메시지: {ex.Message}\n\n" +
                    "Output 창의 디버그 탭에서 자세한 로그를 확인하세요.",
                    "#DC3545",
                    "#F8D7DA");
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
                CheckUpdateButton.Content = "🔄 지금 업데이트 확인";
            }
        }
    }
}
