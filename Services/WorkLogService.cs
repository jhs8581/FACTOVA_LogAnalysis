using System;

namespace FACTOVA_LogAnalysis.Services
{
    public enum WorkLogType
    {
        Info,
        Success,
        Warning,
        Error,
        Debug
    }

    /// <summary>
    /// 간소화된 WorkLogService - Debug 출력 전용
    /// (WorkLog UI 탭 제거됨, Debug 출력만 유지)
    /// </summary>
    public class WorkLogService
    {
        public void AddLog(string message, WorkLogType logType = WorkLogType.Info)
        {
            try
            {
                string prefix = PrefixForLogType(logType);
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logMessage = $"[{timestamp}] {prefix} {message}";
                
                // ✅ Debug 출력 (개발/디버깅용)
                System.Diagnostics.Debug.WriteLine(logMessage);
                
                // ✅ 콘솔 출력 (릴리즈 빌드에서도 확인 가능)
                Console.WriteLine(logMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkLogService] Error: {ex.Message}");
            }
        }

        private static string PrefixForLogType(WorkLogType type)
        {
            return type switch
            {
                WorkLogType.Info => "[INFO]",
                WorkLogType.Success => "[✓ OK]",
                WorkLogType.Warning => "[⚠ WARN]",
                WorkLogType.Error => "[✗ ERROR]",
                WorkLogType.Debug => "[DEBUG]",
                _ => string.Empty
            };
        }
    }
}
