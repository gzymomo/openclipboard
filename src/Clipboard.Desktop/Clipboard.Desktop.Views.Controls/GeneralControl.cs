using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using Clipboard.Desktop.Handlers;
using Clipboard.Desktop.ViewModels;
using Clipboard.Desktop.ViewModels.Controls;
using Clipboard.Desktop.Views.Windows;
using GalaSoft.MvvmLight.Ioc;

namespace Clipboard.Desktop.Views.Controls;

public partial class GeneralControl : UserControl, IComponentConnector
{








	public GeneralControl()
	{
		InitializeComponent();
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		if (ConfirmWindow.Instance.ShowDialog(SettingWindow.Instance) == true)
		{
			SimpleIoc.Default.GetInstance<CommonViewModel>().ClipboardHistoryClear();
		}
	}

	private void ActivedHotkeyLabel_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		SimpleIoc.Default.GetInstance<GeneralControlViewModel>().ActivedHotkeyIsChanging = true;
		ActivedHotkeyTextBox.Focus();
	}

	private void QucklyCopyHotkeyLabel_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		SimpleIoc.Default.GetInstance<GeneralControlViewModel>().QuicklyCopyHotkeyIsChanging = true;
		QucklyCopyHotkeyTextBox.Focus();
	}

	private void ActivedHotkeyPopup_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (HotKeyHandler.Instance.PreviewKeyDownEventHandler(e) != null)
		{
			string hotkeyString = SimpleIoc.Default.GetInstance<GeneralControlViewModel>().ActivedHotkeyString;
			SimpleIoc.Default.GetInstance<GeneralControlViewModel>().ActivedHotkeyVertifyResult = HotKeyHandler.Instance.RegistHotKey(Application.Current.MainWindow, ref hotkeyString, e, MainWindow.Instance.ActiveHotKeyHandler);
			SimpleIoc.Default.GetInstance<GeneralControlViewModel>().ActivedHotkeyString = hotkeyString;
		}
		e.Handled = true;
		base.OnPreviewKeyDown(e);
	}

	private void QucklyCopyHotkeyPopup_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (HotKeyHandler.Instance.PreviewKeyDownEventHandler(e) != null)
		{
			string hotkeyString = SimpleIoc.Default.GetInstance<GeneralControlViewModel>().QuicklyCopyHotkeyString;
			SimpleIoc.Default.GetInstance<GeneralControlViewModel>().QuicklyCopyHotkeyVertifyResult = HotKeyHandler.Instance.RegistHotKey(Application.Current.MainWindow, ref hotkeyString, e, delegate
			{
			});
			SimpleIoc.Default.GetInstance<GeneralControlViewModel>().QuicklyCopyHotkeyString = hotkeyString;
		}
		e.Handled = true;
		base.OnPreviewKeyDown(e);
	}

	private void ActivedHotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
	{
		SimpleIoc.Default.GetInstance<GeneralControlViewModel>().ActivedHotkeyIsChanging = false;
	}

	private void QucklyCopyHotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
	{
		SimpleIoc.Default.GetInstance<GeneralControlViewModel>().QuicklyCopyHotkeyIsChanging = false;
	}



}
