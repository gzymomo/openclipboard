using System;
using System.Globalization;
using System.Windows.Data;

namespace Clipboard.Desktop.Converters;

public class StringArrayToStringConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string[] value2)
		{
			return string.Join("\r\n", value2);
		}
		return string.Empty;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
