using System.Text.Json;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.ViewModels.Reports;
using Xunit;

namespace CbsContractsDesktopClient.Tests;

public sealed class ActivityReportLoaderTests
{
    [Fact]
    public async Task LoadAsync_MissingTargets_UsesCreatedAuditsAndSplitsDetail()
    {
        var queryService = new ActivityReportQueryService();
        var loader = new ActivityReportLoader(queryService);

        var result = await loader.LoadAsync(
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero));

        var stageRow = Assert.Single(result.Sections.Single(
            static section => section.Kind == ActivityReportSectionKind.StatusChanges).Rows).DisplayRow;
        Assert.Equal("50/22/028_Э3", stageRow.GetValue("num")?.ToString());
        Assert.Equal("Контрольная проверка", stageRow.GetValue("contragent")?.ToString());
        Assert.Equal("удален", stageRow.GetValue("cost")?.ToString());

        var contractRow = Assert.Single(result.Sections.Single(
            static section => section.Kind == ActivityReportSectionKind.AddedContracts).Rows).DisplayRow;
        Assert.Equal("61/26/001", contractRow.GetValue("num")?.ToString());
        Assert.Equal("ООО Контрагент", contractRow.GetValue("contragent")?.ToString());
        Assert.Equal("удален", contractRow.GetValue("cost")?.ToString());

        var auditRequest = Assert.Single(queryService.DeletedTargetAuditRequests);
        var filters = Assert.IsType<Dictionary<string, object?>>(auditRequest.Filters);
        Assert.Equal(0, filters["action__eq"]);
        Assert.True(filters.ContainsKey("or"));
    }

    private sealed class ActivityReportQueryService : IDataQueryService
    {
        public List<DataQueryRequest> DeletedTargetAuditRequests { get; } = [];

        public Task<IReadOnlyList<TItem>> GetDataAsync<TItem>(
            DataQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var rows = ResolveRows(request);
            return Task.FromResult((IReadOnlyList<TItem>)(object)rows);
        }

        public Task<int> GetCountAsync(DataQueryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<DataQueryPage<TItem>> GetPageAsync<TItem>(
            DataQueryRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private IReadOnlyList<TableDataRow> ResolveRows(DataQueryRequest request)
        {
            var filters = Assert.IsType<Dictionary<string, object?>>(request.Filters);
            if (request.Model == "Stage" || request.Model == "Contract")
            {
                return [];
            }

            if (request.Model == "Comment")
            {
                return [];
            }

            if (filters.ContainsKey("or"))
            {
                DeletedTargetAuditRequests.Add(request);
                return
                [
                    Row(("id", 35692L), ("obj_id", 6393L), ("where", "Этапы"), ("detail", "* 50/22/028_Э3 Контрольная проверка")),
                    Row(("id", 35693L), ("obj_id", 6156L), ("where", "Контракты"), ("detail", "* 61/26/001 ООО Контрагент"))
                ];
            }

            if (filters.ContainsKey("auditable_field__eq")
                && Equals(filters["auditable_field__eq"], "status")
                && !filters.ContainsKey("after__eq"))
            {
                return [Row(("id", 1L), ("obj_id", 6393L), ("where", "Stage"))];
            }

            if (filters.ContainsKey("action__eq")
                && filters.ContainsKey("auditable_type__eq")
                && Equals(filters["auditable_type__eq"], "Contract"))
            {
                return [Row(("id", 2L), ("obj_id", 6156L), ("where", "Contract"))];
            }

            return [];
        }
    }

    private static TableDataRow Row(params (string Key, object? Value)[] values)
    {
        return new TableDataRow
        {
            Values = values.ToDictionary(
                static value => value.Key,
                static value => JsonSerializer.SerializeToElement(value.Value))
        };
    }
}
