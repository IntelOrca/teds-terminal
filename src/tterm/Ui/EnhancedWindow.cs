using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MahApps.Metro.Controls;
using static tterm.Native.Win32;

namespace tterm.Ui
{
    public class EnhancedWindow : MetroWindow
    {
        private readonly WindowInteropHelper _interopHelper;

        protected bool IsResizing { get; private set; }
        protected IntPtr Hwnd => _interopHelper.Handle;
        protected DpiScale Dpi => VisualTreeHelper.GetDpi(this);

        public Size Size
        {
            get => RenderSize;
            set
            {
                if (value != RenderSize)
                {
                    var dpi = Dpi;
                    int width = (int)(value.Width * dpi.DpiScaleX);
                    int height = (int)(value.Height * dpi.DpiScaleY);
                    uint flags = SWP_NOMOVE | SWP_NOZORDER;
                    SetWindowPos(Hwnd, IntPtr.Zero, 0, 0, width, height, flags);
                }
            }
        }

        public EnhancedWindow()
        {
            _interopHelper = new WindowInteropHelper(this);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource.FromHwnd(Hwnd).AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_SIZING:
                    {
                        var bounds = Marshal.PtrToStructure<RECT>(lParam);
                        var newBounds = CorrectWindowBounds(bounds, wParam.ToInt32());
                        if (!newBounds.Equals(bounds))
                        {
                            IsResizing = true;
                            Marshal.StructureToPtr(newBounds, lParam, fDeleteOld: false);
                        }
                        break;
                    }
                case WM_EXITSIZEMOVE:
                    if (IsResizing)
                    {
                        OnResizeEnded();
                        IsResizing = false;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        private RECT CorrectWindowBounds(RECT bounds, int grabDirection)
        {
            var result = bounds;
            var dpi = Dpi;
            var absoluteSize = new Size(bounds.right - bounds.left, bounds.bottom - bounds.top);
            var scaledSize = new Size(absoluteSize.Width / dpi.DpiScaleX, absoluteSize.Height / dpi.DpiScaleY);
            var newScaledSize = GetPreferedSize(scaledSize);
            if (newScaledSize != scaledSize)
            {
                var newAbsoluteSize = new Size(newScaledSize.Width * dpi.DpiScaleX, newScaledSize.Height * dpi.DpiScaleY);
                result = ChangeSizeInDirection(bounds, grabDirection, newAbsoluteSize);
            }
            return result;
        }

        private static RECT ChangeSizeInDirection(RECT bounds, int direction, Size size)
        {
            switch (direction)
            {
                case WMSZ_LEFT:
                case WMSZ_TOPLEFT:
                case WMSZ_BOTTOMLEFT:
                    bounds.left = bounds.right - (int)size.Width;
                    break;
                case WMSZ_RIGHT:
                case WMSZ_TOPRIGHT:
                case WMSZ_BOTTOMRIGHT:
                    bounds.right = bounds.left + (int)size.Width;
                    break;
            }
            switch (direction)
            {
                case WMSZ_TOP:
                case WMSZ_TOPLEFT:
                case WMSZ_TOPRIGHT:
                    bounds.top = bounds.bottom - (int)size.Height;
                    break;
                case WMSZ_BOTTOM:
                case WMSZ_BOTTOMLEFT:
                case WMSZ_BOTTOMRIGHT:
                    bounds.bottom = bounds.top + (int)size.Height;
                    break;
            }
            return bounds;
        }

        protected virtual Size GetPreferedSize(Size size)
        {
            return size;
        }

        protected virtual void OnResizeEnded()
        {
        }
    }
}
