using System;
using Windows.UI.Xaml.Data;

namespace hblink.Shared.Converters
{
    public class LambdaConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || (double)value == 0) return double.NaN;
            return (double)value / 2;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
