using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Services;
using CbsContractsDesktopClient.Services.Orders;
using CbsContractsDesktopClient.Shared.Data;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.Stores.Orders;
using CbsContractsDesktopClient.ViewModels.Workflow;
using CbsContractsDesktopClient.ViewModels.Workflow.EditStates;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Pauli.WinUiKit.Controls;
using Windows.System;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractStatusDialogControls;
using static CbsContractsDesktopClient.Shared.Formatting.AppFormatters;

namespace CbsContractsDesktopClient.Views.Functional;

public sealed class StageOziEditDialog : AppEditDialog
{
    private static readonly IReadOnlySet<long> OziStageStatusIds = new HashSet<long>
    {
        WorkflowStatusIds.InProgress,
        WorkflowStatusIds.Done,
        WorkflowStatusIds.Closed
    };

    private StageEditState _stage;
    private ContractEditState? _contract;
    private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _statusOptions;
    private readonly IReadOnlyList<ReferenceLookupItem> _employeeItems;
    private IReadOnlyList<StagePerformerOption> _performerOptions;
    private StageEditDialogNavigationState? _navigationState;
    private readonly Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? _navigateAsync;
    private readonly Func<bool> _shouldCloseContractAfterSelectedStageClosed;
    private readonly ContractCommentWorkflow _commentWorkflow;
    private readonly StageSupplyEditWorkflow _stageSupplyEditWorkflow;
    private readonly IDataQueryService _dataQueryService;
    private readonly int _profileId;
    private readonly CalendarInput _rideOutAtEditor = new();
    private readonly CalendarInput _sendedAtEditor = new();
    private readonly CalendarInput _completedAtEditor = new();
    private readonly CalendarInput _closedAtEditor = new();
    private readonly Dropdown _statusBox = new();
    private readonly CheckBox _isRideOutBox = new() { Content = "Выехали" };
    private readonly CheckBox _isSendedBox = new() { Content = "Отправили" };
    private readonly CheckBox _toRegistryBox = new() { Content = "Реестр" };
    private readonly TextBox _registryQuarterBox = BuildNumberTextBox();
    private readonly TextBox _registryYearBox = BuildNumberTextBox();
    private readonly MultiSelect _performersMultiSelect = new();
    private readonly TextBox _commentBox = new();
    private readonly CommentBox _commentsBox = BuildCommentsBox();
    private readonly StageOziEditView _view = new();
    private readonly TreeView _branchesTree = new();
    private bool _isApplyingBusinessLogic;
    private bool _businessLogicHandlersAttached;
    private bool _contractCloseCommentApplied;

    public StageOziEditDialog(
        StageEditState stage,
        ContractEditState? contract,
        IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
        IReadOnlyList<ReferenceLookupItem> employeeItems,
        int? profileId,
        StageEditDialogNavigationState? navigationState,
        Func<StageEditDialogNavigationDirection, Task<StageEditDialogNavigationResult?>>? navigateAsync,
        Func<bool> shouldCloseContractAfterSelectedStageClosed)
    {
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(statusOptions);
        ArgumentNullException.ThrowIfNull(employeeItems);
        ArgumentNullException.ThrowIfNull(shouldCloseContractAfterSelectedStageClosed);

        _stage = stage;
        _contract = contract;
        _statusOptions = statusOptions;
        _employeeItems = employeeItems;
        _profileId = profileId
            ?? throw new InvalidOperationException("StageOziEditDialog requires current user profile_id.");
        _navigationState = navigationState;
        _navigateAsync = navigateAsync;
        _shouldCloseContractAfterSelectedStageClosed = shouldCloseContractAfterSelectedStageClosed;
        _commentWorkflow = App.Services.GetRequiredService<ContractCommentWorkflow>();
        _stageSupplyEditWorkflow = App.Services.GetRequiredService<StageSupplyEditWorkflow>();
        _dataQueryService = App.Services.GetRequiredService<IDataQueryService>();
        _performerOptions = CreatePerformerOptions(employeeItems, stage.Performers);
        _view.PreviousButton.Click += StageNavigationButton_Click;
        _view.NextButton.Click += StageNavigationButton_Click;

        Resources["ContentDialogMinWidth"] = 1120d;
        Resources["ContentDialogMaxWidth"] = 1120d;
        Content = BuildContent();
        DialogChrome.Apply(this, _stage.GetEditDialogTitle());
        Loaded += StageOziEditDialog_Loaded;
    }

