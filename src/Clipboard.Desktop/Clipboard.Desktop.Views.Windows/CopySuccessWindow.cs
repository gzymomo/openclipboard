using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Clipboard.Desktop.Views.Windows;

public partial class CopySuccessWindow : Window, IComponentConnector
{
	private static readonly object _instanceLocker = new object();

	private static CopySuccessWindow _instance;

	private readonly object _processLocker = new object();

	private bool _isProcessed = true;

	private Thread _runThead;



	public static CopySuccessWindow Instance
	{
		get
		{
			if (_instance == null)
			{
				lock (_instanceLocker)
				{
					if (_instance == null)
					{
						_instance = new CopySuccessWindow();
					}
				}
			}
			return _instance;
		}
	}

	private CopySuccessWindow()
	{
		InitializeComponent();
		base.Top = SystemParameters.PrimaryScreenHeight / 2.0 - 100.0;
		base.Left = SystemParameters.PrimaryScreenWidth / 2.0 - 50.0;
		base.Loaded += CopySuccessWindow_Loaded;
		_runThead = new Thread((ThreadStart)delegate
		{
			DateTime dateTime = DateTime.Now.AddSeconds(3.0);
			while (DateTime.Now < dateTime)
			{
				Thread.Sleep(1000);
			}
			if (!_isProcessed)
			{
				lock (_processLocker)
				{
					if (!_isProcessed)
					{
						_isProcessed = true;
						Application.Current.Dispatcher.Invoke(delegate
						{
							Close();
						});
					}
				}
			}
		});
	}

	private void CopySuccessWindow_Loaded(object sender, RoutedEventArgs e)
	{
		base.Width = TipTextBlock.ActualWidth + 100.0;
		base.Left = SystemParameters.PrimaryScreenWidth / 2.0 - base.Width / 2.0;
	}

	public new void Show()
	{
		Clear();
		lock (_instance._processLocker)
		{
			_instance._isProcessed = false;
		}
		_instance.BaseShow();
	}

	private void BaseShow()
	{
		base.Show();
		_runThead.Start();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);
	}

	private void Clear()
	{
		if (!_instance._isProcessed)
		{
			lock (_instance._processLocker)
			{
				if (!_instance._isProcessed)
				{
					_instance._runThead.Abort();
					_instance._isProcessed = true;
					_instance.Close();
				}
			}
		}
		lock (_instanceLocker)
		{
			_instance = new CopySuccessWindow();
		}
	}


}
