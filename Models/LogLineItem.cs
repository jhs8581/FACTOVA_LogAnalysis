using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace FACTOVA_LogAnalysis.Models
{
    /// <summary>
    /// DataGrid용 로그 라인 아이템 (Excel 호환 가능 및 추가 속성)
    /// </summary>
    public class LogLineItem : INotifyPropertyChanged
    {
        // ✅ Static Compiled Regex for BARCODE/LOT extraction (성능 최적화)
        private static readonly Regex BarcodeNoRegex = new Regex(@"<BARCODE_NO[^>]*>([^<]+)</BARCODE_NO>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex BarcodeValueRegex = new Regex(@"<BARCODE_VALUE[^>]*>([^<]+)</BARCODE_VALUE>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex LotIdRegex = new Regex(@"<LOT_ID[^>]*>([^<]+)</LOT_ID>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex LotId2Regex = new Regex(@"<LOTID[^>]*>([^<]+)</LOTID>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private bool _isHighlighted;
        private System.Windows.Media.Brush _textColor = System.Windows.Media.Brushes.Black;

        public int LineNumber { get; set; }
        public string Content { get; set; } = ""; // 정제된 실제 Content (NewDataSet도 포함)
        
        /// <summary>
        /// 로그 타입 (DATA, EVENT, DEBUG, EXCEPTION)
        /// </summary>
        public string LogLevel { get; set; } = "";

        public string Timestamp { get; set; } = "";
        public string BusinessName { get; set; } = ""; // 비즈니스명
        public string ExecTime { get; set; } = ""; // 실행시간
        public string TxnId { get; set; } = ""; // 트랜잭션 ID
        public string MsgId { get; set; } = ""; // MSGID 추가
        public string ProcId { get; set; } = ""; // PROCID 추가

        // EVENT 전용 새로운 컬럼들
        public string BCR_ID { get; set; } = "";
        public string RETURN_CODE { get; set; } = "";
        public string MSG_NO { get; set; } = "";
        public string WORK_TYPE { get; set; } = "";
        public string LINESTOP { get; set; } = "";
        public string LINEPASS { get; set; } = "";
        public string ERROR_CODE { get; set; } = "";
        public string ERROR_CODE_DESC { get; set; } = "";
        
        /// <summary>
        /// BARCODE_NO, BARCODE_VALUE, LOT_ID, LOTID 중 첫 번째 값
        /// </summary>
        public string BARCODE_LOT { get; set; } = "";

        /// <summary>
        /// 에러 설명 (EXCEPTION 로그 전용)
        /// </summary>
        public string ErrorDescription { get; set; } = "";

        public bool IsRedBusiness { get; set; }
        
        public System.Windows.Media.Brush TextColor 
        { 
            get => _textColor; 
            set 
            { 
                _textColor = value; 
                OnPropertyChanged(); 
            } 
        }
        
        public bool IsHighlighted 
        { 
            get => _isHighlighted; 
            set 
            { 
                _isHighlighted = value; 
                OnPropertyChanged(); 
            } 
        }

        // 복사용 텍스트 (Content만, Line 제외)
        public string CopyText => Content;
        
        /// <summary>
        /// 기본 생성자 (속성 초기화만)
        /// </summary>
        public LogLineItem()
        {
            SetDefaultColor();
        }
        
        /// <summary>
        /// 기본 생성자 (파싱 처리)
        /// </summary>
        public LogLineItem(int lineNumber, string content)
        {
            LineNumber = lineNumber;
            
            if (!string.IsNullOrWhiteSpace(content))
            {
                string originalContent = content;
                
                // 정보 추출 작업 (순서중요)
                ExtractLogLevel(originalContent);
                ExtractTimestamp(originalContent);
                ExtractBusinessName(originalContent);
                ExtractExecTime(originalContent);
                ExtractTxnId(originalContent);
                ExtractMsgId(originalContent);  // MsgId 추출 추가
                ExtractProcId(originalContent); // ProcId 추출 추가
                
                // EVENT 로그인 경우 추가 필드 추출
                if (LogLevel == "EVENT" || originalContent.Contains("EVENT"))
                {
                    ExtractEventFields(originalContent);
                }
                
                // BARCODE/LOT 추출 (DATA, EVENT 모두 적용)
                ExtractBarcodeLot(originalContent);
                
                // Content 정리 (중복 정보 제거)
                Content = CleanAndFormatContent(originalContent);
            }
            
            // 기본 색상 설정
            SetDefaultColor();
        }

        /// <summary>
        /// 미리 파싱된 정보 생성자 (파싱 생략)
        /// </summary>
        public LogLineItem(int lineNumber, string timestamp, string businessName, string execTime, string txnId, string content)
        {
            LineNumber = lineNumber;
            Timestamp = FormatTimestamp(timestamp);
            BusinessName = businessName;
            ExecTime = FormatExecTime(execTime);
            TxnId = txnId;
            Content = FormatXmlContent(content);
            LogLevel = "DATA"; // 기본값
            
            // BARCODE/LOT 추출 (원본 content 사용)
            ExtractBarcodeLot(content);
            
            // 기본 색상 설정
            SetDefaultColor();
        }

        /// <summary>
        /// EVENT 로그에서 추가 필드들 추출
        /// </summary>
        public void ExtractEventFields(string content)
        {
            try
            {
                // MSG_NO 추출 - 우선적으로 처리 진행
                ExtractMsgNo(content);
                
                // 다른 필드들도 나중에 추가 예정
                ExtractBcrId(content);
                ExtractReturnCode(content);
                ExtractWorkType(content);
                ExtractLineStop(content);
                ExtractLinePass(content);
                ExtractErrorCode(content);
                ExtractErrorCodeDesc(content);
                ExtractBarcodeLot(content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExtractEventFields error: {ex.Message}");
            }
        }

        /// <summary>
        /// MSG_NO 추출 - Content에서 MSG_NO 패턴을 찾아서 추출
        /// </summary>
        private void ExtractMsgNo(string content)
        {
            try
            {
                // 1. XML 태그에서 MSG_NO 추출: <MSG_NO>12345</MSG_NO>
                var xmlMsgNoMatch = Regex.Match(content, @"<MSG_NO[^>]*>([^<]*)</MSG_NO>", RegexOptions.IgnoreCase);
                if (xmlMsgNoMatch.Success)
                {
                    MSG_NO = xmlMsgNoMatch.Groups[1].Value.Trim();
                    return;
                }

                // 2. 키-값 쌍에서 MSG_NO 추출: MSG_NO: 12345, MSG_NO=12345
                var keyValueMatch = Regex.Match(content, @"MSG_NO\s*[:=]\s*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (keyValueMatch.Success)
                {
                    MSG_NO = keyValueMatch.Groups[1].Value.Trim();
                    return;
                }

                // 3. 따옴표나 괄호로 감싸진 MSG_NO: "MSG_NO":"12345", (MSG_NO:12345)
                var quotedMatch = Regex.Match(content, @"[""'\(]?MSG_NO[""'\)]?\s*[:=]\s*[""']?([A-Z0-9\-_]+)[""']?", RegexOptions.IgnoreCase);
                if (quotedMatch.Success)
                {
                    MSG_NO = quotedMatch.Groups[1].Value.Trim();
                    return;
                }

                // 4. JSON 스타일: {"MSG_NO":"12345"}
                var jsonMatch = Regex.Match(content, @"""MSG_NO""\s*:\s*""([^""]+""", RegexOptions.IgnoreCase);
                if (jsonMatch.Success)
                {
                    MSG_NO = jsonMatch.Groups[1].Value.Trim();
                    return;
                }

                // 5. 단순 MSG_NO 뒤에 오는 숫자/문자: MSG_NO 12345
                var simpleMatch = Regex.Match(content, @"MSG_NO\s+([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (simpleMatch.Success)
                {
                    MSG_NO = simpleMatch.Groups[1].Value.Trim();
                    return;
                }

                MSG_NO = "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExtractMsgNo error: {ex.Message}");
                MSG_NO = "";
            }
        }

        private void ExtractBcrId(string content)
        {
            var match = Regex.Match(content, @"BCR_?ID\s*[:=]\s*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            BCR_ID = match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private void ExtractReturnCode(string content)
        {
            var match = Regex.Match(content, @"RETURN_?CODE\s*[:=]\s*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            RETURN_CODE = match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private void ExtractWorkType(string content)
        {
            var match = Regex.Match(content, @"WORK_?TYPE\s*[:=]\s*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            WORK_TYPE = match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private void ExtractLineStop(string content)
        {
            var match = Regex.Match(content, @"LINE_?STOP\s*[:=]\s*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            LINESTOP = match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private void ExtractLinePass(string content)
        {
            var match = Regex.Match(content, @"LINE_?PASS\s*[:=]\s*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            LINEPASS = match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private void ExtractErrorCode(string content)
        {
            var match = Regex.Match(content, @"ERROR_?CODE\s*[:=]\s*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
            ERROR_CODE = match.Success ? match.Groups[1].Value.Trim() : "";
        }

        private void ExtractErrorCodeDesc(string content)
        {
            var match = Regex.Match(content, @"ERROR_?CODE_?DESC\s*[:=]\s*([^,\]\}]+)", RegexOptions.IgnoreCase);
            ERROR_CODE_DESC = match.Success ? match.Groups[1].Value.Trim() : "";
        }

        /// <summary>
        /// BARCODE_NO, BARCODE_VALUE, LOT_ID, LOTID 중 첫 번째 값 추출 - 최적화 버전
        /// </summary>
        private void ExtractBarcodeLot(string content)
        {
            try
            {
                // 1. <BARCODE_NO>VALUE</BARCODE_NO> 형식 (개행 및 공백 포함)
                var barcodeNoMatch = BarcodeNoRegex.Match(content);
                if (barcodeNoMatch.Success)
                {
                    string value = barcodeNoMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        BARCODE_LOT = value;
                        return;
                    }
                }

                // 2. <BARCODE_VALUE>VALUE</BARCODE_VALUE> 형식 (개행 및 공백 포함)
                var barcodeValueMatch = BarcodeValueRegex.Match(content);
                if (barcodeValueMatch.Success)
                {
                    string value = barcodeValueMatch.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        BARCODE_LOT = value;
                        return;
                    }
                }

                // 3. <LOT_ID>VALUE</LOT_ID> 형식 (개행 및 공백 포함)
                var lotIdMatch = LotIdRegex.Match(content);
                if (lotIdMatch.Success && !string.IsNullOrWhiteSpace(lotIdMatch.Groups[1].Value))
                {
                    BARCODE_LOT = lotIdMatch.Groups[1].Value.Trim();
                    return;
                }

                // 4. <LOTID>VALUE</LOTID> 형식 (개행 및 공백 포함)
                var lotIdMatch2 = LotId2Regex.Match(content);
                if (lotIdMatch2.Success && !string.IsNullOrWhiteSpace(lotIdMatch2.Groups[1].Value))
                {
                    BARCODE_LOT = lotIdMatch2.Groups[1].Value.Trim();
                    return;
                }

                BARCODE_LOT = "";
            }
            catch (Exception)
            {
                // ✅ 예외 발생 시 빈 문자열 반환 (디버그 출력 제거)
                BARCODE_LOT = "";
            }
        }

        /// <summary>
        /// Content에서 중복 정보를 제거하고 XML을 정리함
        /// </summary>
        private string CleanAndFormatContent(string originalContent)
        {
            string cleaned = originalContent;

            // 1. 타임스탬프 제거 [10-02-2025 12:54:10]
            cleaned = Regex.Replace(cleaned, @"\[\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}\]", "").Trim();
            
            // 2. ExecuteService():[...] 부분 제거
            cleaned = Regex.Replace(cleaned, @"ExecuteService\(\)\s*:\s*\[\s*[^\]]+\s*\]", "").Trim();
            
            // 3. exec.Time 정보 제거
            cleaned = Regex.Replace(cleaned, @"exec\.Time\s*:\s*[0-9:\.]+", "").Trim();
            
            // 4. TXN_ID 정보 제거
            cleaned = Regex.Replace(cleaned, @"TXN_ID\s*:\s*[A-Z0-9\-_]+\s*:", "").Trim();
            
            // 5. Parameter : 제거
            cleaned = Regex.Replace(cleaned, @"Parameter\s*:", "").Trim();
            
            // 6. 로그 레벨 텍스트 제거 (ERROR, WARN, INFO, DEBUG)
            cleaned = Regex.Replace(cleaned, @"\b(ERROR|WARN|INFO|DEBUG)\b\s*:?", "").Trim();
            
            // 7. 연속된 공백을 하나로 통일
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            
            // 8. NewDataSet XML 포맷팅
            return FormatXmlContent(cleaned);
        }

        /// <summary>
        /// XML Content를 들여쓰기 형태로 포맷팅
        /// </summary>
        private string FormatXmlContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "";

            try
            {
                // NewDataSet이 포함된 경우 XML 포맷팅 시도
                if (content.Contains("<NewDataSet>") && content.Contains("</NewDataSet>"))
                {
                    // XML 파싱 및 포맷팅
                    var doc = XDocument.Parse(content);
                    
                    using (var stringWriter = new StringWriter())
                    {
                        using (var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
                        {
                            Indent = true,
                            IndentChars = "  ", // 2칸 들여쓰기
                            NewLineChars = "\n",
                            NewLineHandling = NewLineHandling.Replace,
                            OmitXmlDeclaration = true
                        }))
                        {
                            doc.WriteTo(xmlWriter);
                        }
                        return stringWriter.ToString();
                    }
                }
                
                // NewDataSet이 아니면 원본 반환
                return content;
            }
            catch (Exception)
            {
                // XML 파싱 실패 시 원본 반환 (한 줄로)
                return Regex.Replace(content, @"\s+", " ").Trim();
            }
        }

        /// <summary>
        /// TimeStamp를 HH:mm:ss 또는 HH:mm:ss.fff 형식으로 변환 (밀리초 보존)
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

                // 입력이 dd-MM-yyyy HH:mm:ss 형식이면 시간(HH:mm:ss)로 변환
                if (DateTime.TryParseExact(timestamp, "dd-MM-yyyy HH:mm:ss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    return parsedDate.ToString("HH:mm:ss");
                }
                
                // 다른 형식들도 시도 후 시간 부분만 반환
                if (DateTime.TryParse(timestamp, out DateTime parsedDate2))
                {
                    // 밀리초가 포함되어 있으면 보존
                    if (timestamp.Contains("."))
                        return parsedDate2.ToString("HH:mm:ss.fff");
                    else
                        return parsedDate2.ToString("HH:mm:ss");
                }
                
                // 파싱 실패 시 정규식으로 시간 부분만 추출 시도
                // 밀리초 포함: HH:mm:ss.fff
                var mWithMs = Regex.Match(timestamp, @"(\d{2}:\d{2}:\d{2}\.\d{3})");
                if (mWithMs.Success)
                    return mWithMs.Groups[1].Value;

                // 밀리초 없음: HH:mm:ss
                var m = Regex.Match(timestamp, @"(\d{2}:\d{2}:\d{2})");
                if (m.Success)
                    return m.Groups[1].Value;

                return timestamp; // 변환 실패시 원본
            }
            catch
            {
                return timestamp;
            }
        }

        private void ExtractLogLevel(string content)
        {
            if (content.Contains("ERROR"))
                LogLevel = "ERROR";
            else if (content.Contains("WARN"))
                LogLevel = "WARN";
            else if (content.Contains("INFO"))
                LogLevel = "INFO";
            else if (content.Contains("DEBUG"))
                LogLevel = "DEBUG";
            else if (content.Contains("EVENT"))
                LogLevel = "EVENT";
            else
                LogLevel = "UNKNOWN";
        }

        private void ExtractTimestamp(string content)
        {
            // [01-01-2024 10:00:01] 또는 [01-01-2024 10:00:01.123] 매칭 (밀리초 지원)
            var match = Regex.Match(content, @"\[(\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2}(?:\.\d{3})?)\]");
            if (match.Success)
            {
                Timestamp = FormatTimestamp(match.Groups[1].Value);
            }
        }

        private void ExtractBusinessName(string content)
        {
            try
            {
                // 1. ExecuteService() : [ 서비스명 ] 형식에서 비즈니스명 추출 (최우선)
                var executeServiceMatch = Regex.Match(content, @"ExecuteService\(\)\s*:\s*\[\s*([^\]]+)\s*\]", RegexOptions.IgnoreCase);
                if (executeServiceMatch.Success)
                {
                    BusinessName = executeServiceMatch.Groups[1].Value.Trim();
                    return;
                }

                // 2. BR_XXX 형태의 비즈니스명 추.extract
                var brBusinessMatch = Regex.Match(content, @"\b(BR_[A-Za-z0-9_]+)\b", RegexOptions.IgnoreCase);
                if (brBusinessMatch.Success)
                {
                    BusinessName = brBusinessMatch.Groups[1].Value.Trim();
                    return;
                }

                // 3. ExecuteService에 포함된 비즈니스명 패턴들
                var executeServicePatterns = new[]
                {
                    @"ExecuteService\(\)\s*:\s*([A-Za-z0-9_]+)", // ExecuteService(): BusinessName
                    @"ExecuteService\s*\(\s*([A-Za-z0-9_]+)\s*\)", // ExecuteService(BusinessName)
                    @"ExecuteService.*?Business:\s*([^,\]\s]+)", // ExecuteService - Business: XXX
                    @"ExecuteService.*?:\s*([A-Za-z0-9_]+)" // ExecuteService: BusinessName
                };

                foreach (var pattern in executeServicePatterns)
                {
                    var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        BusinessName = match.Groups[1].Value.Trim();
                        return;
                    }
                }

                // 4. EXCEPTION 로그에서 일반적인 비즈니스명 패턴들
                var exceptionPatterns = new[]
                {
                    @"Business[:\s]+([A-Za-z0-9_]+)", // Business: XXX
                    @"비즈니스[:\s]+([A-Za-z0-9_]+)", // 비즈니스: XXX
                    @"Service[:\s]+([A-Za-z0-9_]+)", // Service: XXX
                    @"Method[:\s]+([A-Za-z0-9_]+)", // Method: XXX
                    @"Function[:\s]+([A-Za-z0-9_]+)", // Function: XXX
                    @"\b([A-Z][A-Za-z0-9]*(?:Business|Service|Method|Function))\b", // XXXBusiness, XXXService 등
                    @"\b([A-Z]{2,}_[A-Za-z0-9_]+)\b" // 대문자로 시작하는 언더스코어 포함 (예: SFC_XXX, MES_XXX)
                };

                foreach (var pattern in exceptionPatterns)
                {
                    var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var candidate = match.Groups[1].Value.Trim();
                        // 너무 짧거나 숫자만 있는 경우 제외
                        if (candidate.Length >= 3 && !Regex.IsMatch(candidate, @"^\d+$"))
                        {
                            BusinessName = candidate;
                            return;
                        }
                    }
                }

                // 5. XML 형식에서 비즈니스명 추출 시도
                if (content.Contains("<") && content.Contains(">"))
                {
                    var xmlBusinessMatch = Regex.Match(content, @"<(\w+)>.*?</\1>", RegexOptions.IgnoreCase);
                    if (xmlBusinessMatch.Success)
                    {
                        var xmlTag = xmlBusinessMatch.Groups[1].Value;
                        if (xmlTag.Length >= 3 && !Regex.IsMatch(xmlTag, @"^\d+$"))
                        {
                            BusinessName = xmlTag;
                            return;
                        }
                    }
                }

                // 6. 기타 일반적인 식별자 패턴
                var generalPatterns = new[]
                {
                    @"\b([A-Z][A-Za-z0-9]{2,}(?:EIF|IF|Service|Business|Method|Function|Manager|Handler|Controller))\b",
                    @"\b([A-Z]{2,}_[A-Za-z0-9_]{2,})\b"
                };

                foreach (var pattern in generalPatterns)
                {
                    var match = Regex.Match(content, pattern);
                    if (match.Success)
                    {
                        BusinessName = match.Groups[1].Value.Trim();
                        return;
                    }
                }

                // 매칭되는 항목이 없을 때 빈 문자열
                BusinessName = "";
            }
            catch
            {
                BusinessName = "";
            }
        }

        private void ExtractExecTime(string content)
        {
            try
            {
                // exec.Time: 00:00:00.1234567 형식 처리 → 초.소수점 표현
                var execTimeMatch = Regex.Match(content, @"exec\.Time\s*:\s*([0-9:\.]+)", RegexOptions.IgnoreCase);
                if (execTimeMatch.Success)
                {
                    ExecTime = FormatExecTime(execTimeMatch.Groups[1].Value.Trim());
                    return;
                }

                // ExecTime: 1.234s 형식 (s 접미사 처리)
                var execTimeMatch2 = Regex.Match(content, @"ExecTime[:\s]*([0-9]+\.?[0-9]*)", RegexOptions.IgnoreCase);
                if (execTimeMatch2.Success)
                {
                    ExecTime = FormatExecTime(execTimeMatch2.Groups[1].Value);
                    return;
                }
 
                // 실행시간: 1.234초 형식
                var koreanTimeMatch = Regex.Match(content, @"실행시간[:\s]*([0-9]+\.?[0-9]*)\s*초", RegexOptions.IgnoreCase);
                if (koreanTimeMatch.Success)
                {
                    ExecTime = FormatExecTime(koreanTimeMatch.Groups[1].Value);
                    return;
                }

                ExecTime = "";
            }
            catch
            {
                ExecTime = "";
            }
        }

        private void ExtractTxnId(string content)
        {
            try
            {
                // TXN_ID: XXXXXXXXX : 형식 (콜론 포함)
                var txnIdMatch = Regex.Match(content, @"TXN_ID[:\s]*([A-Z0-9\-_]+)\s*:", RegexOptions.IgnoreCase);
                if (txnIdMatch.Success)
                {
                    TxnId = txnIdMatch.Groups[1].Value.Trim();
                    return;
                }

                // TXN_ID: XXXXXXXXX 형식 (콜론 미포함)
                var txnIdMatch2 = Regex.Match(content, @"TXN_ID[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (txnIdMatch2.Success)
                {
                    TxnId = txnIdMatch2.Groups[1].Value.Trim();
                    return;
                }

                // Transaction ID: XXXXXXXXX 형식
                var transactionMatch = Regex.Match(content, @"Transaction\s+ID[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (transactionMatch.Success)
                {
                    TxnId = transactionMatch.Groups[1].Value.Trim();
                    return;
                }

                // TXNID: XXXXXXXXX 형식
                var txnIdMatch3 = Regex.Match(content, @"TXNID[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (txnIdMatch3.Success)
                {
                    TxnId = txnIdMatch3.Groups[1].Value.Trim();
                    return;
                }

                TxnId = "";
            }
            catch
            {
                TxnId = "";
            }
        }

        private void ExtractMsgId(string content)
        {
            try
            {
                // MSGID: XXXXXXXXX 형식
                var msgIdMatch = Regex.Match(content, @"MSGID[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (msgIdMatch.Success)
                {
                    MsgId = msgIdMatch.Groups[1].Value.Trim();
                    return;
                }

                // MsgId: XXXXXXXXX 형식
                var msgIdMatch2 = Regex.Match(content, @"MsgId[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (msgIdMatch2.Success)
                {
                    MsgId = msgIdMatch2.Groups[1].Value.Trim();
                    return;
                }

                // MSG_ID: XXXXXXXXX 형식
                var msgIdMatch3 = Regex.Match(content, @"MSG_ID[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (msgIdMatch3.Success)
                {
                    MsgId = msgIdMatch3.Groups[1].Value.Trim();
                    return;
                }

                // Message ID: XXXXXXXXX 형식
                var messageIdMatch = Regex.Match(content, @"Message\s+ID[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (messageIdMatch.Success)
                {
                    MsgId = messageIdMatch.Groups[1].Value.Trim();
                    return;
                }

                MsgId = "";
            }
            catch
            {
                MsgId = "";
            }
        }

        private void ExtractProcId(string content)
        {
            try
            {
                // PROCID: XXXXXXXXX 형식
                var procIdMatch = Regex.Match(content, @"PROCID[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (procIdMatch.Success)
                {
                    ProcId = procIdMatch.Groups[1].Value.Trim();
                    return;
                }

                // ProcId: XXXXXXXXX 형식
                var procIdMatch2 = Regex.Match(content, @"ProcId[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (procIdMatch2.Success)
                {
                    ProcId = procIdMatch2.Groups[1].Value.Trim();
                    return;
                }

                // PROC_ID: XXXXXXXXX 형식
                var procIdMatch3 = Regex.Match(content, @"PROC_ID[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (procIdMatch3.Success)
                {
                    ProcId = procIdMatch3.Groups[1].Value.Trim();
                    return;
                }

                // Process ID: XXXXXXXXX 형식
                var processIdMatch = Regex.Match(content, @"Process\s+ID[:\s]*([A-Z0-9\-_]+)", RegexOptions.IgnoreCase);
                if (processIdMatch.Success)
                {
                    ProcId = processIdMatch.Groups[1].Value.Trim();
                    return;
                }

                ProcId = "";
            }
            catch
            {
                ProcId = "";
            }
        }

        private void SetDefaultColor()
        {
            TextColor = LogLevel switch
            {
                "ERROR" => System.Windows.Media.Brushes.Red,
                "WARN" => System.Windows.Media.Brushes.Orange,
                "INFO" => System.Windows.Media.Brushes.Black,
                "DEBUG" => System.Windows.Media.Brushes.Gray,
                "EVENT" => System.Windows.Media.Brushes.DarkGreen,
                _ => System.Windows.Media.Brushes.Black
            };
        }

        public void ApplyRedBusinessHighlight()
        {
            if (IsRedBusiness)
            {
                TextColor = System.Windows.Media.Brushes.Red;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 표준 통일화를 위한 execTime 원래 형식 seconds.fraction 형태로 변환
        /// 예: "00:00:00.123" -> "00.123", "00:00:05.123456" -> "05.123456", "1.234s" -> "1.234"
        /// </summary>
        private string FormatExecTime(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            raw = raw.Trim();

            // HH:MM:SS(.fraction)
            var hmsMatch = Regex.Match(raw, "^(\\d{1,2}):(\\d{2}):(\\d{2})(\\.\\d+)?$");
            if (hmsMatch.Success)
            {
                var seconds = hmsMatch.Groups[3].Value.PadLeft(2, '0');
                var fraction = hmsMatch.Groups[4].Success ? hmsMatch.Groups[4].Value : "";
                return seconds + fraction;
            }

            // trailing 's' like 1.234s
            var sMatch = Regex.Match(raw, "^(\\d+\\.?\\d*)s$", RegexOptions.IgnoreCase);
            if (sMatch.Success)
            {
                return sMatch.Groups[1].Value;
            }

            // plain number
            var numMatch = Regex.Match(raw, "^(\\d+\\.?\\d*)$");
            if (numMatch.Success)
                return numMatch.Groups[1].Value;

            return raw;
        }
    }
}
