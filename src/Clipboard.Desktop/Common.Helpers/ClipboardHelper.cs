using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Common.Converters;
using Common.Data.Clipboard;

namespace Common.Helpers;

public static class ClipboardHelper
{
	public static ImageSource ImageFromClipboardDib(this MemoryStream ms)
	{
		if (ms != null)
		{
			byte[] array = new byte[ms.Length];
			ms.Read(array, 0, array.Length);
			BITMAPINFOHEADER bITMAPINFOHEADER = BinaryStructConverter.FromByteArray<BITMAPINFOHEADER>(array);
			int num = Marshal.SizeOf(typeof(BITMAPFILEHEADER));
			int biSize = bITMAPINFOHEADER.biSize;
			int bfSize = num + bITMAPINFOHEADER.biSize + bITMAPINFOHEADER.biSizeImage;
			BITMAPFILEHEADER obj = default(BITMAPFILEHEADER);
			obj.bfType = BITMAPFILEHEADER.BM;
			obj.bfSize = bfSize;
			obj.bfReserved1 = 0;
			obj.bfReserved2 = 0;
			obj.bfOffBits = num + biSize + bITMAPINFOHEADER.biClrUsed * 4;
			byte[] buffer = BinaryStructConverter.ToByteArray(obj);
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.Write(buffer, 0, num);
			memoryStream.Write(array, 0, array.Length);
			memoryStream.Seek(0L, SeekOrigin.Begin);
			return BitmapFrame.Create(memoryStream);
		}
		return null;
	}

	public static MemoryStream StreamFromClipboardDib(this MemoryStream ms)
	{
		if (ms != null)
		{
			byte[] array = new byte[ms.Length];
			ms.Read(array, 0, array.Length);
			BITMAPINFOHEADER bITMAPINFOHEADER = BinaryStructConverter.FromByteArray<BITMAPINFOHEADER>(array);
			int num = Marshal.SizeOf(typeof(BITMAPFILEHEADER));
			int biSize = bITMAPINFOHEADER.biSize;
			int bfSize = num + bITMAPINFOHEADER.biSize + bITMAPINFOHEADER.biSizeImage;
			BITMAPFILEHEADER obj = default(BITMAPFILEHEADER);
			obj.bfType = BITMAPFILEHEADER.BM;
			obj.bfSize = bfSize;
			obj.bfReserved1 = 0;
			obj.bfReserved2 = 0;
			obj.bfOffBits = num + biSize + bITMAPINFOHEADER.biClrUsed * 4;
			byte[] buffer = BinaryStructConverter.ToByteArray(obj);
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.Write(buffer, 0, num);
			memoryStream.Write(array, 0, array.Length);
			memoryStream.Seek(0L, SeekOrigin.Begin);
			return memoryStream;
		}
		return null;
	}
}
