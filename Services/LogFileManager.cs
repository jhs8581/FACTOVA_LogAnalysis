using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;

namespace FACTOVA_LogAnalysis.Services
{
    public class LogFileManager
    {
        // 로그 시작을 판별하는 정규식: [dd-MM-yyyy HH:mm:ss] 또는 [dd-MM-yyyy HH:mm:ss.fff]
        private static readonly Regex LogStartRegex = new Regex(@"^\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?\]", RegexOptions.Compiled | RegexOptions.Multiline);
        
        public string LogFolderPath { get; set; } = "";

        public LogFileManager(string logFolderPath = "")
        {
            LogFolderPath = logFolderPath;
        }

        public string GetLogFilePath(string year, string month, string fileName)
        {
            // 1차 시도: 년/월 폴더 구조
            string filePath = Path.Combine(LogFolderPath, year, month, fileName);
            
            // 1차 시도에 파일이 없으면 2차 시도(루트) 탐색
            if (!File.Exists(filePath))
            {
                filePath = Path.Combine(LogFolderPath, fileName);
            }
            
            return filePath;
        }

        public async Task<string> ReadLogFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"로그 파일을 찾을 수 없습니다: {filePath}");
            }

            // ✅ 바이트 수준에서 인코딩 감지
            var bytes = await File.ReadAllBytesAsync(filePath);
            var encoding = DetectEncodingFromBytes(bytes);

            System.Diagnostics.Debug.WriteLine($"📄 파일: {Path.GetFileName(filePath)}");
            System.Diagnostics.Debug.WriteLine($"🔍 감지된 인코딩: {encoding.EncodingName} (CodePage: {encoding.CodePage})");

            // 감지된 인코딩으로 읽기
            return encoding.GetString(bytes);
        }

        /// <summary>
        /// 바이트 배열에서 인코딩을 감지
        /// </summary>
        private Encoding DetectEncodingFromBytes(byte[] bytes)
        {
            // 1. BOM 확인
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                System.Diagnostics.Debug.WriteLine("✅ UTF-8 BOM 발견");
                return new UTF8Encoding(true); // UTF-8 with BOM
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                System.Diagnostics.Debug.WriteLine("✅ UTF-16 LE BOM 발견");
                return Encoding.Unicode;
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                System.Diagnostics.Debug.WriteLine("✅ UTF-16 BE BOM 발견");
                return Encoding.BigEndianUnicode;
            }

            // 2. BOM 없음 - 내용 분석으로 판단
            // 샘플 크기 (최대 10KB)
            int sampleSize = Math.Min(bytes.Length, 10240);
            
            // UTF-8 유효성 검사
            bool isValidUtf8 = IsValidUtf8(bytes, sampleSize);
            
            // EUC-KR 가능성 검사 (0x81-0xFE 범위의 한글 바이트)
            bool hasEucKrPattern = HasEucKrPattern(bytes, sampleSize);

            System.Diagnostics.Debug.WriteLine($"   UTF-8 유효: {isValidUtf8}");
            System.Diagnostics.Debug.WriteLine($"   EUC-KR 패턴: {hasEucKrPattern}");

            // 판단 로직
            if (isValidUtf8 && !hasEucKrPattern)
            {
                System.Diagnostics.Debug.WriteLine("✅ UTF-8 (BOM 없음) 선택");
                return new UTF8Encoding(false);
            }

            if (hasEucKrPattern)
            {
                System.Diagnostics.Debug.WriteLine("✅ EUC-KR 선택");
                return Encoding.GetEncoding("EUC-KR");
            }

            // 3. 기본값: EUC-KR (한국 Windows 기본)
            System.Diagnostics.Debug.WriteLine("⚠️ 기본값 EUC-KR 사용");
            return Encoding.GetEncoding("EUC-KR");
        }

        /// <summary>
        /// UTF-8 유효성 검사
        /// </summary>
        private bool IsValidUtf8(byte[] bytes, int length)
        {
            int i = 0;
            while (i < length)
            {
                byte b = bytes[i];

                // ASCII (0x00-0x7F)
                if (b <= 0x7F)
                {
                    i++;
                    continue;
                }

                // 2바이트 UTF-8 (0xC0-0xDF)
                if (b >= 0xC0 && b <= 0xDF)
                {
                    if (i + 1 >= length || !IsContinuationByte(bytes[i + 1]))
                        return false;
                    i += 2;
                    continue;
                }

                // 3바이트 UTF-8 (0xE0-0xEF)
                if (b >= 0xE0 && b <= 0xEF)
                {
                    if (i + 2 >= length || !IsContinuationByte(bytes[i + 1]) || !IsContinuationByte(bytes[i + 2]))
                        return false;
                    i += 3;
                    continue;
                }

                // 4바이트 UTF-8 (0xF0-0xF7)
                if (b >= 0xF0 && b <= 0xF7)
                {
                    if (i + 3 >= length || !IsContinuationByte(bytes[i + 1]) || !IsContinuationByte(bytes[i + 2]) || !IsContinuationByte(bytes[i + 3]))
                        return false;
                    i += 4;
                    continue;
                }

                // 유효하지 않은 UTF-8
                return false;
            }

            return true;
        }

        /// <summary>
        /// UTF-8 연속 바이트 확인 (0x80-0xBF)
        /// </summary>
        private bool IsContinuationByte(byte b)
        {
            return b >= 0x80 && b <= 0xBF;
        }

        /// <summary>
        /// EUC-KR 패턴 검사
        /// </summary>
        private bool HasEucKrPattern(byte[] bytes, int length)
        {
            int eucKrCount = 0;
            int i = 0;

            while (i < length - 1)
            {
                byte b1 = bytes[i];
                byte b2 = bytes[i + 1];

                // EUC-KR 한글 범위: 첫 바이트 0x81-0xFE, 두 번째 바이트 0x41-0xFE
                if ((b1 >= 0x81 && b1 <= 0xFE) && (b2 >= 0x41 && b2 <= 0xFE))
                {
                    eucKrCount++;
                    i += 2;
                }
                else
                {
                    i++;
                }
            }

            // 샘플에서 10개 이상의 EUC-KR 패턴이 발견되면 EUC-KR로 판단
            return eucKrCount >= 10;
        }

        public string CleanDataLogs(string dataContent)
        {
            if (string.IsNullOrEmpty(dataContent))
                return dataContent;

            // BIZACTOR_INFO와 TRACE_INFO 제거
            var bizactorRegex = new Regex(@"\s*<__BIZACTOR_INFO__>.*?</__BIZACTOR_INFO__>\s*", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            dataContent = bizactorRegex.Replace(dataContent, "");

            var traceRegex = new Regex(@"\s*<__TRACE_INFO__>.*?</__TRACE_INFO__>\s*", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            dataContent = traceRegex.Replace(dataContent, "");

            // 기본적인 줄바꿈 정리
            dataContent = Regex.Replace(dataContent, @"\s*/\s*exec\.Time\s*:", "\nexec.Time :");
            dataContent = Regex.Replace(dataContent, @"\s*/\s*TXN_ID\s*:", "\nTXN_ID :");
            dataContent = Regex.Replace(dataContent, @":\s*Parameter\s*:\s*<", ":\nParameter :\n<");

            // XML 부분만 들여쓰기 적용 (NewDataSet 한정)
            dataContent = FormatXmlBlocksOnly(dataContent);

            return dataContent;
        }

        public string CleanEventLogs(string eventContent)
        {
            if (string.IsNullOrEmpty(eventContent))
                return eventContent;

            // 불필요한 로그 항목들 제거
            // GetUpdateList - Start, GetUpdateList - End 형태 제거
            var systemGetUpdateListRegex = new Regex(@"^\[.*?\]\s+System\s*:\s*GetUpdateList\s*-\s*(Start|End)\s*.*$", 
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            eventContent = systemGetUpdateListRegex.Replace(eventContent, "");

            // 또는 더 광범위한 GetUpdateList 형태 제거
            var getUpdateListRegex = new Regex(@"^.*GetUpdateList\s*-\s*(Start|End).*$", 
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            eventContent = getUpdateListRegex.Replace(eventContent, "");

            // BIZACTOR_INFO와 TRACE_INFO 제거
            var bizactorRegex = new Regex(@"\s*<__BIZACTOR_INFO__>.*?</__BIZACTOR_INFO__>\s*", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            eventContent = bizactorRegex.Replace(eventContent, "");

            var traceRegex = new Regex(@"\s*<__TRACE_INFO__>.*?</__TRACE_INFO__>\s*", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            eventContent = traceRegex.Replace(eventContent, "");

            // EVENT도 DATA처럼 기본적인 줄바꿈 정리
            eventContent = Regex.Replace(eventContent, @"\s*/\s*exec\.Time\s*:", "\nexec.Time :");
            eventContent = Regex.Replace(eventContent, @"\s*/\s*TXN_ID\s*:", "\nTXN_ID :");
            eventContent = Regex.Replace(eventContent, @":\s*Parameter\s*:\s*<", ":\nParameter :\n<");

            // EVENT도 XML 포맷팅 적용
            eventContent = FormatXmlBlocksOnly(eventContent);

            // 마지막 단계: 빈 줄들을 정리
            // 1. 공백만 있는 줄들은 제거
            eventContent = Regex.Replace(eventContent, @"^\s*$", "", RegexOptions.Multiline);
            
            // 2. 연속 여러 개의 줄바꿈을 하나로 압축
            eventContent = Regex.Replace(eventContent, @"\n+", "\n");
            
            // 3. 앞뒤 공백 및 줄바꿈 제거
            eventContent = eventContent.Trim();

            return eventContent;
        }

        private string FormatXmlBlocksOnly(string content)
        {
            // NewDataSet XML 한정으로 찾아서 들여쓰기 적용
            var xmlRegex = new Regex(@"(<NewDataSet>.*?</NewDataSet>)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            
            return xmlRegex.Replace(content, match =>
            {
                string xmlContent = match.Value;
                
                // 더 정확한 XML 파싱
                try
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(xmlContent);
                    
                    using (var stringWriter = new StringWriter())
                    using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "  ", // 2칸 들여쓰기
                        NewLineChars = "\n",
                        OmitXmlDeclaration = true,
                        NewLineHandling = NewLineHandling.Replace
                    }))
                    {
                        xmlDoc.WriteTo(xmlWriter);
                        xmlWriter.Flush();
                        return stringWriter.ToString();
                    }
                }
                catch
                {
                    // XML 파싱 실패 시 수동 포매팅
                    xmlContent = Regex.Replace(xmlContent, @"><", ">\n<");
                    
                    var lines = xmlContent.Split('\n');
                    var result = new StringBuilder();
                    int indentLevel = 0;

                    foreach (string line in lines)
                    {
                        string trimmedLine = line.Trim();
                        if (string.IsNullOrEmpty(trimmedLine)) continue;

                        // 닫는 태그는 들여쓰기 레벨을 먼저 감소
                        if (trimmedLine.StartsWith("</"))
                        {
                            indentLevel = Math.Max(0, indentLevel - 1);
                        }

                        // 들여쓰기 적용
                        string indent = new string(' ', indentLevel * 2);
                        result.AppendLine(indent + trimmedLine);

                        // 여는 태그는 들여쓰기 레벨을 증가 (자체 닫는 태그는 제외)
                        if (trimmedLine.StartsWith("<") && !trimmedLine.StartsWith("</") && !trimmedLine.EndsWith("/>") && !trimmedLine.Contains("</"))
                        {
                            indentLevel++;
                        }
                    }

                    return result.ToString().TrimEnd();
                }
            });
        }

        public string GetSearchContent(string content, string searchText, string searchMode)
        {
            switch (searchMode)
            {
                case "Range":
                    return GetSearchRange(content, searchText);
                case "Before":
                    return GetSearchBefore(content, searchText);
                case "After":
                    return GetSearchAfter(content, searchText);
                default:
                    return GetSearchRange(content, searchText);
            }
        }

        private string GetSearchBefore(string content, string searchText)
        {
            if (string.IsNullOrEmpty(content) || !content.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return $"No '{searchText}' found in this category.";
            }

            int firstMatchIndex = content.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            
            // 첫 번째 매칭 위치까지 모든 내용 반환
            return content.Substring(0, firstMatchIndex + searchText.Length);
        }

        private string GetSearchAfter(string content, string searchText)
        {
            if (string.IsNullOrEmpty(content) || !content.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return $"No '{searchText}' found in this category.";
            }

            int firstMatchIndex = content.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            
            // 첫 번째 매칭 위치부터 끝까지 모든 내용 반환
            return content.Substring(firstMatchIndex);
        }

        private string GetSearchRange(string categoryContent, string searchText)
        {
            if (string.IsNullOrEmpty(categoryContent) || !categoryContent.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                return $"No '{searchText}' found in this category.";
            }

            int firstMatchIndex = categoryContent.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            int lastMatchIndex = categoryContent.LastIndexOf(searchText, StringComparison.OrdinalIgnoreCase);

            var logStartMatches = LogStartRegex.Matches(categoryContent).Cast<Match>().ToList();
            if (logStartMatches.Count == 0)
            {
                return categoryContent;
            }

            int finalStartIndex = 0;
            var firstLog = logStartMatches.LastOrDefault(m => m.Index <= firstMatchIndex);
            if (firstLog != null)
            {
                finalStartIndex = firstLog.Index;
            }

            int finalEndIndex = categoryContent.Length;
            var lastLog = logStartMatches.LastOrDefault(m => m.Index <= lastMatchIndex);
            if (lastLog != null)
            {
                int lastLogIndexInMatches = logStartMatches.IndexOf(lastLog);
                if (lastLogIndexInMatches + 1 < logStartMatches.Count)
                {
                    finalEndIndex = logStartMatches[lastLogIndexInMatches + 1].Index;
                }
            }

            return categoryContent.Substring(finalStartIndex, finalEndIndex - finalStartIndex);
        }
    }
}
