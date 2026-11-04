using System;
using System.Diagnostics;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace Clipboard.Desktop.ViewModels.Controls;

public class ClipboardDataControlViewModel : ViewModelBase
{
	public RelayCommand<string> LinkClickedCommand { get; private set; } = new Lazy<RelayCommand<string>>(() => new RelayCommand<string>(LinkChecked)).Value;


	public RelayCommand<string> FileLinkClickedCommand { get; private set; } = new Lazy<RelayCommand<string>>(() => new RelayCommand<string>(FileLinkChecked)).Value;


	private static void LinkChecked(string path)
	{
	}

	private static void FileLinkChecked(string path)
	{
		Process.Start(new ProcessStartInfo("Explorer.exe")
		{
			Arguments = "/e,/select," + path
		});
	}
}
