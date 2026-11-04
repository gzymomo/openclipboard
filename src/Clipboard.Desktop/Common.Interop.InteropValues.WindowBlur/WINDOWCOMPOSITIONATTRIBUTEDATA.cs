using System;

namespace Common.Interop.InteropValues.WindowBlur;

internal struct WINDOWCOMPOSITIONATTRIBUTEDATA
{
	public WINDOWCOMPOSITIONATTRIBUTE Attribute;

	public IntPtr Data;

	public int SizeOfData;
}
