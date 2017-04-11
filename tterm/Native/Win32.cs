using System;
using System.Runtime.InteropServices;

namespace tterm.Native
{
    /// <summary>
    /// Native API wrapper for Win32.
    /// </summary>
    internal static class Win32
    {
        public const int WM_SIZING = 0x0214;
        public const int WM_ENTERSIZEMOVE = 0x0231;
        public const int WM_EXITSIZEMOVE = 0x0232;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr LoadLibrary(string path);
    }
}
