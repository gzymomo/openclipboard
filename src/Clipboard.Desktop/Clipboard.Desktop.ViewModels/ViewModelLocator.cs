using Clipboard.Desktop.Handlers;
using Clipboard.Desktop.ViewModels.Controls;
using Clipboard.Desktop.ViewModels.Pages;
using Common.Handlers;
using Common.SQLitelib;
using GalaSoft.MvvmLight.Ioc;

namespace Clipboard.Desktop.ViewModels;

public class ViewModelLocator
{
	public static SettingHandler SettingHandler => SimpleIoc.Default.GetInstance<SettingHandler>();

	public static LinkHandler LinkHandler => SimpleIoc.Default.GetInstance<LinkHandler>();

	public static AutoRunHandler AutoRunHandler => SimpleIoc.Default.GetInstance<AutoRunHandler>();

	public static MainWindowViewModel MainWindowViewModel => SimpleIoc.Default.GetInstance<MainWindowViewModel>();

	public static ContentPageViewModel ContentPageViewModel => SimpleIoc.Default.GetInstance<ContentPageViewModel>();

	public static ClipboardDataControlViewModel ClipboardDataControlViewModel => SimpleIoc.Default.GetInstance<ClipboardDataControlViewModel>();

	public static CommonViewModel CommonViewModel => SimpleIoc.Default.GetInstance<CommonViewModel>();

	public static GeneralControlViewModel GeneralControlViewModel => SimpleIoc.Default.GetInstance<GeneralControlViewModel>();

	public ViewModelLocator()
	{
		SimpleIoc.Default.Register(() => SettingHandler.Instance);
		SimpleIoc.Default.Register<LinkHandler>();
		SimpleIoc.Default.Register<CommonViewModel>();
		SimpleIoc.Default.Register<MainWindowViewModel>();
		SimpleIoc.Default.Register<ContentPageViewModel>();
		SimpleIoc.Default.Register<GeneralControlViewModel>();
		SimpleIoc.Default.Register<ClipboardDataControlViewModel>();
		SimpleIoc.Default.Register(() => SQLiteDBContext.Instance);
		SimpleIoc.Default.Register(() => ClipboardHandler.Instance);
		SimpleIoc.Default.Register(() => AutoRunHandler.Instance);
	}
}
