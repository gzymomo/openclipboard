using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using Clipboard.Data.Enums;
using Clipboard.Desktop.Models;

namespace Clipboard.Desktop.Converters;

public class ClipboardDataInfoConverter : IValueConverter
{
	private static string[] units = new string[6] { " B", " KB", " MB", " GB", " TB", " PB" };

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is ClipboardData clipboardData)
		{
			switch (clipboardData.DataType)
			{
			case ClipboardDataTypes.Text:
				return clipboardData.GetFormatData.ToString().Length;
			case ClipboardDataTypes.Image:
				if (clipboardData.GetFormatData is ImageSource imageSource)
				{
					return $"{Math.Round(imageSource.Width)} × {Math.Round(imageSource.Height)}";
				}
				break;
			case ClipboardDataTypes.File:
			{
				if (!(clipboardData.GetFormatData is string[] array))
				{
					break;
				}
				long num = 0L;
				string[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					FileInfo fileInfo = new FileInfo(array2[i]);
					if (fileInfo.Exists)
					{
						num += fileInfo.Length;
					}
				}
				return HumanReadableFilesize(num);
			}
			}
		}
		return string.Empty;
	}

	private static string HumanReadableFilesize(double size)
	{
		double num = 1024.0;
		int num2 = 0;
		while (size >= num)
		{
			size /= num;
			num2++;
		}
		return Math.Round(size, 1) + units[num2];
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
