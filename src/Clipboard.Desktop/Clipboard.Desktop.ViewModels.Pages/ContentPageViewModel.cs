using System;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace Clipboard.Desktop.ViewModels.Pages;

public class ContentPageViewModel : ViewModelBase
{
	private bool _searchTextBoxVisible;

	private string _searchFilterText = string.Empty;

	public bool SearchTextBoxVisible
	{
		get
		{
			return _searchTextBoxVisible;
		}
		set
		{
			_searchTextBoxVisible = value;
			RaisePropertyChanged("SearchTextBoxVisible");
		}
	}

	public string SearchFilterText
	{
		get
		{
			return _searchFilterText;
		}
		set
		{
			_searchFilterText = value;
			RaisePropertyChanged("SearchFilterText");
		}
	}

	public RelayCommand ClearSearchButtonClickedCommand { get; private set; }

	public ContentPageViewModel()
	{
		ClearSearchButtonClickedCommand = new Lazy<RelayCommand>(() => new RelayCommand(ClearSearchButtonClicked)).Value;
	}

	private void ClearSearchButtonClicked()
	{
		SearchFilterText = string.Empty;
	}
}
