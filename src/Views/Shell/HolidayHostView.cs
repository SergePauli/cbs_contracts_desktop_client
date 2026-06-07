// Hosts the Holiday calendar table and its stage deadline recalculation workflow.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Models.Workspace;
using CbsContractsDesktopClient.Services.Definitions.ReferenceDefinitions;
using CbsContractsDesktopClient.Services.Mutations;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Shared.Dates;
using CbsContractsDesktopClient.ViewModels.References;
using CbsContractsDesktopClient.Views.References;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;
using static CbsContractsDesktopClient.Shared.Dates.BusinessCalendar;

namespace CbsContractsDesktopClient.Views.Shell
{
    public sealed class HolidayHostView : ComplexHostViewBase
    {
        private readonly IHolidayRecalculationService _holidayRecalculationService;
        private readonly IModelMutationService _modelMutationService;
        private readonly IReferenceDefinitionService _referenceDefinitionService;
        private readonly IReferenceLookupCacheService _referenceLookupCacheService;
        private Button? _createButton;
        private Button? _editButton;
        private Button? _deleteButton;
        private Button? _recalculateButton;
        private bool _isRecalculationInProgress;

        public HolidayHostView()
        {
            _holidayRecalculationService = App.Services.GetRequiredService<IHolidayRecalculationService>();
            _modelMutationService = App.Services.GetRequiredService<IModelMutationService>();
            _referenceDefinitionService = App.Services.GetRequiredService<IReferenceDefinitionService>();
            _referenceLookupCacheService = App.Services.GetRequiredService<IReferenceLookupCacheService>();
        }

        protected override IEnumerable<FrameworkElement> BuildHeaderActions()
        {
            _editButton = CreateHeaderIconButton("\uE70F", "Редактировать запись");
            _editButton.Click += async (_, _) => await ShowHolidayEditDialogAsync(isCreateMode: false);

            _deleteButton = CreateHeaderIconButton("\uE74D", "Удалить запись");
            _deleteButton.Click += async (_, _) => await DeleteSelectedHolidayAsync();

            _createButton = CreateHeaderIconButton("\uF8AA", "Добавить запись");
            _createButton.Click += async (_, _) => await ShowHolidayEditDialogAsync(isCreateMode: true);

            _recalculateButton = CreateHeaderIconButton("\uE9D9", "Пересчитать сроки этапов");
            _recalculateButton.Click += async (_, _) => await RecalculateHolidayStagesAsync();
            UpdateActionButtonState();
            return [_editButton, _deleteButton, _createButton, _recalculateButton];
        }

        protected override Task OnRouteLoaded(TablePageDefinition definition)
        {
            UpdateActionButtonState();
            return Task.CompletedTask;
        }

        protected override Task OnRowSelected(TableDataRow? row)
        {
            UpdateActionButtonState();
            return Task.CompletedTask;
        }

        protected override async Task OpenEditDialogAsync(TableDataRow? row)
        {
            if (row is not null)
            {
                await ShowHolidayEditDialogAsync(isCreateMode: false);
            }
        }

        private void UpdateActionButtonState()
        {
            var hasSelectedRow = Store.HasSelectedRow;

            if (_editButton is not null)
            {
                _editButton.IsEnabled = hasSelectedRow && Store.CanEditRows;
            }

            if (_deleteButton is not null)
            {
                _deleteButton.IsEnabled = hasSelectedRow && Store.CanDeleteRows;
            }

            if (_createButton is not null)
            {
                _createButton.IsEnabled = Store.CanCreateRows;
            }

            if (_recalculateButton is not null)
            {
                _recalculateButton.IsEnabled = hasSelectedRow && !_isRecalculationInProgress;
            }
        }

        private async Task ShowHolidayEditDialogAsync(bool isCreateMode)
        {
            var reference = ResolveHolidayReference();
            if (reference is null)
            {
                return;
            }

            if (!isCreateMode && Store.SelectedRow is null)
            {
                return;
            }

            var dialogViewModel = isCreateMode
                ? ReferenceEditViewModel.CreateForCreate(reference)
                : ReferenceEditViewModel.CreateForEdit(reference, Store.SelectedRow!);

            var dialog = new ReferenceEditDialog(dialogViewModel)
            {
                XamlRoot = XamlRoot
            };

            TableDataRow? savedRow = null;

            dialog.SaveRequestedAsync += async args =>
            {
                var values = isCreateMode
                    ? ReferenceEditPayloadBuilder.BuildForCreate(dialogViewModel)
                    : ReferenceEditPayloadBuilder.BuildForUpdate(dialogViewModel);

                try
                {
                    savedRow = isCreateMode
                        ? await _modelMutationService.CreateAsync(reference.Model, values)
                        : await _modelMutationService.UpdateAsync(reference.Model, values);
                }
                catch (Exception ex)
                {
                    dialog.ShowErrorInfo(ex.Message);
                    args.Cancel = true;
                }
            };

            await dialog.ShowAsync();
            if (!dialog.WasSaved || savedRow is null)
            {
                return;
            }

            _referenceLookupCacheService.Invalidate(reference.Model);
            await RefreshTableRowAfterSaveAsync(isCreateMode, savedRow);
            ShowSuccessNotification(
                isCreateMode ? "Запись создана" : "Изменения сохранены",
                BuildReferenceNotificationMessage(reference.Title, TryGetSelectedRowId(savedRow)));
        }

