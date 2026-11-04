using System;

namespace Common.Interop.InteropValues;

internal delegate IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
