using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CbsContractsDesktopClient.Shared.Dialogs;

public static class StageContractDeadlineDialogOptions
{
    public static IReadOnlyList<EnumSelectOption> DeadlineKindOptions()
    {
        return
        [
            new("calendar_plan", "Календарный план"),
            new("calendar_days", "Календарные дни"),
            new("calendar_prepayment", "Календарные от предоплаты"),
            new("working_days", "Рабочие дни"),
            new("working_prepayment", "Рабочие от предоплаты")
        ];
    }

    public static IReadOnlyList<EnumSelectOption> PaymentDeadlineKindOptions()
    {
        return
        [
            new(null, "Не задан"),
            new("c_plan", "Календарный план"),
            new("c_days", "Календарные дни"),
            new("w_days", "Рабочие дни")
        ];
    }

    public static void ConfigureSelectCombo(
        ComboBox comboBox,
        IReadOnlyList<EnumSelectOption> options,
        string? key)
    {
        comboBox.DisplayMemberPath = nameof(EnumSelectOption.Label);
        comboBox.ItemsSource = options;
        comboBox.SelectedItem = options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault();
        comboBox.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    public static string? GetSelectedKey(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as EnumSelectOption)?.Key;
    }
}
