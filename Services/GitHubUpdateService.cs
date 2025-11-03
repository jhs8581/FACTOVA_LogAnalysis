using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

// ✅ WPF 전용 타입 명시
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;

namespace FACTOVA_LogAnalysis.Services
{
    /// <summary>
    /// GitHub 릴리즈를 통한 자동 업데이트 서비스
    /// </summary>
    public class GitHubUpdateService
    {
        private const string GITHUB_REPO_OWNER = "jhs8581";
        private const string GITHUB_REPO_NAME = "FACTOVA_LogAnalysis";  // ✅ 올바른 리포지토리 이름
        private const string GITHUB_API_URL = $"https://api.github.com/repos/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases/latest";
        
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // 캐시된 릴리즈 정보
        private static ReleaseInfo? _cachedReleaseInfo;
        private static DateTime _cacheExpiry = DateTime.MinValue;
        private static DateTime? _lastModified = null;
        private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromHours(6); // 🔥 6시간으로 연장
        
        // 🔥 Rate Limit 정보 캐싱
        private static RateLimitInfo? _rateLimitInfo;

        static GitHubUpdateService()
        {
            // GitHub API는 User-Agent 헤더 필수
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"{GITHUB_REPO_NAME}-UpdateChecker");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        /// <summary>
        /// 현재 애플리케이션 버전
        /// </summary>
        public static Version GetCurrentVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version ?? new Version(1, 0, 0);
        }

