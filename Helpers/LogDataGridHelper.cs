using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Services;

namespace FACTOVA_LogAnalysis.Helpers
{
    public static class LogDataGridHelper
    {
        /// <summary>
        /// 로그 내용을 LogLineItem 리스트로 변환 (ExecuteService 세션별 처리)
        /// </summary>
        public static List<LogLineItem> ConvertToLogLines(string content)
        {
            var result = new List<LogLineItem>();
            
            if (string.IsNullOrWhiteSpace(content))
                return result;

            try
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                
                int currentLineNumber = 1;
                int sessionStartLine = 1;
                
                // ExecuteService 세션 데이터를 저장할 변수들
                string sessionTimestamp = "";
                string sessionBusinessName = "";
                string sessionExecTime = "";
                string sessionTxnId = "";
                var sessionContentLines = new List<string>();
                
                bool inExecuteSession = false;
                bool collectingNewDataSet = false;
                int newDataSetDepth = 0;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string currentLine = lines[i].Trim();
                    
                    // ExecuteService 시작 감지
                    if (currentLine.Contains("ExecuteService():"))
                    {
                        // 이전 세션이 있다면 완료 처리
                        if (inExecuteSession)
                        {
                            ProcessExecuteSession(result, sessionStartLine, sessionTimestamp, 
                                sessionBusinessName, sessionExecTime, sessionTxnId, sessionContentLines);
                        }
                        
                        // 새 세션 시작
                        sessionStartLine = currentLineNumber;
                        sessionTimestamp = ExtractTimestamp(currentLine);
                        sessionBusinessName = ExtractBusinessNameFromExecuteService(currentLine);
                        sessionExecTime = "";
                        sessionTxnId = "";
                        sessionContentLines.Clear();
                        
                        inExecuteSession = true;
                        collectingNewDataSet = false;
                        newDataSetDepth = 0;
                    }
                    // exec.Time 라인 처리
                    else if (inExecuteSession && currentLine.Contains("exec.Time"))
                    {
                        sessionExecTime = ExtractExecTimeValue(currentLine);
                    }
                    // TXN_ID 라인 처리  
                    else if (inExecuteSession && currentLine.Contains("TXN_ID"))
                    {
                        sessionTxnId = ExtractTxnIdValue(currentLine);
                    }
                    // Parameter : 라인은 무시
                    else if (inExecuteSession && currentLine.Contains("Parameter"))
                    {
                        // 무시
                    }
                    // NewDataSet 수집 시작
                    else if (inExecuteSession && currentLine.Contains("<NewDataSet>"))
                    {
                        collectingNewDataSet = true;
                        newDataSetDepth = 1;
                        sessionContentLines.Add(currentLine);
                    }
                    // NewDataSet 수집 중
                    else if (collectingNewDataSet)
                    {
                        sessionContentLines.Add(currentLine);
                        
                        // 태그 깊이 계산
                        newDataSetDepth += CountOpenTags(currentLine) - CountCloseTags(currentLine);
                        
                        // NewDataSet 완료 감지
                        if (currentLine.Contains("</NewDataSet>") && newDataSetDepth <= 0)
                        {
                            collectingNewDataSet = false;
                        }
                    }
                    // 빈 라인이거나 다른 내용 - 세션 종료 가능성
                    else if (inExecuteSession && string.IsNullOrWhiteSpace(currentLine))
                    {
                        // 다음 라인이 ExecuteService이거나 파일 끝이면 세션 완료
                        if (i + 1 >= lines.Length || lines[i + 1].Contains("ExecuteService():"))
                        {
                            ProcessExecuteSession(result, sessionStartLine, sessionTimestamp, 
                                sessionBusinessName, sessionExecTime, sessionTxnId, sessionContentLines);
                            inExecuteSession = false;
                        }
                    }
                    
                    currentLineNumber++;
                }
                
                // 마지막 세션 처리
                if (inExecuteSession)
                {
                    ProcessExecuteSession(result, sessionStartLine, sessionTimestamp, 
                        sessionBusinessName, sessionExecTime, sessionTxnId, sessionContentLines);
                }
                
