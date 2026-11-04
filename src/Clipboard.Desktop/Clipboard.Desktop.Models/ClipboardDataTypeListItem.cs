using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Clipboard.Data.Enums;
using Clipboard.Data.Interfaces;

namespace Clipboard.Desktop.Models;

public class ClipboardDataTypeListItem : IClipboardDataListItem, INotifyPropertyChanged
{
	private string _resourceKey = string.Empty;

	private string _resourceValue = string.Empty;

	private string _source = string.Empty;

	public string ResourceKey
	{
		get
		{
			return _resourceKey;
		}
		set
		{
			OnPropertyChanged(ref _resourceKey, value, "ResourceKey");
		}
	}

	public string ResourceValue
	{
		get
		{
			return _resourceValue;
		}
		set
		{
			OnPropertyChanged(ref _resourceValue, value, "ResourceValue");
		}
	}

	public string Source
	{
		get
		{
			return _source;
		}
		set
		{
			OnPropertyChanged(ref _source, value, "Source");
		}
	}

	public ClipboardDataListTypes Type { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public ClipboardDataTypeListItem(IClipboardDataListItem clipboardDataListItem)
	{
		ResourceKey = clipboardDataListItem.ResourceKey;
		ResourceValue = clipboardDataListItem.ResourceValue;
		Source = clipboardDataListItem.Source;
		Type = clipboardDataListItem.Type;
		NotifyPropertyChanged();
	}

	protected void OnPropertyChanged<T>(ref T source, T value, [CallerMemberName] string propertyName = null)
	{
		source = value;
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	public void NotifyPropertyChanged()
	{
		ResourceValue = Application.Current.TryFindResource(ResourceKey) as string;
	}
}
