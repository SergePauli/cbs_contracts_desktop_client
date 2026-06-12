using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Dialogs;
using CbsContractsDesktopClient.Shared.Formatting;
using CbsContractsDesktopClient.Views.Controls;
using CbsContractsDesktopClient.Views.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pauli.WinUiKit.Controls;
using Windows.Storage.Pickers;
using Windows.System;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractStatusDialogControls;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed class ContractCommerEditDialog : AppEditDialog
    {
        private const double TabAreaHeight = 500;

        private readonly TableDataRow _contract;
        private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _taskKindOptions;
        private readonly IReadOnlyList<CbsTableFilterOptionDefinition> _statusOptions;
        private readonly Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> _loadContragentOptionsAsync;
        private readonly bool _isCreateMode;
        private readonly Dropdown _taskKindBox = new();
        private readonly ComboBox _statusBox = new();
        private readonly CalendarInput _signedAtEditor = new();
        private readonly TextBox _yearBox = BuildNumberTextBox();
        private readonly TextBox _orderBox = new();
        private readonly TextBox _costBox = new();
        private readonly TextBox _commentBox = new();
        private readonly CheckBox _governmentalBox = new();
        private readonly CheckBox _revisionPresentBox = new();
        private readonly TextBox _externalNumberBox = new();
        private readonly TextBox _revisionDescriptionBox = new();
        private readonly CalendarInput _deadlineAtEditor = new();
        private readonly CalendarInput _closedAtEditor = new();
        private readonly TextBox _revisionDocLinkBox = new();
        private readonly TextBox _revisionScanLinkBox = new();
        private readonly TextBox _revisionProtocolLinkBox = new();
        private readonly TextBox _revisionZipLinkBox = new();
        private readonly CheckBox _extAgreementBox = new();
        private readonly CheckBox _multiStageBox = new();
        private readonly Button _resetChangesButton = new();
        private AutoSuggestBox? _contragentBox;
        private IReadOnlyList<CbsTableFilterOptionDefinition> _contragentOptions = [];
        private CbsTableFilterOptionDefinition? _selectedContragentOption;
        private string _contragentInput = string.Empty;
        private readonly List<RevisionEditorState> _revisionEditors = [];
        private StackPanel? _revisionsStack;
        private TabView? _tabs;
        private TabViewItem? _revisionsTab;
        private bool _isUpdatingExtAgreementBox;

        public ContractCommerEditDialog(
            TableDataRow contract,
            IReadOnlyList<CbsTableFilterOptionDefinition> taskKindOptions,
            IReadOnlyList<CbsTableFilterOptionDefinition> statusOptions,
            Func<string, CancellationToken, Task<IReadOnlyList<CbsTableFilterOptionDefinition>>> loadContragentOptionsAsync,
            bool isCreateMode = false)
        {
            ArgumentNullException.ThrowIfNull(contract);
            ArgumentNullException.ThrowIfNull(taskKindOptions);
            ArgumentNullException.ThrowIfNull(statusOptions);
            ArgumentNullException.ThrowIfNull(loadContragentOptionsAsync);

            _contract = contract;
            _taskKindOptions = taskKindOptions;
            _statusOptions = statusOptions;
            _loadContragentOptionsAsync = loadContragentOptionsAsync;
            _isCreateMode = isCreateMode;
            ResetRevisionEditorsFromContract();
            FullSizeDesired = false;
            HorizontalAlignment = HorizontalAlignment.Center;
            Title = BuildDialogTitle();
            Resources["ContentDialogMinWidth"] = 1220d;
            Resources["ContentDialogMinHeight"] = 620d;
            Resources["ContentDialogMaxWidth"] = 1220d;
            Content = BuildEditContent(BuildContent());
            Loaded += ContractCommerEditDialog_Loaded;
            DialogChrome.Apply(this);
        }

        public ObservableCollection<string> ContragentSuggestionLabels { get; } = [];

        public override bool Validate()
        {
            ShowErrorInfo("Сохранение контракта будет подключено следующим этапом.");
            return false;
        }

        private FrameworkElement BuildContent()
        {
            var root = new Grid
            {
                MinWidth = 1208,
                MaxWidth = 1208,
                RowSpacing = 8
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            root.Children.Add(BuildHeader());

            var tabsHost = new Border
            {
                Padding = new Thickness(8, 0, 8, 0),
                Child = BuildTabs()
            };
            Grid.SetRow(tabsHost, 1);
            root.Children.Add(tabsHost);
            return root;
        }

        private UIElement BuildHeader()
        {
            ConfigureTaskKindCombo();
            ConfigureYearBox();
            ConfigureOrderBox();
            ConfigureContragentState();
            ConfigureCostBox();
            ConfigureStatusCombo();
            ConfigureSignedAtEditor();
            ConfigureCommentBox();
            ConfigureFlagBoxes();
            ConfigureResetChangesButton();

            var header = new StackPanel
            {
                Spacing = 6
            };
            header.Children.Add(BuildHeaderIdentityRow());
            header.Children.Add(BuildHeaderCommentRow());

            return new Border
            {
                Padding = new Thickness(8, 4, 8, 0),
                Child = header
            };
        }

        private UIElement BuildHeaderIdentityRow()
        {
            var grid = new Grid
            {
                ColumnSpacing = 8
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.Children.Add(BuildLabeledControl("Тип", _taskKindBox));

            var firstSeparator = BuildNumberSeparator();
            Grid.SetColumn(firstSeparator, 1);
            grid.Children.Add(firstSeparator);

            var yearEditor = (FrameworkElement)BuildLabeledControl("Год", _yearBox);
            Grid.SetColumn(yearEditor, 2);
            grid.Children.Add(yearEditor);

            var secondSeparator = BuildNumberSeparator();
            Grid.SetColumn(secondSeparator, 3);
            grid.Children.Add(secondSeparator);

            var orderEditor = (FrameworkElement)BuildLabeledControl("П№", _orderBox);
            Grid.SetColumn(orderEditor, 4);
            grid.Children.Add(orderEditor);

            var contragentEditor = (FrameworkElement)BuildLabeledControl("Контрагент", BuildContragentEditor());
            Grid.SetColumn(contragentEditor, 6);
            grid.Children.Add(contragentEditor);

            var statusEditor = (FrameworkElement)BuildLabeledControl("Статус", _statusBox);
            Grid.SetColumn(statusEditor, 8);
            grid.Children.Add(statusEditor);

            var signedAtEditor = (FrameworkElement)BuildLabeledControl("Подписан", _signedAtEditor);
            Grid.SetColumn(signedAtEditor, 10);
            grid.Children.Add(signedAtEditor);

            var costEditor = (FrameworkElement)BuildLabeledControl("Сумма", _costBox);
            Grid.SetColumn(costEditor, 12);
            grid.Children.Add(costEditor);

            return grid;
        }

        private UIElement BuildHeaderCommentRow()
        {
            var grid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 3
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(640) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.Children.Add(new TextBlock
            {
                Text = "Комментарий контракта",
                FontSize = 12
            });

            Grid.SetRow(_commentBox, 1);
            grid.Children.Add(_commentBox);

            var extAgreement = BuildFlagHost(_extAgreementBox, "ДС");
            Grid.SetRow(extAgreement, 1);
            Grid.SetColumn(extAgreement, 1);
            grid.Children.Add(extAgreement);

            var multiStage = BuildFlagHost(_multiStageBox, "МЭ");
            Grid.SetRow(multiStage, 1);
            Grid.SetColumn(multiStage, 2);
            grid.Children.Add(multiStage);

            if (!_isCreateMode)
            {
                Grid.SetRow(_resetChangesButton, 1);
                Grid.SetColumn(_resetChangesButton, 3);
                grid.Children.Add(_resetChangesButton);
            }

            return grid;
        }

        private void ConfigureYearBox()
        {
            var year = TryGetLong(_contract.GetValue("year")) ?? DateTime.Now.Year;
            _yearBox.Text = (year % 100).ToString("00");
            _yearBox.MaxLength = 2;
            _yearBox.MinWidth = 38;
            _yearBox.TextAlignment = TextAlignment.Right;
            _yearBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            _yearBox.PreviewKeyDown += OnYearBoxPreviewKeyDown;
            _yearBox.PointerWheelChanged += OnYearBoxPointerWheelChanged;
        }

        private void ConfigureOrderBox()
        {
            var order = TryGetLong(_contract.GetValue("order"));
            _orderBox.Text = order is null ? string.Empty : order.Value.ToString("000");
            _orderBox.IsReadOnly = true;
            _orderBox.IsTabStop = false;
            _orderBox.MinWidth = 38;
            _orderBox.TextAlignment = TextAlignment.Right;
            _orderBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            if (order is null)
            {
                ToolTipService.SetToolTip(_orderBox, "Будет присвоен после сохранения");
            }
        }

        private void ConfigureCostBox()
        {
            var stagesCost = SumStageCosts();
            _costBox.Text = stagesCost is null ? string.Empty : AppFormatters.FormatMoney(stagesCost.Value);
            _costBox.IsReadOnly = true;
            _costBox.IsTabStop = false;
            _costBox.MinWidth = 120;
            _costBox.TextAlignment = TextAlignment.Right;
            _costBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        private decimal? SumStageCosts()
        {
            decimal sum = 0;
            var hasStageCost = false;
            foreach (var stage in EnumerateObjectArray(_contract, "stages"))
            {
                var stageCost = TryGetStageCost(stage);
                if (stageCost is null)
                {
                    continue;
                }

                sum += stageCost.Value;
                hasStageCost = true;
            }

            return hasStageCost ? sum : null;
        }

        private static decimal? TryGetStageCost(System.Text.Json.JsonElement stage)
        {
            var value = TryGetValue(stage, "cost");
            return value?.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number when value.Value.TryGetDecimal(out var decimalValue) => decimalValue,
                System.Text.Json.JsonValueKind.String when decimal.TryParse(
                    value.Value.GetString(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedValue) => parsedValue,
                _ => null
            };
        }

        private static FrameworkElement BuildNumberSeparator()
        {
            return new TextBlock
            {
                Text = "/",
                Margin = new Thickness(0, 19, 0, 0),
                FontSize = 20,
                LineHeight = 22,
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        private void ConfigureTaskKindCombo()
        {
            var options = _taskKindOptions
                .Select(static option => new TaskKindSelectOption(option.Value?.ToString() ?? string.Empty, option.Label))
                .ToList();
            var selectedCode = GetText(_contract, "task_kind.code", "code");

            _taskKindBox.DisplayMemberPath = nameof(TaskKindSelectOption.Label);
            _taskKindBox.TextMemberPath = nameof(TaskKindSelectOption.Code);
            _taskKindBox.MatchMemberPath = nameof(TaskKindSelectOption.Code);
            _taskKindBox.MinWidth = 48;
            _taskKindBox.Items.Clear();
            foreach (var option in options)
            {
                _taskKindBox.Items.Add(option);

                if (string.Equals(option.Code, selectedCode, StringComparison.OrdinalIgnoreCase))
                {
                    _taskKindBox.SelectedItem = option;
                }
            }

            _taskKindBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        private void ContractCommerEditDialog_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ContractCommerEditDialog_Loaded;
            if (string.IsNullOrWhiteSpace(_taskKindBox.Text))
            {
                return;
            }

            DispatcherQueue.TryEnqueue(() => _statusBox.Focus(FocusState.Programmatic));
        }

        private void ConfigureStatusCombo()
        {
            var statusId = ResolveStatusId();
            var statusOptions = BuildStatusOptions(_statusOptions, includeEmpty: false);
            if (statusOptions.All(option => option.Value != statusId))
            {
                throw new InvalidOperationException($"Contract status options must contain status id {statusId}.");
            }

            StageContractStatusDialogControls.ConfigureStatusCombo(
                _statusBox,
                statusOptions,
                statusId);
            _statusBox.MinWidth = 120;
        }

        private long ResolveStatusId()
        {
            var statusId = TryGetLong(_contract.GetValue("status.id")) ?? TryGetLong(_contract.GetValue("status_id"));
            if (statusId is long existingStatusId)
            {
                return existingStatusId;
            }

            if (!_isCreateMode)
            {
                throw new InvalidOperationException("Contract edit row must contain status_id or status.id.");
            }

            return _statusOptions
                .Where(static option => string.Equals(option.Label, "В проекте", StringComparison.CurrentCultureIgnoreCase))
                .Select(static option => TryGetLong(option.Value))
                .FirstOrDefault(static id => id is not null)
                ?? throw new InvalidOperationException("Contract status options must contain 'В проекте'.");
        }

        private void ConfigureSignedAtEditor()
        {
            _signedAtEditor.Date = AppFormatters.ParseDate(_contract.GetValue("signed_at"));
            _signedAtEditor.MinWidth = 110;
            _signedAtEditor.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        private void ConfigureCommentBox()
        {
            _commentBox.PlaceholderText = string.Empty;
            _commentBox.MinWidth = 640;
            _commentBox.Width = 640;
            _commentBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        private void ConfigureFlagBoxes()
        {
            SetExtAgreementChecked(_revisionEditors.Count > 0);
            _extAgreementBox.Checked -= ExtAgreementBox_Checked;
            _extAgreementBox.Unchecked -= ExtAgreementBox_Unchecked;
            _extAgreementBox.Checked += ExtAgreementBox_Checked;
            _extAgreementBox.Unchecked += ExtAgreementBox_Unchecked;
            ToolTipService.SetToolTip(_extAgreementBox, "Дополнительные соглашения");

            _multiStageBox.IsChecked = IsMultiStageContract();
            ToolTipService.SetToolTip(_multiStageBox, "Многоэтапный контракт");
        }

        private void ConfigureResetChangesButton()
        {
            var background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68));
            var hoverBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 220, 38, 38));
            var pressedBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 185, 28, 28));
            var disabledBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 255, 205, 210));
            var foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
            var disabledForeground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 156, 63, 63));

            _resetChangesButton.Width = 24;
            _resetChangesButton.MinHeight = 0;
            _resetChangesButton.Height = 24;
            _resetChangesButton.Padding = new Thickness(0);
            _resetChangesButton.Background = background;
            _resetChangesButton.Foreground = foreground;
            _resetChangesButton.HorizontalAlignment = HorizontalAlignment.Left;
            _resetChangesButton.VerticalAlignment = VerticalAlignment.Center;
            _resetChangesButton.Resources["ButtonBackground"] = background;
            _resetChangesButton.Resources["ButtonBackgroundPointerOver"] = hoverBackground;
            _resetChangesButton.Resources["ButtonBackgroundPressed"] = pressedBackground;
            _resetChangesButton.Resources["ButtonBackgroundDisabled"] = disabledBackground;
            _resetChangesButton.Resources["ButtonBorderBrushPointerOver"] = hoverBackground;
            _resetChangesButton.Resources["ButtonBorderBrushPressed"] = pressedBackground;
            _resetChangesButton.Resources["ButtonBorderBrushDisabled"] = disabledBackground;
            _resetChangesButton.Resources["ButtonForeground"] = foreground;
            _resetChangesButton.Resources["ButtonForegroundPointerOver"] = foreground;
            _resetChangesButton.Resources["ButtonForegroundPressed"] = foreground;
            _resetChangesButton.Resources["ButtonForegroundDisabled"] = disabledForeground;
            _resetChangesButton.Content = new FontIcon
            {
                Glyph = "\uE72C",
                FontSize = 12
            };
            ToolTipService.SetToolTip(
                _resetChangesButton,
                "Отменить несохраненные изменения формы");
            _resetChangesButton.Click += ResetChangesButton_Click;
        }

        private FrameworkElement BuildContragentEditor()
        {
            _contragentBox = DialogLookupEditors.BuildAutoSuggestBox(
                nameof(ContragentSuggestionLabels),
                () => _contragentInput,
                UpdateContragentOptionsAsync,
                TrySelectContragentSuggestion,
                CommitContragentInput,
                () => _contragentInput,
                minWidth: 390,
                maxWidth: 390,
                maxSuggestionListHeight: 240,
                bindingSource: this);
            return _contragentBox;
        }

        private void ConfigureContragentState()
        {
            var contragentId = TryGetLong(_contract.GetValue("contragent.id")) ?? TryGetLong(_contract.GetValue("contragent_id"));
            var contragentName = GetText(_contract, "contragent.full_name", "contragent.name");
            _contragentInput = contragentName ?? string.Empty;
            _selectedContragentOption = contragentId is null || string.IsNullOrWhiteSpace(contragentName)
                ? null
                : new CbsTableFilterOptionDefinition
                {
                    Value = contragentId.Value,
                    Label = contragentName
                };
            _contragentOptions = _selectedContragentOption is null
                ? []
                : [_selectedContragentOption];
            RefreshContragentSuggestionLabels();
        }

        private async Task UpdateContragentOptionsAsync(string rawInput)
        {
            var searchText = rawInput?.Trim() ?? string.Empty;
            _contragentInput = rawInput ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText)
                || string.Equals(searchText, _selectedContragentOption?.Label, StringComparison.CurrentCultureIgnoreCase))
            {
                _contragentOptions = _selectedContragentOption is null
                    ? []
                    : [_selectedContragentOption];
                RefreshContragentSuggestionLabels();
                return;
            }

            var options = await _loadContragentOptionsAsync(searchText, CancellationToken.None);
            _contragentOptions = MergeSelectedContragentOption(options);
            RefreshContragentSuggestionLabels();
        }

        private bool TrySelectContragentSuggestion(string? label)
        {
            var option = FindContragentOption(label);
            if (option is null)
            {
                return false;
            }

            SelectContragentOption(option);
            return true;
        }

        private void CommitContragentInput(string? rawInput)
        {
            var option = FindContragentOption(rawInput);
            if (option is not null)
            {
                SelectContragentOption(option);
                return;
            }

            _contragentInput = _selectedContragentOption?.Label ?? string.Empty;
        }

        private void SelectContragentOption(CbsTableFilterOptionDefinition option)
        {
            _selectedContragentOption = option;
            _contragentInput = option.Label;
            _contragentOptions = MergeSelectedContragentOption(_contragentOptions);
            RefreshContragentSuggestionLabels();
        }

        private CbsTableFilterOptionDefinition? FindContragentOption(string? label)
        {
            var normalizedLabel = label?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalizedLabel)
                ? null
                : _contragentOptions.FirstOrDefault(option =>
                    string.Equals(option.Label, normalizedLabel, StringComparison.CurrentCultureIgnoreCase));
        }

        private IReadOnlyList<CbsTableFilterOptionDefinition> MergeSelectedContragentOption(
            IReadOnlyList<CbsTableFilterOptionDefinition> options)
        {
            if (_selectedContragentOption is null
                || options.Any(option => Equals(option.Value, _selectedContragentOption.Value)))
            {
                return options;
            }

            return [_selectedContragentOption, .. options];
        }

        private void RefreshContragentSuggestionLabels()
        {
            ContragentSuggestionLabels.Clear();
            foreach (var label in _contragentOptions
                .Select(static option => option.Label)
                .Where(static label => !string.IsNullOrWhiteSpace(label)))
            {
                ContragentSuggestionLabels.Add(label);
            }
        }

        private void OnYearBoxPreviewKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Key == VirtualKey.Up)
            {
                ChangeYear(1);
                args.Handled = true;
            }
            else if (args.Key == VirtualKey.Down)
            {
                ChangeYear(-1);
                args.Handled = true;
            }
        }

        private void OnYearBoxPointerWheelChanged(object sender, PointerRoutedEventArgs args)
        {
            var wheelDelta = args.GetCurrentPoint(_yearBox).Properties.MouseWheelDelta;
            if (wheelDelta == 0)
            {
                return;
            }

            ChangeYear(wheelDelta > 0 ? 1 : -1);
            args.Handled = true;
        }

        private void ChangeYear(int delta)
        {
            var year = TryGetLong(_yearBox.Text) ?? DateTime.Now.Year % 100;
            var nextYear = (year + delta) % 100;
            if (nextYear < 0)
            {
                nextYear += 100;
            }

            _yearBox.Text = nextYear.ToString("00");
            _yearBox.SelectAll();
        }

        private void ResetChangesButton_Click(object sender, RoutedEventArgs e)
        {
            BuildResetConfirmationMenu().ShowAt(_resetChangesButton);
        }

        private MenuFlyout BuildResetConfirmationMenu()
        {
            var menu = new MenuFlyout();

            var resetItem = new MenuFlyoutItem
            {
                Text = "Сбросить изменения"
            };
            resetItem.Click += (_, _) => ResetEditorsFromContract();
            menu.Items.Add(resetItem);

            menu.Items.Add(new MenuFlyoutItem
            {
                Text = "Отмена"
            });

            return menu;
        }

        private void ResetEditorsFromContract()
        {
            ResetTaskKindFromContract();
            _yearBox.Text = ((TryGetLong(_contract.GetValue("year")) ?? DateTime.Now.Year) % 100).ToString("00");
            _orderBox.Text = TryGetLong(_contract.GetValue("order")) is long order
                ? order.ToString("000")
                : string.Empty;
            ConfigureContragentState();
            if (_contragentBox is not null)
            {
                _contragentBox.Text = _contragentInput;
            }

            ConfigureStatusCombo();
            _signedAtEditor.Date = AppFormatters.ParseDate(_contract.GetValue("signed_at"));
            _costBox.Text = SumStageCosts() is decimal stagesCost
                ? AppFormatters.FormatMoney(stagesCost)
                : string.Empty;
            _commentBox.Text = string.Empty;
            ResetMainTabEditorsFromContract();
            ResetRevisionEditorsFromContract();
            SetExtAgreementChecked(_revisionEditors.Count > 0);
            RefreshRevisionsStack();
            _multiStageBox.IsChecked = IsMultiStageContract();
        }

        private void ResetMainTabEditorsFromContract()
        {
            _governmentalBox.IsChecked = TryGetBool(_contract.GetValue("governmental")) == true;
            _externalNumberBox.Text = GetText(_contract, "external_number") ?? string.Empty;
            _deadlineAtEditor.Date = AppFormatters.ParseDate(_contract.GetValue("deadline_at"));
            _closedAtEditor.Date = AppFormatters.ParseDate(_contract.GetValue("closed_at"));

            var contractRevision = GetContractRevision();
            _revisionPresentBox.IsChecked = contractRevision is not null
                && TryGetBool(TryGetValue(contractRevision.Value, "is_present")) == true;
            _revisionDescriptionBox.Text = contractRevision is null
                ? string.Empty
                : TryGetString(contractRevision.Value, "description") ?? string.Empty;
            _revisionDocLinkBox.Text = contractRevision is null
                ? string.Empty
                : TryGetString(contractRevision.Value, "doc_link") ?? string.Empty;
            _revisionScanLinkBox.Text = contractRevision is null
                ? string.Empty
                : TryGetString(contractRevision.Value, "scan_link") ?? string.Empty;
            _revisionProtocolLinkBox.Text = contractRevision is null
                ? string.Empty
                : TryGetString(contractRevision.Value, "protocol_link") ?? string.Empty;
            _revisionZipLinkBox.Text = contractRevision is null
                ? string.Empty
                : TryGetString(contractRevision.Value, "zip_link") ?? string.Empty;
        }

        private JsonElement? GetContractRevision()
        {
            var revision = EnumerateObjectArray(_contract, "revisions").FirstOrDefault();
            if (revision.ValueKind == JsonValueKind.Object)
            {
                return revision;
            }

            return _isCreateMode
                ? null
                : throw new InvalidOperationException("Contract edit row must contain revisions[0].");
        }

        private void ResetRevisionEditorsFromContract()
        {
            _revisionEditors.Clear();
            foreach (var revision in EnumerateObjectArray(_contract, "revisions").Skip(1))
            {
                _revisionEditors.Add(new RevisionEditorState
                {
                    Number = TryGetLong(TryGetValue(revision, "priority")) ?? 0,
                    IsPresent = TryGetBool(TryGetValue(revision, "is_present")) == true,
                    Description = TryGetString(revision, "description") ?? string.Empty,
                    DocLink = TryGetString(revision, "doc_link") ?? string.Empty,
                    ScanLink = TryGetString(revision, "scan_link") ?? string.Empty,
                    ProtocolLink = TryGetString(revision, "protocol_link") ?? string.Empty
                });
            }

            _revisionEditors.Sort(static (left, right) => left.Number.CompareTo(right.Number));
        }

        private void ResetTaskKindFromContract()
        {
            var selectedCode = GetText(_contract, "task_kind.code", "code");
            _taskKindBox.SelectedItem = _taskKindBox.Items
                .OfType<TaskKindSelectOption>()
                .FirstOrDefault(option => string.Equals(option.Code, selectedCode, StringComparison.OrdinalIgnoreCase));
            if (_taskKindBox.SelectedItem is null)
            {
                _taskKindBox.Text = string.Empty;
            }
        }

        private bool HasExtAgreement()
        {
            return _revisionEditors.Count > 0;
        }

        private void ExtAgreementBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingExtAgreementBox || _revisionEditors.Count > 0)
            {
                return;
            }

            AddRevision(1);
            SelectRevisionsTab();
        }

        private void ExtAgreementBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingExtAgreementBox)
            {
                return;
            }

            if (_revisionEditors.Count > 0)
            {
                SetExtAgreementChecked(true);
            }
        }

        private void AddRevision(long number)
        {
            if (_revisionEditors.Any(revision => revision.Number == number))
            {
                ShowErrorInfo($"Ревизия с номером {number} уже существует.");
                return;
            }

            _revisionEditors.Add(new RevisionEditorState
            {
                Number = number,
                Description = "Доп. соглашение"
            });
            _revisionEditors.Sort(static (left, right) => left.Number.CompareTo(right.Number));
            SetExtAgreementChecked(true);
            RefreshRevisionsStack();
        }

        private void DeleteRevision(RevisionEditorState revision)
        {
            if (revision.Number == 1 && _revisionEditors.Any(item => item.Number > revision.Number))
            {
                ShowErrorInfo($"Нельзя удалить ревизию № {revision.Number}, пока существуют ревизии с большим номером.");
                return;
            }

            _revisionEditors.Remove(revision);
            if (_revisionEditors.Count == 0)
            {
                SetExtAgreementChecked(false);
            }

            RefreshRevisionsStack();
        }

        private void SetExtAgreementChecked(bool isChecked)
        {
            _isUpdatingExtAgreementBox = true;
            _extAgreementBox.IsChecked = isChecked;
            _isUpdatingExtAgreementBox = false;
        }

        private void SelectRevisionsTab()
        {
            if (_tabs is not null && _revisionsTab is not null)
            {
                _tabs.SelectedItem = _revisionsTab;
            }
        }

        private bool IsMultiStageContract()
        {
            var explicitValue =
                TryGetBool(_contract.GetValue("multyStage"))
                ?? TryGetBool(_contract.GetValue("multiStage"))
                ?? TryGetBool(_contract.GetValue("is_multistage"));
            if (explicitValue is not null)
            {
                return explicitValue.Value;
            }

            var stages = EnumerateObjectArray(_contract, "stages").ToList();
            return stages.Any(stage => TryGetLong(TryGetValue(stage, "priority")) > 0)
                || stages.Count > 1;
        }

        private static FrameworkElement BuildFlagHost(CheckBox checkBox, string label)
        {
            checkBox.VerticalAlignment = VerticalAlignment.Center;
            checkBox.HorizontalAlignment = HorizontalAlignment.Left;
            checkBox.HorizontalContentAlignment = HorizontalAlignment.Left;
            checkBox.MinHeight = 0;
            checkBox.MinWidth = 0;
            checkBox.Width = 48;
            checkBox.Padding = new Thickness(0);
            checkBox.Margin = new Thickness(0);
            checkBox.Content = new TextBlock
            {
                Text = label,
                Margin = new Thickness(3, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            return new Grid
            {
                Width = 48,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    checkBox
                }
            };
        }

        private TabView BuildTabs()
        {
            var tabView = new TabView
            {
                IsAddTabButtonVisible = false,
                Height = TabAreaHeight,
                TabWidthMode = TabViewWidthMode.Equal
            };
            _tabs = tabView;

            tabView.TabItems.Add(BuildColoredTab(
                "Контракт",
                Microsoft.UI.ColorHelper.FromArgb(255, 255, 251, 237),
                Microsoft.UI.ColorHelper.FromArgb(255, 237, 233, 220),
                BuildMainTabContent()));
            tabView.TabItems.Add(BuildColoredTab(
                "Этапы",
                Microsoft.UI.ColorHelper.FromArgb(255, 239, 255, 242),
                Microsoft.UI.ColorHelper.FromArgb(255, 220, 235, 223),
                BuildPlaceholder("Разметка этапов будет добавлена после утверждения единого шаблона AppEditDialog.")));
            _revisionsTab = BuildColoredTab(
                "Ревизии",
                Microsoft.UI.ColorHelper.FromArgb(255, 239, 250, 255),
                Microsoft.UI.ColorHelper.FromArgb(255, 222, 233, 237),
                BuildRevisionsTabContent());
            tabView.TabItems.Add(_revisionsTab);

            return tabView;
        }

        private UIElement BuildMainTabContent()
        {
            ResetMainTabEditorsFromContract();
            ConfigureMainTabEditors();

            var grid = new Grid
            {
                Padding = new Thickness(8),
                ColumnSpacing = 8,
                RowSpacing = 8
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var governmental = BuildInlineCheckBox(_governmentalBox, "ГосКонтракт");
            grid.Children.Add(governmental);

            var revisionPresent = BuildInputLineCheckBox(_revisionPresentBox, "В наличии");
            Grid.SetColumn(revisionPresent, 1);
            grid.Children.Add(revisionPresent);

            var revisionDescription = (FrameworkElement)BuildLabeledControl("Тип документа", _revisionDescriptionBox, spacing: 3);
            Grid.SetColumn(revisionDescription, 2);
            Grid.SetColumnSpan(revisionDescription, 2);
            grid.Children.Add(revisionDescription);

            var externalNumber = (FrameworkElement)BuildLabeledControl("Внешний номер", _externalNumberBox, spacing: 3);
            Grid.SetRow(externalNumber, 1);
            grid.Children.Add(externalNumber);

            var deadlineAt = (FrameworkElement)BuildLabeledControl("Срок завершения", _deadlineAtEditor, spacing: 3);
            Grid.SetRow(deadlineAt, 1);
            Grid.SetColumn(deadlineAt, 1);
            grid.Children.Add(deadlineAt);

            var closedAt = (FrameworkElement)BuildLabeledControl("Дата закрытия", _closedAtEditor, spacing: 3);
            Grid.SetRow(closedAt, 1);
            Grid.SetColumn(closedAt, 2);
            grid.Children.Add(closedAt);

            var filesHeader = BuildSectionSeparator("Файл");
            Grid.SetRow(filesHeader, 2);
            Grid.SetColumnSpan(filesHeader, 5);
            grid.Children.Add(filesHeader);

            var docLink = BuildFileRow("Исходник", "\uf000", _revisionDocLinkBox);
            Grid.SetRow(docLink, 3);
            Grid.SetColumnSpan(docLink, 4);
            grid.Children.Add(docLink);

            var scanLink = BuildFileRow("Скан", "\uea90", _revisionScanLinkBox);
            Grid.SetRow(scanLink, 4);
            Grid.SetColumnSpan(scanLink, 4);
            grid.Children.Add(scanLink);

            var protocolLink = BuildFileRow("Протокол", "\ue9a4", _revisionProtocolLinkBox);
            Grid.SetRow(protocolLink, 5);
            Grid.SetColumnSpan(protocolLink, 4);
            grid.Children.Add(protocolLink);

            return grid;
        }

        private UIElement BuildRevisionsTabContent()
        {
            _revisionsStack = new StackPanel
            {
                Padding = new Thickness(8),
                Spacing = 10
            };
            RefreshRevisionsStack();
            return _revisionsStack;
        }

        private void RefreshRevisionsStack()
        {
            if (_revisionsStack is null)
            {
                return;
            }

            _revisionsStack.Children.Clear();
            if (_revisionEditors.Count == 0)
            {
                _revisionsStack.Children.Add(BuildPlaceholder("Дополнительные соглашения отсутствуют."));
                return;
            }

            foreach (var revision in _revisionEditors.OrderBy(static revision => revision.Number))
            {
                _revisionsStack.Children.Add(BuildRevisionSection(revision));
            }
        }

        private UIElement BuildRevisionSection(RevisionEditorState revision)
        {
            var grid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 8
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(132) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(390) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var numberBox = new TextBox
            {
                Text = FormatRevisionNumber(revision.Number),
                IsReadOnly = true,
                IsTabStop = false,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var number = (FrameworkElement)BuildLabeledControl("Номер", numberBox, spacing: 3);
            grid.Children.Add(number);

            var presentBox = new CheckBox
            {
                IsChecked = revision.IsPresent
            };
            presentBox.Checked += (_, _) => revision.IsPresent = true;
            presentBox.Unchecked += (_, _) => revision.IsPresent = false;
            var present = BuildInputLineCheckBox(presentBox, "В наличии");
            Grid.SetColumn(present, 1);
            grid.Children.Add(present);

            var descriptionBox = new TextBox
            {
                Text = revision.Description,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            descriptionBox.TextChanged += (_, _) => revision.Description = descriptionBox.Text ?? string.Empty;
            var description = (FrameworkElement)BuildLabeledControl(
                "Тип документа",
                BuildRevisionDescriptionEditor(
                    descriptionBox,
                    () => AddRevision(revision.Number + 1),
                    () => DeleteRevision(revision)),
                spacing: 3);
            Grid.SetColumn(description, 2);
            Grid.SetColumnSpan(description, 2);
            grid.Children.Add(description);

            var docLink = BuildFileRow("Исходник", "\uf000", BuildRevisionFileTextBox(revision.DocLink, value => revision.DocLink = value));
            Grid.SetRow(docLink, 1);
            Grid.SetColumnSpan(docLink, 4);
            grid.Children.Add(docLink);

            var scanLink = BuildFileRow("Скан", "\uea90", BuildRevisionFileTextBox(revision.ScanLink, value => revision.ScanLink = value));
            Grid.SetRow(scanLink, 2);
            Grid.SetColumnSpan(scanLink, 4);
            grid.Children.Add(scanLink);

            var protocolLink = BuildFileRow("Протокол", "\ue9a4", BuildRevisionFileTextBox(revision.ProtocolLink, value => revision.ProtocolLink = value));
            Grid.SetRow(protocolLink, 3);
            Grid.SetColumnSpan(protocolLink, 4);
            grid.Children.Add(protocolLink);

            var separator = BuildSectionSeparator(null);
            Grid.SetRow(separator, 4);
            Grid.SetColumnSpan(separator, 4);
            grid.Children.Add(separator);

            return grid;
        }

        private void ConfigureMainTabEditors()
        {
            _governmentalBox.Content = null;
            _governmentalBox.HorizontalAlignment = HorizontalAlignment.Left;
            _governmentalBox.VerticalAlignment = VerticalAlignment.Center;
            _governmentalBox.MinHeight = 0;
            ToolTipService.SetToolTip(_governmentalBox, "Государственный контракт");

            _revisionPresentBox.Content = null;
            _revisionPresentBox.HorizontalAlignment = HorizontalAlignment.Left;
            _revisionPresentBox.VerticalAlignment = VerticalAlignment.Center;
            _revisionPresentBox.MinHeight = 0;
            ToolTipService.SetToolTip(_revisionPresentBox, "Документ в наличии");

            _revisionDescriptionBox.MinWidth = 260;
            _revisionDescriptionBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            _externalNumberBox.MinWidth = 260;
            _externalNumberBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            _deadlineAtEditor.MinWidth = 140;
            _deadlineAtEditor.HorizontalAlignment = HorizontalAlignment.Stretch;

            _closedAtEditor.MinWidth = 140;
            _closedAtEditor.HorizontalAlignment = HorizontalAlignment.Stretch;

            ConfigureFileTextBox(_revisionDocLinkBox);
            ConfigureFileTextBox(_revisionScanLinkBox);
            ConfigureFileTextBox(_revisionProtocolLinkBox);
            ConfigureFileTextBox(_revisionZipLinkBox);
        }

        private static void ConfigureFileTextBox(TextBox textBox)
        {
            textBox.MinWidth = 260;
            textBox.IsReadOnly = true;
            textBox.IsTabStop = false;
            textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        private static TextBox BuildRevisionFileTextBox(string value, Action<string> updateValue)
        {
            var textBox = new TextBox
            {
                Text = value
            };
            textBox.TextChanged += (_, _) => updateValue(textBox.Text ?? string.Empty);
            ConfigureFileTextBox(textBox);
            return textBox;
        }

        private static string FormatRevisionNumber(long number)
        {
            return number > 0
                ? number.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static FrameworkElement BuildInputLineCheckBox(CheckBox checkBox, string label)
        {
            var element = BuildInlineCheckBox(checkBox, label);
            element.Margin = new Thickness(0, 18, 0, 0);
            return element;
        }

        private static FrameworkElement BuildRevisionDescriptionEditor(
            TextBox descriptionBox,
            Action addRevision,
            Action deleteRevision)
        {
            descriptionBox.MinWidth = 285;
            descriptionBox.HorizontalAlignment = HorizontalAlignment.Stretch;

            var grid = new Grid
            {
                ColumnSpacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(285) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            grid.Children.Add(descriptionBox);

            var addButton = BuildRevisionActionButton(
                "\ue710",
                "Добавить ревизию",
                Microsoft.UI.ColorHelper.FromArgb(255, 34, 197, 94),
                Microsoft.UI.ColorHelper.FromArgb(255, 22, 163, 74),
                Microsoft.UI.ColorHelper.FromArgb(255, 21, 128, 61));
            addButton.Click += (_, _) => addRevision();
            Grid.SetColumn(addButton, 1);
            grid.Children.Add(addButton);

            var deleteButton = BuildRevisionActionButton(
                "\ue74d",
                "Удалить ревизию",
                Microsoft.UI.ColorHelper.FromArgb(255, 239, 68, 68),
                Microsoft.UI.ColorHelper.FromArgb(255, 220, 38, 38),
                Microsoft.UI.ColorHelper.FromArgb(255, 185, 28, 28));
            deleteButton.Click += (_, _) => deleteRevision();
            Grid.SetColumn(deleteButton, 2);
            grid.Children.Add(deleteButton);

            return grid;
        }

        private static Button BuildRevisionActionButton(
            string iconGlyph,
            string tooltip,
            Windows.UI.Color backgroundColor,
            Windows.UI.Color hoverColor,
            Windows.UI.Color pressedColor)
        {
            var background = new Microsoft.UI.Xaml.Media.SolidColorBrush(backgroundColor);
            var hoverBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(hoverColor);
            var pressedBackground = new Microsoft.UI.Xaml.Media.SolidColorBrush(pressedColor);
            var foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);

            var button = new Button
            {
                Width = 24,
                Height = 24,
                MinWidth = 24,
                Padding = new Thickness(0),
                Background = background,
                Foreground = foreground,
                Content = new TextBlock
                {
                    Text = iconGlyph,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = foreground
                }
            };
            button.Resources["ButtonBackground"] = background;
            button.Resources["ButtonBackgroundPointerOver"] = hoverBackground;
            button.Resources["ButtonBackgroundPressed"] = pressedBackground;
            button.Resources["ButtonBorderBrush"] = background;
            button.Resources["ButtonBorderBrushPointerOver"] = hoverBackground;
            button.Resources["ButtonBorderBrushPressed"] = pressedBackground;
            button.Resources["ButtonForeground"] = foreground;
            button.Resources["ButtonForegroundPointerOver"] = foreground;
            button.Resources["ButtonForegroundPressed"] = foreground;
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        private static FrameworkElement BuildSectionSeparator(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return new Grid
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    Children =
                    {
                        BuildSeparatorLine()
                    }
                };
            }

            var leftLine = BuildSeparatorLine();

            var titleBlock = new TextBlock
            {
                Text = title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleBlock, 1);

            var rightLine = BuildSeparatorLine();
            Grid.SetColumn(rightLine, 2);

            return new Grid
            {
                Margin = new Thickness(0, 4, 0, 0),
                ColumnSpacing = 8,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                Children =
                {
                    leftLine,
                    titleBlock,
                    rightLine
                }
            };
        }

        private static Border BuildSeparatorLine()
        {
            var line = new Border
            {
                Height = 1,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 222, 226, 230)),
                VerticalAlignment = VerticalAlignment.Center
            };
            return line;
        }

        private FrameworkElement BuildFileRow(string label, string iconGlyph, TextBox editor)
        {
            var grid = new Grid
            {
                ColumnSpacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center
            });

            var icon = new TextBlock
            {
                Text = iconGlyph,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 1);
            grid.Children.Add(icon);

            Grid.SetColumn(editor, 2);
            grid.Children.Add(editor);

            var attachButton = BuildFileActionButton("\ue723", "Прикрепить");
            attachButton.Click += async (_, _) => await PickFilePathAsync(editor);
            Grid.SetColumn(attachButton, 3);
            grid.Children.Add(attachButton);

            var openButton = BuildFileActionButton("\ue8a7", "Открыть");
            openButton.Click += (_, _) => OpenFilePath(editor.Text);
            Grid.SetColumn(openButton, 4);
            grid.Children.Add(openButton);

            return grid;
        }

        private static Button BuildFileActionButton(string iconGlyph, string tooltip)
        {
            var button = new Button
            {
                Width = 24,
                Height = 24,
                MinWidth = 24,
                Padding = new Thickness(0),
                Content = new TextBlock
                {
                    Text = iconGlyph,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            ToolTipService.SetToolTip(button, tooltip);
            return button;
        }

        private async Task PickFilePathAsync(TextBox target)
        {
            try
            {
                var picker = new FileOpenPicker
                {
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                picker.FileTypeFilter.Add("*");

                if (App.CurrentWindow is not null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var file = await picker.PickSingleFileAsync();
                if (file is not null)
                {
                    target.Text = file.Path;
                }
            }
            catch (Exception ex)
            {
                ShowErrorInfo($"Не удалось выбрать файл: {ex.Message}");
            }
        }

        private void OpenFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ShowErrorInfo("Путь к файлу не заполнен.");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath.Trim(),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ShowErrorInfo($"Не удалось открыть файл: {ex.Message}");
            }
        }

        private static TabViewItem BuildColoredTab(
            string title,
            Windows.UI.Color backgroundColor,
            Windows.UI.Color borderColor,
            UIElement content)
        {
            var background = new Microsoft.UI.Xaml.Media.SolidColorBrush(backgroundColor);
            var selectedBorder = new Microsoft.UI.Xaml.Media.SolidColorBrush(borderColor);
            var item = new TabViewItem
            {
                Header = new Border
                {
                    Background = background,
                    Padding = new Thickness(8, 2, 8, 2),
                    Child = new TextBlock
                    {
                        Text = title,
                        TextWrapping = TextWrapping.NoWrap
                    }
                },
                Content = new Border
                {
                    Background = background,
                    Child = new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollMode = ScrollMode.Enabled,
                        Content = content
                    }
                }
            };
            item.Resources["TabViewItemHeaderBackground"] = background;
            item.Resources["TabViewItemHeaderBackgroundSelected"] = background;
            item.Resources["TabViewItemHeaderBackgroundPointerOver"] = background;
            item.Resources["TabViewItemHeaderBackgroundPressed"] = background;
            item.Resources["TabViewItemHeaderBackgroundDisabled"] = background;
            item.Resources["TabViewItemBorderBrush"] = selectedBorder;
            item.Resources["TabViewSelectedItemBorderBrush"] = selectedBorder;
            return item;
        }

        private static FrameworkElement BuildInlineCheckBox(CheckBox checkBox, string label)
        {
            checkBox.Content = null;
            checkBox.HorizontalAlignment = HorizontalAlignment.Left;
            checkBox.VerticalAlignment = VerticalAlignment.Center;
            checkBox.MinHeight = 0;
            checkBox.MinWidth = 0;
            checkBox.Padding = new Thickness(0);
            checkBox.Margin = new Thickness(0);

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 3,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    checkBox,
                    new TextBlock
                    {
                        Text = label,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
        }

        private static UIElement BuildPlaceholder(string text)
        {
            return new Border
            {
                Padding = new Thickness(8),
                Child = new TextBlock
                {
                    Text = text,
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        private string BuildDialogTitle()
        {
            if (_isCreateMode)
            {
                return "Создание контракта";
            }

            var name = GetText(_contract, "name");
            return string.IsNullOrWhiteSpace(name)
                ? "Редактирование контракта"
                : $"Редактирование контракта {name}";
        }

        private sealed record TaskKindSelectOption(string Code, string Label);

        private sealed class RevisionEditorState
        {
            public long Number { get; init; }

            public bool IsPresent { get; set; }

            public string Description { get; set; } = string.Empty;

            public string DocLink { get; set; } = string.Empty;

            public string ScanLink { get; set; } = string.Empty;

            public string ProtocolLink { get; set; } = string.Empty;
        }
    }
}