                System.Diagnostics.Debug.WriteLine($"ConvertToLogLines 완료: {result.Count}개 세션 생성");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConvertToLogLines 오류: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// ExecuteService 세션을 하나의 LogLineItem으로 처리
        /// </summary>
        private static void ProcessExecuteSession(List<LogLineItem> result, int lineNumber, 
            string timestamp, string businessName, string execTime, string txnId, List<string> contentLines)
        {
            try
            {
                // Content 조합 (NewDataSet XML 포매팅)
                string content = "";
                if (contentLines.Count > 0)
                {
                    string rawContent = string.Join("\n", contentLines).Trim();
                    content = FormatXmlContent(rawContent);
                }
                
                // TimeStamp 형식 변환 (dd-MM-yyyy HH:mm:ss → yyyy-MM-dd HH:mm:ss)
                string formattedTimestamp = FormatTimestamp(timestamp);
                
                // LogLineItem 생성 (직접 값 설정)
                var logItem = new LogLineItem(lineNumber, formattedTimestamp, businessName, execTime, txnId, content);
                
                result.Add(logItem);
                
                System.Diagnostics.Debug.WriteLine($"세션 처리: Line {lineNumber}, Business: {businessName}, ExecTime: {execTime}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessExecuteSession 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// TimeStamp를 HH:mm:ss 형식으로 변환
        /// ? 밀리초 포함 시 보존: HH:mm:ss.fff
        /// </summary>
        private static string FormatTimestamp(string timestamp)
        {
            if (string.IsNullOrWhiteSpace(timestamp))
                return "";

            try
            {
                // ? 이미 HH:mm:ss 또는 HH:mm:ss.fff 형식이면 그대로 반환
                var alreadyFormatted = Regex.Match(timestamp, @"^\d{2}:\d{2}:\d{2}(?:\.\d{1,3})?$");
                if (alreadyFormatted.Success)
                {
                    return timestamp;  // ? 이미 변환된 형식이므로 그대로 반환
                }

                // 밀리초 포함 형식: dd-MM-yyyy HH:mm:ss.fff
                if (DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss.fff", 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.None, out DateTime parsedWithMs))
                {
                    return parsedWithMs.ToString("HH:mm:ss.fff");
                }

                // 기본 형식: dd-MM-yyyy HH:mm:ss (밀리초 없음)
                if (DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss", 
                    System.Globalization.CultureInfo.InvariantCulture, 
                    System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    return parsedDate.ToString("HH:mm:ss");
                }

                // 다른 형식도 시도
                if (DateTime.TryParse(timestamp, out DateTime parsedDate2))
                {
                    // 밀리초가 포함되어 있으면 유지
                    if (timestamp.Contains("."))
                        return parsedDate2.ToString("HH:mm:ss.fff");
                    else
                        return parsedDate2.ToString("HH:mm:ss");
               }

                return timestamp; // 파싱 실패 시 원본 반환
            }
            catch
            {
                return timestamp;
            }
        }

        /// <summary>
        /// XML Content를 들여쓰기 형태로 포매팅
        /// </summary>
        private static string FormatXmlContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            try
            {
                // NewDataSet이 포함된 경우 XML 포매팅 시도
                if (content.Contains("<NewDataSet>") && content.Contains("</NewDataSet>"))
                {
                    // XML 파싱 및 포매팅
                    var doc = System.Xml.Linq.XDocument.Parse(content);
                    
                    using (var stringWriter = new System.IO.StringWriter())
                    {
                        using (var xmlWriter = System.Xml.XmlWriter.Create(stringWriter, new System.Xml.XmlWriterSettings
                        {
                            Indent = true,
                            
                            IndentChars = "  ", // 2칸 들여쓰기
                            NewLineChars = "\n",
                            NewLineHandling = System.Xml.NewLineHandling.Replace,
                            OmitXmlDeclaration = true
                        }))
                        {
                            doc.WriteTo(xmlWriter);
                        }
                        return stringWriter.ToString();
                    }
                }
                
                // NewDataSet이 없으면 원본 반환
                return content;
            }
            catch (Exception)
            {
                // XML 파sing 실패 시 원본 반환 (한 줄로)
                return System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ").Trim();
            }
        }

        /// <summary>
        /// 타임스탬프 추출
        /// ?? 지원 형식: [dd-MM-yyyy HH:mm:ss] 또는 [dd-MM-yyyy HH:mm:ss.fff]
        /// </summary>
        private static string ExtractTimestamp(string line)
        {
            // 밀리초 포함 여부를 모두 처리
            var match = Regex.Match(line, @"\[(\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?)\]");
            return match.Success ? match.Groups[1].Value : "";
        }

        /// <summary>
        /// ExecuteService에서 비즈니스명 추출
        /// </summary>
        private static string ExtractBusinessNameFromExecuteService(string line)
        {
            var match = Regex.Match(line, @"ExecuteService\(\)\s*:\s*\[\s*([^\]]+)\s*\]", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        /// <summary>
        /// exec.Time 값 추출
        /// </summary>
        private static string ExtractExecTimeValue(string line)
        {
            var match = Regex.Match(line, @"exec\.Time\s*:\s*([0-9:\.]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        /// <summary>
        /// TXN_ID 값 추출
        /// </summary>
        private static string ExtractTxnIdValue(string line)
        {
            var match = Regex.Match(line, @"TXN_ID\s*:\s*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        /// <summary>
        /// XML 태그 개수 계산 (열기)
        /// </summary>
        private static int CountOpenTags(string line)
        {
            return Regex.Matches(line, @"<[^/][^>]*>").Count;
        }

        /// <summary>
        /// XML 태그 개수 계산 (닫기)
        /// </summary>
        private static int CountCloseTags(string line)
        {
            return Regex.Matches(line, @"</[^>]+>").Count;
        }

        /// <summary>
        /// 선택된 행들을 클립보드에 복사 (Content만, Line 제외)
        /// </summary>
        public static void CopySelectedRows(DataGrid dataGrid)
        {
            try
            {
                var selectedItems = dataGrid.SelectedItems.Cast<LogLineItem>().OrderBy(x => x.LineNumber);
                
                if (!selectedItems.Any())
                {
                    System.Windows.Clipboard.SetText("선택된 행이 없습니다.");
                    return;
                }

                var copyText = new StringBuilder();
                
                foreach (var item in selectedItems)
                {
                    // Content만 복사 (Line 번호는 제외)
                    copyText.AppendLine(item.CopyText);
                }
                
                // 마지막 줄바꿈 제거
                string finalText = copyText.ToString().TrimEnd();
                
                System.Windows.Clipboard.SetText(finalText);
                
                System.Diagnostics.Debug.WriteLine($"복사 완료: {selectedItems.Count()}개 행 (Content만)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CopySelectedRows 오류: {ex.Message}");
                System.Windows.Clipboard.SetText($"복사 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 모든 행 선택
        /// </summary>
        public static void SelectAll(DataGrid dataGrid)
        {
            try
            {
                dataGrid.SelectAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SelectAll 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 선택 해제
        /// </summary>
        public static void ClearSelection(DataGrid dataGrid)
        {
            try
            {
                dataGrid.UnselectAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearSelection 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 선택된 행 개수 반환
        /// </summary>
        public static int GetSelectedCount(DataGrid dataGrid)
        {
            try
            {
                return dataGrid.SelectedItems.Count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 특정 라인으로 스크롤
        /// </summary>
        public static void ScrollToLine(DataGrid dataGrid, int lineNumber)
        {
            try
            {
                if (dataGrid.ItemsSource is ObservableCollection<LogLineItem> items)
                {
                    var targetItem = items.FirstOrDefault(x => x.LineNumber == lineNumber);
                    if (targetItem != null)
                    {
                        dataGrid.ScrollIntoView(targetItem);
                        dataGrid.SelectedItem = targetItem;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScrollToLine 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 키워드로 검색하여 스크롤
        /// </summary>
        public static void FindAndScrollToKeyword(DataGrid dataGrid, ObservableCollection<LogLineItem> items, string keyword)
        {
            try
            {
                var foundItem = items.FirstOrDefault(x => 
                    x.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    x.BusinessName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                
                if (foundItem != null)
                {
                    dataGrid.ScrollIntoView(foundItem);
                    dataGrid.SelectedItem = foundItem;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FindAndScrollToKeyword 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 빨간색 비즈니스 하이라이트 적용
        /// </summary>
        public static void ApplyRedBusinessHighlight(ObservableCollection<LogLineItem> items, RedBusinessListManager redBusinessManager)
        {
            try
            {
                var redBusinessNames = redBusinessManager.GetEnabledBusinessNames();
                
                foreach (var item in items)
                {
                    bool isRedBusiness = redBusinessNames.Any(businessName => 
                        item.BusinessName.Contains(businessName, StringComparison.OrdinalIgnoreCase));
                    
                    item.IsRedBusiness = isRedBusiness;
                    if (isRedBusiness)
                    {
                        item.ApplyRedBusinessHighlight();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyRedBusinessHighlight 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 검색 결과 하이라이트
        /// </summary>
        public static void HighlightSearchResults(ObservableCollection<LogLineItem> items, string searchText)
        {
            try
            {
                foreach (var item in items)
                {
                    bool matches = item.Content.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                  item.BusinessName.Contains(searchText, StringComparison.OrdinalIgnoreCase);
                    
                    item.IsHighlighted = matches;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HighlightSearchResults 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 하이라이트 초기화
        /// </summary>
        public static void ClearHighlights(ObservableCollection<LogLineItem> items)
        {
            try
            {
                foreach (var item in items)
                {
                    item.IsHighlighted = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearHighlights 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 필터 적용
        /// </summary>
        public static void ApplyFilters(DataGrid dataGrid, ObservableCollection<LogLineItem> items, 
            string logLevel, string businessName, string keyword)
        {
            try
            {
                var view = CollectionViewSource.GetDefaultView(items);
                
                view.Filter = item =>
                {
                    if (item is not LogLineItem logItem) return false;
                    
                    // 로그 레벨 필터
                    if (logLevel != "ALL" && logItem.LogLevel != logLevel)
                        return false;
                    
                    // 비즈니스명 필터
                    if (businessName != "ALL" && !logItem.BusinessName.Contains(businessName, StringComparison.OrdinalIgnoreCase))
                        return false;
                    
                    // 키워드 필터
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        bool containsKeyword = logItem.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                              logItem.BusinessName.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                        if (!containsKeyword)
                            return false;
                    }
                    
                    return true;
                };
                
                view.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyFilters 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// MsgId 필터 적용
        /// </summary>
        public static void ApplyMsgIdFilter(DataGrid dataGrid, ObservableCollection<LogLineItem> items, 
            string msgId, string keyword)
        {
            try
            {
                var view = CollectionViewSource.GetDefaultView(items);
                
                view.Filter = item =>
                {
                    if (item is not LogLineItem logItem) return false;
                    
                    // MsgId 필터
                    if (msgId != "ALL" && logItem.MsgId != msgId)
                        return false;
                    
                    // 키워드 필터
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        bool containsKeyword = logItem.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                              logItem.BusinessName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                              logItem.MsgId.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                        if (!containsKeyword)
                            return false;
                    }
                    
                    return true;
                };
                
                view.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ApplyMsgIdFilter 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 유니크한 비즈니스명 목록 반환
        /// </summary>
        public static List<string> GetUniqueBusinessNames(ObservableCollection<LogLineItem> items)
        {
            try
            {
                return items
                    .Where(x => !string.IsNullOrWhiteSpace(x.BusinessName))
                    .Select(x => x.BusinessName)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUniqueBusinessNames 오류: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// 유니크한 MsgId 목록 반환
        /// </summary>
        public static List<string> GetUniqueMsgIds(ObservableCollection<LogLineItem> items)
        {
            try
            {
                return items
                    .Where(x => !string.IsNullOrWhiteSpace(x.MsgId))
                    .Select(x => x.MsgId)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUniqueMsgIds 오류: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// DataGrid 기본 기능 설정
        /// </summary>
        public static void SetupDataGridFeatures(DataGrid dataGrid)
        {
            try
            {
                // 키보드 지원
                dataGrid.KeyDown += (sender, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.C && 
                        (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
                    {
                        CopySelectedRows(dataGrid);
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.A && 
                            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
                    {
                        SelectAll(dataGrid);
                        e.Handled = true;
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetupDataGridFeatures 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// EXCEPTION 로그를 전용 파싱 (ExecuteServiceSync 처리)
        /// </summary>
        public static List<LogLineItem> ConvertExceptionLogLines(string content)
        {
            var result = new List<LogLineItem>();
            
            if (string.IsNullOrWhiteSpace(content))
                return result;

            try
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                
                int currentLineNumber = 1;
                int sessionStartLine = 1;
                
                // ExecuteServiceSync 세션 데이터를 저장할 변수들
                string sessionTimestamp = "";
                string sessionBusinessName = "";
                string sessionErrorDesc = "";
                var sessionContentLines = new List<string>();
                
                bool inExceptionSession = false;
                bool collectingNewDataSet = false;
                bool collectingErrorDesc = false;
                int newDataSetDepth = 0;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string currentLine = lines[i].Trim();
                    
                    // ExecuteServiceSync Exception 시작 감지
                    if (currentLine.Contains("ExecuteServiceSync():") && currentLine.Contains("Exception"))
                    {
                        // 이전 세션이 있다면 완료 처리
                        if (inExceptionSession)
                        {
                            ProcessExceptionSession(result, sessionStartLine, sessionTimestamp, 
                                sessionBusinessName, sessionContentLines, sessionErrorDesc);
                        }
                        
                        // 새 세션 시작
                        sessionStartLine = currentLineNumber;
                        sessionTimestamp = ExtractTimestamp(currentLine);
                        sessionBusinessName = ExtractBusinessNameFromExceptionService(currentLine);
                        sessionErrorDesc = "";
                        sessionContentLines.Clear();
                        
                        inExceptionSession = true;
                        collectingNewDataSet = false;
                        collectingErrorDesc = false;
                        newDataSetDepth = 0;
                    }
                    // Parameter : 라인은 무시
                    else if (inExceptionSession && currentLine.Contains("Parameter"))
                    {
                        // 무시
                    }
                    // NewDataSet 수집 시작
                    else if ( inExceptionSession && currentLine.Contains("<NewDataSet>"))
                    {
                        collectingNewDataSet = true;
                        newDataSetDepth = 1;
                        sessionContentLines.Add(currentLine);
                    }
                    // NewDataSet 수집 중
                    else if (collectingNewDataSet)
                    {
                        sessionContentLines.Add(currentLine);
                        
                        // 태그 깊이 계산
                        newDataSetDepth += CountOpenTags(currentLine) - CountCloseTags(currentLine);
                        
                        // NewDataSet 완료 감지 - 이후부터 Error Description 수집 시작
                        if (currentLine.Contains("</NewDataSet>") && newDataSetDepth <= 0)
                        {
                            collectingNewDataSet = false;
                            collectingErrorDesc = true; // Error Description 수집 시작
                        }
                    }
                    // Error Description 수집 (</NewDataSet> 이후)
                    else if (collectingErrorDesc && !string.IsNullOrWhiteSpace(currentLine))
                    {
                        // : 로 시작하는 라인이 실제 에러 메시지
                        if (currentLine.StartsWith(":"))
                        {
                            sessionErrorDesc += currentLine.Substring(1).Trim() + " ";
                        }
                        // 위치: 로 시작하는 스택 트레이스
                        else if (currentLine.StartsWith("위치:"))
                        {
                            if (!string.IsNullOrEmpty(sessionErrorDesc)) sessionErrorDesc += "\n";
                            sessionErrorDesc += currentLine;
                        }
                        // 기타 에러 관련 정보
                        else
                        {
                            if (!string.IsNullOrEmpty(sessionErrorDesc)) sessionErrorDesc += " ";
                            sessionErrorDesc += currentLine;
                        }
                    }
                    // 빈 라인이거나 구분자 라인 - 세션 종료 가능성
                    else if (inExceptionSession && (string.IsNullOrWhiteSpace(currentLine) || currentLine.Contains("---")))
                    {
                        // 다음 라인이 ExecuteServiceSync이거나 파일 끝이면 세션 완료
                        if (i + 1 >= lines.Length || (i + 1 < lines.Length && lines[i + 1].Contains("ExecuteServiceSync():")))
                        {
                            ProcessExceptionSession(result, sessionStartLine, sessionTimestamp, 
                                sessionBusinessName, sessionContentLines, sessionErrorDesc);
                            inExceptionSession = false;
                        }
                    }
                    
                    currentLineNumber++;
                }
                
                // 마지막 세션 처리
                if (inExceptionSession)
                {
                    ProcessExceptionSession(result, sessionStartLine, sessionTimestamp, 
                        sessionBusinessName, sessionContentLines, sessionErrorDesc);
                }
                
                System.Diagnostics.Debug.WriteLine($"ConvertExceptionLogLines 완료: {result.Count}개 세션 생성");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConvertExceptionLogLines 오류: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// ExecuteServiceSync 세션을 하나의 LogLineItem으로 처리 (예외 로그 전용)
        /// </summary>
        private static void ProcessExceptionSession(List<LogLineItem> result, int lineNumber, 
            string timestamp, string businessName, List<string> contentLines, string errorDesc)
        {
            try
            {
                // Content 조합 (NewDataSet XML 포매팅)
                string content = "";
                if (contentLines.Count > 0)
                {
                    string rawContent = string.Join("\n", contentLines).Trim();
                    content = FormatXmlContent(rawContent);
                }
                
                // TimeStamp 형식 변환 (dd-MM-yyyy HH:mm:ss → yyyy-MM-dd HH:mm:ss)
                string formattedTimestamp = FormatTimestamp(timestamp);
                
                // Error Description 정리
                string cleanErrorDesc = string.IsNullOrWhiteSpace(errorDesc) ? "" : errorDesc.Trim();
                
                // LogLineItem 생성 (EXCEPTION 세션 - ExecTime와 TxnId는 빈 값)
                var logItem = new LogLineItem(lineNumber, formattedTimestamp, businessName, "", "", content)
                {
                    LogLevel = "EXCEPTION",
                    ErrorDescription = cleanErrorDesc
                };
                
                result.Add(logItem);
                
                System.Diagnostics.Debug.WriteLine($"예외 세션 처리: Line {lineNumber}, Business: {businessName}, Error: {cleanErrorDesc.Substring(0, Math.Min(50, cleanErrorDesc.Length))}...");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ProcessExceptionSession 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// ExecuteServiceSync에서 비즈니스명 추출
        /// </summary>
        private static string ExtractBusinessNameFromExceptionService(string line)
        {
            // [날짜] ExecuteServiceSync():[비즈니스명] Exception
            var match = Regex.Match(line, @"ExecuteServiceSync\(\)\s*:\s*\[\s*([^\]]+)\s*\]\s*Exception", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        /// <summary>
        /// 라인에서 타임스탬프 제거
        /// ?? 지원 형식: [dd-MM-yyyy HH:mm:ss] 또는 [dd-MM-yyyy HH:mm:ss.fff]
        /// </summary>
        private static string RemoveTimestampFromLine(string line)
        {
            try
            {
                // 밀리초 포함 여부를 모두 처리
                var match = Regex.Match(line, @"\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?\]\s*(.*)");
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
    }
}