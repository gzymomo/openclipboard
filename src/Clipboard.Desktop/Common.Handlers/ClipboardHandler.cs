using System;
using System.Threading;
using System.Windows.Interop;
using Common.Interop;

namespace Common.Handlers;

public class ClipboardHandler
{
	private HwndSource HWndSource;

	private IntPtr HookId = IntPtr.Zero;

	private int Count;

	public static ClipboardHandler Instance { get; private set; } = new Lazy<ClipboardHandler>(() => new ClipboardHandler()).Value;


	public event Action ContentChanged;

	private ClipboardHandler()
	{
	}

	public void Start()
	{
		if (HookId == IntPtr.Zero)
		{
			HookId = WindowHandler.CreateHandle();
			HWndSource = HwndSource.FromHwnd(HookId);
			if (HWndSource != null)
			{
				HWndSource.AddHook(WinProc);
				InteropMethods.AddClipboardFormatListener(HookId);
			}
		}
		if (HookId != IntPtr.Zero)
		{
			Count++;
		}
	}

	public void Stop()
	{
		if (Count <= 0)
		{
			return;
		}
		Count--;
		if (Count < 1)
		{
			HWndSource?.RemoveHook(WinProc);
			InteropMethods.RemoveClipboardFormatListener(HookId);
			HookId = IntPtr.Zero;
		}
	}

	public void PasteToTargetWinByPtr(IntPtr hWnd)
	{
		InteropMethods.SendMessage(hWnd, 256, 23, 0);
		InteropMethods.SendMessage(hWnd, 256, 134, 0);
		Thread.Sleep(10);
		InteropMethods.SendMessage(hWnd, 256, 23, 1);
		InteropMethods.SendMessage(hWnd, 256, 134, 1);
	}

	private IntPtr WinProc(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
	{
		if (msg == 797)
		{
			this.ContentChanged?.Invoke();
		}
		return IntPtr.Zero;
	}
}
