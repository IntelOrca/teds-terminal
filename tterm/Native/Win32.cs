using System.Runtime.InteropServices;

namespace tterm.Native
{
    internal class Win32
    {
        public const int WM_SIZING = 0x0214;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
    }
}
