using System.Runtime.InteropServices;

namespace Common.Data.Clipboard;

[StructLayout(LayoutKind.Sequential, Pack = 2)]
internal struct BITMAPFILEHEADER
{
	public static readonly short BM = 19778;

	public short bfType;

	public int bfSize;

	public short bfReserved1;

	public short bfReserved2;

	public int bfOffBits;
}
