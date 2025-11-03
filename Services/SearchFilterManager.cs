using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using FACTOVA_LogAnalysis.Models;
using FACTOVA_LogAnalysis.Services;
using FACTOVA_LogAnalysis.Helpers;

namespace FACTOVA_LogAnalysis.Services
{
    /// <summary>
    /// 검색 및 필터 기능을 통합 관리하는 서비스 클래스
    /// 텍스트 검색, DataGrid 필터링, 하이라이트 기능 제공
    /// </summary>
    public class SearchFilterManager
    {
        #region Fields

        private readonly WorkLogService _workLogService;
        private readonly SearchService _searchService;
        private FindDialog? _findDialog;
        
        // 검색 상태
        private string _currentSearchText = "";
        private bool _isCaseSensitive = false;
        private object? _activeSearchTarget;
        
        // 필터 상태
        private readonly Dictionary<string, string> _logLevelFilters = new();
        private readonly Dictionary<string, string> _businessNameFilters = new();
        private readonly Dictionary<string, string> _keywordFilters = new();

        // handler references so we can unsubscribe
        private Action<string, int, int>? _findDialogProgressHandler;
        private Action<int, int>? _searchServiceStatusHandler;

        #endregion

        #region Properties

        public string CurrentSearchText => _currentSearchText;
        public bool IsCaseSensitive => _isCaseSensitive;
        public bool IsSearchActive => !string.IsNullOrWhiteSpace(_currentSearchText);

        #endregion

        #region Events

        public event Action<string, int, int>? SearchProgressUpdated; // 검색어, 현재 인덱스, 총 개수
        public event Action? SearchCompleted;

        #endregion

        #region Constructor

        public SearchFilterManager(WorkLogService workLogService, SearchService searchService)
        {
            _workLogService = workLogService ?? throw new ArgumentNullException(nameof(workLogService));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            
            // SearchService 이벤트 구독
            _search_service_StatusSubscribe();
        }

        private void _search_service_StatusSubscribe()
        {
            _searchService.StatusUpdated += (current, total) => 
            {
                SearchProgressUpdated?.Invoke(_currentSearchText, current, total);
            };
        }

        #endregion

        #region Public Methods - Search

