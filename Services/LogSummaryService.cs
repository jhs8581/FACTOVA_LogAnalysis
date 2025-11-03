using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using FACTOVA_LogAnalysis.Models;

namespace FACTOVA_LogAnalysis.Services
{
    public class LogSummaryService
    {
        private static readonly Regex LogStartRegex = new Regex(@"^\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}\]", RegexOptions.Compiled | RegexOptions.Multiline);

        public void GenerateExecuteServiceSummaryForDataGrid(string originalLogContent, string cleanedLogContent, 
            ObservableCollection<LogSummaryItem> summaryItems)
        {
            summaryItems.Clear();
            var matches = LogStartRegex.Matches(originalLogContent);
            var cleanedLines = cleanedLogContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            
            for (int i = 0; i < matches.Count; i++)
            {
                int startIndex = matches[i].Index;
                int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : originalLogContent.Length;
                string entry = originalLogContent.Substring(startIndex, endIndex - startIndex);
                
                // ExecuteService가 포함된 항목만 처리
                if (entry.Contains("ExecuteService():[ "))
                {
                    var entryLines = entry.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    string timestamp = "";
                    string bizName = "";
                    string execTime = "";
                    int lineNumber = 0;
                    
                    foreach (string line in entryLines)
                    {
                        // [월-일-년 시:분:초] ExecuteService():[ 비즈명 ] 패턴 찾기
                        var executeServiceMatch = Regex.Match(line, @"(\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}\])\s+ExecuteService\(\):\[\s*([^\]]+)\s*\]");
                        if (executeServiceMatch.Success)
                        {
                            timestamp = executeServiceMatch.Groups[1].Value;
                            bizName = executeServiceMatch.Groups[2].Value.Trim();
                            
                            // 더 정확한 라인 번호 매칭을 위해 정확한 패턴으로 검색
                            string exactPattern = $"{timestamp}] ExecuteService():[{bizName}]";
                            
                            // 정제된 로그에서 정확한 매칭 찾기
                            for (int lineIdx = 0; lineIdx < cleanedLines.Length; lineIdx++)
                            {
                                string currentLine = cleanedLines[lineIdx];
                                
                                // 정확한 패턴 매칭 (대소문자 구분하지 않음)
                                if (currentLine.Contains(exactPattern, StringComparison.OrdinalIgnoreCase))
                                {
                                    lineNumber = lineIdx + 1; // 1-based 행 번호
                                    System.Diagnostics.Debug.WriteLine($"정확한 매칭: 라인 {lineNumber} = {timestamp} - {bizName}");
                                    break;
                                }
                            }
                            
                            // 정확한 매칭 실패 시 타임스탬프만으로 검색
                            if (lineNumber == 0)
                            {
                                for (int lineIdx = 0; lineIdx < cleanedLines.Length; lineIdx++)
                                {
                                    string currentLine = cleanedLines[lineIdx];
                                    
                                    if (currentLine.Contains(timestamp) && 
                                        currentLine.Contains("ExecuteService") && 
                                        currentLine.Contains(bizName))
                                    {
                                        lineNumber = lineIdx + 1; // 1-based 행 번호
                                        System.Diagnostics.Debug.WriteLine($"타임스탬프 매칭: 라인 {lineNumber} = {timestamp} - {bizName}");
                                        break;
                                    }
                                }
                            }
                            
                            // 여전히 실패하면 중복 방지 검색
                            if (lineNumber == 0)
                            {
                                var usedLineNumbers = summaryItems.Select(item => item.LineNumber).ToHashSet();
                                string secondarySearchPattern = $"ExecuteService():[{bizName}]";
                                
                                for (int lineIdx = 0; lineIdx < cleanedLines.Length; lineIdx++)
                                {
                                    int candidateLineNumber = lineIdx + 1;
                                    
                                    // 이미 사용된 행 번호는 건너뛰기
                                    if (usedLineNumbers.Contains(candidateLineNumber))
                                        continue;
                                        
                                    if (cleanedLines[lineIdx].Contains(secondarySearchPattern, StringComparison.OrdinalIgnoreCase))
                                    {
                                        lineNumber = candidateLineNumber;
                                        System.Diagnostics.Debug.WriteLine($"백업 매칭: 라인 {lineNumber} = {bizName}");
                                        break;
                                    }
                                }
                            }
                        }
                        
                        // exec.Time 추출
                        var execTimeMatch = Regex.Match(line, @"exec\.Time\s*:\s*(\d{2}:\d{2}:\d{2}\.\d+)");
                        if (execTimeMatch.Success)
                        {
                            execTime = execTimeMatch.Groups[1].Value;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(timestamp) && !string.IsNullOrEmpty(bizName))
                    {
                        summaryItems.Add(new LogSummaryItem
                        {
                            Timestamp = timestamp,
                            BusinessName = bizName,
                            ExecTime = execTime,
                            SearchKeyword = $"ExecuteService():[{bizName}]", // DATA 로그 검색용 키워드
                            LineNumber = lineNumber
                        });
                        
                        System.Diagnostics.Debug.WriteLine($"요약 항목 추가: {timestamp} - {bizName} -> 행 {lineNumber}");
                    }
                }
            }
            
            // 최종 검증: 중복 행 번호 리포트
            var finalDuplicates = summaryItems.GroupBy(item => item.LineNumber)
                                               .Where(group => group.Key > 0 && group.Count() > 1)
                                               .ToList();
            if (finalDuplicates.Any())
            {
                System.Diagnostics.Debug.WriteLine($"최종 중복 행 번호: {string.Join(", ", finalDuplicates.Select(g => $"행{g.Key}({g.Count()}개)"))}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"행 번호 중복 없음. 총 {summaryItems.Count}개 항목 생성.");
            }
        }

        public string GenerateExecuteServiceSummary(string fullLogContent)
        {
            var summaryLines = new List<string>();
            var matches = LogStartRegex.Matches(fullLogContent);
            
            for (int i = 0; i < matches.Count; i++)
            {
                int startIndex = matches[i].Index;
                int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : fullLogContent.Length;
                string entry = fullLogContent.Substring(startIndex, endIndex - startIndex);
                
                // ExecuteService가 포함된 항목만 처리
                if (entry.Contains("ExecuteService():[ "))
                {
                    var lines = entry.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        // [월-일-년 시:분:초] ExecuteService():[ 비즈명 ] 패턴 찾기
                        var executeServiceMatch = Regex.Match(line, @"(\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}\])\s+ExecuteService\(\):\[\s*([^\]]+)\s*\]");
                        if (executeServiceMatch.Success)
                        {
                            string timestamp = executeServiceMatch.Groups[1].Value;
                            string bizName = executeServiceMatch.Groups[2].Value.Trim();
                            summaryLines.Add($"{timestamp} ExecuteService : [ {bizName} ]");
                        }
                    }
                }
            }
            
            return string.Join(Environment.NewLine, summaryLines);
        }

        public string GenerateFilteredExecuteServiceSummary(string fullLogContent, double minExecTimeSeconds)
        {
            var summaryLines = new List<string>();
            var matches = LogStartRegex.Matches(fullLogContent);
            
            for (int i = 0; i < matches.Count; i++)
            {
                int startIndex = matches[i].Index;
                int endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : fullLogContent.Length;
                string entry = fullLogContent.Substring(startIndex, endIndex - startIndex);
                
                // ExecuteService가 포함된 항목만 처리
                if (entry.Contains("ExecuteService():[ "))
                {
                    var lines = entry.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    string timestamp = "";
                    string bizName = "";
                    double execTime = 0;
                    
                    foreach (string line in lines)
                    {
                        // 타임스탬프와 비즈명 추출
                        var executeServiceMatch = Regex.Match(line, @"(\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}\])\s+ExecuteService\(\):\[\s*([^\]]+)\s*\]");
                        if (executeServiceMatch.Success)
                        {
                            timestamp = executeServiceMatch.Groups[1].Value;
                            bizName = executeServiceMatch.Groups[2].Value.Trim();
                        }
                        
                        // exec.Time 추출 (00:00:05.1234567 형식)
                        var execTimeMatch = Regex.Match(line, @"exec\.Time\s*:\s*(\d{2}):(\d{2}):(\d{2})\.(\d+)");
                        if (execTimeMatch.Success)
                        {
                            int hours = int.Parse(execTimeMatch.Groups[1].Value);
                            int minutes = int.Parse(execTimeMatch.Groups[2].Value);
                            int seconds = int.Parse(execTimeMatch.Groups[3].Value);
                            double milliseconds = double.Parse("0." + execTimeMatch.Groups[4].Value);
                            
                            execTime = hours * 3600 + minutes * 60 + seconds + milliseconds;
                        }
                    }
                    
                    // 필터 조건에 맞는 경우만 추가
                    if (!string.IsNullOrEmpty(timestamp) && !string.IsNullOrEmpty(bizName) && execTime >= minExecTimeSeconds)
                    {
                        summaryLines.Add($"{timestamp} ExecuteService : [ {bizName} ] (exec.Time: {execTime:F3}s)");
                    }
                }
            }
            
            return string.Join(Environment.NewLine, summaryLines);
        }
    }
}