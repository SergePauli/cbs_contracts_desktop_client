using CbsContractsDesktopClient.Services.Table;
using CbsContractsDesktopClient.Models.Table;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CbsContractsDesktopClient.Views.Controls;

internal static class TablePresentationResources
{
    public static TablePresentationContext Capture(bool showCostFraction, CbsTableDensity density = CbsTableDensity.Compact)
    {
        string[] keys = ["ShellPrimaryTextBrush", "ShellSecondaryTextBrush", "ShellTableRowBackgroundBrush",
            "SystemFillColorSuccessBrush", "StageGovernmentForegroundBrush", "StageDeadlineAlertBackgroundBrush",
            "StageDeadlineWarningBackgroundBrush", "StageDeadlineTextBrush", "ShellTableGridLineBrush",
            "ShellTableHeaderBackgroundBrush", "ShellTableHeaderTextBrush"];
        return new(keys.ToDictionary(key => key, key =>
        {
            var color = ((SolidColorBrush)Application.Current.Resources[key]).Color;
            return (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B);
        }), showCostFraction, density switch { CbsTableDensity.Comfortable => 14, CbsTableDensity.Standard => 13, _ => 12 });
    }

    public static Color ToColor(uint argb) => Color.FromArgb((byte)(argb >> 24),
        (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}
