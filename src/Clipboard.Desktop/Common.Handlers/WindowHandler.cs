using System;
using System.Windows;
using System.Windows.Interop;
using Common.Interop;

namespace Common.Handlers;

public static class WindowHandler
{
	public static IntPtr CreateHandle()
	{
		return new WindowInteropHelper(new Window()).EnsureHandle();
	}

	public static IntPtr GetHandle(this Window window)
	{
		return new WindowInteropHelper(window).EnsureHandle();
	}

	public static HwndSource GetHwndSource(this Window window)
	{
		return HwndSource.FromHwnd(window.GetHandle());
	}

	public static void SetWindowToForegroundWithAttachThreadInput(Window window)
	{
		uint lpdwProcessId;
		uint thisWindowThreadId = InteropMethods.GetWindowThreadProcessId(new WindowInteropHelper(window).Handle, out lpdwProcessId);
		uint currentForegroundWindowThreadId = InteropMethods.GetWindowThreadProcessId(InteropMethods.GetForegroundWindow(), out lpdwProcessId);
		InteropMethods.AttachThreadInput(in currentForegroundWindowThreadId, in thisWindowThreadId, isAttach: true);
		window.Show();
		window.Activate();
		InteropMethods.AttachThreadInput(in currentForegroundWindowThreadId, in thisWindowThreadId, isAttach: false);
		if (!window.Topmost)
		{
			window.Topmost = true;
			window.Topmost = false;
		}
	}
}
