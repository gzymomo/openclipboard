using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Clipboard.Data.Attributes;
using Clipboard.Data.Enums;
using Common.Helpers;

namespace Clipboard.Desktop.Views.Controls;

public partial class Text_ContentDisplayControl : UserControl, IComponentConnector
{
	private string _value;

	public static readonly DependencyProperty TextTypeProperty;


	public TextTypes TextType
	{
		get
		{
			return (TextTypes)GetValue(TextTypeProperty);
		}
		set
		{
			SetCurrentValue(TextTypeProperty, value);
		}
	}

	static Text_ContentDisplayControl()
	{
		TextTypeProperty = DependencyProperty.Register("TextType", typeof(TextTypes), typeof(UserControl), new PropertyMetadata(TextTypes.Text));
	}

	public Text_ContentDisplayControl()
	{
		InitializeComponent();
		base.DataContextChanged += Text_ContentDisplayControl_DataContextChanged;
	}

	private void Text_ContentDisplayControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue is string value)
		{
			_value = value;
			ValueVertify();
		}
	}

	private void ValueVertify()
	{
		if (Regex.IsMatch(_value, TextTypes.HttpLink.GetAttributeOfType<StringVertifyRegexAttribute>().Pattern))
		{
			TextType = TextTypes.HttpLink;
		}
		else if (Regex.IsMatch(_value, TextTypes.Color.GetAttributeOfType<StringVertifyRegexAttribute>().Pattern))
		{
			TextType = TextTypes.Color;
			base.Margin = new Thickness(0.0);
			if (!IsLightColor(ColorTranslator.FromHtml(_value)))
			{
				base.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(byte.MaxValue, byte.MaxValue, byte.MaxValue));
			}
		}
		else
		{
			TextType = TextTypes.Text;
		}
	}

	private bool IsLightColor(System.Drawing.Color color)
	{
		double num = 1.0 - (0.299 * (double)(int)color.R + 0.587 * (double)(int)color.G + 0.114 * (double)(int)color.B) / 255.0;
		if (num < 0.35)
		{
			if (color.A >= 200)
			{
				return num < 0.1;
			}
			return true;
		}
		return color.A < 100;
	}


}
