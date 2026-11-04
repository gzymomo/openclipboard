using System;
using System.Windows;
using System.Windows.Media;

namespace Common.Helpers;

/// <summary>
/// 维护全局毛玻璃底色 <c>GlassTintBrush</c>，供窗口根容器通过 DynamicResource 引用。
/// 透明度为 0 时去掉底色和高光，只保留 DWM 模糊（真正的“无着色磨砂玻璃”）。
/// </summary>
public static class GlassTheme
{
	public const int DefaultOpacity = 60;

	public const string BrushResourceKey = "GlassTintBrush";

	public const string HighlightOpacityKey = "GlassHighlightOpacity";

	public static void Apply(int opacityPercent)
	{
		int value = Math.Max(0, Math.Min(100, opacityPercent));
		byte alpha = (byte)Math.Round(255.0 * value / 100.0);
		bool hasTint = value > 0;

		Application application = Application.Current;
		if (application == null)
		{
			return;
		}

		application.Resources[BrushResourceKey] = new SolidColorBrush(Color.FromArgb(alpha, 0xF7, 0xF7, 0xFA));
		application.Resources[HighlightOpacityKey] = hasTint ? 1.0 : 0.0;
	}
}
