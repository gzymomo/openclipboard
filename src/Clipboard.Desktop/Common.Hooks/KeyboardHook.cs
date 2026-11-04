using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Common.Data.Args;
using Common.Interop;
using Common.Interop.InteropValues;

namespace Common.Hooks;

public class KeyboardHook
{
	private static IntPtr HookId = IntPtr.Zero;

	private static readonly HookProc Proc = HookCallback;

	private static int VirtualKey;

	private static readonly IntPtr KeyDownIntPtr = (IntPtr)256;

	private static readonly IntPtr KeyUpIntPtr = (IntPtr)257;

	private static readonly IntPtr SyskeyDownIntPtr = (IntPtr)260;

	private static readonly IntPtr SyskeyUpIntPtr = (IntPtr)261;

	private static int Count;

	public static event EventHandler<KeyboardHookEventArgs> KeyDown;

	public static event EventHandler<KeyboardHookEventArgs> KeyUp;

	public static void Start()
	{
		if (HookId == IntPtr.Zero)
		{
			HookId = SetHook(Proc);
		}
		if (HookId != IntPtr.Zero)
		{
			Count++;
		}
	}

	public static void Stop()
	{
		Count--;
		if (Count < 1)
		{
			InteropMethods.UnhookWindowsHookEx(HookId);
			HookId = IntPtr.Zero;
		}
	}

	private static IntPtr SetHook(HookProc proc)
	{
		using Process process = Process.GetCurrentProcess();
		using ProcessModule processModule = process.MainModule;
		if (processModule != null)
		{
			return InteropMethods.SetWindowsHookEx(13, proc, InteropMethods.GetModuleHandle(processModule.ModuleName), 0u);
		}
		return IntPtr.Zero;
	}

	private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
	{
		if (nCode >= 0)
		{
			if (wParam == KeyDownIntPtr)
			{
				int num = Marshal.ReadInt32(lParam);
				if (VirtualKey != num)
				{
					VirtualKey = num;
					KeyboardHook.KeyDown?.Invoke(null, new KeyboardHookEventArgs(num, isSystemKey: false));
				}
			}
			else if (wParam == SyskeyDownIntPtr)
			{
				int num2 = Marshal.ReadInt32(lParam);
				if (VirtualKey != num2)
				{
					VirtualKey = num2;
					KeyboardHook.KeyDown?.Invoke(null, new KeyboardHookEventArgs(num2, isSystemKey: true));
				}
			}
			else if (wParam == KeyUpIntPtr)
			{
				int virtualKey = Marshal.ReadInt32(lParam);
				VirtualKey = -1;
				KeyboardHook.KeyUp?.Invoke(null, new KeyboardHookEventArgs(virtualKey, isSystemKey: false));
			}
			else if (wParam == SyskeyUpIntPtr)
			{
				int virtualKey2 = Marshal.ReadInt32(lParam);
				VirtualKey = -1;
				KeyboardHook.KeyUp?.Invoke(null, new KeyboardHookEventArgs(virtualKey2, isSystemKey: true));
			}
		}
		return InteropMethods.CallNextHookEx(HookId, nCode, wParam, lParam);
	}
}
