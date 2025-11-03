using System;
using System.Diagnostics;
using System.Windows;
using FACTOVA_LogAnalysis.Services;

// ✅ WPF 전용 타입 명시
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;

namespace FACTOVA_LogAnalysis.Views
{
    /// <summary>
    /// UpdateNotificationWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class UpdateNotificationWindow : Window
    {
        private readonly ReleaseInfo _releaseInfo;

        public UpdateNotificationWindow(ReleaseInfo releaseInfo)
        {
            InitializeComponent();
            _releaseInfo = releaseInfo;
            InitializeContent();
        }

        private void InitializeContent()
        {
            // 버전 정보 표시
            VersionInfoTextBlock.Text = $"현재: v{_releaseInfo.CurrentVersion} → 최신: v{_releaseInfo.LatestVersion}";

            // 릴리즈 노트 표시
            if (!string.IsNullOrWhiteSpace(_releaseInfo.ReleaseNotes))
            {
                ReleaseNotesTextBlock.Text = _releaseInfo.ReleaseNotes;
            }
            else
            {
                ReleaseNotesTextBlock.Text = "새로운 버전이 출시되었습니다.";
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DownloadButton.IsEnabled = false;
                DownloadButton.Content = "⏳ 다운로드 중...";

                if (string.IsNullOrEmpty(_releaseInfo.FileName))
                {
                    // 파일이 없으면 GitHub 페이지 열기
                    GitHubUpdateService.OpenReleasePage(_releaseInfo.ReleaseUrl);
                    
                    WpfMessageBox.Show(
                        "다운로드 파일을 찾을 수 없습니다.\n브라우저에서 수동으로 다운로드해주세요.",
                        "다운로드",
                        WpfMessageBoxButton.OK,
                        WpfMessageBoxImage.Information);
                    
                    DialogResult = true;
                    Close();
                    return;
                }

                // 진행 상황 표시
                var progress = new Progress<int>(percent =>
                {
                    DownloadButton.Content = $"⏳ 다운로드 중... {percent}%";
                });

                var success = await GitHubUpdateService.DownloadAndInstallUpdateAsync(
                    _releaseInfo.DownloadUrl,
                    _releaseInfo.FileName,
                    progress);

                if (success)
                {
                    DialogResult = true;
                    Close();
                }
                else
                {
                    DownloadButton.IsEnabled = true;
                    DownloadButton.Content = "⬇️ 다운로드";
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"다운로드 페이지를 열 수 없습니다:\n{ex.Message}", 
                    "오류", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
                
                DownloadButton.IsEnabled = true;
                DownloadButton.Content = "⬇️ 다운로드";
            }
        }

        private void LaterButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
