using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace CbsContractsDesktopClient.Helpers
{
    public class StringVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}