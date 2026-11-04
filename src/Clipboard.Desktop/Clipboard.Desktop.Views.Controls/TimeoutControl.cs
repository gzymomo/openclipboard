using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Clipboard.Desktop.Views.Controls;

public partial class TimeoutControl : TextBlock, IComponentConnector
{
	private DateTime _dateTime;

	private Thread _updateThread;


	public TimeoutControl()
	{
		InitializeComponent();
		base.Loaded += TimeoutControl_Loaded;
		base.DataContextChanged += TimeoutControl_DataContextChanged;
	}

	private void TimeoutControl_Loaded(object sender, RoutedEventArgs e)
	{
		LoadDate();
	}

	private void LoadDate()
	{
		TimeSpan timeSpan = DateTime.Now.Subtract(_dateTime);
		if (timeSpan.Days > 0)
		{
			base.Text = ((timeSpan.Days == 1) ? TFR("ClipboardDataType_Item_CreateTime_Day_one") : string.Format(TFR("ClipboardDataType_Item_CreateTime_Days"), timeSpan.Days));
		}
		else if (timeSpan.Hours > 0)
		{
			base.Text = ((timeSpan.Hours == 1) ? TFR("ClipboardDataType_Item_CreateTime_Hour_one") : string.Format(TFR("ClipboardDataType_Item_CreateTime_Hours"), timeSpan.Hours));
			new Thread((ThreadStart)delegate
			{
				Thread.Sleep(3600000);
				Application.Current?.Dispatcher.Invoke(delegate
				{
					LoadDate();
				});
			}).Start();
		}
		else if (timeSpan.Minutes > 0)
		{
			base.Text = ((timeSpan.Minutes == 1) ? TFR("ClipboardDataType_Item_CreateTime_Minute_one") : string.Format(TFR("ClipboardDataType_Item_CreateTime_Minutes"), timeSpan.Minutes));
			new Thread((ThreadStart)delegate
			{
				Thread.Sleep(60000);
				Application.Current?.Dispatcher.Invoke(delegate
				{
					LoadDate();
				});
			}).Start();
		}
		else
		{
			if (timeSpan.Seconds < 0)
			{
				return;
			}
			base.Text = ((timeSpan.Seconds == 1) ? TFR("ClipboardDataType_Item_CreateTime_Second_one") : string.Format(TFR("ClipboardDataType_Item_CreateTime_Seconds"), timeSpan.Seconds));
			new Thread((ThreadStart)delegate
			{
				Thread.Sleep(30000);
				Application.Current?.Dispatcher.Invoke(delegate
				{
					LoadDate();
				});
			}).Start();
		}
	}

	private string TFR(string key)
	{
		return Application.Current?.TryFindResource(key)?.ToString() ?? string.Empty;
	}

	private void TimeoutControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue is DateTime dateTime)
		{
			_dateTime = dateTime;
			base.ToolTip = _dateTime.ToString("g");
		}
	}


}
