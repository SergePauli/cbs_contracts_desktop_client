using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Services;
using Microsoft.UI.Xaml.Data;
using Windows.Foundation;

namespace CbsContractsDesktopClient.Collections
{
    public class LazyDataCollection<TItem> : ObservableCollection<TItem> , ISupportIncrementalLoading
        where TItem : class
    {
        private const bool DiagnosticsEnabled = false;
        private const int MaxTraceLines = 120;
        private readonly IDataQueryService _dataQueryService;
        private readonly Func<TItem> _placeholderFactory;
        private readonly HashSet<int> _residentIndexes = [];
        private readonly SemaphoreSlim _commitGate = new(1, 1);
        private LazyDataQuery _query;
        private CancellationTokenSource? _viewportLoadCts;
        private bool _isInitialized;
        private bool _isLoading;
        private string _errorMessage = string.Empty;
        private int _totalCount;
        private int _loadedCount;
        private long _viewportRequestVersion;
        private string _lastCountRequestJson = string.Empty;
        private string _lastPageRequestJson = string.Empty;
        private string _traceLog = string.Empty;

        public LazyDataCollection(
            IDataQueryService dataQueryService,
            LazyDataQuery query,
            Func<TItem> placeholderFactory,
            Func<TItem, bool>? isPlaceholder = null)
        {
            _dataQueryService = dataQueryService;
            _query = query;
            _placeholderFactory = placeholderFactory;
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value)
                {
                    return;
                }

                _isLoading = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsLoading)));
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage == value)
                {
                    return;
                }

                _errorMessage = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(ErrorMessage)));
            }
        }

        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (_totalCount == value)
                {
                    return;
                }

                _totalCount = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(TotalCount)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasMoreItems)));
            }
        }

        public int LoadedCount
        {
            get => _loadedCount;
            private set
            {
                if (_loadedCount == value)
                {
                    return;
                }

                _loadedCount = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(LoadedCount)));
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasMoreItems)));
            }
        }

        public int ResidentCount => _residentIndexes.Count;

        public bool HasMoreItems => LoadedCount < TotalCount;

        public string LastCountRequestJson
        {
            get => _lastCountRequestJson;
            private set
            {
                if (_lastCountRequestJson == value)
                {
                    return;
                }

                _lastCountRequestJson = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(LastCountRequestJson)));
            }
        }

        public string LastPageRequestJson
        {
            get => _lastPageRequestJson;
            private set
            {
                if (_lastPageRequestJson == value)
                {
                    return;
                }

                _lastPageRequestJson = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(LastPageRequestJson)));
            }
        }

        public string TraceLog
        {
            get => _traceLog;
            private set
            {
                if (_traceLog == value)
                {
                    return;
                }

                _traceLog = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(TraceLog)));
            }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
            {
                return;
            }

            await RefreshAsync(cancellationToken);
        }

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            if (IsLoading)
            {
                return;
            }

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var countRequest = _query.CreateCountRequest();
                LastCountRequestJson = SerializeRequest(countRequest);
                AppendTrace($"COUNT {countRequest.Model} offset=- limit=- loaded={LoadedCount}");
                TotalCount = await _dataQueryService.GetCountAsync(countRequest, cancellationToken);
                AppendTrace($"COUNT RESULT {countRequest.Model} total={TotalCount}");
                LoadedCount = 0;
                _residentIndexes.Clear();
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(ResidentCount)));

                Clear();

                if (TotalCount > 0)
                {
                    EnsurePlaceholderCapacity(TotalCount);

                    var pageRequest = _query.CreatePageRequest(0, _query.PageSize);
                    LastPageRequestJson = SerializeRequest(pageRequest);
                    AppendTrace($"PAGE {pageRequest.Model} offset={pageRequest.Offset} limit={pageRequest.Limit} loaded={LoadedCount}");
                    var firstPage = await _dataQueryService.GetDataAsync<TItem>(
                        pageRequest,
                        cancellationToken);
                    AppendTrace($"PAGE RESULT {pageRequest.Model} offset={pageRequest.Offset} fetched={firstPage.Count}");

                    ReplaceRange(0, firstPage);
                }

                _isInitialized = true;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasMoreItems)));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task ReplaceQueryAsync(LazyDataQuery query, CancellationToken cancellationToken = default)
        {
            _query = query;
            _isInitialized = false;
            await RefreshAsync(cancellationToken);
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return LoadMoreItemsCoreAsync(count).AsAsyncOperation();
        }

        private async Task<LoadMoreItemsResult> LoadMoreItemsCoreAsync(uint count)
        {
            if (IsLoading || !HasMoreItems)
            {
                return new LoadMoreItemsResult { Count = 0 };
            }

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var offset = LoadedCount;
                var remaining = TotalCount - LoadedCount;
                var requestedCount = count == 0 ? _query.PageSize : (int)count;
                var pageSize = Math.Min(Math.Max(_query.PageSize, requestedCount), remaining);
                var pageRequest = _query.CreatePageRequest(offset, pageSize);
                LastPageRequestJson = SerializeRequest(pageRequest);
                AppendTrace($"PAGE {pageRequest.Model} offset={pageRequest.Offset} limit={pageRequest.Limit} requested={count} loaded={LoadedCount}");

                var page = await _dataQueryService.GetDataAsync<TItem>(
                    pageRequest);
                AppendTrace($"PAGE RESULT {pageRequest.Model} offset={pageRequest.Offset} fetched={page.Count}");

                ReplaceRange(offset, page);

                OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasMoreItems)));
                return new LoadMoreItemsResult { Count = (uint)page.Count };
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return new LoadMoreItemsResult { Count = 0 };
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void EnsurePlaceholderCapacity(int totalCount)
        {
            for (var index = 0; index < totalCount; index++)
            {
                Add(_placeholderFactory());
            }
        }

        public async Task<bool> EnsureRangeLoadedAsync(int startIndex, int endExclusive, CancellationToken cancellationToken = default)
        {
            AppendTrace($"STEP ENSURE 01 enter start={startIndex} end={endExclusive} total={TotalCount}");
            if (TotalCount <= 0)
            {
                AppendTrace("STEP ENSURE 02 exit-total<=0");
                return false;
            }

            var start = Math.Max(0, startIndex);
            var end = Math.Min(TotalCount, endExclusive);
            AppendTrace($"STEP ENSURE 03 normalized start={start} end={end}");
            if (start >= end)
            {
                AppendTrace("STEP ENSURE 04 exit-start>=end");
                return false;
            }

            _viewportLoadCts?.Cancel();
            _viewportLoadCts?.Dispose();
            _viewportLoadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var viewportToken = _viewportLoadCts.Token;
            var requestVersion = Interlocked.Increment(ref _viewportRequestVersion);
            var hasLoadedPages = false;
            AppendTrace($"STEP ENSURE 05 request-created version={requestVersion}");
            try
            {
                for (var pageStart = AlignDown(start, _query.PageSize); pageStart < end; pageStart += _query.PageSize)
                {
                    viewportToken.ThrowIfCancellationRequested();
                    var pageEnd = Math.Min(TotalCount, pageStart + _query.PageSize);
                    AppendTrace($"STEP ENSURE 07 page-check start={pageStart} end={pageEnd}");
                    if (IsRangeResident(pageStart, pageEnd))
                    {
                        AppendTrace($"STEP ENSURE 08 page-resident start={pageStart} end={pageEnd}");
                        continue;
                    }

                    AppendTrace($"STEP ENSURE 09 before-load start={pageStart} len={pageEnd - pageStart}");
                    var page = await LoadPageAsync(pageStart, pageEnd - pageStart, "viewport", viewportToken);
                    AppendTrace($"STEP ENSURE 09a after-load-await start={pageStart} version={requestVersion} current={_viewportRequestVersion}");

                    if (requestVersion != _viewportRequestVersion)
                    {
                        AppendTrace($"STEP ENSURE 09b stale-request version={requestVersion} current={_viewportRequestVersion}");
                        return hasLoadedPages;
                    }

                    AppendTrace($"STEP ENSURE 09c before-commit-wait start={pageStart} version={requestVersion}");
                    await _commitGate.WaitAsync(viewportToken);
                    AppendTrace($"STEP ENSURE 09d commit-acquired start={pageStart} version={requestVersion}");
                    try
                    {
                        if (requestVersion != _viewportRequestVersion)
                        {
                            AppendTrace($"STEP ENSURE 09e stale-before-commit version={requestVersion} current={_viewportRequestVersion}");
                            return hasLoadedPages;
                        }

                        ReplaceRange(pageStart, page);
                        hasLoadedPages = true;
                        AppendTrace($"STEP ENSURE 09f after-commit start={pageStart} version={requestVersion}");
                    }
                    finally
                    {
                        _commitGate.Release();
                        AppendTrace($"STEP ENSURE 09g commit-release start={pageStart} version={requestVersion}");
                    }

                    AppendTrace($"STEP ENSURE 10 after-load start={pageStart} end={pageEnd}");
                }
            }
            finally
            {
                AppendTrace($"STEP ENSURE 11 request-exit version={requestVersion} current={_viewportRequestVersion}");
            }

            return hasLoadedPages;
        }

        public bool ReleaseOutsideRange(int keepStart, int keepEnd)
        {
            if (_residentIndexes.Count == 0)
            {
                return false;
            }

            var residentBefore = ResidentCount;
            var normalizedStart = Math.Max(0, keepStart);
            var normalizedEnd = Math.Max(normalizedStart, Math.Min(TotalCount, keepEnd));
            var indexesToRelease = _residentIndexes
                .Where(index => index < normalizedStart || index >= normalizedEnd)
                .ToList();

            if (indexesToRelease.Count == 0)
            {
                return false;
            }

            foreach (var index in indexesToRelease)
            {
                this[index] = _placeholderFactory();
                _residentIndexes.Remove(index);
            }

            AppendTrace(
                $"RELEASE RESULT keep={normalizedStart}..{normalizedEnd} released={indexesToRelease.Count} residentBefore={residentBefore} residentAfter={ResidentCount}");
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(ResidentCount)));
            return true;
        }

        private void ReplaceRange(int startIndex, IReadOnlyList<TItem> items)
        {
            AppendTrace($"STEP REPLACE 01 enter start={startIndex} count={items.Count}");
            for (var index = 0; index < items.Count; index++)
            {
                var targetIndex = startIndex + index;
                AppendTrace($"STEP REPLACE 02 before-set target={targetIndex}");
                this[targetIndex] = items[index];
                AppendTrace($"STEP REPLACE 03 after-set target={targetIndex}");
                _residentIndexes.Add(targetIndex);
                AppendTrace($"STEP REPLACE 04 after-resident-add target={targetIndex} resident={ResidentCount}");
            }

            AppendTrace($"STEP REPLACE 05 before-loadedcount start={startIndex} count={items.Count} currentLoaded={LoadedCount}");
            LoadedCount = Math.Max(LoadedCount, startIndex + items.Count);
            AppendTrace($"STEP REPLACE 06 after-loadedcount loaded={LoadedCount}");
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(ResidentCount)));
            AppendTrace($"STEP REPLACE 07 exit resident={ResidentCount}");
        }

        private async Task<IReadOnlyList<TItem>> LoadPageAsync(int startIndex, int length, string reason, CancellationToken cancellationToken)
        {
            AppendTrace($"STEP LOAD 01 enter start={startIndex} length={length} reason={reason}");
            if (length <= 0)
            {
                AppendTrace("STEP LOAD 02 exit-length<=0");
                return [];
            }

            IsLoading = true;
            ErrorMessage = string.Empty;
            AppendTrace("STEP LOAD 03 loading-flag-set");

            try
            {
                var pageRequest = _query.CreatePageRequest(startIndex, length);
                AppendTrace($"STEP LOAD 04 request-created offset={pageRequest.Offset} limit={pageRequest.Limit}");
                LastPageRequestJson = SerializeRequest(pageRequest);
                AppendTrace($"PAGE {pageRequest.Model} offset={pageRequest.Offset} limit={pageRequest.Limit} reason={reason} loaded={LoadedCount}");

                AppendTrace($"STEP LOAD 05 before-getdata offset={pageRequest.Offset} limit={pageRequest.Limit}");
                var page = await _dataQueryService.GetDataAsync<TItem>(pageRequest, cancellationToken);
                AppendTrace($"STEP LOAD 06 after-getdata offset={pageRequest.Offset} fetched={page.Count}");
                AppendTrace($"PAGE RESULT {pageRequest.Model} offset={pageRequest.Offset} fetched={page.Count} reason={reason}");
                AppendTrace($"STEP LOAD 07 return-page offset={pageRequest.Offset}");
                return page;
            }
            catch (OperationCanceledException)
            {
                AppendTrace("STEP LOAD 09 canceled");
                throw;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                AppendTrace($"STEP LOAD 10 error message={ex.Message}");
                return [];
            }
            finally
            {
                IsLoading = false;
                AppendTrace("STEP LOAD 11 loading-flag-cleared");
            }
        }

        private bool IsRangeResident(int startIndex, int endExclusive)
        {
            for (var index = startIndex; index < endExclusive; index++)
            {
                if (!_residentIndexes.Contains(index))
                {
                    return false;
                }
            }

            return true;
        }

        private static int AlignDown(int value, int pageSize)
        {
            if (pageSize <= 0)
            {
                return value;
            }

            return (value / pageSize) * pageSize;
        }

        private void AppendTrace(string message)
        {
            if (!DiagnosticsEnabled)
            {
                return;
            }

            var line = $"[{FormatTraceTimestamp(DateTime.Now)}] {message}";
            TraceLog = TrimTrace(
                string.IsNullOrWhiteSpace(TraceLog)
                    ? line
                    : $"{line}{Environment.NewLine}{TraceLog}");
        }

        private static string SerializeRequest(DataQueryRequest request)
        {
            return JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private static string TrimTrace(string trace)
        {
            var lines = trace
                .Split(Environment.NewLine, StringSplitOptions.None)
                .Take(MaxTraceLines);

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatTraceTimestamp(DateTime timestamp)
        {
            return timestamp.ToString("mm':'ss'.'fff");
        }
    }
}
