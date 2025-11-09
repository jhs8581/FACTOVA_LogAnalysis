using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Helpers;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace FACTOVA_LogAnalysis.Services
{
    /// <summary>
    /// 로그 로딩 로직을 담당하는 서비스 클래스
    /// MainWindow에서 분리하여 로그 파일 로딩 기능을 전담
    /// </summary>
    public class LogLoadingService
    {
        #region Fields

        private readonly LogFileManager _logFileManager;
        private readonly WorkLogService _workLogService;

        // ✅ 성능 최적화: Compiled Regex를 static으로 캐싱
        private static readonly Regex TimestampRegex = new Regex(
            @"\[(\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?)\]",
            RegexOptions.Compiled);

        private static readonly Regex MsgIdRegex = new Regex(
            @"<MSGID=(\d+)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ProcIdRegex = new Regex(
            @"<PROCID=([^>]*)>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ItemPatternRegex = new Regex(
            @"\[(\d+),\s*\d+=\{[^}]*<NAME=([^>]+)>[^}]*<VALUE=([^>]*)>[^}]*\}\]",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex NameValuePatternRegex = new Regex(
            @"<NAME=([^>]+)>\s*<VALUE=([^>]*)>",
            RegexOptions.Compiled);

        private static readonly Regex TimestampLineRegex = new Regex(
            @"\[\d{2}-\d{2}-\d{4} (\d{2}):(\d{2}):(\d{2})(?:\.(\d{3}))?\]",
            RegexOptions.Compiled);

        private static readonly Regex TimestampExtractRegex = new Regex(
            @"\[(\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?)\]",
            RegexOptions.Compiled);

        private static readonly Regex TimestampRemoveRegex = new Regex(
            @"\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?\]\s*(.*)",
            RegexOptions.Compiled);

        #endregion

        #region Constructor

        public LogLoadingService(LogFileManager logFileManager, WorkLogService workLogService)
        {
            _logFileManager = logFileManager ?? throw new ArgumentNullException(nameof(logFileManager));
            _workLogService = workLogService ?? throw new ArgumentNullException(nameof(workLogService));
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 개별 로그 파일들을 로드하는 메인 메소드
        /// </summary>
        public async Task LoadIndividualLogFiles(DateTime selectedDate, string searchText, string searchMode,
            WpfTextBox dataTextBox, WpfTextBox eventTextBox, WpfTextBox debugTextBox, WpfTextBox exceptionTextBox,
            WpfTextBox dataLineTextBox, WpfTextBox eventLineTextBox, WpfTextBox debugLineTextBox, WpfTextBox exceptionLineTextBox,
            TimeSpan fromTime = default, TimeSpan toTime = default)
        {
            string year = selectedDate.ToString("yyyy");
            string month = selectedDate.Month.ToString();
            string dateString = selectedDate.ToString("MMddyyyy");

            var logFiles = new Dictionary<string, (string fileName, WpfTextBox textBox, WpfTextBox lineNumberBox, Func<string, string> cleaner)>
            {
                ["DATA"] = ($"LGE GMES_DATA_{dateString}.log", dataTextBox, dataLineTextBox, _logFileManager.CleanDataLogs),
                ["EVENT"] = ($"LGE GMES_EVENT_{dateString}.log", eventTextBox, eventLineTextBox, _logFileManager.CleanEventLogs),
                ["DEBUG"] = ($"LGE GMES_DEBUG_{dateString}.log", debugTextBox, debugLineTextBox, content => content),
                ["EXCEPTION"] = ($"LGE GMES_EXCEPTION_{dateString}.log", exceptionTextBox, exceptionLineTextBox, content => content)
            };

            foreach (var kvp in logFiles)
            {
                string logType = kvp.Key;
                string fileName = kvp.Value.fileName;
                WpfTextBox textBox = kvp.Value.textBox;
                WpfTextBox lineNumberBox = kvp.Value.lineNumberBox;
                Func<string, string> cleaner = kvp.Value.cleaner;

                await LoadSingleLogFileToTextBox(logType, fileName, textBox, lineNumberBox, cleaner, searchText, searchMode, year, month, default, default);
            }
        }

        /// <summary>
        /// 단일 로그 파일을 TextBox에 로드
        /// </summary>
        public async Task LoadSingleLogFileToTextBox(string logType, string fileName, WpfTextBox textBox, 
            WpfTextBox lineNumberBox, Func<string, string> cleaner, string searchText, string searchMode, 
            string year, string month, TimeSpan fromTime = default, TimeSpan toTime = default)
        {
            string filePath = _logFileManager.GetLogFilePath(year, month, fileName);

            try
            {
                if (File.Exists(filePath))
                {
                    string content = await _logFileManager.ReadLogFileAsync(filePath);
                    
                    if (fromTime != default || toTime != default)
                    {
                        content = FilterLogByTimeRange(content, fromTime, toTime);
                        if (string.IsNullOrEmpty(content))
                        {
                            textBox.Text = $"No {logType} logs found in time range {fromTime:hh\\:mm} ~ {toTime:hh\\:mm}";
                            lineNumberBox.Text = "1";
                            return;
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(searchText))
                    {
                        if (content.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                        {
                            content = _logFileManager.GetSearchContent(content, searchText, searchMode);
                        }
                        else
                        {
                            textBox.Text = $"'{searchText}' not found in {logType} log.";
                            lineNumberBox.Text = "1";
                            return;
                        }
                    }
                    
                    string cleanedContent = cleaner(content);
                    textBox.Text = cleanedContent;
                    GenerateLineNumbers(cleanedContent, lineNumberBox);
                }
                else
                {
                    textBox.Text = $"{logType} log file not found.";
                    lineNumberBox.Text = "1";
                }
            }
            catch (Exception ex)
            {
                textBox.Text = $"Error reading {logType} log file: {ex.Message}";
                lineNumberBox.Text = "1";
            }
        }

        /// <summary>
        /// 로그 타입별로 DataGrid용 데이터 로드
        /// </summary>
        public async Task<List<LogLineItem>> LoadLogDataForDataGrid(string logType, DateTime selectedDate, string searchText, string searchMode, TimeSpan fromTime = default, TimeSpan toTime = default)
        {
            try
            {
                string year = selectedDate.ToString("yyyy");
                string month = selectedDate.Month.ToString();
                string dateString = selectedDate.ToString("MMddyyyy");
                string fileName = $"LGE GMES_{logType}_{dateString}.log";
                
                string filePath = _logFileManager.GetLogFilePath(year, month, fileName);
                
                if (!File.Exists(filePath))
                {
                    return new List<LogLineItem>();
                }

                string content = await _logFileManager.ReadLogFileAsync(filePath);
                
                if (fromTime != default || toTime != default)
                {
                    content = FilterLogByTimeRange(content, fromTime, toTime);
                    if (string.IsNullOrEmpty(content))
                    {
                        return new List<LogLineItem>();
                    }
                }
                
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    if (content.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        content = _logFileManager.GetSearchContent(content, searchText, searchMode);
                    }
                    else
                    {
                        return new List<LogLineItem>();
                    }
                }
                
                string cleanedContent = logType switch
                {
                    "DATA" => _logFileManager.CleanDataLogs(content),
                    "EVENT" => _logFileManager.CleanEventLogs(content),
                    _ => content
                };
                
                List<LogLineItem> result;
                if (logType == "DATA")
                {
                    result = LogDataGridHelper.ConvertToLogLines(cleanedContent);
                }
                else if (logType == "EXCEPTION")
                {
                    result = LogDataGridHelper.ConvertExceptionLogLines(cleanedContent);
                }
                else
                {
                    result = ConvertSimpleLogLines(cleanedContent, logType);
                }
                
                foreach (var item in result)
                {
                    item.LogLevel = logType;
                }
                
                return result;
            }
            catch (Exception)
            {
                return new List<LogLineItem>();
            }
        }

        /// <summary>
        /// 모든 DataGrid에 데이터를 병렬로 로드
        /// </summary>
        public async Task<Dictionary<string, List<LogLineItem>>> LoadAllDataGridData(DateTime selectedDate, string searchText, string searchMode, TimeSpan fromTime = default, TimeSpan toTime = default)
        {
            var result = new Dictionary<string, List<LogLineItem>>();
            var logTypes = new[] { "DATA", "EVENT", "DEBUG", "EXCEPTION" };
            
            var tasks = logTypes.Select(async logType => 
            {
                var data = await LoadLogDataForDataGrid(logType, selectedDate, searchText, searchMode, fromTime, toTime);
                return new { LogType = logType, Data = data };
            });
            
            var results = await Task.WhenAll(tasks);
            
            foreach (var item in results)
            {
                result[item.LogType] = item.Data;
            }
            
            return result;
        }

        /// <summary>
        /// 자동 로그 파일 로드 (날짜 변경 시)
        /// </summary>
        public async Task AutoLoadLogFiles(DateTime selectedDate, string searchText, string searchMode,
            WpfTextBox dataTextBox, WpfTextBox eventTextBox, WpfTextBox debugTextBox, WpfTextBox exceptionTextBox,
            WpfTextBox dataLineTextBox, WpfTextBox eventLineTextBox, WpfTextBox debugLineTextBox, WpfTextBox exceptionLineTextBox)
        {
            try
            {
                await LoadIndividualLogFiles(selectedDate, searchText, searchMode,
                    dataTextBox, eventTextBox, debugTextBox, exceptionTextBox,
                    dataLineTextBox, eventLineTextBox, debugLineTextBox, exceptionLineTextBox);
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 시간 범위로 로그 내용 필터링 - 최적화
        /// </summary>
        private string FilterLogByTimeRange(string content, TimeSpan fromTime, TimeSpan toTime)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            try
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var filteredLines = new List<string>(lines.Length / 2);
                bool lastLineWasInRange = false;

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (lastLineWasInRange)
                        {
                            filteredLines.Add(line);
                        }
                        continue;
                    }

                    var timeMatch = TimestampLineRegex.Match(line);
                    
                    if (timeMatch.Success)
                    {
                        int hours = int.Parse(timeMatch.Groups[1].Value);
                        int minutes = int.Parse(timeMatch.Groups[2].Value);
                        int seconds = int.Parse(timeMatch.Groups[3].Value);
                        
                        int milliseconds = 0;
                        if (timeMatch.Groups[4].Success)
                        {
                            milliseconds = int.Parse(timeMatch.Groups[4].Value);
                        }
                        
                        var logTime = new TimeSpan(0, hours, minutes, seconds, milliseconds);

                        if (logTime >= fromTime && logTime <= toTime)
                        {
                            filteredLines.Add(line);
                            lastLineWasInRange = true;
                        }
                        else
                        {
                            lastLineWasInRange = false;
                        }
                    }
                    else
                    {
                        if (lastLineWasInRange)
                        {
                            filteredLines.Add(line);
                        }
                    }
                }

                return string.Join("\n", filteredLines);
            }
            catch (Exception)
            {
                return content;
            }
        }

        /// <summary>
        /// 행 번호 생성
        /// </summary>
        private void GenerateLineNumbers(string content, WpfTextBox lineNumberBox)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var lineNumbers = new StringBuilder(lines.Length * 4);
            
            for (int i = 1; i <= lines.Length; i++)
            {
                lineNumbers.AppendLine(i.ToString());
            }
            
            lineNumberBox.Text = lineNumbers.ToString();
        }

        /// <summary>
        /// 단순 로그 라인들을 LogLineItem으로 변환 (EVENT, DEBUG용)
        /// </summary>
        private List<LogLineItem> ConvertSimpleLogLines(string content, string logType)
        {
            if (logType == "EVENT")
            {
                return ConvertEventLogLines(content);
            }
            
            var result = new List<LogLineItem>();
            
            if (string.IsNullOrWhiteSpace(content))
                return result;

            try
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    string timestamp = ExtractTimestampFromLine(line);
                    string businessName = ExtractBusinessNameFromSimpleLine(line, logType);
                    string cleanContent = RemoveTimestampFromLine(line);
                    
                    var logItem = new LogLineItem(i + 1, timestamp, businessName, "", "", cleanContent)
                    {
                        LogLevel = logType
                    };
                    
                    result.Add(logItem);
                }
                
                return result;
            }
            catch (Exception)
            {
                return result;
            }
        }

        /// <summary>
        /// EVENT 로그를 정교하게 처리하여 LogLineItem으로 변환 - 최적화
        /// </summary>
        private List<LogLineItem> ConvertEventLogLines(string content)
        {
            var result = new List<LogLineItem>();
            if (string.IsNullOrWhiteSpace(content))
                return result;
            
            try
            {
                // ✅ Static Regex 사용
                var matches = TimestampRegex.Matches(content);
                
                if (matches.Count == 0)
                {
                    return result;
                }
                
                int lineNumber = 1;
				
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    string timestamp = FormatTimestamp(match.Groups[1].Value);
                    
                    int startIndex = match.Index;
                    int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : content.Length;
                    
                    string blockContent = content.Substring(startIndex, endIndex - startIndex).Trim();
                    string contentWithoutTimestamp = blockContent.Substring(match.Length).Trim();
                    
                    if (string.IsNullOrWhiteSpace(contentWithoutTimestamp))
                        continue;
                    
                    var logItem = ProcessEventLogBlock(contentWithoutTimestamp, timestamp, lineNumber);
                    if (logItem != null)
                    {
                        // ✅ ExtractEventFields 제거 - 이미 파싱 완료
                        result.Add(logItem);
                        lineNumber++;
                    }
                }
                
                return result;
            }
            catch (Exception)
            {
                return result;
            }
        }

        /// <summary>
        /// 단일 EVENT 로그 블록을 처리하여 LogLineItem으로 변환 - 최적화
        /// </summary>
        private LogLineItem ProcessEventLogBlock(string content, string timestamp, int lineNumber)
        {
            try
            {
                string msgId = "";
                string procId = "";
                string businessName = "EVENT";
                string displayContent = content;
                
                // ✅ ReadOnlySpan 활용으로 성능 개선
                ReadOnlySpan<char> contentSpan = content.AsSpan();
                
                // ZPL 체크
                if (contentSpan.IndexOf("^XA".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new LogLineItem
                    {
                        LineNumber = lineNumber,
                        Timestamp = timestamp,
                        MsgId = "ZPL",
                        ProcId = "",
                        BusinessName = "ZPL",
                        Content = content,
                        LogLevel = "EVENT"
                    };
                }
                
                // SENDDATA/RECVDATA/RECV 체크
                bool hasSendData = contentSpan.IndexOf("[SENDDATA]".AsSpan()) >= 0;
                bool hasRecvData = contentSpan.IndexOf("[RECVDATA]".AsSpan()) >= 0;
                bool hasRecv = contentSpan.IndexOf("[RECV]".AsSpan()) >= 0;
                
                if (hasSendData || hasRecvData || hasRecv)
                {
                    string blockType = hasRecvData ? "RECVDATA" : (hasSendData ? "SENDDATA" : "RECV");
                    
                    // ✅ Static Regex 사용
                    var msgIdMatch = MsgIdRegex.Match(content);
                    if (msgIdMatch.Success)
                    {
                        msgId = msgIdMatch.Groups[1].Value;
                    }
                    
                    var procIdMatch = ProcIdRegex.Match(content);
                    if (procIdMatch.Success)
                    {
                        procId = procIdMatch.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(procId))
                        {
                            businessName = procId;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(msgId) && !string.IsNullOrEmpty(procId))
                    {
                        businessName = $"{procId}_{msgId}";
                    }
                    else if (!string.IsNullOrEmpty(msgId))
                    {
                        businessName = $"PROC_{msgId}";
                    }
                    else if (string.IsNullOrEmpty(businessName) || businessName == "EVENT")
                    {
                        businessName = blockType;
                    }
                    
                    string itemContent = ExtractItemContent(content);
                    if (!string.IsNullOrEmpty(itemContent))
                    {
                        displayContent = $"[{blockType}]\n{itemContent}";
                    }
                }
                else
                {
                    var defaultItem = CreateDefaultEventLogItem(content, lineNumber);
                    if (defaultItem != null)
                    {
                        msgId = defaultItem.MsgId;
                        procId = defaultItem.ProcId;
                        businessName = defaultItem.BusinessName;
                        displayContent = defaultItem.Content;
                    }
                }
                
                return new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = timestamp,
                    MsgId = msgId,
                    ProcId = procId,
                    BusinessName = businessName,
                    Content = displayContent,
                    LogLevel = "EVENT"
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// SENDDATA/RECVDATA 블록을 하나의 LogLineItem으로 처리
        /// </summary>
        private (LogLineItem?, int) ProcessEventDataBlock(string[] lines, int startIndex, int lineNumber, string blockType)
        {
            try
            {
                string startLine = lines[startIndex];
                string timestamp = ExtractTimestampFromLine(startLine);
                
                var contentBuilder = new StringBuilder();
                contentBuilder.AppendLine(startLine);
                int i = startIndex + 1;
                int braceCount = 0;
                bool blockEnded = false;
                
                foreach (char c in startLine)
                {
                    if (c == '{') braceCount++;
                    else if (c == '}') braceCount--;
                }
                
                if (braceCount == 0 && (startLine.Contains("DYNAMIC.EVENT.REQUEST") || startLine.Contains("DYNAMIC.EVENT.RESPONSE")))
                {
                    braceCount = 1;
                }
                
                while (i < lines.Length && !blockEnded)
                {
                    string nextLine = lines[i];
                    contentBuilder.AppendLine(nextLine);
                    
                    foreach (char c in nextLine)
                    {
                        if (c == '{') braceCount++;
                        else if (c == '}') braceCount--;
                    }
                    
                    if (braceCount <= 0)
                    {
                        blockEnded = true;
                    }
                    i++;
                    
                    if (i - startIndex > 100)
                    {
                        blockEnded = true;
                    }
                }
                
                string fullContent = contentBuilder.ToString();
                
                // ✅ Static Regex 사용
                var msgIdMatch = MsgIdRegex.Match(fullContent);
                string msgId = msgIdMatch.Success ? msgIdMatch.Groups[1].Value : "";
                
                var procIdMatch = ProcIdRegex.Match(fullContent);
                string procId = procIdMatch.Success ? procIdMatch.Groups[1].Value : "";
                
                string itemContent = ExtractItemContent(fullContent);
                string businessName = !string.IsNullOrEmpty(procId) ? $"PROC_{procId}" : $"{blockType}_EVENT";
                
                var eventItem = new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = FormatTimestamp(timestamp),
                    MsgId = msgId,
                    ProcId = procId,
                    BusinessName = businessName,
                    Content = itemContent,
                    LogLevel = "EVENT"
                };
                
                return (eventItem, i - 1);
            }
            catch (Exception)
            {
                return (null, startIndex);
            }
        }

        /// <summary>
        /// ZPL (^XA ... ^XZ) 블록을 하나의 LogLineItem으로 처리
        /// </summary>
        private (LogLineItem?, int) ProcessZplBlock(string[] lines, int startIndex, int lineNumber)
        {
            try
            {
                var contentBuilder = new StringBuilder();
                int i = startIndex;
                bool ended = false;
                int maxLines = lines.Length;
                int guard = 0;
                while (i < maxLines && !ended && guard < 1000)
                {
                    string l = lines[i];
                    contentBuilder.AppendLine(l);
                    if (l.Contains("^XZ"))
                    {
                        ended = true;
                        i++;
                        break;
                    }
                    i++;
                    guard++;
                }
                string fullContent = contentBuilder.ToString().TrimEnd();
                var item = new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = ExtractTimestampFromLine(lines[startIndex]),
                    MsgId = "ZPL",
                    ProcId = "",
                    BusinessName = "ZPL",
                    Content = fullContent,
                    LogLevel = "EVENT"
                };
                return (item, i - 1);
            }
            catch (Exception)
            {
                return (null, startIndex);
            }
        }

        /// <summary>
        /// DataSend / DataReceive 단일 라인을 처리하여 LogLineItem 생성
        /// </summary>
        private LogLineItem? ProcessDataTransferLine(string line, int lineNumber, string type)
        {
            try
            {
                string timestamp = ExtractTimestampFromLine(line);

                // MsgId 정규화
                string normalizedType = string.Equals(type, "DataReceive", StringComparison.OrdinalIgnoreCase) ? "DataReceive" :
                                         string.Equals(type, "DataSend", StringComparison.OrdinalIgnoreCase) ? "DataSend" : type;

                string payload = string.Empty;

                // 찾기 기준: keyword 위치 이후에서 페이로드를 찾음
                var keyMatch = System.Text.RegularExpressions.Regex.Match(line, @"\b(DataSend|DataReceive)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (keyMatch.Success)
                {
                    int searchStart = keyMatch.Index + keyMatch.Length;
                    string tail = line.Substring(searchStart);

                    // 1) 파이프 형식: DataSend|PAYLOAD
                    var mPipe = System.Text.RegularExpressions.Regex.Match(tail, @"\|\s*([A-Za-z0-9]{6,})");
                    if (mPipe.Success)
                    {
                        payload = mPipe.Groups[1].Value;

                        // 파이프 형식일 때는 IP가 라인 앞부분에 [x.x.x.x] 형태로 있을 수 있음 ? 같이 보여주기
                        var ipMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[\d{1,3}(?:\.\d{1,3}){3}\]");
                        if (ipMatch.Success)
                        {
                            payload = ipMatch.Value + " : " + payload;
                        }
                    }
                    else
                    {
                        // 2) '- : PAYLOAD' 또는 '- :PAYLOAD'
                        var mDash = System.Text.RegularExpressions.Regex.Match(tail, @"-\s*:\s*([A-Za-z0-9]{6,})");
                        if (mDash.Success)
                        {
                            payload = mDash.Groups[1].Value;
                        }
                        else
                        {
                            // 3) ':' 뒤의 긴 알파넘
                            var mColon = System.Text.RegularExpressions.Regex.Match(tail, @":\s*([A-Za-z0-9]{6,})");
                            if (mColon.Success)
                            {
                                payload = mColon.Groups[1].Value;
                            }
                            else
                            {
                                // 4) 키워드 이후에 나오는 가장 긴 알파넘 토큰(길이 >= 6)
                                var mmAll = System.Text.RegularExpressions.Regex.Matches(tail, @"([A-Za-z0-9]{6,})");
                                if (mmAll.Count > 0)
                                {
                                    // 가장 긴 토큰 선택
                                    string best = "";
                                    foreach (System.Text.RegularExpressions.Match mm in mmAll)
                                    {
                                        if (mm.Value.Length > best.Length) best = mm.Value;
                                    }
                                    payload = best;
                                }
                            }
                        }
                    }
                }

                // 추가: 키워드가 없더라도 '[IP] : PAYLOAD' 형태로 IP+데이터가 명시된 경우 우선 추출
                if (string.IsNullOrEmpty(payload))
                {
                    var ipPayload = System.Text.RegularExpressions.Regex.Match(line, @"\[(\d{1,3}(?:\.\d{1,3}){3})\]\s*:?\s*([A-Za-z0-9]{6,})");
                    if (ipPayload.Success)
                    {
                        var ip = ipPayload.Groups[1].Value;
                        var data = ipPayload.Groups[2].Value;
                        payload = $"[{ip}] : {data}";
                    }
                }

                // 마지막 보정: 만약 위에서 못찾았고 라인 전체에서 긴 토큰이 있으면 그것을 사용
                if (string.IsNullOrEmpty(payload))
                {
                    var any = System.Text.RegularExpressions.Regex.Match(line, @"([A-Za-z0-9]{8,})");
                    if (any.Success)
                        payload = any.Groups[1].Value;
                }

                var item = new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = timestamp,
                    MsgId = normalizedType,
                    ProcId = string.Empty,
                    BusinessName = normalizedType,
                    Content = string.IsNullOrEmpty(payload) ? RemoveTimestampFromLine(line) : payload,
                    LogLevel = "EVENT"
                };
                return item;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// SENDDATA/RECVDATA 블록에 포함된 라인인지 확인
        /// </summary>
        private bool IsPartOfEventDataBlock(string line)
        {
            return line.Contains("[ELEMENT,") || 
                   line.Contains("[ITEM,") || 
                   line.Contains("[1,") || 
                   line.Contains("[2,") ||
                   line.Contains("[3,") ||
                   line.Contains("[4,") ||
                   line.Contains("NAME=") || 
                   line.Contains("VALUE=") || 
                   line.Contains("<ACK=") ||
                   line.Trim().Equals("}") ||
                   line.Trim().Equals("}}");
        }

        /// <summary>
        /// 기본 EVENT 로그 아이템 생성
        /// </summary>
        private LogLineItem? CreateDefaultEventLogItem(string line, int lineNumber)
        {
            try
            {
                string timestamp = ExtractTimestampFromLine(line);
                string content = RemoveTimestampFromLine(line);
                
                if (string.IsNullOrWhiteSpace(content) || content.Trim() == ":")
                {
                    return null;
                }
                
                string msgId = "";
                string cleanedContent = content;
                
                // ✅ ReadOnlySpan 활용
                ReadOnlySpan<char> contentSpan = content.AsSpan();
                
                if (contentSpan.IndexOf("USBLampOnOff".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    msgId = "USBLamp";
                }
                else if (contentSpan.IndexOf("Click".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    msgId = "Click";
                }
                else if (contentSpan.IndexOf("FrameOperation_ScannerData_ReceivedEvent".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    msgId = "SCAN";
                    
                    var scannerMatch = Regex.Match(content, @".*-\s*\[([^\]]+)\]");
                    if (scannerMatch.Success)
                    {
                        cleanedContent = scannerMatch.Groups[1].Value.Replace("/", " / ").Replace("  ", " ").Trim();
                    }
                }
                
                return new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = timestamp,
                    MsgId = msgId,
                    ProcId = "",
                    BusinessName = "EVENT_GENERAL",
                    Content = cleanedContent,
                    LogLevel = "EVENT"
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// EVENT 내용에서 ITEM 추출하고 간결하게 포맷 - 최적화
        /// </summary>
        private string ExtractItemContent(string content)
        {
            try
            {
                var items = new List<string>();

                // ✅ Static Regex 사용
                var matches = ItemPatternRegex.Matches(content);
                
                if (matches.Count > 0)
                {
                    foreach (Match match in matches)
                    {
                        string itemIndex = match.Groups[1].Value;
                        string name = match.Groups[2].Value.Trim();
                        string value = match.Groups[3].Value.Trim();
                        
                        if (string.IsNullOrEmpty(value))
                        {
                            value = "(empty)";
                        }
                        
                        items.Add($"[{itemIndex}] {name} : {value}");
                    }
                }
                
                if (items.Count == 0)
                {
                    var nameValueMatches = NameValuePatternRegex.Matches(content);
                    
                    int index = 1;
                    foreach (Match match in nameValueMatches)
                    {
                        string name = match.Groups[1].Value.Trim();
                        string value = match.Groups[2].Value.Trim();
                        
                        if (string.IsNullOrEmpty(value))
                        {
                            value = "(empty)";
                        }
                        
                        items.Add($"[{index}] {name} : {value}");
                        index++;
                    }
                }
                
                if (items.Count > 0)
                {
                    return string.Join("\n", items);
                }
                
                return "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// 타임스탬프 형식 변환
        /// </summary>
        private string FormatTimestamp(string timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp))
                return "";

            try
            {
                if (DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss.fff",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime parsedWithMs))
                {
                    return parsedWithMs.ToString("HH:mm:ss.fff");
                }

                if (DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    return parsedDate.ToString("HH:mm:ss");
                }

                if (DateTime.TryParse(timestamp, out DateTime parsedDate2))
                {
                    if (timestamp.Contains("."))
                        return parsedDate2.ToString("HH:mm:ss.fff");
                    else
                        return parsedDate2.ToString("HH:mm:ss");
                }

                var mWithMs = Regex.Match(timestamp, @"(\d{2}:\d{2}:\d{2}\.\d{3})");
                if (mWithMs.Success)
                    return mWithMs.Groups[1].Value;

                var m = Regex.Match(timestamp, @"(\d{2}:\d{2}:\d{2})");
                if (m.Success)
                    return m.Groups[1].Value;

                return timestamp;
            }
            catch
            {
                return timestamp;
            }
        }

        /// <summary>
        /// 라인에서 타임스탬프 추출
        /// ?? 지원 형식: [dd-MM-yyyy HH:mm:ss] 또는 [dd-MM-yyyy HH:mm:ss.fff]
        /// </summary>
        private string ExtractTimestampFromLine(string line)
        {
            try
            {
                var match = TimestampExtractRegex.Match(line);
                if (match.Success)
                {
                    string originalTimestamp = match.Groups[1].Value;
                    return FormatTimestamp(originalTimestamp);
                }
                return DateTime.Now.ToString("HH:mm:ss");
            }
            catch
            {
                return DateTime.Now.ToString("HH:mm:ss");
            }
        }

        /// <summary>
        /// 라인에서 타임스탬프 제거
        /// ?? 지원 형식: [dd-MM-yyyy HH:mm:ss] 또는 [dd-MM-yyyy HH:mm:ss.fff]
        /// </summary>
        private string RemoveTimestampFromLine(string line)
        {
            try
            {
                var match = TimestampRemoveRegex.Match(line);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
                return line;
            }
            catch
            {
                return line;
            }
        }

        /// <summary>
        /// 단순 라인에서 비즈니스명 추출
        /// </summary>
        private string ExtractBusinessNameFromSimpleLine(string line, string logType)
        {
            try
            {
                if (logType == "EVENT")
                {
                    if (line.Contains("User Login:")) return "USER_LOGIN";
                    if (line.Contains("Menu Access:")) return "MENU_ACCESS";
                    if (line.Contains("Button Click:")) return "BUTTON_CLICK";
                    if (line.Contains("Process Started:")) return "PROCESS_START";
                    if (line.Contains("Equipment Status:")) return "EQUIPMENT_STATUS";
                    return "EVENT_GENERAL";
                }
                
                if (logType == "DEBUG")
                {
                    if (line.Contains("Database")) return "DB_DEBUG";
                    if (line.Contains("Query")) return "QUERY_DEBUG";
                    if (line.Contains("Memory")) return "MEMORY_DEBUG";
                    return "DEBUG_GENERAL";
                }
                
                return logType;
            }
            catch
            {
                return logType;
            }
        }

        #endregion
    }
}
