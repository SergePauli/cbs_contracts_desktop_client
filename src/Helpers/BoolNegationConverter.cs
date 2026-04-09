using Microsoft.UI.Xaml.Data;

namespace CbsContractsDesktopClient.Helpers
{
    public class BoolNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool flag)
            {
                return !flag;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool flag)
            {
                return !flag;
            }
            return false;
        }
    }
}