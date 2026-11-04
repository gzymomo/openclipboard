using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Common.Interop.InteropValues.WindowBlur;

namespace Common.Interop;

/// <summary>
/// 给窗口开启 DWM 背景模糊（毛玻璃），并应用 Windows 11 圆角。
/// 玻璃底色由窗口自身的半透明 Background 控制。
/// </summary>
public class WindowBlur
{
	public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(WindowBlur), new PropertyMetadata(false, OnIsEnabledChanged));

	public static readonly DependencyProperty WindowBlurProperty = DependencyProperty.RegisterAttached("WindowBlur", typeof(WindowBlur), typeof(WindowBlur), new PropertyMetadata(null, OnWindowBlurChanged));

	private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

	private const int DWMWCP_ROUND = 2;

	private Window _window;

	public static void SetIsEnabled(DependencyObject element, bool value)
	{
		element.SetValue(IsEnabledProperty, value);
	}

	public static bool GetIsEnabled(DependencyObject element)
	{
		return (bool)element.GetValue(IsEnabledProperty);
	}

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is Window window)
		{
			if (true.Equals(e.OldValue))
			{
				GetWindowBlur(window)?.Detach();
				window.ClearValue(WindowBlurProperty);
			}
			if (true.Equals(e.NewValue))
			{
				WindowBlur windowBlur = new WindowBlur();
				windowBlur.Attach(window);
				window.SetValue(WindowBlurProperty, windowBlur);
			}
		}
	}

	public static void SetWindowBlur(DependencyObject element, WindowBlur value)
	{
		element.SetValue(WindowBlurProperty, value);
	}

	public static WindowBlur GetWindowBlur(DependencyObject element)
	{
		return (WindowBlur)element.GetValue(WindowBlurProperty);
	}

	private static void OnWindowBlurChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is Window window)
		{
			(e.OldValue as WindowBlur)?.Detach();
			(e.NewValue as WindowBlur)?.Attach(window);
		}
	}

	private void Attach(Window window)
	{
		_window = window;
		if ((HwndSource)PresentationSource.FromVisual(window) == null)
		{
			window.SourceInitialized += OnSourceInitialized;
		}
		else
		{
			AttachCore();
		}
	}

	private void Detach()
	{
		try
		{
			DetachCore();
		}
		finally
		{
			_window = null;
		}
	}

	private void OnSourceInitialized(object sender, EventArgs e)
	{
		((Window)sender).SourceInitialized -= OnSourceInitialized;
		AttachCore();
	}

	private void AttachCore()
	{
		EnableBlur(_window);
	}

	private void DetachCore()
	{
		_window.SourceInitialized += OnSourceInitialized;
	}

	private static void EnableBlur(Window window)
	{
		WindowInteropHelper windowInteropHelper = new WindowInteropHelper(window);
		IntPtr handle = windowInteropHelper.Handle;
		if (handle == IntPtr.Zero)
		{
			return;
		}

		ACCENTPOLICY accentPolicy = new ACCENTPOLICY
		{
			AccentState = ACCENTSTATE.ACCENT_ENABLE_BLURBEHIND
		};

		int num = Marshal.SizeOf(accentPolicy);
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		try
		{
			Marshal.StructureToPtr(accentPolicy, intPtr, fDeleteOld: false);
			WINDOWCOMPOSITIONATTRIBUTEDATA data = new WINDOWCOMPOSITIONATTRIBUTEDATA
			{
				Attribute = WINDOWCOMPOSITIONATTRIBUTE.WCA_ACCENT_POLICY,
				SizeOfData = num,
				Data = intPtr
			};
			InteropMethods.SetWindowCompositionAttribute(handle, ref data);
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}

		// Windows 11：圆角，让窗口更接近玻璃面板
		int cornerPreference = DWMWCP_ROUND;
		InteropMethods.DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));
	}
}
