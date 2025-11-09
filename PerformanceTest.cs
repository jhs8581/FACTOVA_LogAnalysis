using System;
using System.Diagnostics;
using System.Text;
using FACTOVA_LogAnalysis.Helpers;

namespace FACTOVA_LogAnalysis
{
    /// <summary>
    /// 로그 변환 성능 테스트
    /// 사용법: MainWindow 생성자에서 PerformanceTest.RunLogParsingTest(); 호출
    /// </summary>
    public static class PerformanceTest
    {
        /// <summary>
        /// 100만 줄 DATA 로그 변환 성능 테스트
        /// </summary>
        public static void RunLogParsingTest()
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("📊 100만 줄 DATA 로그 변환 성능 테스트 시작");
            Console.WriteLine("=".PadRight(80, '='));
            
            // 1. 테스트 데이터 생성
            Console.WriteLine("\n🔨 1단계: 테스트 데이터 생성 중...");
            var sw = Stopwatch.StartNew();
            string testData = GenerateDummyDataLog(1_000_000);
            sw.Stop();
            Console.WriteLine($"✅ 데이터 생성 완료: {sw.ElapsedMilliseconds:N0}ms");
            Console.WriteLine($"   - 총 크기: {testData.Length / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"   - 줄 수: {testData.Split('\n').Length:N0} 줄");
            
            // 2. 변환 테스트
            Console.WriteLine("\n⚡ 2단계: ConvertToLogLines 변환 테스트...");
            sw.Restart();
            var result = LogDataGridHelper.ConvertToLogLines(testData);
            sw.Stop();
            
            // 3. 결과 출력
            Console.WriteLine($"\n✅ 변환 완료!");
            Console.WriteLine($"   - 소요 시간: {sw.ElapsedMilliseconds:N0}ms ({sw.Elapsed.TotalSeconds:F2}초)");
            Console.WriteLine($"   - 생성된 세션: {result.Count:N0}개");
            Console.WriteLine($"   - 처리 속도: {1_000_000.0 / sw.ElapsedMilliseconds * 1000:N0} 줄/초");
            
            // 4. 메모리 사용량
            var memoryUsed = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            Console.WriteLine($"   - 메모리 사용: {memoryUsed:F2} MB");
            
            // 5. 샘플 결과 확인
            Console.WriteLine("\n📋 샘플 결과 (처음 3개 세션):");
            for (int i = 0; i < Math.Min(3, result.Count); i++)
            {
                var item = result[i];
                Console.WriteLine($"   [{i + 1}] Line:{item.LineNumber}, " +
                    $"Time:{item.Timestamp}, " +
                    $"Business:{item.BusinessName}, " +
                    $"ExecTime:{item.ExecTime}, " +
                    $"TxnId:{item.TxnId}");
            }
            
            Console.WriteLine("\n=".PadRight(80, '='));
            Console.WriteLine("✅ 성능 테스트 완료!");
            Console.WriteLine("=".PadRight(80, '='));
        }
        
        /// <summary>
        /// 더미 DATA 로그 생성 (ExecuteService 세션 형식)
        /// </summary>
        private static string GenerateDummyDataLog(int totalLines)
        {
            var sb = new StringBuilder(totalLines * 200); // 평균 200자/줄
            int sessionCount = totalLines / 10; // 10줄당 1세션
            
            var random = new Random(42); // 재현 가능한 랜덤
            var businessNames = new[] { 
                "BR_MATERIAL_IN", "BR_MATERIAL_OUT", "BR_PROCESS_START", 
                "BR_PROCESS_END", "BR_INSPECTION", "BR_SHIPPING" 
            };
            
            for (int i = 0; i < sessionCount; i++)
            {
                var timestamp = DateTime.Today.AddSeconds(i).ToString("dd-MM-yyyy HH:mm:ss.fff");
                var business = businessNames[random.Next(businessNames.Length)];
                var execTime = $"00:00:0{random.Next(1, 9)}.{random.Next(100, 999)}";
                var txnId = $"TXN{random.Next(100000, 999999)}";
                
                // ExecuteService 라인
                sb.AppendLine($"[{timestamp}] ExecuteService():[ {business} ]");
                
                // exec.Time 라인
                sb.AppendLine($"exec.Time : {execTime}");
                
                // TXN_ID 라인
                sb.AppendLine($"TXN_ID : {txnId} :");
                
                // Parameter 라인
                sb.AppendLine("Parameter :");
                
                // NewDataSet XML (5줄)
                sb.AppendLine("<NewDataSet>");
                sb.AppendLine("  <Table>");
                sb.AppendLine($"    <BARCODE_NO>BC{random.Next(10000, 99999)}</BARCODE_NO>");
                sb.AppendLine($"    <LOT_ID>LOT{random.Next(1000, 9999)}</LOT_ID>");
                sb.AppendLine("  </Table>");
                sb.AppendLine("</NewDataSet>");
                
                // 빈 줄 (세션 구분)
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 간단한 성능 테스트 (작은 샘플)
        /// </summary>
        public static void RunQuickTest()
        {
            Console.WriteLine("\n🔬 빠른 성능 테스트 (1만 줄)");
            Console.WriteLine("-".PadRight(80, '-'));
            
            var sw = Stopwatch.StartNew();
            string testData = GenerateDummyDataLog(10_000);
            var result = LogDataGridHelper.ConvertToLogLines(testData);
            sw.Stop();
            
            Console.WriteLine($"✅ 1만 줄 처리: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   - 생성된 세션: {result.Count}개");
            Console.WriteLine($"   - 예상 100만 줄 시간: {sw.ElapsedMilliseconds * 100}ms ({sw.ElapsedMilliseconds * 100 / 1000.0:F1}초)");
        }
    }
}
