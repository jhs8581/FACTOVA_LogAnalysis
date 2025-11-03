using System.Configuration;
using System.Data;
using System.Text;
using System.Windows;

namespace FACTOVA_LogAnalysis
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
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

            base.OnStartup(e);
        }
    }
}
