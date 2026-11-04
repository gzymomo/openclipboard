using System;
using System.Globalization;
using System.Windows.Data;

namespace Clipboard.Desktop.Converters;

public class OppositeConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return 0.0 - (double)value + (double)int.Parse(parameter.ToString());
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return 0.0 - (double)value + (double)parameter;
	}
}
