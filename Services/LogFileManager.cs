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

            // ✅ 여러 인코딩을 시도하여 자동 감지
            Encoding[] encodingsToTry = new[]
            {
                Encoding.UTF8,                          // UTF-8 시도
                Encoding.GetEncoding("EUC-KR"),        // EUC-KR (한국어 기본)
                Encoding.GetEncoding(949),             // CP949 (Windows 한국어)
                Encoding.Default,                       // 시스템 기본 인코딩
            };

            string? bestContent = null;
            int bestScore = -1;

            foreach (var encoding in encodingsToTry)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(filePath, encoding);
                    
                    // 한글 깨짐 체크: �, ?, 같은 문자가 많으면 낮은 점수
                    int score = CalculateEncodingScore(content);
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestContent = content;
                    }
                    
                    // 완벽한 점수면 바로 리턴
                    if (score >= 95)
                        return content;
                }
                catch
                {
                    // 이 인코딩은 실패, 다음 시도
                    continue;
                }
            }

            // 가장 좋은 결과 반환
            return bestContent ?? await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        }

        /// <summary>
        /// 인코딩 품질 점수 계산 (0-100)
        /// 한글이 제대로 표시되면 높은 점수
        /// </summary>
        private int CalculateEncodingScore(string content)
        {
            if (string.IsNullOrEmpty(content))
                return 0;

            int totalChars = Math.Min(content.Length, 1000); // 처음 1000자만 샘플링
            int badChars = 0;

            for (int i = 0; i < totalChars; i++)
            {
                char c = content[i];
                
                // 깨진 문자 감지
                if (c == '�' || c == '\uFFFD')
                {
                    badChars += 3; // 높은 페널티
                }
                // 연속된 물음표 (인코딩 오류 가능성)
                else if (c == '?' && i > 0 && content[i - 1] == '?')
                {
                    badChars += 1;
                }
            }

            // 점수 계산: 깨진 문자가 적을수록 높은 점수
            double badRatio = (double)badChars / totalChars;
            return (int)((1.0 - badRatio) * 100);
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
