using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using HandyControl.Controls;

namespace Clipboard.Desktop.Views.Windows;

public partial class SettingWindow : HandyControl.Controls.Window, IComponentConnector
{
	private bool _isClosed = true;

	private bool _isShowed;




	public static SettingWindow Instance { get; private set; } = new SettingWindow();


	private SettingWindow()
	{
		Instance = this;
		InitializeComponent();
		base.Loaded += (s, e) => App.ApplyRoundCorners(this);
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		_isClosed = true;
		base.OnClosing(e);
	}

	public new void Show()
	{
		if (Instance == null || Instance._isClosed)
		{
			if (Instance._isShowed)
			{
				Instance = new SettingWindow();
			}
			Instance.BaseShow();
			Instance._isClosed = false;
			Instance._isShowed = true;
		}
		else
		{
			Instance.Activate();
		}
	}

	private void BaseShow()
	{
		base.Show();
	}

	private void Border_MouseMove(object sender, MouseEventArgs e)
	{
		if (!e.Handled && MouseButtonState.Pressed.Equals(e.LeftButton))
		{
			DragMove();
			e.Handled = true;
		}
		base.OnMouseMove(e);
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}


}
