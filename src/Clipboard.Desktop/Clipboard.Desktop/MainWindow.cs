using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using Clipboard.Desktop.Handlers;
using Clipboard.Desktop.Properties;
using Clipboard.Desktop.ViewModels;
using Clipboard.Desktop.ViewModels.Controls;
using Clipboard.Desktop.Views.Windows;
using Common.Handlers;
using GalaSoft.MvvmLight.Ioc;
using HandyControl.Controls;

namespace Clipboard.Desktop;

public partial class MainWindow : System.Windows.Window, IComponentConnector
{
	private const int GWL_EXSTYLE = -20;

	private readonly IntPtr WS_EX_TOOLWINDOW = (IntPtr)128;

	private static readonly object _visibilityChangedLocker = new object();

	private DoubleAnimation _showAnimation = new DoubleAnimation(0.0, 400.0, new Duration(new TimeSpan(0, 0, 0, 0, 266)), FillBehavior.HoldEnd);

	private DoubleAnimation _hideAnimation = new DoubleAnimation(400.0, 0.0, new Duration(new TimeSpan(0, 0, 0, 0, 157)), FillBehavior.HoldEnd);



	public static MainWindow Instance { get; private set; }

	public MainWindow()
	{
		Instance = this;
		InitializeComponent();
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		base.Left = 0.0;
		base.Top = SystemParameters.PrimaryScreenHeight;
		base.Loaded += MainWindow_Loaded;
		base.Closing += MainWindow_Closing;
		base.SizeChanged += MainWindow_SizeChanged;
		base.Topmost = true;
		base.ShowInTaskbar = false;
	}

	private void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		SimpleIoc.Default.GetInstance<CommonViewModel>().SelfWHnd = this.GetHandle();
		ClipboardHandler.Instance.Start();
		if (!string.IsNullOrWhiteSpace(SimpleIoc.Default.GetInstance<GeneralControlViewModel>().ActivedHotkeyString))
		{
			HotKeyParameter hotKeyParameter = HotKeyParameter.ToObject(SimpleIoc.Default.GetInstance<GeneralControlViewModel>().ActivedHotkeyString);
			HotKeyHandler.Instance.Regist(this, hotKeyParameter.Control, hotKeyParameter.Vk, ActiveHotKeyHandler);
		}
		if (Settings.Default.IsFirstRun == "1")
		{
			SimpleIoc.Default.GetInstance<AutoRunHandler>().IsAutoRun = true;
			Settings.Default.IsFirstRun = "0";
			Settings.Default.Save();
		}
		Hide();
	}

	internal void ActiveHotKeyHandler()
	{
		if (base.Visibility == Visibility.Visible)
		{
			Hide();
		}
		else
		{
			Show();
		}
	}

	private void MainWindow_Closing(object sender, CancelEventArgs e)
	{
		ClipboardHandler.Instance.Stop();
	}

	private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		base.Top = SystemParameters.PrimaryScreenHeight - base.ActualHeight;
	}

	private void MenuItem_Click(object sender, RoutedEventArgs e)
	{
		SettingWindow.Instance.Show();
	}

	private void NotifyIconContextContent_Click(object sender, RoutedEventArgs e)
	{
		if (base.Visibility != 0)
		{
			Show();
			Activate();
		}
	}

	private void AdUpdate()
	{
		Activate();
	}

	private void Window_Deactivated(object sender, EventArgs e)
	{
		Hide();
	}



}
