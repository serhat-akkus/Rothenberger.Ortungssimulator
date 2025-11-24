using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Rothenberger.Ortungssimulator.ViewModels;

namespace Rothenberger.Ortungssimulator.Converters
{
    public class SinusVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ToneType tone)
            {
                return (tone == ToneType.SinusLinear || tone == ToneType.SinusLogarithmic)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}