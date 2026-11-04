using System;
using System.Globalization;
using System.Windows.Data;
using Common.Helpers;

namespace Clipboard.Desktop.Converters;

public class Base642ImageSourceConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string text && !string.IsNullOrWhiteSpace(text))
		{
			try
			{
				return text.Base642ImageSource();
			}
			catch
			{
				return null;
			}
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
