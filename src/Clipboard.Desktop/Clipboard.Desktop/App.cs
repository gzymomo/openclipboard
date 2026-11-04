using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Clipboard.Desktop.Properties;
using Common.Helpers;

namespace Clipboard.Desktop;

public partial class App : Application
{

	public App()
	{
		base.DispatcherUnhandledException += App_DispatcherUnhandledException;
	}

	private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		Debug.WriteLine("Unhandled exception: " + e.Exception);
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
		{
			Application.Current.Shutdown();
			return;
		}
		base.OnStartup(e);

		GlassTheme.Apply(Settings.Default.GlassOpacity);

		if (Application.Current.MainWindow != null)
		{
			Application.Current.MainWindow.Loaded += (s, args) => ApplyRoundCorners(Application.Current.MainWindow);
		}
	}

	public static void ApplyRoundCorners(DependencyObject obj)
	{
		int count = VisualTreeHelper.GetChildrenCount(obj);
		for (int i = 0; i < count; i++)
		{
			var child = VisualTreeHelper.GetChild(obj, i);
			if (child is Border border)
			{
				border.CornerRadius = new CornerRadius(8);
			}
			ApplyRoundCorners(child);
		}
	}


}