    public long Id => _stage.Id;

    public bool ShouldCloseContract()
    {
        SyncStageStateFromEditors();
        return _shouldCloseContractAfterSelectedStageClosed();
    }

    public IReadOnlyDictionary<string, object?> BuildContractClosePayload()
    {
        return StageEditPayloadBuilderHelpers.BuildContractClosePayload(RequireContract().Id, _closedAtEditor.Date);
    }

    public IReadOnlyDictionary<string, object?> BuildPayload()
    {
        SyncStageStateFromEditors();
        return StageOziEditPayloadBuilder.BuildForUpdate(
            _stage,
            BuildSelectedPerformers(),
            _commentBox.Text,
            _profileId);
    }

    public override bool Validate()
    {
        if (_isRideOutBox.IsChecked == true && _rideOutAtEditor.Date is null)
        {
            ShowErrorInfo("Укажите дату выезда к клиенту.");
            return false;
        }

        if (_isSendedBox.IsChecked == true && _sendedAtEditor.Date is null)
        {
            ShowErrorInfo("Укажите дату отправки документов.");
            return false;
        }

        if (GetSelectedStatusOption()?.Value == WorkflowStatusIds.Closed && _closedAtEditor.Date is null)
        {
            ShowErrorInfo("Для закрытого этапа укажите дату закрытия.");
            return false;
        }

        if (_toRegistryBox.IsChecked == true
            && (TryGetInt(_registryQuarterBox.Text) is not (>= 1 and <= 4)
                || TryGetInt(_registryYearBox.Text) is null))
        {
            ShowErrorInfo("Для реестра укажите квартал от 1 до 4 и год.");
            return false;
        }

        ShowErrorInfo(string.Empty);
        return true;
    }

    private UIElement BuildContent()
    {
        InitializeStaticView();
        RenderStageContent();
        return BuildEditContent(_view);
    }

    private void InitializeStaticView()
    {
        _view.ContractTitleSlot.Content = BuildDialogSectionTitle(RequireContract().GetSectionTitle());
        _view.ExternalNumberValue.Text = FormatSummaryValue(_contract?.ExternalNumber ?? string.Empty);
        _view.ContragentValue.Text = FormatSummaryValue(_contract?.ContragentName ?? string.Empty);
        _view.ContractCostValue.Text = FormatSummaryValue(FormatMoney(_contract?.Cost));
        _view.ContractKindValue.Text = FormatSummaryValue(BuildContractKindText());
        UpdateContractSummaryPanel();
        InitializeNavigationButton(_view.PreviousButton, "Предыдущий этап", StageEditDialogNavigationDirection.Previous);
        InitializeNavigationButton(_view.NextButton, "Следующий этап", StageEditDialogNavigationDirection.Next);
        InitializeEditorSlots();
    }

