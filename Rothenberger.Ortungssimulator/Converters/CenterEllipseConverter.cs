using System;
using System.Globalization;
using System.Windows.Data;

namespace Rothenberger.Ortungssimulator.Converters
{
    public class CenterEllipseConverter : IValueConverter
    {
        // Verschiebt die Ellipse so, dass sie zentriert auf der Position liegt
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double pos = value is double d ? d : 0;
            double size = parameter != null ? System.Convert.ToDouble(parameter) : 0;
            return pos - size / 2;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}