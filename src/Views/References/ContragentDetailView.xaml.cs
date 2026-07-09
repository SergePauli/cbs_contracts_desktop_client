using CbsContractsDesktopClient.Models.References;
using CbsContractsDesktopClient.Services.References;
using CbsContractsDesktopClient.Stores.Contragents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;
using static CbsContractsDesktopClient.Shared.Data.JsonDataReader;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed partial class ContragentDetailView : UserControl
    {
        private readonly ContragentDetailStore _contragentDetailStore;
        private readonly IReferenceLookupCacheService? _referenceLookupCacheService;
        private CancellationTokenSource? _ownershipLookupCts;
        private int _refreshVersion;

        public static readonly DependencyProperty RowProperty =
            DependencyProperty.Register(
                nameof(Row),
                typeof(TableDataRow),
                typeof(ContragentDetailView),
                new PropertyMetadata(null, OnRowChanged));

        public static readonly DependencyProperty ContractsRowProperty =
            DependencyProperty.Register(
                nameof(ContractsRow),
                typeof(TableDataRow),
                typeof(ContragentDetailView),
                new PropertyMetadata(null, OnContractsRowChanged));

        public event EventHandler<EmployeeBoxEditRequestedEventArgs>? EmployeeEditRequested;

        public ContragentDetailView()
        {
            _contragentDetailStore = App.Services.GetRequiredService<ContragentDetailStore>();
            InitializeComponent();
            _referenceLookupCacheService = App.Services.GetService<IReferenceLookupCacheService>();
            EmployeesBox.EditRequested += (_, args) => EmployeeEditRequested?.Invoke(this, args);
            Refresh();
        }

        public TableDataRow? Row
        {
            get => (TableDataRow?)GetValue(RowProperty);
            set => SetValue(RowProperty, value);
        }

        public TableDataRow? ContractsRow
        {
            get => (TableDataRow?)GetValue(ContractsRowProperty);
            set => SetValue(ContractsRowProperty, value);
        }

        public string BuildClipboardText()
        {
            var row = Row;
            if (row is null || row.IsPlaceholder)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            AddClipboardLine(lines, "Форма", BuildInlineValue(OwnershipFullNameTextBlock.Text, OwnershipCodeTextBlock.Text));
            AddClipboardLine(lines, "Полное имя", FullNameTextBlock.Text);
            AddClipboardLine(lines, "Реквизиты", RequisitesTextBlock.Text);
            AddClipboardLine(lines, "Описание", DescriptionTextBlock.Text);
            AddClipboardLine(lines, "Контакты", BuildContactsText(row));
            AddClipboardLine(lines, "Адреса", AddressesTextBlock.Text);
            AddClipboardLine(lines, "Контракты", BuildContractLinksClipboardText(ContractsRow));
            return string.Join(Environment.NewLine, lines);
        }

        private static void OnRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ContragentDetailView)d).Refresh();
        }

        private static void OnContractsRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ContragentDetailView)d).RefreshContractLinks();
        }

        private void Refresh()
        {
            var refreshVersion = ++_refreshVersion;
            _ownershipLookupCts?.Cancel();

            var row = Row;
            if (row is null || row.IsPlaceholder)
            {
                OwnershipFullNameTextBlock.Text = "Форма не указана";
                OwnershipCodeTextBlock.Text = string.Empty;
                FullNameTextBlock.Text = string.Empty;
                RequisitesTextBlock.Text = string.Empty;
                DescriptionTextBlock.Text = string.Empty;
                AddressesTextBlock.Text = string.Empty;
                ContactsPanel.Children.Clear();
                ContractLinksPanel.Children.Clear();
                _contragentDetailStore.SetContragent(null);
                EmployeesBox.Employees = [];
                return;
            }

            _contragentDetailStore.SetContragent(row);
            var fullName = TryGetText(row, "requisites.organization.full_name", "full_name");

            OwnershipFullNameTextBlock.Text = BuildOwnershipFullNameText(row);
            OwnershipCodeTextBlock.Text = BuildOwnershipCodeText(row);
            _ = RefreshOwnershipFromReferenceAsync(row, refreshVersion);
            FullNameTextBlock.Text = fullName ?? string.Empty;
            RequisitesTextBlock.Text = BuildRequisitesText(row);
            DescriptionTextBlock.Text = TryGetText(row, "description") ?? string.Empty;
            AddressesTextBlock.Text = BuildAddressesText(row);
            RenderContacts(BuildContactsText(row));
            RefreshContractLinks();
            EmployeesBox.Employees = _contragentDetailStore.Employees;
        }

        private void RefreshContractLinks()
        {
            RenderContractLinks(ContractsRow is null || ContractsRow.IsPlaceholder
                ? []
                : ReadContractLinks(ContractsRow));
        }

        private static void AddClipboardLine(ICollection<string> lines, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            lines.Add($"{label}: {value.Trim()}");
        }

        private static string BuildInlineValue(params string?[] values)
        {
            return string.Join(
                " ",
                values
                    .Select(static value => value?.Trim() ?? string.Empty)
                    .Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildContractLinksClipboardText(TableDataRow? contractsRow)
        {
            if (contractsRow is null || contractsRow.IsPlaceholder)
            {
                return string.Empty;
            }

            var contracts = ReadContractLinks(contractsRow);
            return contracts.Count == 0
                ? string.Empty
                : string.Join(", ", contracts.Select(static contract => contract.Title));
        }

        private static string BuildOwnershipFullNameText(TableDataRow row)
        {
            return TryGetText(
                row,
                "requisites.organization.ownership.full_name",
                "ownership.full_name",
                "requisites.organization.ownership.name",
                "ownership.name") ?? "Форма не указана";
        }

        private static string BuildOwnershipCodeText(TableDataRow row)
        {
            var code = TryGetText(
                row,
                "requisites.organization.ownership.code",
                "ownership.code",
                "requisites.organization.ownership.okopf",
                "ownership.okopf",
                "requisites.organization.okopf",
                "okopf");
            return string.IsNullOrWhiteSpace(code)
                ? string.Empty
                : $"код: {code}";
        }

        private async Task RefreshOwnershipFromReferenceAsync(TableDataRow row, int refreshVersion)
        {
            if (_referenceLookupCacheService is null)
            {
                return;
            }

            var ownershipId = GetOwnershipId(row);
            var ownershipCode = GetOwnershipCode(row);
            if (ownershipId is null && string.IsNullOrWhiteSpace(ownershipCode))
            {
                return;
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _ownershipLookupCts = cancellationTokenSource;

            try
            {
                var ownership = await _referenceLookupCacheService.FindOwnershipAsync(
                    ownershipId,
                    ownershipCode,
                    cancellationTokenSource.Token);

                if (cancellationTokenSource.IsCancellationRequested || refreshVersion != _refreshVersion)
                {
                    return;
                }

                if (ownership is null)
                {
                    return;
                }

                var fullName = !string.IsNullOrWhiteSpace(ownership.FullName)
                    ? ownership.FullName
                    : ownership.DisplayName;
                var code = ownership.Code;
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    OwnershipFullNameTextBlock.Text = fullName;
                }

                OwnershipCodeTextBlock.Text = string.IsNullOrWhiteSpace(code)
                    ? string.Empty
                    : $"код: {code}";
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        private static object? GetOwnershipId(TableDataRow row)
        {
            return TryGetText(
                row,
                "requisites.organization.ownership.id",
                "ownership.id");
        }

        private static string? GetOwnershipCode(TableDataRow row)
        {
            return TryGetText(
                row,
                "requisites.organization.ownership.okopf",
                "ownership.okopf",
                "requisites.organization.ownership.code",
                "ownership.code",
                "requisites.organization.okopf",
                "okopf");
        }

        private void RenderContacts(string contactsText)
        {
            ContactsPanel.Children.Clear();
            ContactsPanel.ColumnDefinitions.Clear();
            ContactsPanel.RowDefinitions.Clear();

            ContactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ContactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ContactsPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ContactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ContactsPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var column = 0;
            var row = 0;
            foreach (var value in DialogContactsEditor.ParseContactValues(contactsText))
            {
                if (!ContactTypeClassifier.TryClassify(value, out var match))
                {
                    continue;
                }

                var element = (FrameworkElement)DialogContactsEditor.BuildContactElement(value, match, showRemoveButton: false);
                Grid.SetColumn(element, column);
                Grid.SetRow(element, row);
                ContactsPanel.Children.Add(element);

                column++;
                if (column == 3)
                {
                    column = 0;
                    row++;
                }

                if (row > 1)
                {
                    return;
                }
            }
        }

        private static string BuildRequisitesText(TableDataRow row)
        {
            var parts = new[]
            {
                FormatPart("ИНН", TryGetText(row, "requisites.organization.inn", "inn")),
                FormatPart("КПП", TryGetText(row, "requisites.organization.kpp", "kpp")),
                FormatPart("Подразделение", TryGetText(row, "requisites.organization.division", "division", "contragent.division"))
            };

            return string.Join("; ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
        }

        private static string BuildAddressesText(TableDataRow row)
        {
            var registered = TryGetText(
                row,
                "registred_addr.address.value",
                "registered_addr.address.value");
            var real = TryGetText(row, "real_addr.address.value", "address");

            if (!string.IsNullOrWhiteSpace(registered) && !string.IsNullOrWhiteSpace(real))
            {
                return string.Equals(registered, real, StringComparison.CurrentCultureIgnoreCase)
                    ? real
                    : $"Юр.: {registered}; факт.: {real}";
            }

            return real ?? registered ?? string.Empty;
        }

        private static string BuildContactsText(TableDataRow row)
        {
            var directText = TryGetText(
                row,
                "contacts.contact_attributes.value",
                "contacts.contact_attributes.name",
                "contacts.contact.value",
                "contacts.contact.name",
                "contacts.value",
                "contacts.name");
            if (!string.IsNullOrWhiteSpace(directText))
            {
                return directText;
            }

            return row.Values.TryGetValue("contacts", out var contactsElement)
                ? string.Join(", ", ReadContacts(contactsElement))
                : string.Empty;
        }

        private static IReadOnlyList<string> ReadContacts(JsonElement contactsElement)
        {
            if (contactsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return contactsElement
                .EnumerateArray()
                .Select(ReadContactValue)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList()!;
        }

        private void RenderContractLinks(IReadOnlyList<ContractLinkItem> contracts)
        {
            ContractLinksPanel.Children.Clear();
            if (contracts.Count == 0)
            {
                ContractLinksPanel.Children.Add(new TextBlock
                {
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellCaptionTextBrush"],
                    Text = "НЕТ ЗАПИСЕЙ"
                });
                return;
            }

            foreach (var contract in contracts.Take(4))
            {
                ContractLinksPanel.Children.Add(new HyperlinkButton
                {
                    Content = contract.Title,
                    Padding = new Thickness(0),
                    MinWidth = 0,
                    MinHeight = 20,
                    Tag = contract
                });
            }
        }

        private static IReadOnlyList<ContractLinkItem> ReadContractLinks(TableDataRow row)
        {
            var contractsElement = TryGetArray(row, "contracts")
                ?? TryGetArray(row, "contragent.contracts");
            if (contractsElement is null)
            {
                return [];
            }

            return EnumerateObjectArray(contractsElement)
                .Select(ReadContractLink)
                .Where(static contract => contract is not null)
                .Cast<ContractLinkItem>()
                .ToList();
        }

        private static ContractLinkItem? ReadContractLink(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var id = ReadLongProperty(item, "id");
            var title = ReadStringProperty(item, "name");
            if (string.IsNullOrWhiteSpace(title))
            {
                return null;
            }

            return new ContractLinkItem(id, title);
        }

        private static string? ReadContactValue(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                return item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : null;
            }

            return ReadNestedContactValue(item, "contact_attributes")
                ?? ReadNestedContactValue(item, "contact")
                ?? ReadStringProperty(item, "value")
                ?? ReadStringProperty(item, "name");
        }

        private static string? ReadNestedContactValue(JsonElement item, string propertyName)
        {
            return item.TryGetProperty(propertyName, out var nested)
                ? ReadStringProperty(nested, "value") ?? ReadStringProperty(nested, "name")
                : null;
        }

        private static string? ReadStringProperty(JsonElement item, string propertyName)
        {
            return item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty(propertyName, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static long? ReadLongProperty(JsonElement item, string propertyName)
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt64(out var number) => number,
                JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
                _ => null
            };
        }

        private static string? FormatPart(string label, string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : $"{label}: {value}";
        }

        private sealed record ContractLinkItem(long? Id, string Title);
    }
}
