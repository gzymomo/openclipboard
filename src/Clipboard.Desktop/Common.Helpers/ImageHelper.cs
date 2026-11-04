using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Common.Interop;

namespace Common.Helpers;

public static class ImageHelper
{
	public static ImageSource Base642ImageSource(this string base64)
	{
		if (string.IsNullOrWhiteSpace(base64))
		{
			return null;
		}
		byte[] buffer = Convert.FromBase64String(base64);
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.StreamSource = new MemoryStream(buffer);
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}

	public static string Save2Base64(this BitmapSource imageSource)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using Bitmap bitmap = imageSource.ToBitmap();
		bitmap.Save(memoryStream, ImageFormat.Png);
		return memoryStream.Save2Base64();
	}

	public static string Save2Base64(this Bitmap bitmap)
	{
		using MemoryStream memoryStream = new MemoryStream();
		bitmap.Save(memoryStream, ImageFormat.Png);
		return memoryStream.Save2Base64();
	}

	public static string Save2Base64(this MemoryStream memoryStream)
	{
		return Convert.ToBase64String(memoryStream.ToArray());
	}

	public static Bitmap ToBitmap(this BitmapSource bitmapSource)
	{
		Bitmap bitmap = new Bitmap(bitmapSource.PixelWidth, bitmapSource.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		BitmapData bitmapData = bitmap.LockBits(new Rectangle(System.Drawing.Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		bitmapSource.CopyPixels(Int32Rect.Empty, bitmapData.Scan0, bitmapData.Height * bitmapData.Stride, bitmapData.Stride);
		bitmap.UnlockBits(bitmapData);
		return bitmap;
	}

	public static ImageSource ToBitmapSource(this Bitmap bmp)
	{
		try
		{
			IntPtr hbitmap = bmp.GetHbitmap();
			BitmapSource result = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
			InteropMethods.DeleteObject(hbitmap);
			return result;
		}
		catch (Exception)
		{
			return null;
		}
	}
}
