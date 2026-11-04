using System;
using System.Globalization;
using System.Windows.Data;
using Clipboard.Desktop.Handlers;

namespace Clipboard.Desktop.Converters;

public class HotkeyParameterConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (string.IsNullOrWhiteSpace(value as string))
		{
			return null;
		}
		try
		{
			HotKeyParameter hotKeyParameter = HotKeyParameter.ToObject(value as string);
			string text = string.Empty;
			switch (hotKeyParameter.Control)
			{
			case 2:
				text += "Ctrl + ";
				break;
			case 4:
				text += "Shift + ";
				break;
			case 1:
				text += "Alt + ";
				break;
			case 8:
				text += "Windows + ";
				break;
			}
			return text + hotKeyParameter.Vk;
		}
		catch
		{
			return null;
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value;
	}
}
