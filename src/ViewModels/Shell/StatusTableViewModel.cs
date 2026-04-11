using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;

namespace CbsContractsDesktopClient.ViewModels.Shell
{
    public partial class StatusTableViewModel : ObservableObject
    {
        private readonly IDataQueryService _dataQueryService;
        private bool _isLoaded;

        public StatusTableViewModel(IDataQueryService dataQueryService)
        {
            _dataQueryService = dataQueryService;
        }

        public ObservableCollection<StatusItem> Items { get; } = [];

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        [ObservableProperty]
        public partial string ErrorMessage { get; set; } = string.Empty;

        public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (_isLoaded || IsLoading)
            {
                return;
            }

            await ReloadAsync(cancellationToken);
        }

        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                var data = await _dataQueryService.GetDataAsync<StatusItem>(
                    new DataQueryRequest
                    {
                        Model = "Status",
                        Preset = "item",
                        Sorts = ["id asc"],
                        Limit = 200,
                        Offset = 0
                    },
                    cancellationToken);

                Items.Clear();
                foreach (var item in data)
                {
                    Items.Add(item);
                }

                _isLoaded = true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Не удалось загрузить справочник Status: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
