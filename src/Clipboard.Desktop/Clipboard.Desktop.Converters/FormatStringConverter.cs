using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Clipboard.Desktop.Converters;

public class FormatStringConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values[0] == DependencyProperty.UnsetValue || values[0] == null)
		{
			return string.Empty;
		}
		string format = (string)values[0];
		object[] args = values.Where((object o, int i) => i != 0).ToArray();
		return string.Format(format, args);
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