        private async Task DeleteSelectedHolidayAsync()
        {
            var reference = ResolveHolidayReference();
            if (reference is null || Store.SelectedRow is null)
            {
                return;
            }

            var id = TryGetSelectedRowId(Store.SelectedRow);
            if (id is null)
            {
                await ShowErrorDialogAsync(
                    "Не удалось удалить запись.",
                    "У выбранной записи отсутствует корректный ID.");
                return;
            }

            if (!await ConfirmDialogAsync(
                    "Удаление записи",
                    "Удалить выбранную запись?",
                    "Удалить",
                    defaultButton: ContentDialogButton.Close))
            {
                return;
            }

            try
            {
                await _modelMutationService.DeleteAsync(reference.Model, id.Value);
                _referenceLookupCacheService.Invalidate(reference.Model);
                ApplyDeletedRowUpdate(id.Value);
                ShowSuccessNotification(
                    "Запись удалена",
                    BuildReferenceNotificationMessage(reference.Title, id.Value));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Не удалось удалить запись.", ex.Message);
            }
        }

        private ReferenceDefinition? ResolveHolidayReference()
        {
            return _referenceDefinitionService.TryGetByRoute("/holidays", out var reference)
                ? reference
                : null;
        }

        private static string BuildReferenceNotificationMessage(string title, long? id)
        {
            return id.HasValue
                ? $"{title}, ID {id.Value}"
                : title;
        }

        private async Task RecalculateHolidayStagesAsync()
        {
            if (Store.SelectedRow is null || _isRecalculationInProgress)
            {
                return;
            }

            var holiday = TryCreateHolidayInterval(Store.SelectedRow);
            if (holiday is null)
            {
                await ShowErrorDialogAsync(
                    "Не удалось пересчитать сроки.",
                    "Не удалось определить период выбранного календарного дня.");
                return;
            }

            _isRecalculationInProgress = true;
            UpdateActionButtonState();

            try
            {
                var affectedStages = await LoadAffectedStagesAsync(holiday);
                if (affectedStages.Count == 0)
                {
                    await ShowInfoDialogAsync(
                        "Пересчет не требуется",
                        "Этапы в выбранном интервале не найдены.");
                    return;
                }

                var uniqueContracts = affectedStages
                    .Select(static stage => stage.ContractId)
                    .Where(static contractId => contractId is not null)
                    .Distinct()
                    .Count();

                if (!await ConfirmDialogAsync(
                        "Пересчитать сроки",
                        $"Будут затронуты {affectedStages.Count} этап(ов) в {uniqueContracts} контракте(ах). Продолжить?",
                        "Пересчитать"))
                {
                    return;
                }

                var holidays = await _holidayRecalculationService.GetHolidayCalendarDaysAsync();
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

                        await _modelMutationService.UpdateAsync("Stage", patch);
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

                await Store.ReloadCurrentReferenceAsync();

                if (errors.Count > 0)
                {
                    await ShowErrorDialogAsync(
                        "Пересчет завершен с ошибками",
                        $"Обработано: {processedCount}. Изменено: {updatedCount}. Контрактов затронуто: {touchedContracts.Count}.{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, errors.Take(10))}");
                }
                else
                {
                    ShowSuccessNotification(
                        "Пересчет завершен",
                        $"Этапов обработано: {processedCount}, изменено: {updatedCount}, контрактов затронуто: {touchedContracts.Count}");
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Ошибки при пересчете сроков", ex.Message);
            }
            finally
            {
                _isRecalculationInProgress = false;
                UpdateActionButtonState();
            }
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
                .Select(static item => item!)
                .ToList();
        }

        private static HolidayInterval? TryCreateHolidayInterval(TableDataRow row)
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

        private static StageRecalcCandidate? TryCreateStageCandidate(TableDataRow row)
        {
            var id = TryGetLong(row.GetValue("id"));
            if (id is null)
            {
                return null;
            }

            return new StageRecalcCandidate(
                Id: id.Value,
                ListKey: row.GetValue("list_key")?.ToString(),
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

        private sealed record HolidayInterval(string IntervalStart, string IntervalEnd, DateTime StartDate, DateTime EndDate);

        private sealed record StageRecalcCandidate(
            long Id,
            string? ListKey,
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
