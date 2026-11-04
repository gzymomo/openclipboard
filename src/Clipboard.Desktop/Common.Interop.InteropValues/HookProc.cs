using System;

namespace Common.Interop.InteropValues;

internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
