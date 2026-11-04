using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Common.Handlers;

public class AutoRunHandler : INotifyPropertyChanged
{
	private Action<bool> _autoRunStatusChangedEventHandler;

	private Action _statusUpdateEventHandler;

	private string _applicationName = "OpenClipboard";

	private bool _isAutoRun;

	private string startupPath = string.Empty;

	private static AutoRunHandler _instance;

	public bool IsAutoRun
	{
		get
		{
			return _isAutoRun;
		}
		set
		{
			_autoRunStatusChangedEventHandler?.Invoke(value);
			OnPropertyChagned(ref _isAutoRun, value, "IsAutoRun");
		}
	}

	public static AutoRunHandler Instance
	{
		get
		{
			if (_instance == null)
			{
				if (Application.Current.Resources.Contains("AutoRun"))
				{
					_instance = (AutoRunHandler)Application.Current.Resources["AutoRun"];
				}
				else
				{
					_instance = new AutoRunHandler();
				}
			}
			return _instance;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void StatusUpdate()
	{
		_statusUpdateEventHandler?.Invoke();
		OnPropertyChagned("IsAutoRun");
	}

	public AutoRunHandler()
	{
		startupPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), _applicationName + ".lnk");
		_autoRunStatusChangedEventHandler = AutoRunStatusChangedEventHandler;
		_statusUpdateEventHandler = delegate
		{
			_isAutoRun = File.Exists(startupPath);
		};
	}

	private void AutoRunStatusChangedEventHandler(bool status)
	{
		if (status)
		{
			if (!File.Exists(startupPath))
			{
				dynamic val = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
				dynamic val2 = val.CreateShortcut(startupPath);
				val2.TargetPath = Assembly.GetEntryAssembly().Location;
				val2.WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
				val2.Save();
			}
		}
		else if (File.Exists(startupPath))
		{
			File.Delete(startupPath);
		}
	}

	internal void OnPropertyChagned([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	internal void OnPropertyChagned<T>(ref T source, T value, [CallerMemberName] string propertyName = null)
	{
		source = value;
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
