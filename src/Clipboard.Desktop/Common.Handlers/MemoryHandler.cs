using System;
using System.Diagnostics;
using Common.Interop;

namespace Common.Handlers;

public class MemoryHandler
{
	public static void FlushMemory()
	{
		GC.Collect();
		GC.WaitForPendingFinalizers();
		if (Environment.OSVersion.Platform == PlatformID.Win32NT)
		{
			InteropMethods.SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
		}
	}
}
