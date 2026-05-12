using System.Text.Json;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Services.Workspace;
using CbsContractsDesktopClient.ViewModels.Shell;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ReferencesContentViewModelTests : IDisposable
{
    private readonly string _temporaryDirectory;
    private readonly string _settingsFilePath;

    public ReferencesContentViewModelTests()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "CbsContractsDesktopClient.Tests", Guid.NewGuid().ToString("N"));
        _settingsFilePath = Path.Combine(_temporaryDirectory, "user-settings.json");
    }

    [Fact]
    public async Task EnsureLoadedAsync_UsesStageSpecificStatusOptions()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Status"] =
                [
                    CreateRow(("id", 1), ("name", "Подписан")),
                    CreateRow(("id", 2), ("name", "Ожидается")),
                    CreateRow(("id", 0), ("name", "В проекте")),
                    CreateRow(("id", 7), ("name", "Заморожен")),
                    CreateRow(("id", 4), ("name", "Выполнен")),
                    CreateRow(("id", 5), ("name", "Закрыт")),
                    CreateRow(("id", 3), ("name", "Профинансирован")),
                    CreateRow(("id", 6), ("name", "Отменен"))
                ],
                ["Stage"] = []
            }
        };
        var settingsService = new LocalUserSettingsService(_settingsFilePath);
        var referenceDefinitionService = new ReferenceDefinitionService(settingsService);
        var tablePageDefinitionService = new TablePageDefinitionService(referenceDefinitionService, settingsService);
        var shellViewModel = new AppShellViewModel(new FakeUserService())
        {
            CurrentRoute = "/stages"
        };
        var viewModel = new ReferencesContentViewModel(
            shellViewModel,
            dataQueryService,
            referenceDefinitionService,
            tablePageDefinitionService,
            new ReferenceLookupCacheService(dataQueryService));

        await viewModel.EnsureLoadedAsync();

        var options = viewModel.CurrentFilterOptionsSources["StageStatus"];
        Assert.Equal([null, 2L, 4L, 5L, 6L, 7L], options.Select(static option => option.Value));
        Assert.Equal("Пустой", options[0].Label);
        Assert.DoesNotContain(options, static option => option.Value is 0L or 1L or 3L);
    }

    [Fact]
    public async Task EnsureLoadedAsync_FormatsStageTaskKindOptionsWithCodeAndName()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Status"] =
                [
                    CreateRow(("id", 2), ("name", "Ожидается")),
                    CreateRow(("id", 4), ("name", "Выполнен")),
                    CreateRow(("id", 5), ("name", "Закрыт")),
                    CreateRow(("id", 6), ("name", "Отменен")),
                    CreateRow(("id", 7), ("name", "Заморожен"))
                ],
                ["TaskKind"] =
                [
                    CreateRow(("id", 7), ("code", "06"), ("name", "Доп. аттестация")),
                    CreateRow(("id", 11), ("code", "11"), ("name", "Поставка ПО"))
                ],
                ["Stage"] = []
            }
        };
        var settingsService = new LocalUserSettingsService(_settingsFilePath);
        var referenceDefinitionService = new ReferenceDefinitionService(settingsService);
        var tablePageDefinitionService = new TablePageDefinitionService(referenceDefinitionService, settingsService);
        var shellViewModel = new AppShellViewModel(new FakeUserService())
        {
            CurrentRoute = "/stages"
        };
        var viewModel = new ReferencesContentViewModel(
            shellViewModel,
            dataQueryService,
            referenceDefinitionService,
            tablePageDefinitionService,
            new ReferenceLookupCacheService(dataQueryService));

        await viewModel.EnsureLoadedAsync();

        var options = viewModel.CurrentFilterOptionsSources["TaskKind"];
        Assert.Contains(options, static option => Equals(option.Value, 7L) && option.Label == "06 - Доп. аттестация");
        Assert.Contains(options, static option => Equals(option.Value, 11L) && option.Label == "11 - Поставка ПО");
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static ReferenceDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new ReferenceDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
        };
    }

    private sealed class FakeDataQueryService : IDataQueryService
    {
        public Dictionary<string, IReadOnlyList<ReferenceDataRow>> RowsByModel { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<TItem>> GetDataAsync<TItem>(
            DataQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var rows = RowsByModel.GetValueOrDefault(request.Model) ?? [];
            return Task.FromResult<IReadOnlyList<TItem>>(rows.Cast<TItem>().ToList());
        }

        public Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            var rows = RowsByModel.GetValueOrDefault(request.Model) ?? [];
            return Task.FromResult(rows.Count);
        }

        public async Task<DataQueryPage<TItem>> GetPageAsync<TItem>(
            DataQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            return new DataQueryPage<TItem>
            {
                Items = await GetDataAsync<TItem>(request, cancellationToken),
                TotalCount = await GetCountAsync(request, cancellationToken)
            };
        }
    }

    private sealed class FakeUserService : IUserService
    {
        public User? CurrentUser { get; set; } = new()
        {
            Username = "tester",
            Role = "admin"
        };

        public bool IsAuthenticated => CurrentUser is not null;

        public void SetCurrentUser(User user)
        {
            CurrentUser = user;
        }

        public void ClearCurrentUser()
        {
            CurrentUser = null;
        }

        public bool HasRole(string role)
        {
            return string.Equals(CurrentUser?.Role, role, StringComparison.OrdinalIgnoreCase);
        }
    }
}
