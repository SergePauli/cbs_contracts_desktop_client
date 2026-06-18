using CbsContractsDesktopClient.Models.Table;
using CbsContractsDesktopClient.Shared.Data;
using Pauli.WinUiKit.Controls;
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
        bool includeEmpty,
        string emptyLabel = "Не определен")
    {
        var result = new List<EnumSelectOption>();
        if (includeEmpty)
        {
            result.Add(new EnumSelectOption(null, emptyLabel));
        }

        result.AddRange(options
            .Select(option => new EnumSelectOption(null, option.Label, JsonDataReader.TryGetLong(option.Value)))
            .Where(option => option.Value is not null)
            .OrderBy(option => option.Value));

        return result;
    }

    public static IReadOnlyList<EnumSelectOption> BuildStatusOptions(
        IReadOnlyList<CbsTableFilterOptionDefinition> options,
        IReadOnlySet<long> allowedStatusIds,
        string emptyLabel = "Не определен")
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
        comboBox.VerticalContentAlignment = VerticalAlignment.Center;
        comboBox.MinHeight = 0;
        comboBox.Padding = new Thickness(4, 0, 4, 0);

        ComboBoxItem? selectedItem = null;
        foreach (var option in options)
        {
            var item = new ComboBoxItem
            {
                Tag = option,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinHeight = 0,
                Padding = new Thickness(0),
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

    public static void ConfigureStatusDropdown(
        Dropdown dropdown,
        IReadOnlyList<EnumSelectOption> options,
        long? value,
        Action<EnumSelectOption?>? selectionChanged = null)
    {
        dropdown.DisplayMemberPath = nameof(EnumSelectOption.Label);
        dropdown.TextMemberPath = nameof(EnumSelectOption.Label);
        dropdown.MatchMemberPath = nameof(EnumSelectOption.Label);
        dropdown.IsClearButtonEnabled = false;
        dropdown.SelectedContentBuilder = BuildSelectedStatusDropdownContent;
        dropdown.ItemContentBuilder = BuildStatusDropdownItemContent;
        dropdown.ItemsSource = options;
        dropdown.SelectedItem = options.FirstOrDefault(option => option.Value == value || (value is null && option.Value is null))
            ?? options.FirstOrDefault();
        ApplyStatusDropdownHighlight(dropdown);
        dropdown.SelectionChanged += (_, _) =>
        {
            ApplyStatusDropdownHighlight(dropdown);
            selectionChanged?.Invoke(GetSelectedStatusOption(dropdown));
        };
    }

    public static EnumSelectOption? GetSelectedStatusOption(Dropdown dropdown)
    {
        return dropdown.SelectedItem as EnumSelectOption;
    }

    private static UIElement? BuildSelectedStatusDropdownContent(object? item)
    {
        return item is EnumSelectOption option
            ? BuildStatusBadge(
                option.Label,
                option.Value,
                horizontalAlignment: HorizontalAlignment.Center)
            : null;
    }

    private static UIElement? BuildStatusDropdownItemContent(object item)
    {
        return item is EnumSelectOption option
            ? BuildStatusBadge(
                option.Label,
                option.Value,
                horizontalAlignment: HorizontalAlignment.Stretch)
            : new TextBlock { Text = item.ToString() ?? string.Empty };
    }

    private static void ApplyStatusDropdownHighlight(Dropdown dropdown)
    {
        if (dropdown.SelectedItem is not EnumSelectOption option)
        {
            dropdown.HoverBorderBrush = null;
            dropdown.HighlightBackground = null;
            return;
        }

        var colors = ResolveStatusBadgeColors(option.Value);
        dropdown.HoverBorderBrush = new SolidColorBrush(colors.Background);
        dropdown.HighlightBackground = new SolidColorBrush(Color.FromArgb(112, colors.Background.R, colors.Background.G, colors.Background.B));
    }

    public static Border BuildStatusBadge(
        string statusName,
        long? statusId,
        HorizontalAlignment horizontalAlignment = HorizontalAlignment.Left)
    {
        var colors = ResolveStatusBadgeColors(statusId);
        return new Border
        {
            Margin = new Thickness(0),
            Padding = new Thickness(6, 0, 6, 0),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = horizontalAlignment,
            VerticalAlignment = VerticalAlignment.Center,
            MinHeight = 18,
            MaxWidth = 160,
            Background = new SolidColorBrush(colors.Background),
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(statusName) ? "-" : statusName,
                Foreground = new SolidColorBrush(colors.Foreground),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                LineHeight = 16,
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

