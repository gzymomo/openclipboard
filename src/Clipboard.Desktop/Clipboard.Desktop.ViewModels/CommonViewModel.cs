using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Clipboard.Data.Enums;
using Clipboard.Data.Interfaces;
using Clipboard.Desktop.Extensions;
using Clipboard.Desktop.Models;
using Clipboard.Desktop.ViewModels.Controls;
using Common.Handlers;
using Common.Helpers;
using Common.Interop;
using Common.SQLitelib;
using Common.SQLitelib.DataModels;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Clipboard.Desktop.ViewModels;

public class CommonViewModel : ViewModelBase
{
	private static readonly object _previousDataLocker = new object();

	private ClipboardHandler _clipboard;

	private IClipboardData _currentClipboardData;

	private SQLiteDBContext _dbContext;

	private IClipboardData _previousData;

	private IntPtr _previousWHnd = IntPtr.Zero;

	private IntPtr _selfWHnd = IntPtr.Zero;

	private Queue<(object, IClipboardData)> ClipboardDataQueue = new Queue<(object, IClipboardData)>();

	private TimeSpan _saveInterval = new TimeSpan(0, 0, 0, 0, 500);

	private DateTime _lastSaveTime = DateTime.Now;

	public ObservableCollection<IClipboardData> ClipboardDataCollection { get; private set; } = new ObservableCollection<IClipboardData>();


	public RelayCommand<IClipboardData> ClipboardDataUpdateCommand { get; private set; }

	public RelayCommand ClipboardHistoryClearCommand { get; private set; }

	public IClipboardData CurrentClipboardData
	{
		get
		{
			return _currentClipboardData;
		}
		set
		{
			_currentClipboardData = value;
			RaisePropertyChanged("CurrentClipboardData");
		}
	}

	public IClipboardData PreviousData
	{
		get
		{
			lock (_previousDataLocker)
			{
				return _previousData;
			}
		}
		set
		{
			lock (_previousDataLocker)
			{
				_previousData = value;
			}
			RaisePropertyChanged("PreviousData");
		}
	}

	public IntPtr ForegroundWHnd
	{
		get
		{
			return _previousWHnd;
		}
		set
		{
			_previousWHnd = value;
			RaisePropertyChanged("ForegroundWHnd");
		}
	}

	public IntPtr SelfWHnd
	{
		get
		{
			return _selfWHnd;
		}
		set
		{
			_selfWHnd = value;
			RaisePropertyChanged("SelfWHnd");
		}
	}

	public CommonViewModel(ClipboardHandler clipboard, SQLiteDBContext dbContext, GeneralControlViewModel gcvm)
	{
		ClipboardDataListener();
		_dbContext = dbContext;
		_clipboard = clipboard;
		_clipboard.ContentChanged += Clipboard_ContentChanged;
		ClipboardDataCollection.CollectionChanged += ClipboardDataCollection_CollectionChanged;
		ClipboardInitializeAsync((HistoryMode)gcvm.HistoryRecord);
		ClipboardDataUpdateCommand = new Lazy<RelayCommand<IClipboardData>>(() => new RelayCommand<IClipboardData>(ClipboardDataUpdateAsync)).Value;
		ClipboardHistoryClearCommand = new Lazy<RelayCommand>(() => new RelayCommand(ClipboardHistoryClear)).Value;
	}

	private void Clipboard_ContentChanged()
	{
		int num = 0;
		Clipboard.Desktop.Models.ClipboardData clipboardData = new Clipboard.Desktop.Models.ClipboardData();
		while (true)
		{
			try
			{
				if (++num > 10)
				{
					break;
				}
				if (System.Windows.Clipboard.ContainsText())
				{
					clipboardData.DataType = ClipboardDataTypes.Text;
					clipboardData.Data = System.Windows.Clipboard.GetText();
				}
				else if (System.Windows.Clipboard.ContainsImage())
				{
					clipboardData.DataType = ClipboardDataTypes.Image;
					using MemoryStream ms = System.Windows.Clipboard.GetData("DeviceIndependentBitmap") as MemoryStream;
					clipboardData.Data = ms.StreamFromClipboardDib().Save2Base64();
				}
				else if (System.Windows.Clipboard.ContainsFileDropList())
				{
					clipboardData.DataType = ClipboardDataTypes.File;
					clipboardData.Data = JsonConvert.SerializeObject(System.Windows.Clipboard.GetFileDropList());
				}
				if (clipboardData.DataType != 0)
				{
					ClipboardDataCaptured(clipboardData);
				}
				break;
			}
			catch (Exception ex)
			{
				if (ex is COMException { ErrorCode: -2147221040 })
				{
					Thread.Sleep(10);
					continue;
				}
				break;
			}
		}
	}

	private void ClipboardDataAdd2Collection(IClipboardData data)
	{
		lock (ClipboardDataQueue)
		{
			object obj = new object();
			lock (obj)
			{
				PreviousData = data;
				ClipboardDataQueue.Enqueue((obj, data));
			}
		}
	}