        /// <summary>
        /// 검색 다이얼로그 열기
        /// </summary>
        public void OpenFindDialog(Window owner, object target, string selectedText = "")
        {
            try
            {
                if (_findDialog == null || !_findDialog.IsVisible)
                {
                    _findDialog = new FindDialog(owner as MainWindow, target, selectedText);

                    // subscribe to SearchService.StatusUpdated directly so dialog reliably receives updates
                    _searchServiceStatusHandler = (current, total) =>
                    {
                        try
                        {
                            _findDialog?.Dispatcher.BeginInvoke(() => _findDialog.UpdateStatus(current, total));
                        }
                        catch { }
                    };

                    _searchService.StatusUpdated += _searchServiceStatusHandler;

                    // show initial status
                    _findDialog.UpdateStatus(0, 0);

                    _findDialog.Show();
                }
                else
                {
                    _findDialog.Activate();
                    if (!string.IsNullOrEmpty(selectedText))
                    {
                        _findDialog.SetSelectedText(selectedText);
                    }
                }
                
                _activeSearchTarget = target;
                _workLogService.AddLog("검색 다이얼로그 열림", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 검색 다이얼로그 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 텍스트 검색 및 하이라이트
        /// </summary>
        public void SearchAndHighlight(string searchText, bool caseSensitive, object target)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    ClearSearch();
                    return;
                }

                _currentSearchText = searchText;
                _isCaseSensitive = caseSensitive;
                _activeSearchTarget = target;

                _searchService.FindAndHighlightAll(searchText, caseSensitive, target);
                
                _workLogService.AddLog($"검색 실행: '{searchText}' (대소문자 구분: {caseSensitive})", WorkLogType.Info);
                SearchCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 검색 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 다음 검색 결과로 이동
        /// </summary>
        public void FindNext()
        {
            try
            {
                if (_activeSearchTarget != null && !string.IsNullOrWhiteSpace(_currentSearchText))
                {
                    _searchService.HighlightNext(_activeSearchTarget, _currentSearchText);
                    _workLogService.AddLog("다음 검색 결과로 이동", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 다음 검색 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 이전 검색 결과로 이동
        /// </summary>
        public void FindPrevious()
        {
            try
            {
                if (_activeSearchTarget != null && !string.IsNullOrWhiteSpace(_currentSearchText))
                {
                    _searchService.HighlightPrevious(_activeSearchTarget, _currentSearchText);
                    _workLogService.AddLog("이전 검색 결과로 이동", WorkLogType.Info);
                }
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 이전 검색 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 검색 초기화
        /// </summary>
        public void ClearSearch()
        {
            try
            {
                _searchService.ClearSearch();
                _currentSearchText = "";
                _isCaseSensitive = false;
                _activeSearchTarget = null;
                
                _workLogService.AddLog("검색 결과 초기화", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 검색 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 검색 다이얼로그 닫기
        /// </summary>
        public void CloseFindDialog()
        {
            try
            {
                // unsubscribe handler if set
                if (_searchServiceStatusHandler != null)
                {
                    _searchService.StatusUpdated -= _searchServiceStatusHandler;
                    _searchServiceStatusHandler = null;
                }

                if (_findDialogProgressHandler != null)
                {
                    SearchProgressUpdated -= _findDialogProgressHandler;
                    _findDialogProgressHandler = null;
                }

                _findDialog?.Close();
                _findDialog = null;
                _workLogService.AddLog("? 검색 다이얼로그 닫힘", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 검색 다이얼로그 닫기 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion

        #region Public Methods - Filters

        /// <summary>
        /// DataGrid에 로그 레벨 필터 적용
        /// </summary>
        public void ApplyLogLevelFilter(string dataGridKey, string logLevel, DataGrid dataGrid, ObservableCollection<LogLineItem> items)
        {
            try
            {
                _logLevelFilters[dataGridKey] = logLevel;
                ApplyAllFilters(dataGridKey, dataGrid, items);
                
                _workLogService.AddLog($"{dataGridKey} 로그 레벨 필터: {logLevel}", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 로그 레벨 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// DataGrid에 비즈니스명 필터 적용
        /// </summary>
        public void ApplyBusinessNameFilter(string dataGridKey, string businessName, DataGrid dataGrid, ObservableCollection<LogLineItem> items)
        {
            try
            {
                _businessNameFilters[dataGridKey] = businessName;
                ApplyAllFilters(dataGridKey, dataGrid, items);
                
                _workLogService.AddLog($"??? {dataGridKey} 비즈니스명 필터: {businessName}", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 비즈니스명 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// DataGrid에 키워드 필터 적용
        /// </summary>
        public void ApplyKeywordFilter(string dataGridKey, string keyword, DataGrid dataGrid, ObservableCollection<LogLineItem> items)
        {
            try
            {
                _keywordFilters[dataGridKey] = keyword;
                ApplyAllFilters(dataGridKey, dataGrid, items);
                
                _workLogService.AddLog($"{dataGridKey} 키워드 필터: '{keyword}'", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 키워드 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 모든 DataGrid에 글로벌 키워드 필터 적용
        /// </summary>
        public void ApplyGlobalKeywordFilter(string keyword, 
            Dictionary<string, (DataGrid dataGrid, ObservableCollection<LogLineItem> items)> dataGrids)
        {
            try
            {
                foreach (var kvp in dataGrids)
                {
                    string key = kvp.Key;
                    var (dataGrid, items) = kvp.Value;
                    
                    _keywordFilters[key] = keyword;
                    ApplyAllFilters(key, dataGrid, items);
                }
                
                var totalFiltered = GetTotalFilteredCount(dataGrids);
                _workLogService.AddLog($"글로벌 키워드 필터: '{keyword}' (총 {totalFiltered}개 행)", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 글로벌 키워드 필터 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 특정 DataGrid의 모든 필터 초기화
        /// </summary>
        public void ClearFilters(string dataGridKey, DataGrid dataGrid, ObservableCollection<LogLineItem> items)
        {
            try
            {
                _logLevelFilters.Remove(dataGridKey);
                _businessNameFilters.Remove(dataGridKey);
                _keywordFilters.Remove(dataGridKey);
                
                var view = CollectionViewSource.GetDefaultView(items);
                if (view != null)
                {
                    view.Filter = null;
                    view.Refresh();
                }
                
                _workLogService.AddLog($"{dataGridKey} 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 모든 DataGrid의 필터 초기화
        /// </summary>
        public void ClearAllFilters(Dictionary<string, (DataGrid dataGrid, ObservableCollection<LogLineItem> items)> dataGrids)
        {
            try
            {
                _logLevelFilters.Clear();
                _businessNameFilters.Clear();
                _keywordFilters.Clear();
                
                foreach (var kvp in dataGrids)
                {
                    var (dataGrid, items) = kvp.Value;
                    var view = CollectionViewSource.GetDefaultView(items);
                    if (view != null)
                    {
                        view.Filter = null;
                        view.Refresh();
                    }
                }
                
                _workLogService.AddLog("모든 필터 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 전체 필터 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// DataGrid 하이라이트 적용
        /// </summary>
        public void ApplySearchHighlight(string searchText, ObservableCollection<LogLineItem> items)
        {
            try
            {
                LogDataGridHelper.HighlightSearchResults(items, searchText);
                _workLogService.AddLog($"검색 하이라이트 적용: '{searchText}'", WorkLogType.Info);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 하이라이트 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 모든 하이라이트 초기화
        /// </summary>
        public void ClearAllHighlights(Dictionary<string, ObservableCollection<LogLineItem>> collections)
        {
            try
            {
                foreach (var kvp in collections)
                {
                    LogDataGridHelper.ClearHighlights(kvp.Value);
                }
                
                _workLogService.AddLog("모든 하이라이트 초기화", WorkLogType.Success);
            }
            catch (Exception ex)
            {
                _workLogService.AddLog($"? 하이라이트 초기화 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        #endregion

        #region Public Methods - Status

        /// <summary>
        /// 필터 상태 정보 반환
        /// </summary>
        public string GetFilterStatus()
        {
            try
            {
                int logLevelCount = _logLevelFilters.Count(f => f.Value != "ALL");
                int businessNameCount = _businessNameFilters.Count(f => f.Value != "ALL");
                int keywordCount = _keywordFilters.Count(f => !string.IsNullOrWhiteSpace(f.Value));
                
                return $"활성 필터: 로그레벨({logLevelCount}), 비즈니스명({businessNameCount}), 키워드({keywordCount})";
            }
            catch
            {
                return "필터 상태 확인 중...";
            }
        }

        /// <summary>
        /// 검색 상태 정보 반환
        /// </summary>
        public string GetSearchStatus()
        {
            if (IsSearchActive)
            {
                return $"검색 중: '{_currentSearchText}' (대소문자 구분: {_isCaseSensitive})";
            }
            return "검색 비활성";
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 특정 DataGrid에 모든 필터 적용
        /// </summary>
        private void ApplyAllFilters(string dataGridKey, DataGrid dataGrid, ObservableCollection<LogLineItem> items)
        {
            try
            {
                var view = CollectionViewSource.GetDefaultView(items);
                if (view == null) return;

                string logLevel = _logLevelFilters.GetValueOrDefault(dataGridKey, "ALL");
                string businessName = _businessNameFilters.GetValueOrDefault(dataGridKey, "ALL");
                string keyword = _keywordFilters.GetValueOrDefault(dataGridKey, "");

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
                _workLogService.AddLog($"? 필터 적용 오류: {ex.Message}", WorkLogType.Error);
            }
        }

        /// <summary>
        /// 전체 필터링된 행 수 계산
        /// </summary>
        private int GetTotalFilteredCount(Dictionary<string, (DataGrid dataGrid, ObservableCollection<LogLineItem> items)> dataGrids)
        {
            try
            {
                int totalCount = 0;
                foreach (var kvp in dataGrids)
                {
                    var (dataGrid, items) = kvp.Value;
                    var view = CollectionViewSource.GetDefaultView(items);
                    totalCount += view?.Cast<object>().Count() ?? 0;
                }
                return totalCount;
            }
            catch
            {
                return dataGrids.Values.Sum(pair => pair.items.Count);
            }
        }

        #endregion
    }
}