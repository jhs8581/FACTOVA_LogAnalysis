using System;
using System.Text.RegularExpressions;
using FACTOVA_LogAnalysis.Models;

namespace FACTOVA_LogAnalysis.Helpers
{
    public static class LogParser
    {
        private static readonly Regex DataLogRegex = new Regex(
            @"\[(?<timestamp>\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2})\] (?<business>\w+) (?<exectime>\d+\.\d+) (?<txnid>\w+) (?<content>.*)",
            RegexOptions.Compiled);

        private static readonly Regex DebugLogRegex = new Regex(
            @"\[(?<timestamp>\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2})\] (?<log>.*)",
            RegexOptions.Compiled);

        // EVENT 로그를 인식하는 정규식 - 다양한 로그 형식에 맞게 개선
        private static readonly Regex EventLogRegex = new Regex(
            @"\[(?<timestamp>\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2})\]\[SENDDATA\]\s*DYNAMIC\.EVENT\.REQUEST=\{(?<content>.*?)\}(?=\s*$|\s*\[|\s*\Z)",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

        // ELEMENT 형식의 EVENT 로그를 인식하는 새로운 정규식 추가
        private static readonly Regex ElementEventLogRegex = new Regex(
            @"\[(?<timestamp>\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2})\]\s*(?<msgid>\d+)\s+\[ELEMENT,\s*ELEMENT=\{(?<content>.*?)\}\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // MSGID를 추출하는 정규식 - ELEMENT 구문에서 찾기
        private static readonly Regex MsgIdRegex = new Regex(@"<MSGID=(?<msgid>\d+)>", RegexOptions.Compiled);
        
        // PROCID를 추출하는 정규식 추가
        private static readonly Regex ProcIdRegex = new Regex(@"<PROCID=(?<procid>[^>]+)>", RegexOptions.Compiled);
        
        // ITEM 섹션을 추출하는 정규식 - 더 정확하게 개선
        private static readonly Regex ItemSectionRegex = new Regex(@"\[ITEM,\s*ITEM=\{(.*?)\}\]", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

        public static LogLineItem? Parse(string line, int lineNumber)
        {
            // ELEMENT 형식의 EVENT 로그 먼저 확인
            var elementEventMatch = ElementEventLogRegex.Match(line);
            if (elementEventMatch.Success)
            {
                string content = elementEventMatch.Groups["content"].Value;
                string msgId = elementEventMatch.Groups["msgid"].Value;
                
                // PROCID 추출
                var procIdMatch = ProcIdRegex.Match(content);
                string procId = procIdMatch.Success ? procIdMatch.Groups["procid"].Value : "";

                return new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = FormatTimestamp(elementEventMatch.Groups["timestamp"].Value),
                    MsgId = msgId,
                    BusinessName = "ELEMENT_EVENT",
                    Content = $"[ELEMENT, ELEMENT={{<PROCID={procId}> <MSGID={msgId}>}}]",
                    LogLevel = "EVENT"
                };
            }

            // EVENT 로그 확인을 다음 시도
            if (line.Contains("[SENDDATA]") && line.Contains("DYNAMIC.EVENT.REQUEST"))
            {
                var eventMatch = EventLogRegex.Match(line);
                if (eventMatch.Success)
                {
                    string fullContent = eventMatch.Groups["content"].Value;
                    var msgIdMatch = MsgIdRegex.Match(fullContent);
                    var procIdMatch = ProcIdRegex.Match(fullContent);

                    string content = ExtractItemsFromEventContent(fullContent);

                    return new LogLineItem
                    {
                        LineNumber = lineNumber,
                        Timestamp = FormatTimestamp(eventMatch.Groups["timestamp"].Value),
                        MsgId = msgIdMatch.Success ? msgIdMatch.Groups["msgid"].Value : string.Empty,
                        BusinessName = procIdMatch.Success ? $"PROC_{procIdMatch.Groups["procid"].Value}" : "EVENT",
                        Content = content,
                        LogLevel = "EVENT"
                    };
                }
            }

            var dataMatch = DataLogRegex.Match(line);
            if (dataMatch.Success)
            {
                return new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = FormatTimestamp(dataMatch.Groups["timestamp"].Value),
                    BusinessName = dataMatch.Groups["business"].Value,
                    ExecTime = dataMatch.Groups["exectime"].Value,
                    TxnId = dataMatch.Groups["txnid"].Value,
                    Content = dataMatch.Groups["content"].Value.Trim(),
                    LogLevel = "DATA"
                };
            }

            var debugMatch = DebugLogRegex.Match(line);
            if (debugMatch.Success)
            {
                return new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = FormatTimestamp(debugMatch.Groups["timestamp"].Value),
                    Content = debugMatch.Groups["log"].Value.Trim(),
                    LogLevel = "DEBUG"
                };
            }

            return null;
        }

        /// <summary>
        /// TimeStamp를 yyyy-MM-dd HH:mm:ss 형식으로 변환
        /// </summary>
        private static string FormatTimestamp(string timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp))
                return "";

            try
            {
                // 원본 형식: dd-MM-yyyy HH:mm:ss 를 yyyy-MM-dd HH:mm:ss
                if (DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss", 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    return parsedDate.ToString("yyyy-MM-dd HH:mm:ss");
                }

                // 다른 형식들도 시도
                if (DateTime.TryParse(timestamp, out DateTime parsedDate2))
                {
                    return parsedDate2.ToString("yyyy-MM-dd HH:mm:ss");
                }

                return timestamp; // 파싱 실패 시 원본 반환
            }
            catch
            {
                return timestamp;
            }
        }

        /// <summary>
        /// EVENT 내용에서 ITEM정보 추출하여 정리
        /// </summary>
        private static string ExtractItemsFromEventContent(string content)
        {
            try
            {
                // 첫 번째 시도: ITEM 섹션 전체 추출
                var itemMatch = ItemSectionRegex.Match(content);
                if (itemMatch.Success)
                {
                    return CleanItemContent(itemMatch.Groups[1].Value);
                }

                // 두 번째 시도: 개별 아이템들 [1, 1={...}], [2, 2={...}] 형식 찾기
                var itemsPattern = @"\[\d+,\s*\d+=\{[^}]*\}\]";
                var itemsMatches = Regex.Matches(content, itemsPattern);
                
                if (itemsMatches.Count > 0)
                {
                    var items = new System.Collections.Generic.List<string>();
                    foreach (Match match in itemsMatches)
                    {
                        items.Add(match.Value);
                    }
                    return string.Join(" ", items);
                }

                // 세 번째 시도: ELEMENT 부분 제거하고 나머지 반환
                var elementPattern = @"\[ELEMENT,\s*ELEMENT=\{[^}]*\}\]";
                var cleanedContent = Regex.Replace(content, elementPattern, "").Trim();
                
                return CleanItemContent(cleanedContent);
            }
            catch
            {
                return content.Trim();
            }
        }

        /// <summary>
        /// ITEM 내용 정리
        /// </summary>
        private static string CleanItemContent(string itemContent)
        {
            return itemContent
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\t", " ")
                .Replace("  ", " ")
                .Trim();
        }
    }
}
