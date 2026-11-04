using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using Clipboard.Data.Enums;
using Clipboard.Data.Interfaces;
using Clipboard.Desktop.Models;
using Clipboard.Desktop.ViewModels;
using Clipboard.Desktop.ViewModels.Pages;
using Clipboard.Desktop.Views.Windows;
using GalaSoft.MvvmLight.Ioc;
using Newtonsoft.Json;

namespace Clipboard.Desktop.Views.Pages;

public partial class ContentPage : Page, IComponentConnector, IStyleConnector
{
	private bool _isFavorite;

	private bool _threadIsRun;

	private DateTime _lastTextChangedTime = DateTime.Now;

	private static readonly object _textChangedEventThreadLocker = new object();

	private static readonly TimeSpan _textChangedTimeInterval = new TimeSpan(0, 0, 0, 0, 200);

	private Thread _textChangedEventThread;

	private bool _mouseLeaveSearchBorder;











	public ContentPage()
	{
		InitializeComponent();
		SearchTextBox.ContextMenu = null;
	}

	private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
	{
		try
		{
			bool flag = false;
			string value = SearchTextBox?.Text;
			bool flag2 = string.IsNullOrWhiteSpace(value);
			if (flag2 && !_isFavorite)
			{
				flag = true;
			}
			else if (e.Item is IClipboardData clipboardData && clipboardData != null)
			{
				if (flag2)
				{
					flag = clipboardData.IsFavorite;
				}
				else
				{
					flag = clipboardData.DataType switch
					{
						ClipboardDataTypes.Text => clipboardData.Data.Contains(value), 
						ClipboardDataTypes.Image => false, 
						ClipboardDataTypes.File => clipboardData.Data.Contains(value), 
						_ => false, 
					};
					if (_isFavorite)
					{
						flag &= clipboardData.IsFavorite;
					}
				}
			}
			e.Accepted = flag;
		}
		catch (Exception ex)
		{
			ex.ToString();
		}
	}

	private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		_lastTextChangedTime = DateTime.Now;
		if (_threadIsRun)
		{
			return;
		}
		lock (_textChangedEventThreadLocker)
		{
			if (_threadIsRun)
			{
				return;
			}
			_textChangedEventThread?.Abort();
			_threadIsRun = true;
			_textChangedEventThread = new Thread((ThreadStart)delegate
			{
				while (DateTime.Now.Subtract(_lastTextChangedTime) < _textChangedTimeInterval)
				{
					Thread.Sleep(100);
				}
				Application.Current.Dispatcher.Invoke(delegate
				{
					(FindResource("DataTypeListCollectionSource") as CollectionViewSource)?.View.Refresh();
				});
				_threadIsRun = false;
			});
			_textChangedEventThread.Start();
		}
	}

	private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (!e.Handled)
		{
			if (sender is ScrollViewer scrollViewer && scrollViewer != null)
			{
				double offset = scrollViewer.HorizontalOffset - (double)e.Delta;
				scrollViewer.ScrollToHorizontalOffset(offset);
				e.Handled = true;
			}
			GC.Collect();
		}
		base.OnPreviewMouseWheel(e);
	}

	private void History_RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		_isFavorite = false;
		(FindResource("DataTypeListCollectionSource") as CollectionViewSource)?.View.Refresh();
	}

	private void Favorite_RadioButton_Checked(object sender, RoutedEventArgs e)
	{
		_isFavorite = true;
		(FindResource("DataTypeListCollectionSource") as CollectionViewSource)?.View.Refresh();
	}

	private void MenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (ConfirmWindow.Instance.ShowDialog(Application.Current.MainWindow, WindowStartupLocation.Manual) == true)
		{
			SimpleIoc.Default.GetInstance<CommonViewModel>().ClipboardHistoryClear();
		}
	}

	private void ListBox_MenuItem_Click(object sender, RoutedEventArgs e)
	{
		List<ClipboardData> list = new List<ClipboardData>();
		if (!(sender is MenuItem { Tag: var tag }))
		{
			return;
		}
		switch (tag?.ToString())
		{
		case "Copy":
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			foreach (ClipboardData item in (IEnumerable)HistoryListBox.Items)
			{
				if (item.IsSelected)
				{
					switch (item.DataType)
					{
					case ClipboardDataTypes.Text:
						num++;
						break;
					case ClipboardDataTypes.Image:
						num2++;
						break;
					case ClipboardDataTypes.File:
						num3++;
						break;
					}
					list.Add(item);
				}
			}
			if (list.Count <= 0)
			{
				break;
			}
			if (num == 0 && num2 > 0)
			{
				SimpleIoc.Default.GetInstance<CommonViewModel>().SendMessage2PreviousWindow(list[0]);
			}
			else if (num == 0 && num3 > 0)
			{
				List<string> list2 = new List<string>();
				foreach (ClipboardData item2 in list)
				{
					list2.AddRange(item2.GetFormatData as string[]);
				}
				SimpleIoc.Default.GetInstance<CommonViewModel>().SendMessage2PreviousWindow(new ClipboardData
				{
					DataType = ClipboardDataTypes.File,
					Data = JsonConvert.SerializeObject(list2)
				});
			}
			else if (num > 0)
			{
				SimpleIoc.Default.GetInstance<CommonViewModel>().SendMessage2PreviousWindow(new ClipboardData
				{
					DataType = ClipboardDataTypes.Text,
					Data = string.Join("\r\n", list.Select((ClipboardData item) => item.Data).ToArray())
				});
			}
			break;
		}
		case "Favorite":
		{
			foreach (ClipboardData item3 in (IEnumerable)HistoryListBox.Items)
			{
				if (item3.IsSelected)
				{
					item3.IsFavorite = true;
					list.Add(item3);
				}
			}
			CommonViewModel instance2 = SimpleIoc.Default.GetInstance<CommonViewModel>();
			IClipboardData[] data = list.ToArray();
			instance2.ClipboardDataUpdateAsync(data);
			break;
		}
		case "Delete":
		{
			foreach (ClipboardData item4 in (IEnumerable)HistoryListBox.Items)
			{
				if (item4.IsSelected)
				{
					item4.IsDeleted = true;
					list.Add(item4);
				}
			}
			CommonViewModel instance = SimpleIoc.Default.GetInstance<CommonViewModel>();
			IClipboardData[] data = list.ToArray();
			instance.ClipboardDataUpdateAsync(data);
			break;
		}
		}
	}

	private void Menu_Exit_Click(object sender, RoutedEventArgs e)
	{
		MenuToggleButton.IsChecked = false;
		Application.Current.MainWindow.Hide();
	}

	private void Menu_Setting_Click(object sender, RoutedEventArgs e)
	{
		SettingWindow.Instance.Show();
		MenuToggleButton.IsChecked = false;
		Application.Current.MainWindow.Hide();
	}

	private void SearchCheckBox_Checked(object sender, RoutedEventArgs e)
	{
		SearchTextBox.Focus();
		_mouseLeaveSearchBorder = false;
	}

	private void Border_MouseLeave(object sender, MouseEventArgs e)
	{
		_mouseLeaveSearchBorder = true;
	}

	private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
	{
		if (_mouseLeaveSearchBorder && string.IsNullOrWhiteSpace(SimpleIoc.Default.GetInstance<ContentPageViewModel>().SearchFilterText))
		{
			SimpleIoc.Default.GetInstance<ContentPageViewModel>().SearchTextBoxVisible = false;
		}
	}

	private void Border_MouseEnter(object sender, MouseEventArgs e)
	{
		_mouseLeaveSearchBorder = false;
	}



}
