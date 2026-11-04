using System;
using Clipboard.Desktop.Properties;
using Common.Helpers;
using GalaSoft.MvvmLight;

namespace Clipboard.Desktop.ViewModels.Controls;

public class GeneralControlViewModel : ViewModelBase
{
	private string _activedHotkeyString = Settings.Default.ActivedHotkeyString;

	private bool _activedHotkeyVertifyResult = true;

	private bool _activedHotkeyIsChanging;

	private string _quicklyCopyHotkeyString = Settings.Default.QuicklyCopyHotkeyString;

	private bool _quicklyCopyHotkeyVertifyResult = true;

	private bool _quicklyCopyHotkeyIsChanging;

	private int _historyRecord = Settings.Default.HistoryRecord;

	private int _glassOpacity = Settings.Default.GlassOpacity;

	public string ActivedHotkeyString
	{
		get
		{
			return _activedHotkeyString;
		}
		set
		{
			_activedHotkeyString = value;
			Settings.Default.ActivedHotkeyString = value;
			Settings.Default.Save();
			RaisePropertyChanged("ActivedHotkeyString");
		}
	}

	public bool ActivedHotkeyVertifyResult
	{
		get
		{
			return _activedHotkeyVertifyResult;
		}
		set
		{
			_activedHotkeyVertifyResult = value;
			RaisePropertyChanged("ActivedHotkeyVertifyResult");
		}
	}

	public bool ActivedHotkeyIsChanging
	{
		get
		{
			return _activedHotkeyIsChanging;
		}
		set
		{
			_activedHotkeyIsChanging = value;
			RaisePropertyChanged("ActivedHotkeyIsChanging");
		}
	}

	public string QuicklyCopyHotkeyString
	{
		get
		{
			return _quicklyCopyHotkeyString;
		}
		set
		{
			_quicklyCopyHotkeyString = value;
			Settings.Default.QuicklyCopyHotkeyString = value;
			Settings.Default.Save();
			RaisePropertyChanged("QuicklyCopyHotkeyString");
		}
	}

	public bool QuicklyCopyHotkeyVertifyResult
	{
		get
		{
			return _quicklyCopyHotkeyVertifyResult;
		}
		set
		{
			_quicklyCopyHotkeyVertifyResult = value;
			RaisePropertyChanged("QuicklyCopyHotkeyVertifyResult");
		}
	}

	public bool QuicklyCopyHotkeyIsChanging
	{
		get
		{
			return _quicklyCopyHotkeyIsChanging;
		}
		set
		{
			_quicklyCopyHotkeyIsChanging = value;
			RaisePropertyChanged("QuicklyCopyHotkeyIsChanging");
		}
	}

	public int HistoryRecord
	{
		get
		{
			return _historyRecord;
		}
		set
		{
			_historyRecord = value;
			Settings.Default.HistoryRecord = value;
			Settings.Default.Save();
			RaisePropertyChanged("HistoryRecord");
		}
	}

	public int GlassOpacity
	{
		get
		{
			return _glassOpacity;
		}
		set
		{
			int value2 = Math.Max(0, Math.Min(100, value));
			if (_glassOpacity != value2)
			{
				_glassOpacity = value2;
				Settings.Default.GlassOpacity = value2;
				Settings.Default.Save();
				GlassTheme.Apply(value2);
				RaisePropertyChanged("GlassOpacity");
			}
		}
	}
}
