using System.Text.Json;
using CbsContractsDesktopClient.Models;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Definitions.TablePageDefinitions;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Services.Settings;
using CbsContractsDesktopClient.Services.Workspace;
using CbsContractsDesktopClient.Stores.Table;
using CbsContractsDesktopClient.ViewModels.Shell;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class TablePageStoreTests : IDisposable
{
    private readonly string _temporaryDirectory;
    private readonly string _settingsFilePath;

    public TablePageStoreTests()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "CbsContractsDesktopClient.Tests", Guid.NewGuid().ToString("N"));
        _settingsFilePath = Path.Combine(_temporaryDirectory, "user-settings.json");
    }

    [Fact]
    public async Task EnsureLoadedAsync_DoesNotLoadStageSpecificStatusOptions()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Status"] =
                [
                    CreateRow(("id", 1), ("name", "РџРѕРґРїРёСЃР°РЅ")),
                    CreateRow(("id", 2), ("name", "РћР¶РёРґР°РµС‚СЃСЏ")),
                    CreateRow(("id", 0), ("name", "Р’ РїСЂРѕРµРєС‚Рµ")),
                    CreateRow(("id", 7), ("name", "Р—Р°РјРѕСЂРѕР¶РµРЅ")),
                    CreateRow(("id", 4), ("name", "Р’С‹РїРѕР»РЅРµРЅ")),
                    CreateRow(("id", 5), ("name", "Р—Р°РєСЂС‹С‚")),
                    CreateRow(("id", 3), ("name", "РџСЂРѕС„РёРЅР°РЅСЃРёСЂРѕРІР°РЅ")),
                    CreateRow(("id", 6), ("name", "РћС‚РјРµРЅРµРЅ"))
                ],
                ["Stage"] = []
            }
        };
        var settingsService = new LocalUserSettingsService(_settingsFilePath);
        var referenceDefinitionService = new ReferenceDefinitionService(new TableSettingsService(settingsService));
        var tablePageDefinitionService = new TablePageDefinitionService(referenceDefinitionService, new TableSettingsService(settingsService));
        var shellViewModel = new AppShellViewModel(new FakeUserService())
        {
            CurrentRoute = "/stages"
        };
        var viewModel = new TablePageStore(
            shellViewModel,
            dataQueryService,
            referenceDefinitionService,
            tablePageDefinitionService,
            new ReferenceLookupCacheService(dataQueryService));

        await viewModel.EnsureLoadedAsync();

        var options = viewModel.CurrentFilterOptionsSources["StageStatus"];
        Assert.Empty(options);
        Assert.DoesNotContain(dataQueryService.DataRequests, static request => request.Model == "Status");
    }

    [Fact]
    public async Task EnsureLoadedAsync_DoesNotFormatStageTaskKindOptions()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Status"] =
                [
                    CreateRow(("id", 2), ("name", "РћР¶РёРґР°РµС‚СЃСЏ")),
                    CreateRow(("id", 4), ("name", "Р’С‹РїРѕР»РЅРµРЅ")),
                    CreateRow(("id", 5), ("name", "Р—Р°РєСЂС‹С‚")),
                    CreateRow(("id", 6), ("name", "РћС‚РјРµРЅРµРЅ")),
                    CreateRow(("id", 7), ("name", "Р—Р°РјРѕСЂРѕР¶РµРЅ"))
                ],
                ["TaskKind"] =
                [
                    CreateRow(("id", 7), ("code", "06"), ("name", "Р”РѕРї. Р°С‚С‚РµСЃС‚Р°С†РёСЏ")),
                    CreateRow(("id", 11), ("code", "11"), ("name", "РџРѕСЃС‚Р°РІРєР° РџРћ"))
                ],
                ["Stage"] = []
            }
        };
        var settingsService = new LocalUserSettingsService(_settingsFilePath);
        var referenceDefinitionService = new ReferenceDefinitionService(new TableSettingsService(settingsService));
        var tablePageDefinitionService = new TablePageDefinitionService(referenceDefinitionService, new TableSettingsService(settingsService));
        var shellViewModel = new AppShellViewModel(new FakeUserService())
        {
            CurrentRoute = "/stages"
        };
        var viewModel = new TablePageStore(
            shellViewModel,
            dataQueryService,
            referenceDefinitionService,
            tablePageDefinitionService,
            new ReferenceLookupCacheService(dataQueryService));

        await viewModel.EnsureLoadedAsync();

        var options = viewModel.CurrentFilterOptionsSources["TaskKind"];
        Assert.Empty(options);
        Assert.DoesNotContain(dataQueryService.DataRequests, static request => request.Model == "TaskKind");
    }

    [Fact]
    public async Task ApplySavedRowUpdate_ReplacesLoadedRowWithoutReloading()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Position"] =
                [
                    CreateRow(("id", 1), ("name", "Old"), ("description", "Keep"))
                ]
            }
        };
        var viewModel = CreateViewModel(dataQueryService, "/references/Position");

        await viewModel.EnsureLoadedAsync();
        viewModel.SelectedRow = viewModel.Items[0];
        var dataRequestCount = dataQueryService.DataRequestCount;
        var countRequestCount = dataQueryService.CountRequestCount;

        var applied = viewModel.ApplySavedRowUpdate(
            CreateRow(("id", 1), ("name", "Server")),
            new Dictionary<string, object?>
            {
                ["id"] = 1L,
                ["name"] = "Local",
                ["person_attributes"] = new Dictionary<string, object?>()
            });

        Assert.True(applied);
        Assert.Equal(dataRequestCount, dataQueryService.DataRequestCount);
        Assert.Equal(countRequestCount, dataQueryService.CountRequestCount);
        Assert.Equal("Server", viewModel.Items[0].GetValue("name"));
        Assert.Equal("Keep", viewModel.Items[0].GetValue("description"));
        Assert.Null(viewModel.Items[0].GetValue("person_attributes"));
        Assert.Same(viewModel.Items[0], viewModel.SelectedRow);
    }

    [Fact]
    public async Task ApplySavedRowUpdate_UsesPayloadWhenResponseOmitsChangedField()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Position"] =
                [
                    CreateRow(("id", 2), ("name", "Old"))
                ]
            }
        };
        var viewModel = CreateViewModel(dataQueryService, "/references/Position");

        await viewModel.EnsureLoadedAsync();

        var applied = viewModel.ApplySavedRowUpdate(
            CreateRow(("id", 2)),
            new Dictionary<string, object?>
            {
                ["id"] = 2L,
                ["name"] = "Local"
            });

        Assert.True(applied);
        Assert.Equal("Local", viewModel.Items[0].GetValue("name"));
    }

    [Fact]
    public async Task ApplySavedRowUpdate_ReturnsFalseWhenRowIsNotLoaded()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Position"] =
                [
                    CreateRow(("id", 1), ("name", "Old"))
                ]
            }
        };
        var viewModel = CreateViewModel(dataQueryService, "/references/Position");

        await viewModel.EnsureLoadedAsync();

        var applied = viewModel.ApplySavedRowUpdate(
            CreateRow(("id", 99), ("name", "Missing")),
            new Dictionary<string, object?>
            {
                ["id"] = 99L,
                ["name"] = "Missing"
            });

        Assert.False(applied);
        Assert.Equal("Old", viewModel.Items[0].GetValue("name"));
    }

    [Fact]
    public async Task ResetFiltersAsync_ForStagesAppliesUserDefaultFilters()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Status"] = [],
                ["TaskKind"] = [],
                ["Stage"] = []
            }
        };
        var userService = new FakeUserService
        {
            CurrentUser = new User
            {
                Username = "tester",
                Role = "admin",
                Statuses = JsonSerializer.Serialize(new Dictionary<string, string?>
                {
                    ["s_statuses"] = JsonSerializer.Serialize(new object?[]
                    {
                        new Dictionary<string, object?> { ["id"] = 2L, ["name"] = "Р’ СЂР°Р±РѕС‚Рµ" },
                        new Dictionary<string, object?> { ["id"] = 4L, ["name"] = "Р’С‹РїРѕР»РЅРµРЅРѕ" },
                        new Dictionary<string, object?> { ["id"] = null, ["name"] = "Пустой" }
                    }),
                    ["s_funded"] = "false"
                })
            }
        };
        var viewModel = CreateViewModel(dataQueryService, "/stages", userService);

        await viewModel.EnsureLoadedAsync();

        var filters = await viewModel.ResetFiltersAsync();

        var statusFilter = Assert.Single(filters, static filter => filter.FieldKey == "status");
        Assert.Equal(DataFilterMatchMode.In, statusFilter.MatchMode);
        Assert.Equal([2L, 4L, null], Assert.IsAssignableFrom<IReadOnlyList<long?>>(statusFilter.Value));

        var fundedFilter = Assert.Single(filters, static filter => filter.FieldKey == "is_funded");
        Assert.Equal(DataFilterMatchMode.Equals, fundedFilter.MatchMode);
        Assert.Equal(false, fundedFilter.Value);
    }

    [Fact]
    public async Task ClearFiltersAsync_RemovesEveryFilter()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Status"] = [],
                ["TaskKind"] = [],
                ["Stage"] = []
            }
        };
        var userService = new FakeUserService
        {
            CurrentUser = new User
            {
                Username = "tester",
                Role = "admin",
                Statuses = JsonSerializer.Serialize(new Dictionary<string, string?>
                {
                    ["s_statuses"] = JsonSerializer.Serialize(new object?[]
                    {
                        new Dictionary<string, object?> { ["id"] = 2L, ["name"] = "Р’ СЂР°Р±РѕС‚Рµ" }
                    }),
                    ["s_funded"] = "false"
                })
            }
        };
        var viewModel = CreateViewModel(dataQueryService, "/stages", userService);

        await viewModel.EnsureLoadedAsync();
        await viewModel.ResetFiltersAsync();

        var filters = await viewModel.ClearFiltersAsync();

        Assert.Empty(filters);
        Assert.Empty(viewModel.CurrentFilters);
        Assert.All(viewModel.FilterFields, static filter => Assert.Null(filter.Value));
    }

    [Fact]
    public async Task EnsureViewportWindowLoadedAsync_SkipsEmptyVisibleWindow()
    {
        var dataQueryService = new FakeDataQueryService
        {
            RowsByModel =
            {
                ["Position"] = Enumerable.Range(1, 120)
                    .Select(id => CreateRow(("id", id), ("name", $"Position {id}")))
                    .ToList()
            }
        };
        var viewModel = CreateViewModel(dataQueryService, "/references/Position");

        await viewModel.EnsureLoadedAsync();
        dataQueryService.ResetRequestCounters();

        await viewModel.EnsureViewportWindowLoadedAsync(256, 256, 1);

        Assert.Equal(0, dataQueryService.DataRequestCount);
        Assert.Equal(0, dataQueryService.CountRequestCount);
        Assert.Empty(dataQueryService.DataRequests);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static TableDataRow CreateRow(params (string Key, object? Value)[] values)
    {
        return new TableDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
        };
    }

    private TablePageStore CreateViewModel(
        FakeDataQueryService dataQueryService,
        string route,
        FakeUserService? userService = null)
    {
        userService ??= new FakeUserService();
        var settingsService = new LocalUserSettingsService(_settingsFilePath);
        var referenceDefinitionService = new ReferenceDefinitionService(new TableSettingsService(settingsService));
        var tablePageDefinitionService = new TablePageDefinitionService(referenceDefinitionService, new TableSettingsService(settingsService));
        var shellViewModel = new AppShellViewModel(userService)
        {
            CurrentRoute = route
        };

        return new TablePageStore(
            shellViewModel,
            dataQueryService,
            referenceDefinitionService,
            tablePageDefinitionService,
            new ReferenceLookupCacheService(dataQueryService),
            userService);
    }

    private sealed class FakeDataQueryService : IDataQueryService
    {
        public Dictionary<string, IReadOnlyList<TableDataRow>> RowsByModel { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int DataRequestCount { get; private set; }

        public int CountRequestCount { get; private set; }

        public List<(string Model, int? Offset, int? Limit)> DataRequests { get; } = [];

        public void ResetRequestCounters()
        {
            DataRequestCount = 0;
            CountRequestCount = 0;
            DataRequests.Clear();
        }

        public Task<IReadOnlyList<TItem>> GetDataAsync<TItem>(
            DataQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            DataRequestCount++;
            DataRequests.Add((request.Model, request.Offset, request.Limit));
            var rows = RowsByModel.GetValueOrDefault(request.Model) ?? [];
            var offset = request.Offset ?? 0;
            var limit = request.Limit ?? rows.Count;
            return Task.FromResult<IReadOnlyList<TItem>>(rows.Skip(offset).Take(limit).Cast<TItem>().ToList());
        }

        public Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default)
        {
            CountRequestCount++;
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




