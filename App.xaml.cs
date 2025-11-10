using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;
using System.Runtime.InteropServices;

namespace FACTOVA_LogAnalysis
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        // ✅ 작업 표시줄 고정을 위한 AppUserModelID 설정
        [DllImport("shell32.dll", SetLastError = true)]
        static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        protected override void OnStartup(StartupEventArgs e)
        {
            // ✅ 전역 인코딩 설정: UTF-8을 기본으로 설정
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            // ✅ 콘솔 출력도 UTF-8로 설정 (디버그 로그용)
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
            }
            catch
            {
                // 콘솔이 없는 환경에서는 무시
            }

            // ✅ 작업 표시줄 고정을 위한 AppUserModelID 설정
            try
            {
                SetCurrentProcessExplicitAppUserModelID("FACTOVA.LogAnalysis.1.0");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppUserModelID 설정 실패: {ex.Message}");
            }

            base.OnStartup(e);
        }
    }
}