    private static void InitializeNavigationButton(
        Button button,
        string tooltip,
        StageEditDialogNavigationDirection direction)
    {
        button.Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 99, 102, 241));
        button.BorderBrush = button.Background;
        button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 79, 70, 229));
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 67, 56, 202));
        button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 79, 70, 229));
        button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 67, 56, 202));
        ToolTipService.SetToolTip(button, tooltip);
        button.Tag = direction;
    }

    private void InitializeEditorSlots()
    {
        _view.PerformersSlot.Content = _performersMultiSelect;
        _view.RideOutCheckSlot.Content = _isRideOutBox;
        _view.RideOutAtSlot.Content = _rideOutAtEditor;
        _view.SendedCheckSlot.Content = _isSendedBox;
        _view.SendedAtSlot.Content = _sendedAtEditor;
        _view.RegistryCheckSlot.Content = _toRegistryBox;
        _view.RegistryQuarterSlot.Content = _registryQuarterBox;
        _view.RegistryYearSlot.Content = _registryYearBox;
        _view.StatusSlot.Content = _statusBox;
        _view.CompletedAtSlot.Content = _completedAtEditor;
        _view.ClosedAtSlot.Content = _closedAtEditor;
        _branchesTree.HorizontalAlignment = HorizontalAlignment.Stretch;
        _branchesTree.VerticalAlignment = VerticalAlignment.Stretch;
        _branchesTree.MinHeight = 260;
        _branchesTree.ItemTemplate = (DataTemplate?)_view.Resources["StageTreeItemTemplate"];
        _view.BranchesSlot.Content = _branchesTree;
        _commentBox.PlaceholderText = "Комментарий";
        _commentBox.KeyDown += CommentBox_KeyDown;
        AttachBusinessLogicHandlers();
        ConfigureTabChain();
    }

    private void StageOziEditDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= StageOziEditDialog_Loaded;
        FocusCheckBox(_isRideOutBox);
    }

    private void ConfigureTabChain()
    {
        _isRideOutBox.TabIndex = 0;
        _rideOutAtEditor.TabIndex = 1;
        _isSendedBox.TabIndex = 2;
        _sendedAtEditor.TabIndex = 3;
        _statusBox.TabIndex = 4;
        _completedAtEditor.TabIndex = 5;
        _closedAtEditor.TabIndex = 6;
        _toRegistryBox.TabIndex = 7;
        _registryQuarterBox.TabIndex = 8;
        _registryYearBox.TabIndex = 9;
        _commentBox.TabIndex = 10;

        _isRideOutBox.PreviewKeyDown += (_, args) => FocusDateEditorOnTab(_rideOutAtEditor, args);
        _rideOutAtEditor.OnTab = (_, args) => FocusCheckBoxOnTab(_isSendedBox, args);
        _isSendedBox.PreviewKeyDown += (_, args) => FocusDateEditorOnTab(_sendedAtEditor, args);
        _sendedAtEditor.OnTab = (_, args) => FocusDropdownOnTab(_statusBox, args);
        _statusBox.OnTab = StatusBox_OnTab;
        _statusBox.SelectionCommitted += StatusBox_SelectionCommitted;
        _completedAtEditor.OnTab = (_, args) => FocusDateEditor(_closedAtEditor, args);
        _closedAtEditor.OnTab = (_, args) => FocusCheckBoxOnTab(_toRegistryBox, args);
        _toRegistryBox.PreviewKeyDown += (_, args) => FocusTextBoxOnTab(_registryQuarterBox, args);
        _registryQuarterBox.PreviewKeyDown += (_, args) => FocusTextBoxOnTab(_registryYearBox, args);
        _registryYearBox.PreviewKeyDown += (_, args) => FocusTextBoxOnTab(_commentBox, args);
    }

    private void FocusCheckBox(CheckBox checkBox)
    {
        DispatcherQueue.TryEnqueue(() => checkBox.Focus(FocusState.Programmatic));
    }

    private void FocusCheckBoxOnTab(CheckBox checkBox, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Tab)
        {
            return;
        }

        FocusCheckBox(checkBox);
        args.Handled = true;
    }

    private void FocusDateEditorOnTab(CalendarInput editor, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Tab)
        {
            return;
        }

        FocusDateEditor(editor, args);
    }

    private void FocusDropdownOnTab(Dropdown dropdown, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Tab)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => dropdown.FocusInput(FocusState.Programmatic));
        args.Handled = true;
    }

    private void FocusDateEditor(CalendarInput editor, KeyRoutedEventArgs args)
    {
        FocusDateEditor(editor);
        args.Handled = true;
    }

    private void FocusDateEditor(CalendarInput editor)
    {
        DispatcherQueue.TryEnqueue(() => editor.FocusInput(FocusState.Programmatic));
    }

    private void FocusTextBoxOnTab(TextBox textBox, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Tab)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            textBox.Focus(FocusState.Programmatic);
            textBox.Select(textBox.Text.Length, 0);
        });
        args.Handled = true;
    }

    private void StatusBox_OnTab(Dropdown dropdown, KeyRoutedEventArgs args)
    {
        dropdown.CloseDropDown();
        FocusDateEditor(_completedAtEditor);
        args.Handled = true;
    }

    private void StatusBox_SelectionCommitted(object? sender, EventArgs args)
    {
        ApplyBusinessLogic();
        FocusDateEditor(_completedAtEditor);
    }

    private void RenderStageContent()
    {
        UpdateStageSummaryPanel();
        UpdateStageEditors();
        RefreshStageBranches();
    }

    private void RefreshStageBranches()
    {
        _commentsBox.Comments = _stage.Id > 0
            ? _commentWorkflow.ReadStageComments(_stage.Id)
            : [];
        _branchesTree.ItemsSource = null;
        _branchesTree.ItemsSource = new List<ContractStageTreeItem>
        {
            new ContractStageTreeItem(
                BuildBranchHeader("Комментарии"),
                [new ContractStageTreeItem(BuildCommentsBranchContent())],
                isExpanded: true),
            new ContractStageTreeItem(
                BuildBranchHeader("Поставка"),
                [
                    new ContractStageTreeItem(
                        BuildSupplyBranchContent(),
                        contentMargin: new Thickness(-56, 0, 0, 0))
                ])
        };
    }

    private FrameworkElement BuildCommentsBranchContent()
    {
        var layout = new Grid
        {
            RowSpacing = 4,
            Margin = new Thickness(4, 2, 4, 4)
        };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_commentBox, 0);
        Grid.SetRow(_commentsBox, 1);
        layout.Children.Add(_commentBox);
        layout.Children.Add(_commentsBox);
        return layout;
    }

    private FrameworkElement BuildSupplyBranchContent()
    {
        if (_stage.Id <= 0)
        {
            return new TextBlock
            {
                Text = "Поставка станет доступна после сохранения этапа.",
                FontSize = 11,
                Margin = new Thickness(4)
            };
        }

        var view = new StageSupplyView(new StageSupplyStore(_dataQueryService, _stage.Id));
        view.CreateRequested += async (_, args) =>
            await ShowStageSupplyFlyoutAsync(view, null, args.Anchor);
        view.EditRequested += async (_, args) =>
            await ShowStageSupplyFlyoutAsync(view, args.Row, args.Anchor);
        view.DeleteRequested += async (_, args) =>
            await DeleteStageSupplyAsync(view, args.Row);
        view.LoadFailed += (_, args) =>
            ShowErrorInfo($"Не удалось загрузить поставку этапа: {args.Exception.Message}");
        return view;
    }

    private async Task ShowStageSupplyFlyoutAsync(
        StageSupplyView supplyView,
        TableDataRow? sourceRow,
        FrameworkElement anchor)
    {
        try
        {
            var viewModel = await _stageSupplyEditWorkflow.CreateViewModelAsync(_stage.Id, sourceRow);
            var flyout = new StageSupplyEditFlyout(_stageSupplyEditWorkflow, viewModel);
            if (await flyout.ShowAsync(anchor))
            {
                await supplyView.ReloadAsync(flyout.SavedRowId);
            }
        }
        catch (Exception ex)
        {
            ShowErrorInfo($"Не удалось открыть позицию поставки: {ex.Message}");
        }
    }

    private async Task DeleteStageSupplyAsync(StageSupplyView supplyView, TableDataRow sourceRow)
    {
        try
        {
            await _stageSupplyEditWorkflow.DeleteAsync(sourceRow);
            await supplyView.ReloadAsync();
        }
        catch (Exception ex)
        {
            ShowErrorInfo($"Не удалось удалить позицию поставки: {ex.Message}");
        }
    }

    private static TextBlock BuildBranchHeader(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = Application.Current.Resources["ShellTableHeaderTextBrush"] as Brush
    };

    private async void CommentBox_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Enter || _stage.Id <= 0)
        {
            return;
        }

        args.Handled = true;
        if (string.IsNullOrWhiteSpace(_commentBox.Text))
        {
            return;
        }

        _commentBox.IsEnabled = false;
        try
        {
            var contractId = RequireContract().Id;
            if (contractId <= 0)
            {
                throw new InvalidOperationException("Persisted stage comment requires contract id.");
            }

            var result = await _commentWorkflow.SaveStageCommentAsync(
                contractId,
                _stage.Id,
                _stage.ListKey,
                _commentBox.Text);
            _commentBox.Text = string.Empty;
            _commentsBox.Comments = result.Comments;
            ShowErrorInfo(string.Empty);
        }
        catch (Exception ex)
        {
            ShowErrorInfo(ex.Message);
        }
        finally
        {
            _commentBox.IsEnabled = true;
        }
    }

    private static CommentBox BuildCommentsBox()
    {
        return new CommentBox
        {
            Height = 220,
            MaxHeight = 220,
            VerticalAlignment = VerticalAlignment.Stretch
        };
    }

    private void UpdateStageSummaryPanel()
    {
        UpdateContractSummaryPanel();
        UpdateStageTitle();
        UpdateStageNavigationButtons();
        _view.StartAtValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.StartAt));
        _view.PrepaymentValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.PrepaymentAt ?? _stage.PaymentAt));
        _view.DeadlineAtValue.Text = FormatSummaryValue(FormatDisplayDate(_stage.DeadlineAt));
        _view.TasksValue.Text = FormatSummaryValue(BuildTasksText());
    }

    private void UpdateContractSummaryPanel()
    {
        var contract = RequireContract();
        _view.SignedAtValue.Text = FormatSummaryValue(FormatDisplayDate(contract.SignedAt));
        _view.ContractClosedAtValue.Text = FormatSummaryValue(FormatDisplayDate(contract.ClosedAt));
        _view.ContractStatusSlot.Content = BuildStatusBadge(
            contract.Status.Name!,
            contract.Status.Id,
            horizontalAlignment: HorizontalAlignment.Left);
    }

    private void UpdateStageTitle()
    {
        _view.StageTitleValue.Inlines.Clear();
        var title = _stage.GetSectionTitle(_contract);
        var accentText = _stage.GetSectionTitleAmount(_contract);
        if (string.IsNullOrWhiteSpace(accentText))
        {
            _view.StageTitleValue.Text = title;
            return;
        }

        _view.StageTitleValue.Text = string.Empty;
        _view.StageTitleValue.Inlines.Add(new Run { Text = title + " " });
        _view.StageTitleValue.Inlines.Add(new Run
        {
            Text = accentText,
            Foreground = Application.Current.Resources["ShellAccentBrush"] as Brush
        });
    }

    private void UpdateStageNavigationButtons()
    {
        var state = _navigationState ?? new StageEditDialogNavigationState(false, false);
        var hasNavigation = state.CanPrevious || state.CanNext;
        _view.PreviousButton.Visibility = hasNavigation ? Visibility.Visible : Visibility.Collapsed;
        _view.NextButton.Visibility = hasNavigation ? Visibility.Visible : Visibility.Collapsed;
        _view.PreviousButton.IsEnabled = state.CanPrevious;
        _view.NextButton.IsEnabled = state.CanNext;
    }

    private void UpdateStageEditors()
    {
        _isApplyingBusinessLogic = true;
        try
        {
            _rideOutAtEditor.Date = _stage.RideOutAt;
            _sendedAtEditor.Date = _stage.SendedAt;
            _completedAtEditor.Date = _stage.CompletedAt;
            _closedAtEditor.Date = _stage.ClosedAt;
            _isRideOutBox.IsChecked = _stage.IsRideOut == true;
            _isSendedBox.IsChecked = _stage.IsSended == true;
            _toRegistryBox.IsChecked = _stage.RegistryQuarter is not null || _stage.RegistryYear is not null;
            _registryQuarterBox.Text = _stage.RegistryQuarter?.ToString() ?? string.Empty;
            _registryYearBox.Text = _stage.RegistryYear?.ToString() ?? string.Empty;
            ConfigureStatusDropdown(_statusBox, BuildOziStageStatusOptions(_statusOptions), _stage.Status.Id);
        }
        finally
        {
            _isApplyingBusinessLogic = false;
        }

        ConfigurePerformersMultiSelect();
    }

    private static string FormatSummaryValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void AttachBusinessLogicHandlers()
    {
        if (_businessLogicHandlersAttached)
        {
            return;
        }

        _businessLogicHandlersAttached = true;
        _isRideOutBox.Checked += (_, _) => ApplyBusinessLogic();
        _isRideOutBox.Unchecked += (_, _) => ApplyBusinessLogic();
        _isSendedBox.Checked += (_, _) => ApplyBusinessLogic();
        _isSendedBox.Unchecked += (_, _) => ApplyBusinessLogic();
        _toRegistryBox.Checked += (_, _) => ApplyBusinessLogic();
        _toRegistryBox.Unchecked += (_, _) => ApplyBusinessLogic();
    }

    private void ApplyBusinessLogic()
    {
        if (_isApplyingBusinessLogic)
        {
            return;
        }

        _isApplyingBusinessLogic = true;
        try
        {
            if (_isRideOutBox.IsChecked != true)
            {
                _rideOutAtEditor.Date = null;
            }
            _rideOutAtEditor.IsReadOnly = _isRideOutBox.IsChecked != true;

            if (_isSendedBox.IsChecked != true)
            {
                _sendedAtEditor.Date = null;
            }
            _sendedAtEditor.IsReadOnly = _isSendedBox.IsChecked != true;

            if (GetSelectedStatusOption()?.Value == WorkflowStatusIds.Done && _completedAtEditor.Date is null)
            {
                _completedAtEditor.Date = DateTimeOffset.Now;
            }

            var statusId = GetSelectedStatusOption()?.Value;
            if (statusId != WorkflowStatusIds.Closed)
            {
                _closedAtEditor.Date = null;
            }
            else if (_closedAtEditor.Date is null)
            {
                _closedAtEditor.Date = DateTimeOffset.Now;
            }

            ApplyContractClosePreview();

            var hasRegistry = _toRegistryBox.IsChecked == true;
            _registryQuarterBox.IsEnabled = hasRegistry;
            _registryYearBox.IsEnabled = hasRegistry;
            if (hasRegistry)
            {
                _registryQuarterBox.Text = string.IsNullOrWhiteSpace(_registryQuarterBox.Text)
                    ? ResolveDefaultRegistryQuarter().ToString()
                    : _registryQuarterBox.Text;
                _registryYearBox.Text = string.IsNullOrWhiteSpace(_registryYearBox.Text)
                    ? ResolveDefaultRegistryYear().ToString()
                    : _registryYearBox.Text;
            }
            else
            {
                _registryQuarterBox.Text = string.Empty;
                _registryYearBox.Text = string.Empty;
            }
        }
        finally
        {
            _isApplyingBusinessLogic = false;
        }
    }

    private void ConfigurePerformersMultiSelect()
    {
        _performersMultiSelect.Options = _performerOptions;
        _performersMultiSelect.Value = _performerOptions
            .Where(static option => option.IsSelected)
            .ToList();
        _performersMultiSelect.OptionLabel = nameof(StagePerformerOption.Name);
        _performersMultiSelect.Display = "chip";
        _performersMultiSelect.MaxSelectedLabels = 4;
        _performersMultiSelect.Placeholder = "Исполнители";
        _performersMultiSelect.Tooltip = "Исполнители";
    }

    private void SyncStageStateFromEditors()
    {
        SyncStageStateFromEditorsCore();
        AppendContractCloseCommentIfNeeded();
    }

    private void ApplyContractClosePreview()
    {
        SyncStageStateFromEditorsCore();
        var contract = RequireContract();
        if (_shouldCloseContractAfterSelectedStageClosed())
        {
            contract.ApplyClosedStatusPreview(_closedAtEditor.Date);
        }
        else
        {
            contract.RestoreStatusPreview();
        }

        UpdateContractSummaryPanel();
    }

    private void SyncStageStateFromEditorsCore()
    {
        _stage.Status = new StatusEditState(GetSelectedStatusOption()?.Value, GetSelectedStatusOption()?.Label);
        _stage.CompletedAt = _completedAtEditor.Date;
        _stage.ClosedAt = _closedAtEditor.Date;
        _stage.IsRideOut = _isRideOutBox.IsChecked == true;
        _stage.RideOutAt = _stage.IsRideOut == true ? _rideOutAtEditor.Date : null;
        _stage.IsSended = _isSendedBox.IsChecked == true;
        _stage.SendedAt = _stage.IsSended == true ? _sendedAtEditor.Date : null;
        _stage.RegistryQuarter = _toRegistryBox.IsChecked == true ? TryGetInt(_registryQuarterBox.Text) : null;
        _stage.RegistryYear = _toRegistryBox.IsChecked == true ? TryGetInt(_registryYearBox.Text) : null;
        _stage.Performers = BuildSelectedPerformers();
    }

    private void AppendContractCloseCommentIfNeeded()
    {
        if (_contractCloseCommentApplied
            || !_shouldCloseContractAfterSelectedStageClosed())
        {
            return;
        }

        AppendAutomaticComment("Статус контракта был изменен автоматически на \"Закрыт\"");
        _contractCloseCommentApplied = true;
    }

    private void AppendAutomaticComment(string text)
    {
        _commentBox.Text = string.IsNullOrWhiteSpace(_commentBox.Text)
            ? text
            : $"{_commentBox.Text.TrimEnd()}; {text}";
        _commentBox.Select(_commentBox.Text.Length, 0);
    }

    private IReadOnlyList<StagePerformerEditState> BuildSelectedPerformers()
    {
        var result = new List<StagePerformerEditState>();
        var priority = 0;
        foreach (var option in (_performersMultiSelect.Value ?? Array.Empty<object>()).OfType<StagePerformerOption>())
        {
            result.Add(new StagePerformerEditState(
                option.ExistingId,
                option.ExistingListKey,
                option.EmployeeId,
                option.Name,
                priority));
            priority++;
        }

        return result;
    }

    private int ResolveDefaultRegistryQuarter()
    {
        var date = _stage.StartAt ?? DateTimeOffset.Now;
        return ((date.Month - 1) / 3) + 1;
    }

    private int ResolveDefaultRegistryYear()
    {
        return (_contract?.SignedAt ?? _stage.StartAt ?? DateTimeOffset.Now).Year;
    }

    private string BuildContractKindText()
    {
        var parts = new List<string>();
        if (_contract?.Governmental == true)
        {
            parts.Add("государственный");
        }

        if (_contract?.IsMultiStage == true)
        {
            parts.Add("многоэтапный");
        }

        return parts.Count == 0 ? "-" : string.Join("; ", parts);
    }

    private string BuildTasksText()
    {
        return _stage.Tasks.Count == 0
            ? "НЕТ"
            : string.Join("; ", _stage.Tasks.Select(static task => task.Name).Where(static name => !string.IsNullOrWhiteSpace(name)));
    }

    private EnumSelectOption? GetSelectedStatusOption()
    {
        return StageContractStatusDialogControls.GetSelectedStatusOption(_statusBox);
    }

    private static IReadOnlyList<EnumSelectOption> BuildOziStageStatusOptions(
        IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions)
    {
        return statusOptions
            .Select(option => new EnumSelectOption(null, option.Label, JsonDataReader.TryGetLong(option.Value)))
            .Where(option => option.Value is long id && OziStageStatusIds.Contains(id))
            .OrderBy(option => option.Value)
            .ToList();
    }

    private ContractEditState RequireContract()
    {
        return _contract
            ?? throw new InvalidOperationException("Stage edit dialog requires selected contract edit state.");
    }

    private async void RequestNavigation(StageEditDialogNavigationDirection direction)
    {
        try
        {
            if (_navigateAsync is null)
            {
                return;
            }

            var result = await _navigateAsync(direction);
            if (result is null)
            {
                return;
            }

            ApplyNavigationResult(result);
        }
        catch (Exception ex)
        {
            ShowErrorInfo(ex.Message);
        }
    }

    private void ApplyNavigationResult(StageEditDialogNavigationResult result)
    {
        var oldStage = _stage;
        var oldContract = _contract;
        var oldNavigationState = _navigationState;
        var oldPerformerOptions = _performerOptions;

        try
        {
            _stage = result.Stage;
            _contract = result.Contract;
            _navigationState = result.NavigationState;
            _performerOptions = CreatePerformerOptions(_employeeItems, _stage.Performers);
            RenderStageContent();
        }
        catch
        {
            _stage = oldStage;
            _contract = oldContract;
            _navigationState = oldNavigationState;
            _performerOptions = oldPerformerOptions;
            RenderStageContent();
            throw;
        }
    }

    private void StageNavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StageEditDialogNavigationDirection direction })
        {
            RequestNavigation(direction);
        }
    }

    private static IReadOnlyList<StagePerformerOption> CreatePerformerOptions(
        IReadOnlyList<ReferenceLookupItem> employeeItems,
        IReadOnlyList<StagePerformerEditState> selectedPerformers)
    {
        var selectedByEmployee = selectedPerformers
            .Where(static performer => performer.EmployeeId is not null)
            .ToDictionary(static performer => performer.EmployeeId!.Value);

        var options = employeeItems
            .Select(item =>
            {
                var employeeId = JsonDataReader.TryGetLong(item.Id) ?? 0;
                selectedByEmployee.TryGetValue(employeeId, out var selected);
                return new StagePerformerOption(
                    employeeId,
                    item.DisplayName,
                    selected?.Id,
                    selected?.ListKey,
                    selected?.Priority,
                    selected is not null);
            })
            .Where(static item => item.EmployeeId > 0 && !string.IsNullOrWhiteSpace(item.Name))
            .ToList();

        var knownEmployeeIds = options.Select(static option => option.EmployeeId).ToHashSet();
        options.AddRange(selectedPerformers
            .Where(performer => performer.EmployeeId is long employeeId && !knownEmployeeIds.Contains(employeeId))
            .Select(performer => new StagePerformerOption(
                performer.EmployeeId!.Value,
                performer.Name,
                performer.Id,
                performer.ListKey,
                performer.Priority,
                true)));

        return options
            .OrderByDescending(static option => option.IsSelected)
            .ThenBy(static option => option.Priority ?? int.MaxValue)
            .ThenBy(static option => option.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int? TryGetInt(string? value)
    {
        return int.TryParse(value, out var parsedValue) ? parsedValue : null;
    }

    private sealed record StagePerformerOption(
        long EmployeeId,
        string Name,
        long? ExistingId,
        string? ExistingListKey,
        int? Priority,
        bool IsSelected);
}
