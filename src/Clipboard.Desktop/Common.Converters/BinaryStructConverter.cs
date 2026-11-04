using System;
using System.Runtime.InteropServices;

namespace Common.Converters;

public static class BinaryStructConverter
{
	public static T FromByteArray<T>(byte[] bytes) where T : struct
	{
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			int num = Marshal.SizeOf(typeof(T));
			intPtr = Marshal.AllocHGlobal(num);
			Marshal.Copy(bytes, 0, intPtr, num);
			return (T)Marshal.PtrToStructure(intPtr, typeof(T));
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
			}
		}
	}

	public static byte[] ToByteArray<T>(T obj) where T : struct
	{
		IntPtr intPtr = IntPtr.Zero;
		try
		{
			int num = Marshal.SizeOf(typeof(T));
			intPtr = Marshal.AllocHGlobal(num);
			Marshal.StructureToPtr(obj, intPtr, fDeleteOld: true);
			byte[] array = new byte[num];
			Marshal.Copy(intPtr, array, 0, num);
			return array;
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(intPtr);
			}
		}
	}
}
