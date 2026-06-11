using System.Collections.ObjectModel;
using System.Globalization;
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
using Windows.System;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;
using static CbsContractsDesktopClient.Shared.Dialogs.AppDialogLayout;
using static CbsContractsDesktopClient.Shared.Dialogs.StageContractStatusDialogControls;

namespace CbsContractsDesktopClient.Views.Functional
{
    public sealed class ContractCommerEditDialog : AppEditDialog
    {
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
        private readonly CheckBox _extAgreementBox = new();
        private readonly CheckBox _multiStageBox = new();
        private readonly Button _resetChangesButton = new();
        private AutoSuggestBox? _contragentBox;
        private IReadOnlyList<CbsTableFilterOptionDefinition> _contragentOptions = [];
        private CbsTableFilterOptionDefinition? _selectedContragentOption;
        private string _contragentInput = string.Empty;

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

            var tabs = BuildTabs();
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);
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
            _extAgreementBox.IsChecked = HasExtAgreement();
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
            _extAgreementBox.IsChecked = HasExtAgreement();
            _multiStageBox.IsChecked = IsMultiStageContract();
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
            return EnumerateObjectArray(_contract, "revisions").Skip(1).Any();
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
                TabWidthMode = TabViewWidthMode.Equal
            };

            tabView.TabItems.Add(BuildColoredTab(
                "Общее",
                Microsoft.UI.ColorHelper.FromArgb(255, 255, 251, 237),
                Microsoft.UI.ColorHelper.FromArgb(255, 237, 233, 220),
                BuildPlaceholder("Разметка общих полей будет добавлена после утверждения единого шаблона AppEditDialog.")));
            tabView.TabItems.Add(BuildColoredTab(
                "Этапы",
                Microsoft.UI.ColorHelper.FromArgb(255, 239, 255, 242),
                Microsoft.UI.ColorHelper.FromArgb(255, 220, 235, 223),
                BuildPlaceholder("Разметка этапов будет добавлена после утверждения единого шаблона AppEditDialog.")));
            tabView.TabItems.Add(BuildColoredTab(
                "Ревизии",
                Microsoft.UI.ColorHelper.FromArgb(255, 239, 250, 255),
                Microsoft.UI.ColorHelper.FromArgb(255, 222, 233, 237),
                BuildPlaceholder("Разметка ревизий будет добавлена после утверждения единого шаблона AppEditDialog.")));

            return tabView;
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
                    Child = content
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
    }
}
