using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace Clipboard.Desktop.Handlers;

public class SettingHandler : BaseHandler
{
	public delegate void LanguageChangedEvent(string region);

	private static SettingHandler _instance;

	private List<LanguageModel> _languageList;

	private string _language = CultureInfo.InstalledUICulture.Name;

	private LanguageModel _currentItem;

	public static SettingHandler Instance
	{
		get
		{
			if (_instance == null)
			{
				if (Application.Current.Resources.Contains("Setting"))
				{
					_instance = (SettingHandler)Application.Current.Resources["Setting"];
				}
				else
				{
					_instance = new SettingHandler();
				}
			}
			return _instance;
		}
	}

	public IEnumerable<LanguageModel> LanguageList
	{
		get
		{
			if (_languageList == null)
			{
				_languageList = new List<LanguageModel>();
				Type typeFromHandle = typeof(LanguageType);
				foreach (object value in Enum.GetValues(typeFromHandle))
				{
					object[] customAttributes = typeFromHandle.GetField(value.ToString()).GetCustomAttributes(typeof(LanguageAttribute), inherit: false);
					if (customAttributes != null && customAttributes.Length != 0)
					{
						LanguageAttribute languageAttribute = (LanguageAttribute)customAttributes[0];
						_languageList.Add(new LanguageModel
						{
							Val = (int)value,
							FieldName = value.ToString(),
							DisplayName = languageAttribute.DisplayName,
							RegionCode = languageAttribute.RegionCode
						});
					}
				}
				_languageList.Sort();
			}
			return _languageList;
		}
	}

	public string Language
	{
		get
		{
			return _language;
		}
		set
		{
			_language = value;
			string region = value.Split('-')[0];
			LanguageModel languageModel = LanguageList.FirstOrDefault((LanguageModel item) => item.RegionCode.Contains(region));
			if (languageModel == null)
			{
				_language = "en-US";
				CurrentItem = _language;
			}
			else if (languageModel != CurrentItem)
			{
				CurrentItem = _language;
			}
			this.LanguageChangedHandler?.Invoke(value);
			OnPropertyChanged("Language");
		}
	}

	public object CurrentItem
	{
		get
		{
			if (_currentItem == null)
			{
				_currentItem = LanguageList.FirstOrDefault((LanguageModel item) => item.RegionCode.Equals(Language));
			}
			return _currentItem;
		}
		set
		{
			string language = value as string;
			if (language != null)
			{
				_currentItem = LanguageList.FirstOrDefault((LanguageModel item) => item.RegionCode.Equals(language));
			}
			else if (value is LanguageModel languageModel)
			{
				Language = languageModel.RegionCode;
				_currentItem = languageModel;
			}
			OnPropertyChanged("CurrentItem");
		}
	}

	public event LanguageChangedEvent LanguageChangedHandler;
}