        /// <summary>
        /// GitHub에서 최신 릴리즈 정보 확인
        /// </summary>
        /// <param name="forceRefresh">true면 캐시 무시하고 강제로 API 호출</param>
        public static async Task<ReleaseInfo?> CheckForUpdatesAsync(bool forceRefresh = false)
        {
            try
            {
                // 🔥 강제 새로고침이 아니고 캐시가 유효하면 캐시 사용
                if (!forceRefresh && _cachedReleaseInfo != null && DateTime.Now < _cacheExpiry)
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 Using cached release information (expires at {_cacheExpiry:yyyy-MM-dd HH:mm:ss})");
                    return _cachedReleaseInfo;
                }

                // 🔥 강제 새로고침이 아니면서 캐시가 만료되었어도 캐시 반환 (API 호출 최소화)
                if (!forceRefresh && _cachedReleaseInfo != null)
                {
                    System.Diagnostics.Debug.WriteLine($"🔄 Using expired cache (auto-check mode)");
                    return _cachedReleaseInfo;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 Checking for updates from GitHub... (forceRefresh: {forceRefresh})");
                System.Diagnostics.Debug.WriteLine($"   API URL: {GITHUB_API_URL}");

                // 🔥 If-Modified-Since 헤더 추가 (304 Not Modified 응답으로 Rate Limit 절약)
                var request = new HttpRequestMessage(HttpMethod.Get, GITHUB_API_URL);
                if (_lastModified.HasValue)
                {
                    request.Headers.IfModifiedSince = _lastModified.Value;
                    System.Diagnostics.Debug.WriteLine($"   If-Modified-Since: {_lastModified:R}");
                }

                var response = await _httpClient.SendAsync(request);
                
                System.Diagnostics.Debug.WriteLine($"   Response Status: {response.StatusCode}");

                // 🔥 Rate Limit 정보 확인 및 저장
                int? limit = null;
                int? remaining = null;
                DateTime? resetTime = null;
                
                if (response.Headers.TryGetValues("X-RateLimit-Limit", out var limitValues))
                {
                    var limitStr = string.Join(", ", limitValues);
                    if (int.TryParse(limitStr, out var parsedLimit))
                    {
                        limit = parsedLimit;
                    }
                    System.Diagnostics.Debug.WriteLine($"   Rate Limit: {limitStr}");
                }
                if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues))
                {
                    var remainingStr = string.Join(", ", remainingValues);
                    if (int.TryParse(remainingStr, out var parsedRemaining))
                    {
                        remaining = parsedRemaining;
                    }
                    System.Diagnostics.Debug.WriteLine($"   Rate Limit Remaining: {remainingStr}");
                }
                if (response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues))
                {
                    var resetTimestamp = long.Parse(string.Join(", ", resetValues));
                    resetTime = DateTimeOffset.FromUnixTimeSeconds(resetTimestamp).ToLocalTime().DateTime;
                    System.Diagnostics.Debug.WriteLine($"   Rate Limit Reset: {resetTime:yyyy-MM-dd HH:mm:ss}");
                }
                
                // Rate Limit 정보 저장
                if (limit.HasValue && remaining.HasValue && resetTime.HasValue)
                {
                    _rateLimitInfo = new RateLimitInfo
                    {
                        Limit = limit.Value,
                        Remaining = remaining.Value,
                        ResetTime = resetTime.Value
                    };
                }

                // 🔥 304 Not Modified - 캐시 재사용
                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    System.Diagnostics.Debug.WriteLine("✅ Release not modified, using cached data");
                    if (_cachedReleaseInfo != null)
                    {
                        // 캐시 만료 시간 연장
                        _cacheExpiry = DateTime.Now.Add(CACHE_DURATION);
                        return _cachedReleaseInfo;
                    }
                }

                // 🔥 403 Forbidden - Rate Limit 초과
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"❌ GitHub API Rate Limit exceeded!");
                    System.Diagnostics.Debug.WriteLine($"   Error: {errorContent}");
                    
                    // 캐시가 있으면 캐시 반환 (만료되었어도)
                    if (_cachedReleaseInfo != null)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ Returning expired cache due to rate limit");
                        _cacheExpiry = DateTime.Now.Add(TimeSpan.FromHours(1)); // 1시간 후 재시도
                        return _cachedReleaseInfo;
                    }
                    
                    // 🔥 캐시도 없으면 기본 정보 반환 (null이 아닌)
                    System.Diagnostics.Debug.WriteLine("⚠️ No cache available, returning default info");
                    var currentVer = GetCurrentVersion();
                    return new ReleaseInfo
                    {
                        CurrentVersion = currentVer,
                        LatestVersion = currentVer,
                        HasUpdate = false,
                        ReleaseUrl = $"https://github.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases",
                        DownloadUrl = "",
                        ReleaseNotes = "⚠️ GitHub API Rate Limit 초과\n나중에 다시 시도해주세요.",
                        PublishedDate = DateTime.Now,
                        ErrorMessage = "GitHub API Rate Limit이 초과되었습니다. 잠시 후 다시 시도해주세요."
                    };
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"❌ GitHub API request failed: {response.StatusCode}");
                    System.Diagnostics.Debug.WriteLine($"   Error content: {errorContent}");
                    
                    // 404는 릴리즈가 없는 경우
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ No releases found in repository");
                        var currentVer = GetCurrentVersion();
                        return new ReleaseInfo
                        {
                            CurrentVersion = currentVer,
                            LatestVersion = currentVer,
                            HasUpdate = false,
                            ReleaseUrl = $"https://github.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases",
                            DownloadUrl = "",
                            ReleaseNotes = "아직 릴리즈가 없습니다.",
                            PublishedDate = DateTime.Now
                        };
                    }
                    
                    // 🔥 기타 에러도 기본 정보 반환
                    var currentVer2 = GetCurrentVersion();
                    return new ReleaseInfo
                    {
                        CurrentVersion = currentVer2,
                        LatestVersion = currentVer2,
                        HasUpdate = false,
                        ReleaseUrl = $"https://github.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases",
                        DownloadUrl = "",
                        ReleaseNotes = $"⚠️ 업데이트 확인 실패\nHTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                        PublishedDate = DateTime.Now,
                        ErrorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}\n{errorContent}"
                    };
                }

                // 🔥 Last-Modified 헤더 저장
                if (response.Content.Headers.LastModified.HasValue)
                {
                    _lastModified = response.Content.Headers.LastModified.Value.DateTime;
                    System.Diagnostics.Debug.WriteLine($"   Last-Modified: {_lastModified:R}");
                }

                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"   Response JSON length: {json.Length}");
                
                var release = JsonSerializer.Deserialize<GitHubRelease>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (release == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Failed to parse GitHub release JSON");
                    var currentVer = GetCurrentVersion();
                    return new ReleaseInfo
                    {
                        CurrentVersion = currentVer,
                        LatestVersion = currentVer,
                        HasUpdate = false,
                        ReleaseUrl = $"https://github.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases",
                        DownloadUrl = "",
                        ReleaseNotes = "⚠️ JSON 파싱 실패",
                        PublishedDate = DateTime.Now,
                        ErrorMessage = "GitHub API 응답을 파싱할 수 없습니다."
                    };
                }

                System.Diagnostics.Debug.WriteLine($"✅ Latest release: {release.TagName}");
                System.Diagnostics.Debug.WriteLine($"   Name: {release.Name}");
                System.Diagnostics.Debug.WriteLine($"   Published: {release.PublishedAt}");
                System.Diagnostics.Debug.WriteLine($"   Assets count: {release.Assets?.Count ?? 0}");

                // 버전 태그에서 숫자만 추출 (v1.0.0 -> 1.0.0)
                var versionString = release.TagName?.TrimStart('v', 'V') ?? "0.0.0";
                
                if (!Version.TryParse(versionString, out var latestVersion))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Invalid version format: {release.TagName}");
                    var currentVer = GetCurrentVersion();
                    return new ReleaseInfo
                    {
                        CurrentVersion = currentVer,
                        LatestVersion = currentVer,
                        HasUpdate = false,
                        ReleaseUrl = $"https://github.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases",
                        DownloadUrl = "",
                        ReleaseNotes = $"⚠️ 잘못된 버전 형식: {release.TagName}",
                        PublishedDate = DateTime.Now,
                        ErrorMessage = $"버전 태그 형식이 올바르지 않습니다: {release.TagName}"
                    };
                }

                var currentVersion = GetCurrentVersion();
                var hasUpdate = latestVersion > currentVersion;

                System.Diagnostics.Debug.WriteLine($"   Current version: {currentVersion}");
                System.Diagnostics.Debug.WriteLine($"   Latest version: {latestVersion}");
                System.Diagnostics.Debug.WriteLine($"   Update available: {hasUpdate}");

                // 다운로드 URL 찾기 (첫 번째 .exe 또는 .zip 파일)
                string? downloadUrl = null;
                string? fileName = null;

                if (release.Assets != null)
                {
                    System.Diagnostics.Debug.WriteLine($"   Searching for download files in {release.Assets.Count} assets:");
                    foreach (var asset in release.Assets)
                    {
                        System.Diagnostics.Debug.WriteLine($"     - {asset.Name} ({asset.Size} bytes)");
                        
                        if (asset.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true ||
                            asset.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            downloadUrl = asset.BrowserDownloadUrl;
                            fileName = asset.Name;
                            System.Diagnostics.Debug.WriteLine($"   ✅ Found download file: {fileName}");
                            System.Diagnostics.Debug.WriteLine($"      URL: {downloadUrl}");
                            break;
                        }
                    }
                    
                    if (downloadUrl == null)
                    {
                        System.Diagnostics.Debug.WriteLine("   ⚠️ No .exe or .zip file found in assets");
                    }
                }

                var releaseInfo = new ReleaseInfo
                {
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    HasUpdate = hasUpdate,
                    ReleaseUrl = release.HtmlUrl ?? "",
                    DownloadUrl = downloadUrl ?? release.HtmlUrl ?? "",
                    FileName = fileName,
                    ReleaseNotes = release.Body ?? "",
                    PublishedDate = release.PublishedAt
                };

                // 🔥 릴리즈 정보를 캐시함 (6시간)
                _cachedReleaseInfo = releaseInfo;
                _cacheExpiry = DateTime.Now.Add(CACHE_DURATION);
                System.Diagnostics.Debug.WriteLine($"   Cache expires at: {_cacheExpiry:yyyy-MM-dd HH:mm:ss}");

                return releaseInfo;
            }
            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Request timeout: {ex.Message}");
                // 🔥 캐시가 있으면 반환, 없으면 기본 정보 반환
                if (_cachedReleaseInfo != null)
                    return _cachedReleaseInfo;
                
                var currentVer = GetCurrentVersion();
                return new ReleaseInfo
                {
                    CurrentVersion = currentVer,
                    LatestVersion = currentVer,
                    HasUpdate = false,
                    ReleaseUrl = $"https://github.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases",
                    DownloadUrl = "",
                    ReleaseNotes = "⚠️ 요청 시간 초과",
                    PublishedDate = DateTime.Now,
                    ErrorMessage = $"GitHub API 요청이 시간 초과되었습니다: {ex.Message}"
                };
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Network error checking for updates: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   InnerException: {ex.InnerException?.Message}");
                // 🔥 캐시가 있으면 반환, 없으면 기본 정보 반환
                if (_cachedReleaseInfo != null)
                    return _cachedReleaseInfo;
                
                var currentVer = GetCurrentVersion();
                return new ReleaseInfo
                {
                    CurrentVersion = currentVer,
                    LatestVersion = currentVer,
                    HasUpdate = false,
                    ReleaseUrl = $"https://github.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases",
                    DownloadUrl = "",
                    ReleaseNotes = "⚠️ 네트워크 오류",
                    PublishedDate = DateTime.Now,
                    ErrorMessage = $"네트워크 오류: {ex.Message}\n{ex.InnerException?.Message}"
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error checking for updates: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   Type: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
                // 🔥 캐시가 있으면 반환, 없으면 기본 정보 반환
                if (_cachedReleaseInfo != null)
                    return _cachedReleaseInfo;
                
                var currentVer = GetCurrentVersion();
                return new ReleaseInfo
                {
                    CurrentVersion = currentVer,
                    LatestVersion = currentVer,
                    HasUpdate = false,
                    ReleaseUrl = $"https://github.com/{GITHUB_REPO_OWNER}/{GITHUB_REPO_NAME}/releases",
                    DownloadUrl = "",
                    ReleaseNotes = "⚠️ 예상치 못한 오류",
                    PublishedDate = DateTime.Now,
                    ErrorMessage = $"{ex.GetType().Name}: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 업데이트 다운로드 및 설치
        /// </summary>
        public static async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, string fileName, IProgress<int>? progress = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"📥 Downloading update: {downloadUrl}");

                var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                var tempPath = Path.Combine(Path.GetTempPath(), fileName);
                System.Diagnostics.Debug.WriteLine($"   Temp file: {tempPath}");

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progressPercentage = (int)((downloadedBytes * 100) / totalBytes);
                            progress?.Report(progressPercentage);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ Download completed: {tempPath}");

                // 다운로드한 파일 실행
                if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // EXE 파일이면 직접 실행
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempPath,
                        UseShellExecute = true
                    });
                    
                    // 현재 애플리케이션 종료
                    WpfApplication.Current.Shutdown();
                }
                else if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // ZIP 파일이면 탐색기로 폴더 열기
                    Process.Start("explorer.exe", $"/select,\"{tempPath}\"");
                    
                    WpfMessageBox.Show(
                        $"업데이트 파일이 다운로드되었습니다.\n\n" +
                        $"파일 위치: {tempPath}\n\n" +
                        $"압축을 해제하고 새 버전을 설치해주세요.",
                        "다운로드 완료",
                        WpfMessageBoxButton.OK,
                        WpfMessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error downloading update: {ex.Message}");
                WpfMessageBox.Show(
                    $"업데이트 다운로드 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "다운로드 오류",
                    WpfMessageBoxButton.OK,
                    WpfMessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 브라우저에서 릴리즈 페이지 열기
        /// </summary>
        public static void OpenReleasePage(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error opening release page: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 현재 Rate Limit 정보 반환
        /// </summary>
        public static RateLimitInfo? GetRateLimitInfo()
        {
            return _rateLimitInfo;
        }
    }

    /// <summary>
    /// GitHub Release JSON 응답 모델
    /// </summary>
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    /// <summary>
    /// 릴리즈 정보
    /// </summary>
    public class ReleaseInfo
    {
        public Version CurrentVersion { get; set; } = new Version(1, 0, 0);
        public Version LatestVersion { get; set; } = new Version(1, 0, 0);
        public bool HasUpdate { get; set; }
        public string ReleaseUrl { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string? FileName { get; set; }
        public string ReleaseNotes { get; set; } = "";
        public DateTime PublishedDate { get; set; }
        public string? ErrorMessage { get; set; }  // 🔥 에러 메시지 추가
    }
    
    /// <summary>
    /// Rate Limit 정보
    /// </summary>
    public class RateLimitInfo
    {
        public int Limit { get; set; }
        public int Remaining { get; set; }
        public DateTime ResetTime { get; set; }
        
        public string GetStatusText()
        {
            var timeUntilReset = ResetTime - DateTime.Now;
            if (timeUntilReset.TotalSeconds < 0)
            {
                return $"{Remaining}/{Limit} 남음 (리셋됨)";
            }
            else if (timeUntilReset.TotalHours >= 1)
            {
                return $"{Remaining}/{Limit} 남음 ({timeUntilReset.Hours}시간 {timeUntilReset.Minutes}분 후 리셋)";
            }
            else
            {
                return $"{Remaining}/{Limit} 남음 ({timeUntilReset.Minutes}분 후 리셋)";
            }
        }
    }
}
