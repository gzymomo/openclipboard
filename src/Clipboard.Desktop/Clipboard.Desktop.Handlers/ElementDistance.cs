using System.Windows;
using Clipboard.Data.Enums;

namespace Clipboard.Desktop.Handlers;

public class ElementDistance
{
	public static readonly DependencyProperty ElementIndexProperty = DependencyProperty.RegisterAttached("ElementIndex", typeof(int), typeof(ElementDistance), new PropertyMetadata(0, OnElementIndexChanged));

	public static readonly DependencyProperty SegmentsProperty = DependencyProperty.RegisterAttached("Segments", typeof(int), typeof(ElementDistance), new PropertyMetadata(0, OnSegmentsChanged));

	public static readonly DependencyProperty LengthProperty = DependencyProperty.RegisterAttached("Length", typeof(double), typeof(ElementDistance), new PropertyMetadata(0.0, OnLengthChanged));

	public static readonly DependencyProperty TotalLengthProperty = DependencyProperty.RegisterAttached("TotalLength", typeof(double), typeof(ElementDistance), new PropertyMetadata(0.0, OnTotalLengthChanged));

	public static readonly DependencyProperty OffsetModeProperty = DependencyProperty.RegisterAttached("OffsetMode", typeof(OffsetMode), typeof(ElementDistance), new PropertyMetadata(OffsetMode.Left, OnOffsetModeChanged));

	public static void SetElementIndex(DependencyObject element, int value)
	{
		element.SetCurrentValue(ElementIndexProperty, value);
	}

	public static double GetElementIndex(DependencyObject element)
	{
		return (int)element.GetValue(ElementIndexProperty);
	}

	private static void OnElementIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is FrameworkElement element)
		{
			UpdateElementDistance(element);
		}
	}

	public static void SetSegments(DependencyObject element, int value)
	{
		element.SetCurrentValue(SegmentsProperty, value);
	}

	public static int GetSegments(DependencyObject element)
	{
		return (int)element.GetValue(SegmentsProperty);
	}

	private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is FrameworkElement element)
		{
			UpdateElementDistance(element);
		}
	}

	public static void SetLength(DependencyObject element, double value)
	{
		element.SetCurrentValue(LengthProperty, value);
	}

	public static double GetLength(DependencyObject element)
	{
		return (double)element.GetValue(LengthProperty);
	}

	private static void OnLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is FrameworkElement element)
		{
			UpdateElementDistance(element);
		}
	}

	public static void SetTotalLength(DependencyObject element, double value)
	{
		element.SetCurrentValue(TotalLengthProperty, value);
	}

	public static double GetTotalLength(DependencyObject element)
	{
		return (double)element.GetValue(TotalLengthProperty);
	}

	private static void OnTotalLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is FrameworkElement element)
		{
			UpdateElementDistance(element);
		}
	}

	public static void SetOffsetMode(DependencyObject element, OffsetMode value)
	{
		element.SetCurrentValue(OffsetModeProperty, value);
	}

	public static OffsetMode GetOffsetMode(DependencyObject element)
	{
		return (OffsetMode)element.GetValue(OffsetModeProperty);
	}

	private static void OnOffsetModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is FrameworkElement element)
		{
			UpdateElementDistance(element);
		}
	}

	private static void UpdateElementDistance(FrameworkElement element)
	{
		Thickness margin = new Thickness(0.0);
		int segments = GetSegments(element);
		double totalLength = GetTotalLength(element);
		if (segments > 0 && totalLength > 0.0)
		{
			double num = totalLength / (double)segments * GetElementIndex(element);
			switch (GetOffsetMode(element))
			{
			case OffsetMode.Left:
				margin.Left = num - element.ActualWidth / 2.0;
				break;
			case OffsetMode.Top:
				margin.Top = num - element.ActualHeight / 2.0;
				break;
			case OffsetMode.Right:
				margin.Right = num - element.ActualWidth / 2.0;
				break;
			case OffsetMode.Bottom:
				margin.Bottom = num - element.ActualHeight / 2.0;
				break;
			}
		}
		element.Margin = margin;
	}
}
