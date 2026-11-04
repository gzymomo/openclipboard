using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using Clipboard.Desktop.Handlers;
using Clipboard.Desktop.Properties;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace Clipboard.Desktop.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
	private CommonViewModel _cvm;

	private string _lang = string.Empty;

	public RelayCommand LoadedEventCommand { get; private set; }

	public double PrimaryScreenWidth => SystemParameters.PrimaryScreenWidth;

	public MainWindowViewModel(CommonViewModel cvm, SettingHandler settingHandler)
	{
		_cvm = cvm;
		LoadedEventCommand = new Lazy<RelayCommand>(() => new RelayCommand(LoadedEvent)).Value;
		settingHandler.LanguageChangedHandler += SettingWindow_LanguageChangedHandler;
		_lang = Settings.Default.Language;
		if (string.IsNullOrWhiteSpace(_lang))
		{
			settingHandler.Language = CultureInfo.InstalledUICulture.Name;
		}
		else
		{
			settingHandler.Language = _lang;
		}
	}

	private void LoadedEvent()
	{
		_cvm.UpdateForegroundWindowHandle();
	}

	private void SettingWindow_LanguageChangedHandler(string region)
	{
		if (region != _lang)
		{
			Settings.Default.Language = region;
			Settings.Default.Save();
		}
		foreach (ResourceDictionary mergedDictionary in Application.Current.Resources.MergedDictionaries)
		{
			Match match = Regex.Match(mergedDictionary.Source?.OriginalString ?? "", "(?<namespace>[Pp]ack:\\/\\/[Aa]pplication:,,,\\/.+;[Cc]omponent\\/(.*\\/)?Languages\\/Language\\.)(?<regioncode>\\S*-?\\S+)\\.xaml");
			if (match.Groups["regioncode"].Success && !match.Groups["regioncode"].Value.Equals(region))
			{
				mergedDictionary.Source = new Uri(string.Format("{0}{1}.xaml", match.Groups["namespace"], region));
			}
		}
	}
}
