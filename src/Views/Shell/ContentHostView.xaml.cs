using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.Data;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.ViewModels.Shell;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed partial class ContentHostView : UserControl
    {
        private readonly ReferencesContentViewModel _viewModel;
        private readonly IReferenceCrudService _referenceCrudService;
        private readonly IHolidayRecalculationService _holidayRecalculationService;
        private readonly IDataQueryService _dataQueryService;
        private readonly IUserService _userService;
        private CancellationTokenSource? _filterDebounceCts;
        private CancellationTokenSource? _viewportCts;
        private bool _isViewportSubscribed;
        private bool _isHolidayRecalcInProgress;
        private static readonly ReferenceDefinition StageEditDefinition = new()
        {
            Route = "/internal/Stage",
            Model = "Stage",
            Title = "Stage",
            Preset = "edit"
        };

        public ContentHostView()
        {
            _viewModel = App.Services.GetRequiredService<ReferencesContentViewModel>();
            _referenceCrudService = App.Services.GetRequiredService<IReferenceCrudService>();
            _holidayRecalculationService = App.Services.GetRequiredService<IHolidayRecalculationService>();
            _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
            _userService = App.Services.GetRequiredService<IUserService>();
            InitializeComponent();
            DataContext = _viewModel;
            UpdateSelectionActionButtons();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateSelectionActionButtons();
            EnsureViewportSubscription();
            await _viewModel.EnsureLoadedAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _filterDebounceCts?.Cancel();
            _viewportCts?.Cancel();
            RemoveViewportSubscription();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ReferencesContentViewModel.SelectedRow)
                || e.PropertyName == nameof(ReferencesContentViewModel.HasSelectedRow)
                || e.PropertyName == nameof(ReferencesContentViewModel.HasActiveReference))
            {
                UpdateSelectionActionButtons();
            }
        }

        private async void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            _filterDebounceCts?.Cancel();
            await _viewModel.ResetFiltersAsync();
            ReferenceTableView.ClearFilterInputs();
        }

        private async void CreateRowButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowReferenceEditDialogAsync(isCreateMode: true);
        }

        private async void EditSelectedRowButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowReferenceEditDialogAsync(isCreateMode: false);
        }

        private async void DeleteSelectedRowButton_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSelectedRowAsync();
        }

        private async void HolidayRecalcButton_Click(object sender, RoutedEventArgs e)
        {
            await RecalculateHolidayStagesAsync();
        }

        private async void ResetColumnWidthsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ResetColumnWidthsAsync();
        }

        private async void ResetFiltersMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _filterDebounceCts?.Cancel();
            await _viewModel.ResetFiltersAsync();
            ReferenceTableView.ClearFilterInputs();
        }

        private async void ResetSortingMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.ClearSortsAsync();
        }

        private async void ReferenceTableView_SortRequested(object sender, CbsTableSortRequestedEventArgs e)
        {
            if (e.Direction.HasValue)
            {
                await _viewModel.ApplySortAsync(e.FieldKey, e.Direction.Value);
            }
            else
            {
                await _viewModel.ClearSortsAsync();
            }
        }

        private async void ReferenceTableView_LoadMoreRequested(object sender, CbsTableLoadMoreRequestedEventArgs e)
        {
            await _viewModel.LoadMoreAsync();
        }

        private async void ReferenceTableView_RowDoubleTapped(object sender, CbsTableRowDoubleTappedEventArgs e)
        {
            _viewModel.SelectedRow = e.Row;

            if (IsInternEditBlocked())
            {
                return;
            }

            await ShowReferenceEditDialogAsync(isCreateMode: false);
        }

        private async void ReferenceTableView_FilterRequested(object sender, CbsTableFilterRequestedEventArgs e)
        {
            _viewModel.AppendUiTrace(
                $"FILTER UI REQUEST field={e.FieldKey} mode={e.MatchMode} value={DescribeFilterValue(e.Value)}");
            _filterDebounceCts?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _filterDebounceCts = cancellationTokenSource;

            try
            {
                await Task.Delay(250, cancellationTokenSource.Token);
                _viewModel.AppendUiTrace(
                    $"FILTER UI DISPATCH field={e.FieldKey} mode={e.MatchMode} value={DescribeFilterValue(e.Value)}");
                await _viewModel.ApplyFilterAsync(
                    e.FieldKey,
                    e.MatchMode,
                    e.Value,
                    cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _viewModel.AppendUiTrace(
                    $"FILTER UI CANCELED field={e.FieldKey} mode={e.MatchMode}");
            }
        }

        private static string DescribeFilterValue(object? value)
        {
            if (value is null)
            {
                return "<empty>";
            }

            if (value is string text)
            {
                return string.IsNullOrWhiteSpace(text) ? "<empty>" : text;
            }

            if (value is System.Collections.IEnumerable sequence)
            {
                var items = sequence.Cast<object?>().ToArray();
                return items.Length == 0
                    ? "<empty>"
                    : $"[{string.Join(", ", items.Select(static item => item?.ToString() ?? "null"))}]";
            }

            return value.ToString() ?? "<empty>";
        }

        private async void ReferenceTableView_ColumnWidthChanged(object sender, CbsTableColumnWidthChangedEventArgs e)
        {
            await _viewModel.SaveColumnWidthAsync(e.FieldKey, e.Width);
        }

        private void ReferenceTableView_TraceGenerated(object sender, CbsTableTraceEventArgs e)
        {
            if (e is null)
            {
                return;
            }

            _viewModel.AppendUiTrace(e.Message);
        }

        private async void ReferenceTableView_ViewportChanged(object? sender, CbsTableViewportChangedEventArgs e)
        {
            _viewModel.UpdateViewportRetention(
                e.StartIndex,
                e.EndIndex,
                e.RetainedBufferRows);

            _viewportCts?.Cancel();
            var cancellationTokenSource = new CancellationTokenSource();
            _viewportCts = cancellationTokenSource;

            try
            {
                await _viewModel.EnsureViewportWindowLoadedAsync(
                    e.StartIndex,
                    e.EndIndex,
                    e.RetainedBufferRows,
                    cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void EnsureViewportSubscription()
        {
            if (_isViewportSubscribed)
            {
                return;
            }

            ReferenceTableView.ViewportChanged += ReferenceTableView_ViewportChanged;
            _isViewportSubscribed = true;
        }

        private void RemoveViewportSubscription()
        {
            if (!_isViewportSubscribed)
            {
                return;
            }

            ReferenceTableView.ViewportChanged -= ReferenceTableView_ViewportChanged;
            _isViewportSubscribed = false;
        }

        private void UpdateSelectionActionButtons()
        {
            var hasSelectedRow = _viewModel.HasSelectedRow && _viewModel.HasActiveReference;
            var isHolidayReference = string.Equals(_viewModel.CurrentReference?.Route, "/holidays", StringComparison.OrdinalIgnoreCase);
            var canRecalculateHoliday = hasSelectedRow && isHolidayReference && !_isHolidayRecalcInProgress;

            if (EditSelectedRowButton is not null)
            {
                EditSelectedRowButton.IsEnabled = hasSelectedRow;
                EditSelectedRowButton.Foreground = hasSelectedRow
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.RoyalBlue)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (DeleteSelectedRowButton is not null)
            {
                DeleteSelectedRowButton.IsEnabled = hasSelectedRow;
                DeleteSelectedRowButton.Foreground = hasSelectedRow
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Firebrick)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }

            if (HolidayRecalcButton is not null)
            {
                HolidayRecalcButton.Visibility = isHolidayReference ? Visibility.Visible : Visibility.Collapsed;
                HolidayRecalcButton.IsEnabled = canRecalculateHoliday;
                HolidayRecalcButton.Foreground = canRecalculateHoliday
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.SteelBlue)
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellSecondaryTextBrush"];
            }
        }

        private async Task ShowReferenceEditDialogAsync(bool isCreateMode)
        {
            if (_viewModel.CurrentReference is null)
            {
                return;
            }

            if (!isCreateMode && _viewModel.SelectedRow is null)
            {
                return;
            }

            if (_viewModel.CurrentReference.EditorKind == ReferenceEditorKind.Profile)
            {
                await ShowProfileEditDialogAsync(isCreateMode);
                return;
            }

            var dialogViewModel = isCreateMode
                ? ReferenceEditViewModel.CreateForCreate(_viewModel.CurrentReference)
                : ReferenceEditViewModel.CreateForEdit(_viewModel.CurrentReference, _viewModel.SelectedRow!);

            var dialog = new ReferenceEditDialog(dialogViewModel)
            {
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return;
            }

            var values = isCreateMode
                ? ReferenceEditPayloadBuilder.BuildForCreate(dialogViewModel)
                : ReferenceEditPayloadBuilder.BuildForUpdate(dialogViewModel);

            var action = isCreateMode ? "CREATE" : "EDIT";
            var keys = values.Count == 0
                ? "<empty>"
                : string.Join(", ", values.Keys);
            _viewModel.AppendUiTrace($"REFERENCE {action} DIALOG CONFIRMED keys={keys}");

            try
            {
                ReferenceDataRow savedRow;
                if (isCreateMode)
                {
                    savedRow = await _referenceCrudService.CreateAsync(_viewModel.CurrentReference, values);
                }
                else
                {
                    savedRow = await _referenceCrudService.UpdateAsync(_viewModel.CurrentReference, values);
                }

                await _viewModel.ReloadCurrentReferenceAsync();
                ShowSuccessNotification(
                    isCreateMode ? "Запись создана" : "Изменения сохранены",
                    BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, TryGetSelectedRowId(savedRow)));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(
                    isCreateMode ? "Не удалось создать запись." : "Не удалось сохранить изменения.",
                    ex.Message);
            }
        }

        private async Task DeleteSelectedRowAsync()
        {
            if (_viewModel.CurrentReference is null || _viewModel.SelectedRow is null)
            {
                return;
            }

            var id = TryGetSelectedRowId(_viewModel.SelectedRow);
            if (id is null)
            {
                await ShowErrorDialogAsync("Не удалось удалить запись.", "У выбранной записи отсутствует корректный ID.");
                return;
            }

            var confirmDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Удаление записи",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                Content = "Удалить выбранную запись?"
            };

            if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                await _referenceCrudService.DeleteAsync(_viewModel.CurrentReference, id.Value);
                await _viewModel.ReloadCurrentReferenceAsync();
                ShowSuccessNotification(
                    "Запись удалена",
                    BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, id.Value));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось удалить запись.", ex.Message);
            }
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                CloseButtonText = "Закрыть",
                DefaultButton = ContentDialogButton.Close,
                Content = message
            };

            await dialog.ShowAsync();
        }

        private async Task RecalculateHolidayStagesAsync()
        {
            if (_viewModel.SelectedRow is null)
            {
                return;
            }

            var holiday = TryCreateHolidayInterval(_viewModel.SelectedRow);
            if (holiday is null)
            {
                await ShowErrorDialogAsync("Не удалось пересчитать сроки.", "Не удалось определить период выбранного календарного дня.");
                return;
            }

            _isHolidayRecalcInProgress = true;
            UpdateSelectionActionButtons();

            try
            {
                var affectedStages = await LoadAffectedStagesAsync(holiday);
                if (affectedStages.Count == 0)
                {
                    await ShowErrorDialogAsync("Пересчёт не требуется", "Этапы в выбранном интервале не найдены.");
                    return;
                }

                var uniqueContracts = affectedStages
                    .Select(static stage => stage.ContractId)
                    .Where(static contractId => contractId is not null)
                    .Distinct()
                    .Count();

                var confirmDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "Пересчитать сроки",
                    PrimaryButtonText = "Пересчитать",
                    CloseButtonText = "Отмена",
                    DefaultButton = ContentDialogButton.Primary,
                    Content = $"Будут затронуты {affectedStages.Count} этапа(ов) в {uniqueContracts} контракте(ах). Продолжить?"
                };

                if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                var holidays = await LoadHolidayCalendarAsync();
                var processedCount = 0;
                var updatedCount = 0;
                var errors = new List<string>();
                var touchedContracts = new HashSet<long>();

                foreach (var stage in affectedStages)
                {
                    processedCount++;

                    try
                    {
                        var patch = BuildStagePatch(stage, holiday, holidays, errors);
                        if (patch is null)
                        {
                            continue;
                        }

                        await _referenceCrudService.UpdateAsync(StageEditDefinition, patch);
                        updatedCount++;
                        if (stage.ContractId is long contractId)
                        {
                            touchedContracts.Add(contractId);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Этап #{stage.Id}: ошибка обновления - {ex.Message}");
                    }
                }

                await _viewModel.ReloadCurrentReferenceAsync();

                if (errors.Count > 0)
                {
                    await ShowErrorDialogAsync(
                        "Пересчёт завершён с ошибками",
                        $"Обработано: {processedCount}. Изменено: {updatedCount}. Контрактов затронуто: {touchedContracts.Count}.{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, errors.Take(10))}");
                }
                else
                {
                    ShowSuccessNotification(
                        "Пересчёт завершён",
                        $"Этапов обработано: {processedCount}, изменено: {updatedCount}, контрактов затронуто: {touchedContracts.Count}");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось пересчитать сроки.", ex.Message);
            }
            finally
            {
                _isHolidayRecalcInProgress = false;
                UpdateSelectionActionButtons();
            }
        }

        private async Task<IReadOnlyList<HolidayCalendarDay>> LoadHolidayCalendarAsync(CancellationToken cancellationToken = default)
        {
            var rows = await _holidayRecalculationService.GetHolidayCalendarAsync(cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(TryCreateHolidayCalendarDay)
                .Where(static item => item is not null)
                .Cast<HolidayCalendarDay>()
                .ToList();
        }

        private async Task<IReadOnlyList<StageRecalcCandidate>> LoadAffectedStagesAsync(
            HolidayInterval holiday,
            CancellationToken cancellationToken = default)
        {
            var rows = await _holidayRecalculationService.GetAffectedStagesAsync(
                holiday.IntervalStart,
                holiday.IntervalEnd,
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(TryCreateStageCandidate)
                .Where(static item => item is not null)
                .Cast<StageRecalcCandidate>()
                .ToList();
        }

        private static HolidayInterval? TryCreateHolidayInterval(ReferenceDataRow row)
        {
            var beginAt = TryParseDate(row.GetValue("begin_at"));
            if (beginAt is null)
            {
                return null;
            }

            var endAt = TryParseDate(row.GetValue("end_at")) ?? beginAt.Value.Date.AddDays(1).AddTicks(-1);

            return new HolidayInterval(
                IntervalStart: FormatRailsDate(beginAt.Value),
                IntervalEnd: FormatRailsDate(endAt),
                StartDate: beginAt.Value,
                EndDate: endAt);
        }

        private static HolidayCalendarDay? TryCreateHolidayCalendarDay(ReferenceDataRow row)
        {
            var beginAt = TryParseDate(row.GetValue("begin_at"));
            if (beginAt is null)
            {
                return null;
            }

            var endAt = TryParseDate(row.GetValue("end_at"));
            return new HolidayCalendarDay(
                BeginAt: FormatRailsDate(beginAt.Value),
                BeginDate: beginAt.Value.Date,
                EndAt: endAt is null ? null : FormatRailsDate(endAt.Value),
                EndDate: endAt?.Date,
                IsWorkingDay: TryGetBool(row.GetValue("work")) ?? false);
        }

        private static StageRecalcCandidate? TryCreateStageCandidate(ReferenceDataRow row)
        {
            var id = TryGetLong(row.GetValue("id"));
            if (id is null)
            {
                return null;
            }

            return new StageRecalcCandidate(
                Id: id.Value,
                ListKey: row.GetValue("list_key")?.ToString(),
                Head: row.GetValue("head")?.ToString(),
                Name: row.GetValue("name")?.ToString(),
                DeadlineKind: row.GetValue("deadline_kind")?.ToString(),
                PaymentDeadlineKind: row.GetValue("payment_deadline_kind")?.ToString(),
                Duration: TryGetInt(row.GetValue("duration")),
                PaymentDuration: TryGetInt(row.GetValue("payment_duration")),
                StartAt: row.GetValue("start_at")?.ToString(),
                DeadlineAt: row.GetValue("deadline_at")?.ToString(),
                PaymentAt: row.GetValue("payment_at")?.ToString(),
                PrepaymentAt: row.GetValue("prepayment_at")?.ToString(),
                FundedAt: row.GetValue("funded_at")?.ToString(),
                PaymentDeadlineAt: row.GetValue("payment_deadline_at")?.ToString(),
                ContractId: TryGetLong(row.GetValue("contract.id")));
        }

        private static Dictionary<string, object?>? BuildStagePatch(
            StageRecalcCandidate stage,
            HolidayInterval holiday,
            IReadOnlyList<HolidayCalendarDay> holidays,
            List<string> errors)
        {
            var execAffected = IsExecWorkingKind(stage.DeadlineKind)
                && IntersectsRange(stage.StartAt, stage.DeadlineAt, holiday.StartDate, holiday.EndDate);

            var payAffected = IsPaymentWorkingKind(stage.PaymentDeadlineKind)
                && IntersectsRange(stage.FundedAt, stage.PaymentDeadlineAt, holiday.StartDate, holiday.EndDate);

            var patch = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = stage.Id
            };

            if (!string.IsNullOrWhiteSpace(stage.ListKey))
            {
                patch["list_key"] = stage.ListKey;
            }

            if (execAffected)
            {
                var proposedExec = NextDeadlineFor(stage, holidays, errors);
                if (!string.IsNullOrWhiteSpace(proposedExec)
                    && !string.Equals(stage.DeadlineAt, proposedExec, StringComparison.Ordinal))
                {
                    patch["deadline_at"] = proposedExec;
                }
            }

            if (payAffected)
            {
                var proposedPay = NextPaymentDeadlineFor(stage, holidays, errors);
                if (!string.IsNullOrWhiteSpace(proposedPay)
                    && !string.Equals(stage.PaymentDeadlineAt, proposedPay, StringComparison.Ordinal))
                {
                    patch["payment_deadline_at"] = proposedPay;
                }
            }

            return patch.Count > 1 ? patch : null;
        }

        private static string? NextDeadlineFor(
            StageRecalcCandidate stage,
            IReadOnlyList<HolidayCalendarDay> holidays,
            List<string> errors)
        {
            if (stage.Duration is null)
            {
                errors.Add($"Этап #{stage.Id}: пропущен - нет длительности исполнения.");
                return null;
            }

            DateTime? baseDate = stage.DeadlineKind switch
            {
                "working_prepayment" => TryParseDate(stage.PrepaymentAt) ?? TryParseDate(stage.PaymentAt),
                "working_days" => TryParseDate(stage.StartAt),
                _ => null
            };

            if (baseDate is null)
            {
                errors.Add($"Этап #{stage.Id}: пропущен - нет базовой даты для срока исполнения.");
                return null;
            }

            return FormatRailsDate(AddWorkingDaysToDate(baseDate.Value, stage.Duration.Value, holidays));
        }

        private static string? NextPaymentDeadlineFor(
            StageRecalcCandidate stage,
            IReadOnlyList<HolidayCalendarDay> holidays,
            List<string> errors)
        {
            if (stage.PaymentDuration is null)
            {
                errors.Add($"Этап #{stage.Id}: пропущен - нет длительности оплаты.");
                return null;
            }

            if (!IsPaymentWorkingKind(stage.PaymentDeadlineKind))
            {
                return null;
            }

            var baseDate = TryParseDate(stage.FundedAt);
            if (baseDate is null)
            {
                errors.Add($"Этап #{stage.Id}: пропущен - нет базовой даты для срока оплаты.");
                return null;
            }

            return FormatRailsDate(AddWorkingDaysToDate(baseDate.Value, stage.PaymentDuration.Value, holidays));
        }

        private static DateTime AddWorkingDaysToDate(
            DateTime current,
            int days,
            IReadOnlyList<HolidayCalendarDay> holidays)
        {
            var result = current.Date.AddDays(-1);
            var daysCount = 0;

            while (daysCount < days)
            {
                result = result.AddDays(1);
                var calendarDay = holidays.FirstOrDefault(day =>
                    day.BeginAt == FormatRailsDate(result)
                    || day.EndAt == FormatRailsDate(result)
                    || (day.EndDate is DateTime endDate && endDate > result && day.BeginDate < result));

                var dayOfWeek = result.DayOfWeek;
                var isWeekend = dayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                var isWorkingDay = (!isWeekend || calendarDay?.IsWorkingDay == true)
                    && (calendarDay is null || calendarDay.IsWorkingDay);

                if (isWorkingDay)
                {
                    daysCount++;
                }
            }

            return result;
        }

        private static bool IntersectsRange(string? itemStart, string? itemEnd, DateTime? intervalStart, DateTime? intervalEnd)
        {
            var start = TryParseDate(itemStart);
            var end = TryParseDate(itemEnd);
            if (start is null || end is null || intervalStart is null || intervalEnd is null)
            {
                return false;
            }

            return start.Value < intervalEnd.Value && end.Value > intervalStart.Value;
        }

        private static bool IsExecWorkingKind(string? kind)
        {
            return string.Equals(kind, "working_days", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "working_prepayment", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPaymentWorkingKind(string? kind)
        {
            return string.Equals(kind, "w_days", StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime? TryParseDate(object? value)
        {
            if (value is null)
            {
                return null;
            }

            return value switch
            {
                DateTime dateTime => dateTime,
                DateTimeOffset dateTimeOffset => dateTimeOffset.LocalDateTime,
                string text when DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out var parsedDateTime) => parsedDateTime,
                string text when DateTime.TryParse(
                    text,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out var parsedCurrentCultureDateTime) => parsedCurrentCultureDateTime,
                _ => null
            };
        }

        private static string FormatRailsDate(DateTime value)
        {
            return value.ToString("ddd MMM dd yyyy", CultureInfo.InvariantCulture);
        }

        private static long? TryGetLong(object? value)
        {
            return value switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string text when long.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static int? TryGetInt(object? value)
        {
            return value switch
            {
                int int32Value => int32Value,
                long int64Value => (int)int64Value,
                decimal decimalValue => (int)decimalValue,
                string text when int.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static bool? TryGetBool(object? value)
        {
            return value switch
            {
                bool boolValue => boolValue,
                string text when bool.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private ProfileEditDialogState CreateProfileEditDialogState(bool isCreateMode)
        {
            var departmentOptions = _viewModel.CurrentFilterOptionsSources.TryGetValue("Department", out var options)
                ? options
                : [];

            return ProfileEditStateFactory.Create(
                _viewModel.CurrentReference!,
                isCreateMode,
                isCreateMode ? null : _viewModel.SelectedRow,
                departmentOptions);
        }

        private async Task ShowProfileEditDialogAsync(bool isCreateMode)
        {
            var state = CreateProfileEditDialogState(isCreateMode);
            var viewModel = new ProfileEditViewModel(state, LoadPositionOptionsAsync);
            var dialog = new ProfileEditDialog(viewModel)
            {
                XamlRoot = XamlRoot
            };

            ReferenceDataRow? savedRow = null;

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    viewModel.ClearErrorInfo();

                    var payload = isCreateMode
                        ? ProfileEditPayloadBuilder.BuildForCreate(viewModel)
                        : ProfileEditPayloadBuilder.BuildForUpdate(viewModel);

                    if (!isCreateMode && payload.Count <= 1)
                    {
                        viewModel.ShowErrorInfo("Нет изменений для сохранения.");
                        args.Cancel = true;
                        return;
                    }

                    savedRow = isCreateMode
                        ? await _referenceCrudService.CreateAsync(_viewModel.CurrentReference!, payload)
                        : await _referenceCrudService.UpdateAsync(_viewModel.CurrentReference!, payload);
                }
                catch (Exception ex)
                {
                    viewModel.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
                finally
                {
                    deferral.Complete();
                }
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || savedRow is null || _viewModel.CurrentReference is null)
            {
                return;
            }

            await _viewModel.ReloadCurrentReferenceAsync();
            ShowSuccessNotification(
                isCreateMode ? "Запись создана" : "Изменения сохранены",
                BuildReferenceNotificationMessage(_viewModel.CurrentReference.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task<IReadOnlyList<CbsTableFilterOptionDefinition>> LoadPositionOptionsAsync(
            string searchText,
            CancellationToken cancellationToken)
        {
            var normalizedSearchText = searchText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedSearchText))
            {
                return [];
            }

            var rows = await _dataQueryService.GetDataAsync<ReferenceDataRow>(
                new DataQueryRequest
                {
                    Model = "Position",
                    Preset = "item",
                    Filters = new Dictionary<string, object?>
                    {
                        ["name__cnt"] = normalizedSearchText
                    },
                    Sorts = ["name asc"],
                    Limit = 25
                },
                cancellationToken);

            return rows
                .Where(static row => !row.IsPlaceholder)
                .Select(static row => new CbsTableFilterOptionDefinition
                {
                    Value = row.GetValue("id"),
                    Label = row.GetValue("name")?.ToString() ?? string.Empty
                })
                .Where(static option => option.Value is not null && !string.IsNullOrWhiteSpace(option.Label))
                .DistinctBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(static option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private static string BuildReferenceNotificationMessage(string referenceTitle, long? id)
        {
            return id is long value
                ? $"{referenceTitle}, id:{value}"
                : referenceTitle;
        }

        private static void ShowSuccessNotification(string title, string message)
        {
            try
            {
                var notification = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message)
                    .BuildNotification();

                AppNotificationManager.Default.Show(notification);
            }
            catch (COMException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static long? TryGetSelectedRowId(ReferenceDataRow row)
        {
            var rawId = row.GetValue("id");
            return rawId switch
            {
                long int64Value => int64Value,
                int int32Value => int32Value,
                decimal decimalValue => (long)decimalValue,
                string stringValue when long.TryParse(stringValue, out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private bool IsInternEditBlocked()
        {
            var roleText = _userService.CurrentUser?.Role;
            if (string.IsNullOrWhiteSpace(roleText))
            {
                return false;
            }

            return roleText
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(static role => string.Equals(role, "intern", System.StringComparison.OrdinalIgnoreCase));
        }

        private sealed record HolidayInterval(string IntervalStart, string IntervalEnd, DateTime StartDate, DateTime EndDate);

        private sealed record HolidayCalendarDay(
            string BeginAt,
            DateTime BeginDate,
            string? EndAt,
            DateTime? EndDate,
            bool IsWorkingDay);

        private sealed record StageRecalcCandidate(
            long Id,
            string? ListKey,
            string? Head,
            string? Name,
            string? DeadlineKind,
            string? PaymentDeadlineKind,
            int? Duration,
            int? PaymentDuration,
            string? StartAt,
            string? DeadlineAt,
            string? PaymentAt,
            string? PrepaymentAt,
            string? FundedAt,
            string? PaymentDeadlineAt,
            long? ContractId);
    }
}
