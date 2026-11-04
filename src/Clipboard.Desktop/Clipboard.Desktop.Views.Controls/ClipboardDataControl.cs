using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Clipboard.Desktop.Models;
using Clipboard.Desktop.ViewModels;
using Clipboard.Desktop.Views.Windows;
using GalaSoft.MvvmLight.Ioc;

namespace Clipboard.Desktop.Views.Controls;

public partial class ClipboardDataControl : UserControl, IComponentConnector
{

	public ClipboardDataControl()
	{
		InitializeComponent();
	}

	private void UserControl_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (base.DataContext != null && base.DataContext is ClipboardData data)
		{
			SimpleIoc.Default.GetInstance<CommonViewModel>().SendMessage2PreviousWindow(data);
			CopySuccessWindow.Instance.Show();
			Application.Current.MainWindow.Hide();
		}
	}

	private void UserControl_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		base.OnMouseWheel(e);
	}


}
