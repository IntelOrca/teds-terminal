using System;
using System.Runtime.InteropServices;

namespace tterm.Native
{
    /// <summary>
    /// Native API wrapper for Win32.
    /// </summary>
    internal static class Win32
    {
        public const int WM_SIZE = 0x0005;
        public const int WM_SIZING = 0x0214;
        public const int WM_ENTERSIZEMOVE = 0x0231;
        public const int WM_EXITSIZEMOVE = 0x0232;

        public const int WMSZ_LEFT = 1;
        public const int WMSZ_RIGHT = 2;
        public const int WMSZ_TOP = 3;
        public const int WMSZ_TOPLEFT = 4;
        public const int WMSZ_TOPRIGHT = 5;
        public const int WMSZ_BOTTOM = 6;
        public const int WMSZ_BOTTOMLEFT = 7;
        public const int WMSZ_BOTTOMRIGHT = 8;

        public const int SIZE_RESTORED = 0;
        public const int SIZE_MINIMIZED = 1;
        public const int SIZE_MAXIMIZED = 2;
        public const int SIZE_MAXSHOW = 3;
        public const int SIZE_MAXHIDE = 4;

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
