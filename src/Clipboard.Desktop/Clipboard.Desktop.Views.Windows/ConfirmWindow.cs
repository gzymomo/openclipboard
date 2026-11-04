using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Clipboard.Desktop.Views.Windows;

public partial class ConfirmWindow : Window, IComponentConnector
{
	private static readonly object _instanceLocker = new object();

	private static ConfirmWindow _instance;

	private bool _isProcessed = true;

	private bool _isFirst = true;


	public static ConfirmWindow Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_instanceLocker)
				{
					if (_instance == null)
					{
						_instance = new ConfirmWindow();
					}
				}
			}
			return _instance;
		}
	}

	public ConfirmWindow()
	{
		InitializeComponent();
	}

	public bool? ShowDialog(Window parent, WindowStartupLocation windowStartupLocation = WindowStartupLocation.CenterOwner)
	{
		Clear();
		_instance._isProcessed = false;
		_instance.Owner = parent;
		_instance.WindowStartupLocation = windowStartupLocation;
		if (windowStartupLocation == WindowStartupLocation.Manual)
		{
			_instance.Top = parent.Top + 30.0;
			_instance.Left = (parent.Width - base.Width) / 2.0 + parent.Left;
		}
		return _instance.BaseShowDialog();
	}

	private bool? BaseShowDialog()
	{
		return ShowDialog();
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		_isProcessed = true;
		base.DialogResult = true;
	}

	private void Button_Click_1(object sender, RoutedEventArgs e)
	{
		_isProcessed = true;
		base.DialogResult = false;
	}

	private void Clear()
	{
		if (!_instance._isProcessed && !_instance.DialogResult.HasValue)
		{
			_instance.DialogResult = false;
		}
		lock (_instanceLocker)
		{
			_instance = new ConfirmWindow();
		}
	}



}
