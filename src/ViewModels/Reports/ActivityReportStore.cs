using CommunityToolkit.Mvvm.ComponentModel;

namespace CbsContractsDesktopClient.ViewModels.Reports;

public sealed partial class ActivityReportStore : ObservableObject
{
    [ObservableProperty]
    public partial DateTimeOffset StartDate { get; set; } = DateTimeOffset.Now.Date.AddDays(-1);

    [ObservableProperty]
    public partial DateTimeOffset EndDate { get; set; } = DateTimeOffset.Now.Date;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ActivityReportSection> Sections { get; private set; } = [];

    public async Task LoadAsync(ActivityReportLoader loader, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loader);
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var result = await loader.LoadAsync(StartDate.Date, EndDate.Date, cancellationToken);
            Sections = result.Sections;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
