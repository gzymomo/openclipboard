using System;
using System.Runtime.InteropServices;
using Common.Interop.InteropValues;
using Common.Interop.InteropValues.WindowBlur;

namespace Common.Interop;

internal class InteropMethods
{
	internal static class ExternDll
	{
		public const string Activeds = "activeds.dll";

		public const string Fxassert = "Fxassert.dll";

		public const string Vsassert = "vsassert.dll";

		public const string Version = "version.dll";

		public const string Wtsapi32 = "wtsapi32.dll";

		public const string Winspool = "winspool.drv";

		public const string WinMM = "winmm.dll";

		public const string Uxtheme = "uxtheme.dll";

		public const string User32 = "user32.dll";

		public const string Shell32 = "shell32.dll";

		public const string Psapi = "psapi.dll";

		public const string Powrprof = "Powrprof.dll";

		public const string PerfCounter = "perfcounter.dll";

		public const string Olepro32 = "olepro32.dll";

		public const string Oleaut32 = "oleaut32.dll";

		public const string Shlwapi = "shlwapi.dll";

		public const string Oleacc = "oleacc.dll";

		public const string Ntdll = "ntdll.dll";

		public const string Mqrt = "mqrt.dll";

		public const string Msi = "msi.dll";

		public const string Clr = "clr.dll";

		public const string Mscoree = "mscoree.dll";

		public const string Loadperf = "Loadperf.dll";

		public const string Kernel32 = "kernel32.dll";

		public const string Imm32 = "imm32.dll";

		public const string Hhctrl = "hhctrl.ocx";

		public const string Gdiplus = "gdiplus.dll";

		public const string Gdi32 = "gdi32.dll";

		public const string Comdlg32 = "comdlg32.dll";

		public const string Comctl32 = "comctl32.dll";

		public const string Advapi32 = "advapi32.dll";

		public const string Ole32 = "ole32.dll";

		public const string Crypt32 = "crypt32.dll";
	}

	[DllImport("kernel32.dll")]
	internal static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBUTEDATA data);

	[DllImport("dwmapi.dll")]
	internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

	[StructLayout(LayoutKind.Sequential)]
	internal struct MARGINS
	{
		public int Left;
		public int Right;
		public int Top;
		public int Bottom;
	}

	[DllImport("dwmapi.dll")]
	internal static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	internal static extern bool GetCursorPos(out POINT pt);

	[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, int flags);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool AddClipboardFormatListener(IntPtr hwnd);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	internal static extern int SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
	internal static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr GetActiveWindow();

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr SetActiveWindow(IntPtr hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr SetFocus(IntPtr hWnd);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr GetFocus();

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern bool AttachThreadInput(in uint currentForegroundWindowThreadId, in uint thisWindowThreadId, bool isAttach);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

	[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern IntPtr GetModuleHandle(string lpModuleName);

	internal static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
	{
		if (IntPtr.Size == 4)
		{
			return SetWindowLongPtr32(hWnd, nIndex, dwNewLong);
		}
		return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
	}

	[DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLong")]
	internal static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "SetWindowLongPtr")]
	internal static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

	[DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	internal static extern bool DeleteObject(IntPtr hObject);
}
