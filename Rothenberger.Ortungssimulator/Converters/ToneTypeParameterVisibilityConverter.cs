using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Rothenberger.Ortungssimulator.Converters
{
    public class ToneTypeParameterVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return Visibility.Collapsed;
            var toneType = values[0]?.ToString();
            var paramName = parameter?.ToString();

            // Sinus-Typen: Sinus, SinusLinear, SinusLogarithmic
            if ((toneType == "SinusLinear" || toneType == "SinusLogarithmic" || toneType == "SinusExponential") &&
                (paramName == "SinusFrequency" || paramName == "SinusAmplitude"))
                return Visibility.Visible;

            // Shepard-Typ
            if (toneType == "Shepard" && (paramName?.StartsWith("Shepard") ?? false))
                return Visibility.Visible;

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}