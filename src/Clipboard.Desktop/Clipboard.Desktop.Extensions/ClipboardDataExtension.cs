using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Clipboard.Data.Enums;
using Clipboard.Desktop.Models;

namespace Clipboard.Desktop.Extensions;

public static class ClipboardDataExtension
{
	public static void SetClipboardData(this ClipboardData data, dynamic dataObject)
	{
		if (!((dataObject != null) ? true : false))
		{
			return;
		}
		int num = 0;
		while (true)
		{
			try
			{
				if (num++ < 10)
				{
					switch (data.DataType)
					{
					case ClipboardDataTypes.Image:
						System.Windows.Clipboard.SetImage(dataObject);
						break;
					case ClipboardDataTypes.File:
						System.Windows.Clipboard.SetFileDropList(dataObject);
						break;
					case ClipboardDataTypes.Text:
						System.Windows.Clipboard.SetText(dataObject);
						break;
					}
				}
				break;
			}
			catch (Exception ex)
			{
				if (ex is COMException { ErrorCode: -2147221040 })
				{
					Thread.Sleep(10);
					continue;
				}
				break;
			}
		}
	}

	public static void PasteToTargetWindow(IntPtr wHnd)
	{
	}
}
