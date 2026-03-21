using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;

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
            // ✅ 전역 예외 처리기 등록 (크래시 방지 및 로깅)
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

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

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException("Unhandled Exception", e.ExceptionObject as Exception);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("Dispatcher Unhandled Exception", e.Exception);
            e.Handled = true; // 앱이 종료되지 않도록 처리
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogException("Unobserved Task Exception", e.Exception);
            e.SetObserved(); // 예외를 관찰된 것으로 표시
        }

        private void LogException(string type, Exception? ex)
        {
            if (ex == null) return;

            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FACTOVA_LogAnalysis",
                    "crash.log"
                );

                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {type}\n" +
                                  $"Message: {ex.Message}\n" +
                                  $"StackTrace: {ex.StackTrace}\n" +
                                  $"InnerException: {ex.InnerException?.Message}\n" +
                                  new string('-', 80) + "\n\n";

                File.AppendAllText(logPath, logMessage);

                // 사용자에게 알림
                System.Windows.MessageBox.Show(
                    $"오류가 발생했습니다:\n\n{ex.Message}\n\n로그 위치: {logPath}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // 로깅 실패해도 앱은 계속 실행
            }
        }
    }
}
