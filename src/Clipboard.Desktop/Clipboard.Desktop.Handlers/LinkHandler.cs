using System;
using System.Diagnostics;

namespace Clipboard.Desktop.Handlers;

public class LinkHandler
{
	public bool Link(string url)
	{
		try
		{
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
