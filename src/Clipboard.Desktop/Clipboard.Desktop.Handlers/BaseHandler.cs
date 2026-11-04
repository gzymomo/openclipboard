using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Clipboard.Desktop.Handlers;

public abstract class BaseHandler : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
