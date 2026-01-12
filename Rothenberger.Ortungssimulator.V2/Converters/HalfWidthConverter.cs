using System;
using System.Globalization;
using Microsoft.Maui.Controls;

namespace Rothenberger.Ortungssimulator.V2.Converters
{
    public sealed class HalfWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double d ? d * 0.6667 : 0d; // 2/3

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is double d ? d / 0.6667 : 0d; // Kehrwert von 2/3 ist 1.5
    }
}