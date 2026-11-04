using System;
using System.Globalization;
using System.Windows.Data;

namespace Clipboard.Desktop.Converters;

public class GetCollectionLengthConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is object[] array)
		{
			return array.Length.ToString();
		}
		if (value is ListCollectionView listCollectionView)
		{
			return listCollectionView?.Count ?? 0;
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
