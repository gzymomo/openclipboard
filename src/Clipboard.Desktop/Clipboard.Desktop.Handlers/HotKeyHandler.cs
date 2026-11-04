using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;

namespace Clipboard.Desktop.Handlers;

public class HotKeyHandler
{
	public enum HotkeyModifiers
	{
		None = 0,
		Alt = 1,
		Control = 2,
		Shift = 4,
		Win = 8
	}

	private Dictionary<int, Action> keymap = new Dictionary<int, Action>();

	public static HotKeyHandler Instance { get; set; } = new HotKeyHandler();


	private List<HwndSource> _sourceList { get; set; } = new List<HwndSource>();


	public System.Windows.Input.KeyEventArgs PreviewKeyDownEventHandler(System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == Key.Return || e.Key == Key.Delete || e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || e.SystemKey == Key.LeftAlt || e.SystemKey == Key.RightAlt || e.SystemKey == Key.LeftShift || e.SystemKey == Key.RightShift || e.SystemKey == Key.LWin || e.SystemKey == Key.RWin)
		{
			return null;
		}
		if (Regex.IsMatch(e.Key.ToString(), "^([A-Z]|F[0-9]{1,2})$"))
		{
			return e;
		}
		return null;
	}

	public bool RegistHotKey(Window window, ref string hotkeyString, System.Windows.Input.KeyEventArgs e, Action callBack)
	{
		_ = string.Empty;
		Instance.UnRegistAll(window);
		HotKeyParameter hotKeyParameter = (string.IsNullOrEmpty(hotkeyString) ? new HotKeyParameter() : HotKeyParameter.ToObject(hotkeyString));
		hotKeyParameter.Control = (int)e.KeyboardDevice.Modifiers;
		hotKeyParameter.Vk = (Keys)KeyInterop.VirtualKeyFromKey(e.SystemKey);
		if (hotKeyParameter.Vk == Keys.None)
		{
			hotKeyParameter.Vk = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);
		}
		if (hotKeyParameter.Vk != 0 && hotkeyString != hotKeyParameter.ToString())
		{
			hotkeyString = hotKeyParameter.ToString();
			return Instance.Regist(window, (int)e.KeyboardDevice.Modifiers, hotKeyParameter.Vk, callBack);
		}
		return false;
	}

	[DllImport("user32.dll")]
	private static extern bool RegisterHotKey(IntPtr hWnd, int id, int modifiers, Keys vk);

	[DllImport("user32.dll")]
	private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

	public bool Regist(Window window, int modifiers, Keys vk, Action callBack)
	{
		IntPtr hWnd = new WindowInteropHelper(window).EnsureHandle();
		int num = modifiers + (int)vk * 10;
		if (!RegisterHotKey(hWnd, num, modifiers, vk))
		{
			return false;
		}
		keymap[num] = callBack;
		HwndSource hwndSource = _sourceList.FirstOrDefault((HwndSource item) => item.Handle == hWnd);
		if (hwndSource == null)
		{
			hwndSource = PresentationSource.FromVisual(window) as HwndSource;
			hwndSource.AddHook(WndProc);
			_sourceList.Add(hwndSource);
		}
		else
		{
			hwndSource.AddHook(WndProc);
		}
		return true;
	}

	public void UnRegistByKey(IntPtr hWnd, int keyid)
	{
		if (keymap.ContainsKey(keyid))
		{
			UnregisterHotKey(hWnd, keyid);
			keymap.Remove(keyid * 10);
		}
	}

	public void UnRegistAll(IntPtr hWnd)
	{
		foreach (KeyValuePair<int, Action> item in keymap)
		{
			UnregisterHotKey(hWnd, item.Key);
		}
	}

	public void UnRegistAll(Window window)
	{
		IntPtr handle = new WindowInteropHelper(window).Handle;
		foreach (KeyValuePair<int, Action> item in keymap)
		{
			UnregisterHotKey(handle, item.Key);
		}
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handle)
	{
		try
		{
			int key = wParam.ToInt32();
			if (keymap.ContainsKey(key) && keymap.TryGetValue(key, out var value))
			{
				value?.Invoke();
			}
			return IntPtr.Zero;
		}
		catch
		{
			return IntPtr.Zero;
		}
	}
}
