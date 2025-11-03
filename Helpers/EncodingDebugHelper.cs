using System;
using System.IO;
using System.Text;

namespace FACTOVA_LogAnalysis.Helpers
{
    /// <summary>
    /// 인코딩 테스트 및 디버그 유틸리티
    /// </summary>
    public static class EncodingDebugHelper
    {
        /// <summary>
        /// 파일의 인코딩 정보를 상세히 분석
        /// </summary>
        public static void AnalyzeFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                System.Windows.MessageBox.Show($"파일을 찾을 수 없습니다: {filePath}", "오류", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            var bytes = File.ReadAllBytes(filePath);
            var fileName = Path.GetFileName(filePath);
            var fileSize = bytes.Length;

            var sb = new StringBuilder();
            sb.AppendLine($"═══════════════════════════════");
            sb.AppendLine($"📄 파일 분석 결과");
            sb.AppendLine($"═══════════════════════════════");
            sb.AppendLine($"파일명: {fileName}")
                .AppendLine($"크기: {fileSize:N0} bytes")
                .AppendLine();

            // BOM 확인
            sb.AppendLine($"🔍 BOM 분석:");
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                sb.AppendLine($"   ✅ UTF-8 BOM 발견");
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                sb.AppendLine($"   ✅ UTF-16 LE BOM 발견");
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                sb.AppendLine($"   ✅ UTF-16 BE BOM 발견");
            }
            else
            {
                sb.AppendLine($"   ⚠️ BOM 없음");
            }
            sb.AppendLine();

            // 처음 100 바이트 hex 덤프
            sb.AppendLine($"📊 처음 100 바이트 (Hex):");
            int dumpSize = Math.Min(100, bytes.Length);
            for (int i = 0; i < dumpSize; i += 16)
            {
                sb.Append($"   {i:X4}: ");
                for (int j = 0; j < 16 && (i + j) < dumpSize; j++)
                {
                    sb.Append($"{bytes[i + j]:X2} ");
                }
                sb.AppendLine();
            }
            sb.AppendLine();

            // 여러 인코딩으로 읽기 테스트
            sb.AppendLine($"🔤 인코딩별 첫 100자:");
            sb.AppendLine();

            var encodings = new[]
            {
                Encoding.UTF8,
                Encoding.GetEncoding("EUC-KR"),
                Encoding.GetEncoding(949), // CP949
                Encoding.Default,
            };

            foreach (var encoding in encodings)
            {
                try
                {
                    var text = encoding.GetString(bytes);
                    var sample = text.Length > 100 ? text.Substring(0, 100) : text;
                    sb.AppendLine($"[{encoding.EncodingName} - CP:{encoding.CodePage}]")
                        .AppendLine($"{sample}")
                        .AppendLine($"깨진 문자 수: {CountBadChars(sample)}")
                        .AppendLine();
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[{encoding.EncodingName}] ❌ 오류: {ex.Message}")
                        .AppendLine();
                }
            }

            // 결과 표시
            System.Windows.MessageBox.Show(sb.ToString(), "인코딩 분석 결과", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            
            // 클립보드에 복사
            System.Windows.Clipboard.SetText(sb.ToString());
            System.Windows.MessageBox.Show("분석 결과가 클립보드에 복사되었습니다.", "알림", 
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private static int CountBadChars(string text)
        {
            int count = 0;
            foreach (char c in text)
            {
                if (c == '�' || c == '\uFFFD')
                    count++;
            }
            return count;
        }
    }
}
