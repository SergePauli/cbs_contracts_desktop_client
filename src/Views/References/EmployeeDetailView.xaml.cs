using CbsContractsDesktopClient.Models.References;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Views.References
{
    public sealed partial class EmployeeDetailView : UserControl
    {
        public static readonly DependencyProperty RowProperty =
            DependencyProperty.Register(
                nameof(Row),
                typeof(ReferenceDataRow),
                typeof(EmployeeDetailView),
                new PropertyMetadata(null, OnRowChanged));

        public EmployeeDetailView()
        {
            InitializeComponent();
            Refresh();
        }

        public ReferenceDataRow? Row
        {
            get => (ReferenceDataRow?)GetValue(RowProperty);
            set => SetValue(RowProperty, value);
        }

        private static void OnRowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((EmployeeDetailView)d).Refresh();
        }

        private void Refresh()
        {
            var row = Row;
            if (row is null || row.IsPlaceholder)
            {
                EmployeeNameTextBlock.Text = "Сотрудник не выбран";
                EmployeeIdTextBlock.Text = string.Empty;
                EmployeeDismissedStatusTextBlock.Text = string.Empty;
                PositionTextBlock.Text = string.Empty;
                ContragentTextBlock.Text = string.Empty;
                ContactsTextBlock.Text = string.Empty;
                return;
            }

            var employeeName = GetText(row, "person.full_name", "name", "head");
            var id = GetText(row, "id");
            var used = TryGetBoolean(row.GetValue("used"));

            EmployeeNameTextBlock.Text = string.IsNullOrWhiteSpace(employeeName)
                ? "Сотрудник"
                : employeeName;
            EmployeeIdTextBlock.Text = string.IsNullOrWhiteSpace(id)
                ? string.Empty
                : $"ID: {id}";
            EmployeeDismissedStatusTextBlock.Text = used == false
                ? "статус: уволен"
                : string.Empty;
            PositionTextBlock.Text = GetText(row, "position.name") ?? string.Empty;
            ContragentTextBlock.Text = GetText(row, "contragent.full_name", "contragent.name") ?? string.Empty;
            ContactsTextBlock.Text = GetText(row, "person.contacts.name", "person.person_contacts.contact.value") ?? string.Empty;
        }

        private static string? GetText(ReferenceDataRow row, params string[] fieldKeys)
        {
            foreach (var fieldKey in fieldKeys)
            {
                var value = row.GetValue(fieldKey)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool? TryGetBoolean(object? value)
        {
            return value switch
            {
                bool booleanValue => booleanValue,
                string text when bool.TryParse(text, out var parsedValue) => parsedValue,
                _ => null
            };
        }
    }
}
