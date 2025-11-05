using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            _workLogService.AddLog("=== Loading log files ===", WorkLogType.Info);
            _workLogService.AddLog($"Selected date: {selectedDate:yyyy-MM-dd}", WorkLogType.Info);
            
            // 시간 범위 로그 출력
            if (fromTime != default || toTime != default)
            {
                _workLogService.AddLog($"Time range: {fromTime:hh\\:mm} ~ {toTime:hh\\:mm}", WorkLogType.Info);
            }
            
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                _workLogService.AddLog($"Search text: '{searchText}'", WorkLogType.Info);
                _workLogService.AddLog($"Search mode: {searchMode}", WorkLogType.Info);
            }
            
            string year = selectedDate.ToString("yyyy");
            string month = selectedDate.Month.ToString();
            string dateString = selectedDate.ToString("MMddyyyy");
            
            _workLogService.AddLog($"Log folder: {_logFileManager.LogFolderPath}", WorkLogType.Info);

            var logFiles = new Dictionary<string, (string fileName, WpfTextBox textBox, WpfTextBox lineNumberBox, Func<string, string> cleaner)>
            {
                ["DATA"] = ($"LGE GMES_DATA_{dateString}.log", dataTextBox, dataLineTextBox, _logFileManager.CleanDataLogs),
                ["EVENT"] = ($"LGE GMES_EVENT_{dateString}.log", eventTextBox, eventLineTextBox, _logFileManager.CleanEventLogs),
                ["DEBUG"] = ($"LGE GMES_DEBUG_{dateString}.log", debugTextBox, debugLineTextBox, content => content),
                ["EXCEPTION"] = ($"LGE GMES_EXCEPTION_{dateString}.log", exceptionTextBox, exceptionLineTextBox, content => content)
            };

            int processedCount = 0;
            foreach (var kvp in logFiles)
            {
                string logType = kvp.Key;
                string fileName = kvp.Value.fileName;
                WpfTextBox textBox = kvp.Value.textBox;
                WpfTextBox lineNumberBox = kvp.Value.lineNumberBox;
                Func<string, string> cleaner = kvp.Value.cleaner;

                _workLogService.AddLog($"[{++processedCount}/{logFiles.Count}] Processing {logType} log...", WorkLogType.Info);
                // For text view, always load full content regardless of from/to time filter
                await LoadSingleLogFileToTextBox(logType, fileName, textBox, lineNumberBox, cleaner, searchText, searchMode, year, month, default, default);
            }
            
            _workLogService.AddLog("=== Log files loading completed ===", WorkLogType.Success);
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
                    
                    // 시간 범위 필터링 적용
                    if (fromTime != default || toTime != default)
                    {
                        content = FilterLogByTimeRange(content, fromTime, toTime);
                        if (string.IsNullOrEmpty(content))
                        {
                            textBox.Text = $"No {logType} logs found in time range {fromTime:hh\\:mm} ~ {toTime:hh\\:mm}";
                            lineNumberBox.Text = "1";
                            _workLogService.AddLog($"{logType}: No logs in time range", WorkLogType.Warning);
                            return;
                        }
                        _workLogService.AddLog($"{logType}: Time range filtering applied", WorkLogType.Info);
                    }
                    
                    // 검색어 있는 경우 필터링
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
                    
                    // 로그 내용 정리
                    string cleanedContent = cleaner(content);
                    
                    // 텍스트박스에 설정
                    textBox.Text = cleanedContent;
                    
                    // 줄 번호 생성
                    GenerateLineNumbers(cleanedContent, lineNumberBox);
                    
                    int lineCount = cleanedContent.Split('\n').Length;
                    string timeRangeInfo = (fromTime != default || toTime != default) 
                        ? $" (Time: {fromTime:hh\\:mm}~{toTime:hh\\:mm})" 
                        : "";
                    
                    _workLogService.AddLog($"{logType} log loaded successfully ({lineCount} lines){timeRangeInfo}", WorkLogType.Success);
                }
                else
                {
                    textBox.Text = $"{logType} log file not found.";
                    lineNumberBox.Text = "1";
                    _workLogService.AddLog($"{logType} log file not found: {filePath}", WorkLogType.Warning);
                }
            }
            catch (Exception ex)
            {
                textBox.Text = $"Error reading {logType} log file: {ex.Message}";
                lineNumberBox.Text = "1";
                _workLogService.AddLog($"Error loading {logType} log: {ex.Message}", WorkLogType.Error);
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
                    _workLogService.AddLog($"{logType} 파일을 찾을 수 없음: {filePath}", WorkLogType.Warning);
                    return new List<LogLineItem>();
                }

                _workLogService.AddLog($"{logType} 파일 로드 중: {fileName}", WorkLogType.Info);
                string content = await _logFileManager.ReadLogFileAsync(filePath);
                
                // 시간 범위 필터링
                if (fromTime != default || toTime != default)
                {
                    content = FilterLogByTimeRange(content, fromTime, toTime);
                    if (string.IsNullOrEmpty(content))
                    {
                        _workLogService.AddLog($"{logType}: 지정된 시간 범위에 로그가 없음", WorkLogType.Warning);
                        return new List<LogLineItem>();
                    }
                }
                
                // 검색어 필터링
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    if (content.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        content = _logFileManager.GetSearchContent(content, searchText, searchMode);
                    }
                    else
                    {
                        _workLogService.AddLog($"'{searchText}'를 {logType}에서 찾을 수 없음", WorkLogType.Warning);
                        return new List<LogLineItem>();
                    }
                }
                
                // 로그 정리
                string cleanedContent = logType switch
                {
                    "DATA" => _logFileManager.CleanDataLogs(content),
                    "EVENT" => _logFileManager.CleanEventLogs(content),
                    _ => content
                };
                
                // LogLineItem으로 변환
                List<LogLineItem> result;
                if (logType == "DATA")
                {
                    // DATA 로그는 ExecuteService 관련 파싱
                    result = LogDataGridHelper.ConvertToLogLines(cleanedContent);
                }
                else if (logType == "EXCEPTION")
                {
                    // EXCEPTION 로그는 ExecuteServiceSync 관련 파싱
                    result = LogDataGridHelper.ConvertExceptionLogLines(cleanedContent);
                }
                else
                {
                    // EVENT, DEBUG는 단순 라인별 파싱
                    result = ConvertSimpleLogLines(cleanedContent, logType);
                }
                
                // 로그 레벨 설정
                foreach (var item in result)
                {
                    item.LogLevel = logType;
                }
                
                string timeInfo = (fromTime != default || toTime != default) 
                    ? $" (시간범위: {fromTime:hh\\:mm}~{toTime:hh\\:mm})" 
                    : "";
                _workLogService.AddLog($"? {logType}: {result.Count}개 행 파sing 완료{timeInfo}", WorkLogType.Success);
                
                return result;
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} 로드 오류: {ex.Message}", WorkLogType.Error);
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
            
            _workLogService.AddLog("모든 DataGrid 데이터 병렬 로드 시작", WorkLogType.Info);
            
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
            
            var totalCount = result.Values.Sum(list => list.Count);
            _workLogService.AddLog($"? 모든 DataGrid 데이터 로드 완료: 총 {totalCount}개 행", WorkLogType.Success);
            
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
                _workLogService.AddLog($"자동 로드 시작: {selectedDate:yyyy-MM-dd}, 검색어: '{searchText}'", WorkLogType.Info);
                
                await LoadIndividualLogFiles(selectedDate, searchText, searchMode,
                    dataTextBox, eventTextBox, debugTextBox, exceptionTextBox,
                    dataLineTextBox, eventLineTextBox, debugLineTextBox, exceptionLineTextBox);
                
                _workLogService.AddLog("? 자동 로드 완료", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 자동 로드 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 시간 범위로 로그 내용 필터링
        /// ?? 타임스탬프 형식: [dd-MM-yyyy HH:mm:ss] 또는 [dd-MM-yyyy HH:mm:ss.fff] (밀리초 지원)
        /// </summary>
        private string FilterLogByTimeRange(string content, TimeSpan fromTime, TimeSpan toTime)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            try
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var filteredLines = new List<string>();
                bool lastLineWasInRange = false; // 마지막 라인이 범위 안에 있었는지 추적

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        // 빈 라인은 이전 라인이 범위 안에 있었을 때만 포함
                        if (lastLineWasInRange)
                        {
                            filteredLines.Add(line);
                        }
                        continue;
                    }

                    // 타임스탬프 추출: 2가지 형식 지원
                    // 1) 구형: [dd-MM-yyyy HH:mm:ss]
                    // 2) 신형: [dd-MM-yyyy HH:mm:ss.fff] (밀리초 포함)
                    var timeMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[\d{2}-\d{2}-\d{4} (\d{2}):(\d{2}):(\d{2})(?:\.(\d{3}))?\]");
                    
                    if (timeMatch.Success)
                    {
                        // 타임스탬프가 있는 라인
                        int hours = int.Parse(timeMatch.Groups[1].Value);
                        int minutes = int.Parse(timeMatch.Groups[2].Value);
                        int seconds = int.Parse(timeMatch.Groups[3].Value);
                        
                        // 밀리초 처리 (있으면 포함, 없으면 0)
                        int milliseconds = 0;
                        if (timeMatch.Groups[4].Success)
                        {
                            milliseconds = int.Parse(timeMatch.Groups[4].Value);
                        }
                        
                        // 밀리초까지 포함한 정확한 시간 생성
                        var logTime = new TimeSpan(0, hours, minutes, seconds, milliseconds);

                        // 시간 범위 체크 (밀리초 단위까지 정확히 비교)
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
                        // 타임스탬프가 없는 라인 (연속 로그 라인)
                        // 이전 라인이 범위 안에 있었을 때만 포함
                        if (lastLineWasInRange)
                        {
                            filteredLines.Add(line);
                        }
                    }
                }

                string result = string.Join("\n", filteredLines);
                
                _workLogService.AddLog($"Time filtering: {lines.Length} -> {filteredLines.Count} lines (range: {fromTime:hh\\:mm\\:ss}~{toTime:hh\\:mm\\:ss})", WorkLogType.Info);
                
                return result;
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"Time filtering error: {ex.Message}", WorkLogType.Error);
                return content; // 오류 시 원본 반환
            }
        }

        /// <summary>
        /// 행 번호 생성
        /// </summary>
        private void GenerateLineNumbers(string content, WpfTextBox lineNumberBox)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var lineNumbers = new StringBuilder();
            
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
                // EVENT 로그는 특별 처리 - 개선된 파서 사용
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
                    
                    // 타임스탬프 추출
                    string timestamp = ExtractTimestampFromLine(line);
                    
                    // 비즈니스명 추출
                    string businessName = ExtractBusinessNameFromSimpleLine(line, logType);
                    
                    // 내용 (타임스탬프 제거)
                    string cleanContent = RemoveTimestampFromLine(line);
                    
                    var logItem = new LogLineItem(i + 1, timestamp, businessName, "", "", cleanContent)
                    {
                        LogLevel = logType
                    };
                    
                    result.Add(logItem);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? {logType} 단순 파싱 오류: {ex.Message}", WorkLogType.Error);
                return result;
            }
        }

        /// <summary>
        /// EVENT 로그를 정교하게 처리하여 LogLineItem으로 변환 (SENDDATA 등의 블록 포함)
        /// ✅ 타임스탬프 기준으로 블록 단위로 파싱
        /// </summary>
        private List<LogLineItem> ConvertEventLogLines(string content)
        {
            var result = new List<LogLineItem>();
            if (string.IsNullOrWhiteSpace(content))
                return result;
            
            try
            {
                // ✅ 전체 로그를 타임스탬프 기준으로 블록 분리
                var timestampPattern = @"\[(\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?)\]";
                var matches = System.Text.RegularExpressions.Regex.Matches(content, timestampPattern);
                
                if (matches.Count == 0)
                {
                    _workLogService.AddLog("EVENT 로그에 타임스탬프가 없습니다.", WorkLogType.Warning);
                    return result;
                }
                
                int lineNumber = 1;
                
                for (int i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    string timestamp = FormatTimestamp(match.Groups[1].Value);
                    
                    // 현재 타임스탬프부터 다음 타임스탬프 전까지의 내용 추출
                    int startIndex = match.Index;
                    int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : content.Length;
                    
                    string blockContent = content.Substring(startIndex, endIndex - startIndex).Trim();
                    
                    // 타임스탬프 제거한 실제 내용
                    string contentWithoutTimestamp = blockContent.Substring(match.Length).Trim();
                    
                    if (string.IsNullOrWhiteSpace(contentWithoutTimestamp))
                        continue;
                    
                    // ✅ 전체 블록을 하나의 로그로 처리
                    var logItem = ProcessEventLogBlock(contentWithoutTimestamp, timestamp, lineNumber);
                    if (logItem != null)
                    {
                        logItem.ExtractEventFields(logItem.Content);
                        result.Add(logItem);
                        lineNumber++;
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"EVENT log parsing error: {ex.Message}", WorkLogType.Error);
                return result;
            }
        }

        /// <summary>
        /// 단일 EVENT 로그 블록을 처리하여 LogLineItem으로 변환
        /// </summary>
        private LogLineItem ProcessEventLogBlock(string content, string timestamp, int lineNumber)
        {
            try
            {
                // 기본값 설정
                string msgId = "";
                string procId = "";
                string businessName = "EVENT";
                
                // ✅ ZPL 데이터 패턴 우선 확인 (^XA 포함)
                if (content.Contains("^XA", StringComparison.OrdinalIgnoreCase))
                {
                    msgId = "ZPL";
                    businessName = "ZPL";
                    
                    return new LogLineItem
                    {
                        LineNumber = lineNumber,
                        Timestamp = timestamp,
                        MsgId = msgId,
                        ProcId = procId,
                        BusinessName = businessName,
                        Content = content,
                        LogLevel = "EVENT"
                    };
                }
                
                // SENDDATA/RECVDATA 블록인지 확인
                if (content.Contains("[SENDDATA]") || content.Contains("[RECVDATA]"))
                {
                    string blockType = content.Contains("[RECVDATA]") ? "RECVDATA" : "SENDDATA";
                    
                    // 블록에서 MsgId, ProcId 추출 (패턴: DYNAMIC.EVENT.REQUEST: 9000)
                    var dynamicPattern = @"DYNAMIC\.EVENT\.(REQUEST|RESPONSE)\s*[:\-]?\s*(\d+)";
                    var dynamicMatch = System.Text.RegularExpressions.Regex.Match(content, dynamicPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (dynamicMatch.Success)
                    {
                        msgId = dynamicMatch.Groups[2].Value;
                        businessName = $"PROC_{msgId}";
                    }
                }
                else
                {
                    // 일반 로그 아이템으로 처리
                    var defaultItem = CreateDefaultEventLogItem(content, lineNumber);
                    if (defaultItem != null)
                    {
                        msgId = defaultItem.MsgId;
                        procId = defaultItem.ProcId;
                        businessName = defaultItem.BusinessName;
                        content = defaultItem.Content;
                    }
                }
                
                return new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = timestamp,
                    MsgId = msgId,
                    ProcId = procId,
                    BusinessName = businessName,
                    Content = content,
                    LogLevel = "EVENT"
                };
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"EVENT 로그 블록 처리 오류: {ex.Message}", WorkLogType.Error);
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
                // _workLogService.AddLog($"{blockType} 블록 처리 시작: {startLine.Substring(0, Math.Min(startLine.Length, 100))}...", WorkLogType.Info);
                // 전체 블록 수집
                var contentBuilder = new StringBuilder();
                contentBuilder.AppendLine(startLine);
                int i = startIndex + 1;
                int braceCount = 0;
                bool blockEnded = false;
                // 시작 라인에서 중괄호 개수 계산
                foreach (char c in startLine)
                {
                    if (c == '{') braceCount++;
                    else if (c == '}') braceCount--;
                }
                // _workLogService.AddLog($"시작 라인 중괄호 개수: {braceCount}", WorkLogType.Info);
                // 중괄호가 시작 라인에 없다면 1로 설정
                if (braceCount == 0 && (startLine.Contains("DYNAMIC.EVENT.REQUEST") || startLine.Contains("DYNAMIC.EVENT.RESPONSE")))
                {
                    braceCount = 1;
                    // _workLogService.AddLog("중괄호 카운트를 1로 초기화", WorkLogType.Info);
                }
                while (i < lines.Length && !blockEnded)
                {
                    string nextLine = lines[i];
                    contentBuilder.AppendLine(nextLine);
                    // 중괄호 개수로 블록 끝 감지
                    foreach (char c in nextLine)
                    {
                        if (c == '{') braceCount++;
                        else if (c == '}') braceCount--;
                    }
                    // 디버깅: 몇 개 라인은 로그로 출랙
                    // if (i - startIndex < 10)
                    // {
                    //     _workLogService.AddLog($"라인 {i - startIndex + 1}: {nextLine.Trim()} (중괄호: {braceCount})", WorkLogType.Info);
                    // }
                    if (braceCount <= 0)
                    {
                        blockEnded = true;
                        // _workLogService.AddLog($"블록 종료 감지: 중괄호 개수 {braceCount}", WorkLogType.Info);
                    }
                    i++;
                    // 무한 루프 방지: 100라인 이상이면 강제 종료
                    if (i - startIndex > 100)
                    {
                        // _workLogService.AddLog("블록이 너무 길어서 강제 종료", WorkLogType.Warning);
                        blockEnded = true;
                    }
                }
                string fullContent = contentBuilder.ToString();
                // _workLogService.AddLog($"? {blockType} 블록 완료: {i - startIndex}개 라인, 총 {fullContent.Length}자", WorkLogType.Info);
                // 데이터 추출
                string msgId = ExtractMsgIdFromContent(fullContent);
                string procId = ExtractProcIdFromContent(fullContent);
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
                // _workLogService.AddLog($"? {blockType} 아이템 생성: MsgId={msgId}, ProcId={procId}", WorkLogType.Success);
                return (eventItem, i - 1); // 다음 처리할 인덱스 반환
            }
            catch (Exception ex)
            {
                // _workLogService.AddLog($"? {blockType} 블록 처리 오류: {ex.Message}", WorkLogType.Error);
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
                
                // Content에 ":"만 있으면 제외
                if (string.IsNullOrWhiteSpace(content) || content.Trim() == ":")
                {
                    return null;
                }
                
                // 자동 MsgID 할당 및 Content 정리
                string msgId = "";
                string cleanedContent = content;
                
                // USBLampOnOff 패턴
                if (content.Contains("USBLampOnOff", StringComparison.OrdinalIgnoreCase))
                {
                    msgId = "USBLamp";
                }
                // Click 패턴
                else if (content.Contains("Click", StringComparison.OrdinalIgnoreCase))
                {
                    msgId = "Click";
                }
                // Scanner 패턴
                else if (content.Contains("FrameOperation_ScannerData_ReceivedEvent", StringComparison.OrdinalIgnoreCase))
                {
                    msgId = "SCAN";
                    
                    // Scanner 데이터 추출: [COM3 /5I2S009S4H140] -> COM3 / /5I2S009S4H140
                    var scannerMatch = System.Text.RegularExpressions.Regex.Match(content, @".*-\s*\[([^\]]+)\]");
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
        /// EVENT 내용에서 ITEM 추출하고 간결하게 포맷
        /// </summary>
        private string ExtractItemContent(string content)
        {
            try
            {
                var items = new List<string>();

                // SENDDATA/RECVDATA 블록 파싱 및 표시
                var blockPattern = @"\[(SENDDATA|RECVDATA)\][\s\S]*?\}";
                var blockMatches = System.Text.RegularExpressions.Regex.Matches(content, blockPattern);
                foreach (System.Text.RegularExpressions.Match blockMatch in blockMatches)
                {
                    string blockType = blockMatch.Groups[1].Value;
                    items.Add($"[{blockType}]");

                    // ITEM 추출: [번호, 번호={<NAME=...> <VALUE=...>}] 형식
                    var itemPattern = @"\[(\d+),\s*\d+=\{([^}]*)\}\]";
                    var matches = System.Text.RegularExpressions.Regex.Matches(blockMatch.Value, itemPattern);
                    int itemNum = 1;
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        string itemIndex = itemNum.ToString();
                        string itemData = match.Groups[2].Value.Trim();
                        var nameMatch = System.Text.RegularExpressions.Regex.Match(itemData, @"<NAME=([^>]+)>");
                        var valueMatch = System.Text.RegularExpressions.Regex.Match(itemData, @"<VALUE=([^>]*)>");
                        string name = nameMatch.Success ? nameMatch.Groups[1].Value : $"ITEM_{itemIndex}";
                        string value = valueMatch.Success ? valueMatch.Groups[1].Value : "(empty)";
                        if (string.IsNullOrEmpty(value)) value = "(empty)";
                        items.Add($"[{itemIndex}] {name} = {value}");
                        itemNum++;
                    }
                }

                // 기존 ITEM 추출 로직 (패턴1, 패턴2, NAME-VALUE)
                var itemPattern1 = @"\[(\d+),\s*\d+=\{([^}]*)\}\]";
                var matches1 = System.Text.RegularExpressions.Regex.Matches(content, itemPattern1);
                foreach (System.Text.RegularExpressions.Match match in matches1)
                {
                    string itemIndex = match.Groups[1].Value;
                    string itemData = match.Groups[2].Value.Trim();
                    string formattedItem = FormatItemData(itemIndex, itemData);
                    if (!string.IsNullOrEmpty(formattedItem))
                    {
                        items.Add(formattedItem);
                    }
                }
                var itemPattern2 = @"ITEM\[(\d+)\]\s*=\s*(.+)";
                var matches2 = System.Text.RegularExpressions.Regex.Matches(content, itemPattern2);
                foreach (System.Text.RegularExpressions.Match match in matches2)
                {
                    string itemIndex = match.Groups[1].Value;
                    string itemData = match.Groups[2].Value.Trim();
                    items.Add($"[{itemIndex}] {itemData}");
                }
                var nameValuePattern = @"<NAME=([^>]+)>\s*<VALUE=([^>]*)>";
                var nameValueMatches = System.Text.RegularExpressions.Regex.Matches(content, nameValuePattern);
                int index = 1;
                foreach (System.Text.RegularExpressions.Match match in nameValueMatches)
                {
                    string name = match.Groups[1].Value;
                    string value = match.Groups[2].Value;
                    if (string.IsNullOrEmpty(value)) value = "(empty)";
                    items.Add($"[{index}] {name} = {value}");
                    index++;
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
        /// ITEM 데이터를 포맷팅하는 헬퍼 메서드
        /// </summary>
        private string FormatItemData(string itemIndex, string itemData)
        {
            try
            {
                // NAME과 VALUE 추출
                var nameMatch = System.Text.RegularExpressions.Regex.Match(itemData, @"<NAME=([^>]+)>");
                var valueMatch = System.Text.RegularExpressions.Regex.Match(itemData, @"<VALUE=([^>]*)>");
                
                if (nameMatch.Success)
                {
                    string name = nameMatch.Groups[1].Value;
                    string value = valueMatch.Success ? valueMatch.Groups[1].Value : "(empty)";
                    
                    if (string.IsNullOrEmpty(value))
                        value = "(empty)";
                    
                    return $"[{itemIndex}] {name} = {value}";
                }
                
                // NAME 패턴이 없으면 전체 데이터를 정리해서 사용
                string cleanData = itemData
                    .Replace("<NAME=", "")
                    .Replace("<VALUE=", "=")
                    .Replace(">", "")
                    .Trim();
                    
                return $"[{itemIndex}] {cleanData}";
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? ITEM 데이터 포맷 오류: {ex.Message}", WorkLogType.Error);
                return $"[{itemIndex}] {itemData}";
            }
        }

        /// <summary>
        /// EVENT 내용에서 MSGID 추출
        /// </summary>
        private string ExtractMsgIdFromContent(string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content))
                    return "";

                // 1) 기존 태그 형식 우선
                var match = System.Text.RegularExpressions.Regex.Match(content, @"<MSGID=(\d+)>");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }

                // 2) DYNAMIC_EVENT_DATA 혹은 DYNAMIC.EVENT.DATA 같은 패턴에서 숫자 추출
                var dynMatch = System.Text.RegularExpressions.Regex.Match(content, @"DYNAMIC[_\.\s]?EVENT(?:_DATA|\.DATA|\sDATA)?\s*[:\-]?\s*(\d{3,5})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (dynMatch.Success)
                {
                    return dynMatch.Groups[1].Value;
                }

                // 3) 'DYNAMIC_EVENT_DATA 1300' 같은 언더스코어 형식
                var dyn2 = System.Text.RegularExpressions.Regex.Match(content, @"DYNAMIC_EVENT_DATA\s*(\d{3,5})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (dyn2.Success)
                {
                    return dyn2.Groups[1].Value;
                }

                // 4) 콜론으로 구분된 형태에서 숫자 추출 '... : 1300 :'
                var colonMatch = System.Text.RegularExpressions.Regex.Match(content, @":\s*(\d{3,5})\s*:");
                if (colonMatch.Success)
                {
                    return colonMatch.Groups[1].Value;
                }

                // 5) 블록 내부에 독립적인 숫자 라인이 있는 경우 (예: newline 후 바로 1100)
                var lineMatch = System.Text.RegularExpressions.Regex.Match(content, @"(?m)^\s*(\d{3,5})\s*$");
                if (lineMatch.Success)
                {
                    return lineMatch.Groups[1].Value;
                }

                // 6) 임의의 위치에 있는 긴 숫자 토큰(예: 9000, 1300 등)
                var any = System.Text.RegularExpressions.Regex.Match(content, @"\b(\d{3,5})\b");
                if (any.Success)
                {
                    return any.Groups[1].Value;
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// EVENT 내용에서 PROCID 추출
        /// </summary>
        private string ExtractProcIdFromContent(string content)
        {
            try
            {
                var match = System.Text.RegularExpressions.Regex.Match(content, @"<PROCID=([^>]+)>");
                if (match.Success)
                {
                    string procId = match.Groups[1].Value;
                    //_workLogService.AddLog($"PROCID 추출: {procId}", WorkLogType.Info);
                    return procId;
                }
                return "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 타임스탬프 형식 변환
        /// ?? 밀리초 포함 시 보존: HH:mm:ss.fff
        /// </summary>
        private string FormatTimestamp(string timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp))
                return "";

            try
            {
                // 밀리초 포함 형식: dd-MM-yyyy HH:mm:ss.fff
                if (DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss.fff",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime parsedWithMs))
                {
                    return parsedWithMs.ToString("HH:mm:ss.fff");
                }

                // 밀리초 없는 형식: dd-MM-yyyy HH:mm:ss
                if (DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    return parsedDate.ToString("HH:mm:ss");
                }

                // 일반적인 파싱 시도
                if (DateTime.TryParse(timestamp, out DateTime parsedDate2))
                {
                    // 밀리초가 포함되어 있으면 보존
                    if (timestamp.Contains("."))
                        return parsedDate2.ToString("HH:mm:ss.fff");
                    else
                        return parsedDate2.ToString("HH:mm:ss");
                }

                // 실패하면 문자열에서 HH:mm:ss 또는 HH:mm:ss.fff 패턴 추출
                var mWithMs = System.Text.RegularExpressions.Regex.Match(timestamp, @"(\d{2}:\d{2}:\d{2}\.\d{3})");
                if (mWithMs.Success)
                    return mWithMs.Groups[1].Value;

                var m = System.Text.RegularExpressions.Regex.Match(timestamp, @"(\d{2}:\d{2}:\d{2})");
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
                // 밀리초 포함 여부를 모두 처리
                var match = System.Text.RegularExpressions.Regex.Match(line, @"\[(\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?)\]");
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
                // 밀리초 포함 여부를 모두 처리
                var match = System.Text.RegularExpressions.Regex.Match(line, @"\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?\]\s*(.*)");
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

        /// <summary>
        /// ImageList : 블록 처리 (ONBOARD FLASH까지)
        /// </summary>
        private (LogLineItem?, int) ProcessImageListBlock(string[] lines, int startIndex, int lineNumber)
        {
            try
            {
                string startLine = lines[startIndex];
                string timestamp = ExtractTimestampFromLine(startLine);
                
                var contentBuilder = new StringBuilder();
                contentBuilder.AppendLine(RemoveTimestampFromLine(startLine));
                
                int i = startIndex + 1;
                bool blockEnded = false;
                
                // ONBOARD FLASH를 찾을 때까지 계속 읽기
                while (i < lines.Length && !blockEnded)
                {
                    string nextLine = lines[i];
                    string cleanedLine = RemoveTimestampFromLine(nextLine);
                    
                    // 현재 라인을 먼저 추가
                    contentBuilder.AppendLine(cleanedLine);
                    
                    // ONBOARD FLASH를 찾으면 블록 종료
                    if (cleanedLine.Contains("ONBOARD FLASH", StringComparison.OrdinalIgnoreCase))
                    {
                        blockEnded = true;
                        // 다음 처리할 인덱스는 현재 라인 다음
                        i++;
                        break;
                    }
                    
                    i++;
                    
                    // 무한 루프 방지: 100라인 이상이면 강제 종료
                    if (i - startIndex > 100)
                    {
                        _workLogService.AddLog($"?? ImageList 블록이 너무 길어서 강제 종료: {i - startIndex}라인", WorkLogType.Warning);
                        blockEnded = true;
                    }
                }
                
                string fullContent = contentBuilder.ToString().TrimEnd();
                
                var imageListItem = new LogLineItem
                {
                    LineNumber = lineNumber,
                    Timestamp = FormatTimestamp(timestamp),
                    MsgId = "ImageList",
                    ProcId = "",
                    BusinessName = "ImageList",
                    Content = fullContent,
                    LogLevel = "EVENT"
                };
                
                _workLogService.AddLog($"? ImageList 블록 처리 완료: {i - startIndex}라인, 다음 인덱스: {i}", WorkLogType.Info);
                
                return (imageListItem, i - 1); // 다음 처리할 인덱스 반환 (0-based이므로 -1)
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? ImageList 블록 처리 오류: {ex.Message}", WorkLogType.Error);
                return (null, startIndex);
            }
        }

        #endregion
    }
}