	private void ClipboardDataCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		RaisePropertyChanged("ClipboardDataCollection");
	}

	private void ClipboardDataListener()
	{
		new Task(delegate
		{
			while (true)
			{
				if (ClipboardDataQueue.Count > 0)
				{
					lock (ClipboardDataQueue.Peek().Item1)
					{
						Application.Current.Dispatcher.Invoke(delegate
						{
							ClipboardDataCollection.Insert(0, ClipboardDataQueue.Dequeue().Item2);
							RaisePropertyChanged("ClipboardDataCollection");
						});
					}
				}
				else
				{
					Thread.Sleep(200);
				}
			}
		}).Start();
	}

	private async void ClipboardInitializeAsync(HistoryMode historyMode)
	{
		await Task.Run(delegate
		{
			DateTime now = DateTime.Now;
			DateTime dateTime = new DateTime(now.Year, now.Month, now.Day);
			Common.SQLitelib.DataModels.ClipboardData[] array = null;
			switch (historyMode)
			{
			case HistoryMode._7:
				dateTime = dateTime.AddDays(-7.0);
				array = _dbContext.ClipboardData.Where((Common.SQLitelib.DataModels.ClipboardData item) => item.CreateTime >= dateTime).ToArray();
				break;
			case HistoryMode._30:
				dateTime = dateTime.AddDays(-30.0);
				array = _dbContext.ClipboardData.Where((Common.SQLitelib.DataModels.ClipboardData item) => item.CreateTime >= dateTime).ToArray();
				break;
			case HistoryMode.Forever:
				array = _dbContext.ClipboardData.ToArray();
				break;
			}
			if (((array != null) ? array.Length : (-1)) > 0)
			{
				Common.SQLitelib.DataModels.ClipboardData[] array2 = array;
				foreach (Common.SQLitelib.DataModels.ClipboardData data in array2)
				{
					ClipboardDataAdd2Collection(new Clipboard.Desktop.Models.ClipboardData(data));
				}
			}
			Clipboard_ContentChanged();
		});
	}

	private void DBDeleteMethod(IClipboardData data, Common.SQLitelib.DataModels.ClipboardData dbData = null, bool autoSave = true)
	{
		if (dbData == null)
		{
			dbData = _dbContext.ClipboardData.FirstOrDefault((Common.SQLitelib.DataModels.ClipboardData item) => item.ID.Equals(data.ID));
		}
		Application.Current.Dispatcher.Invoke(delegate
		{
			ClipboardDataCollection.Remove(data);
			RaisePropertyChanged("ClipboardDataCollection");
		});
		_dbContext.ClipboardData.Remove(dbData);
		if (autoSave)
		{
			_dbContext.SaveChanges();
			_dbContext.Entry(dbData).State = EntityState.Detached;
		}
	}

	public void ClipboardDataCaptured(IClipboardData data)
	{
		if (PreviousData == null || !PreviousData.DataType.Equals(data.DataType) || !PreviousData.Data.Equals(data.Data))
		{
			ClipboardDataAdd2Collection(data);
			Common.SQLitelib.DataModels.ClipboardData entity = new Common.SQLitelib.DataModels.ClipboardData(data);
			_dbContext.ClipboardData.Add(entity);
			if (DateTime.Now.Subtract(_lastSaveTime) > _saveInterval)
			{
				_dbContext.SaveChangesAsync();
				_dbContext.Entry(entity).State = EntityState.Detached;
				_lastSaveTime = DateTime.Now;
			}
		}
	}

	public async void ClipboardDataUpdateAsync(IClipboardData data)
	{
		if (data == null)
		{
			return;
		}
		await Task.Run(delegate
		{
			lock (_previousDataLocker)
			{
				Common.SQLitelib.DataModels.ClipboardData clipboardData = new Common.SQLitelib.DataModels.ClipboardData(data);
				if (data.IsDeleted)
				{
					DBDeleteMethod(data, clipboardData, autoSave: false);
				}
				else
				{
					_dbContext.Update(clipboardData);
				}
				_dbContext.SaveChanges();
				_dbContext.Entry(clipboardData).State = EntityState.Detached;
			}
		});
	}

	public async void ClipboardDataUpdateAsync(params IClipboardData[] data)
	{
		if (data == null)
		{
			return;
		}
		_dbContext.SaveChanges();
		await Task.Run(delegate
		{
			lock (_previousDataLocker)
			{
				IClipboardData[] array = data;
				foreach (IClipboardData clipboardData in array)
				{
					Common.SQLitelib.DataModels.ClipboardData clipboardData2 = new Common.SQLitelib.DataModels.ClipboardData(clipboardData);
					if (clipboardData.IsDeleted)
					{
						DBDeleteMethod(clipboardData, clipboardData2, autoSave: false);
					}
					else
					{
						_dbContext.Update(clipboardData2);
					}
					_dbContext.SaveChanges();
					_dbContext.Entry(clipboardData2).State = EntityState.Detached;
				}
			}
		});
	}

	public void SendMessage2PreviousWindow(Clipboard.Desktop.Models.ClipboardData data)
	{
		PreviousData = data;
		if (data.DataType == ClipboardDataTypes.Image)
		{
			data.SetClipboardData(data.GetFormatData);
		}
		else
		{
			data.SetClipboardData(data.Data);
		}
		InteropMethods.SetForegroundWindow(ForegroundWHnd);
		uint lpdwProcessId;
		uint thisWindowThreadId = InteropMethods.GetWindowThreadProcessId(SelfWHnd, out lpdwProcessId);
		uint currentForegroundWindowThreadId = InteropMethods.GetWindowThreadProcessId(ForegroundWHnd, out lpdwProcessId);
		InteropMethods.AttachThreadInput(in currentForegroundWindowThreadId, in thisWindowThreadId, isAttach: true);
		IntPtr focus = InteropMethods.GetFocus();
		InteropMethods.AttachThreadInput(in currentForegroundWindowThreadId, in thisWindowThreadId, isAttach: false);
		ClipboardHandler.Instance.PasteToTargetWinByPtr(focus);
	}

	public void ClipboardHistoryClear()
	{
		ClipboardDataCollection.Clear();
		RaisePropertyChanged("ClipboardDataCollection");
		new Task(delegate
		{
			_dbContext.ClearAllClipboardData();
		}).Start();
	}

	public void UpdateForegroundWindowHandle()
	{
		ForegroundWHnd = InteropMethods.GetForegroundWindow();
	}
}
