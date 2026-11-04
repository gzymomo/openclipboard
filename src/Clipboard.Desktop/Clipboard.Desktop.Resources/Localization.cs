using System.Windows;

namespace Clipboard.Desktop.Resources;

public class Localization
{
	public static readonly DependencyProperty ResourceProperty = DependencyProperty.RegisterAttached("Resource", typeof(object), typeof(Localization), new PropertyMetadata(null, OnResourceChanged));

	public static object GetResource(DependencyObject obj)
	{
		return obj.GetValue(ResourceProperty);
	}

	public static void SetResource(DependencyObject obj, object value)
	{
		obj.SetValue(ResourceProperty, value);
	}

	private static void OnResourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d.ReadLocalValue(ResourceProperty).GetType().Name == "ResourceReferenceExpression") && d is FrameworkElement frameworkElement)
		{
			frameworkElement.SetResourceReference(ResourceProperty, e.NewValue);
		}
	}
}
