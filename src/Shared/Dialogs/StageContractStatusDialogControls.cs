using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CbsContractsDesktopClient.Shared.Dialogs;

public static class StageContractStatusDialogControls
{
    public static readonly IReadOnlySet<long> StageStatusIds = new HashSet<long> { 2, 4, 5, 6, 7 };

    public static IReadOnlyList<EnumSelectOption> BuildStageStatusOptions(
        IReadOnlyList<CbsTableFilterOptionDefinition> options)
    {
        return BuildStatusOptions(options, StageStatusIds);
    }

    public static IReadOnlyList<EnumSelectOption> BuildStatusOptions(
        IReadOnlyList<CbsTableFilterOptionDefinition> options,
        IReadOnlySet<long> allowedStatusIds,
        string emptyLabel = "Пустой")
    {
        var result = new List<EnumSelectOption>
        {
            new(null, emptyLabel)
        };

        result.AddRange(options
            .Select(option => new EnumSelectOption(null, option.Label, JsonDataReader.TryGetLong(option.Value)))
            .Where(option => option.Value is long id && allowedStatusIds.Contains(id))
            .OrderBy(option => option.Value));

        return result;
    }

    public static void ConfigureStatusCombo(
        ComboBox comboBox,
        IReadOnlyList<EnumSelectOption> options,
        long? value)
    {
        comboBox.Items.Clear();
        comboBox.DisplayMemberPath = string.Empty;
        comboBox.SelectedValuePath = string.Empty;
        comboBox.HorizontalAlignment = HorizontalAlignment.Stretch;

        ComboBoxItem? selectedItem = null;
        foreach (var option in options)
        {
            var item = new ComboBoxItem
            {
                Tag = option,
                Content = BuildStatusBadge(
                    option.Label,
                    option.Value,
                    horizontalAlignment: HorizontalAlignment.Stretch)
            };
            comboBox.Items.Add(item);

            if (option.Value == value || (value is null && option.Value is null))
            {
                selectedItem = item;
            }
        }

        comboBox.SelectedItem = selectedItem ?? comboBox.Items.Cast<object>().FirstOrDefault();
    }

    public static EnumSelectOption? GetSelectedStatusOption(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag as EnumSelectOption;
    }

    public static Border BuildStatusBadge(
        string statusName,
        long? statusId,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left)
    {
        var colors = ResolveStatusBadgeColors(statusId);
        return new Border
        {
            Margin = new Thickness(0, 1, 0, 1),
            Padding = new Thickness(6, 1, 6, 1),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = horizontalAlignment,
            MaxWidth = 160,
            Background = new SolidColorBrush(colors.Background),
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(statusName) ? "-" : statusName,
                Foreground = new SolidColorBrush(colors.Foreground),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            }
        };
    }

    public static string FindStatusLabel(
        IReadOnlyList<CbsTableFilterOptionDefinition> options,
        long? statusId)
    {
        if (statusId is null)
        {
            return string.Empty;
        }

        return options
            .FirstOrDefault(option => JsonDataReader.TryGetLong(option.Value) == statusId)
            ?.Label
            ?? string.Empty;
    }

    public static (Color Background, Color Foreground) ResolveStatusBadgeColors(long? statusId)
    {
        return statusId switch
        {
            4 or 5 => (Color.FromArgb(255, 201, 233, 212), Color.FromArgb(255, 64, 64, 64)),
            6 => (Color.FromArgb(255, 255, 205, 210), Color.FromArgb(255, 64, 64, 64)),
            1 or 2 => (Color.FromArgb(254, 194, 237, 246), Color.FromArgb(255, 64, 64, 64)),
            3 => (Color.FromArgb(254, 246, 227, 194), Color.FromArgb(255, 64, 64, 64)),
            _ => (Color.FromArgb(255, 222, 226, 230), Color.FromArgb(255, 64, 64, 64))
        };
    }
}

