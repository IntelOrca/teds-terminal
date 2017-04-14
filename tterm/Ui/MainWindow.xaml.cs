using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using tterm.Terminal;
using tterm.Ui.Models;
using static tterm.Native.Win32;

namespace tterm.Ui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private const int MinColumns = 52;
        private const int MinRows = 4;

        private ConfigurationService _configService = new ConfigurationService();
        private Size? _charBufferSize;
        private readonly List<TabDataItem> _leftTabs = new List<TabDataItem>();
        private readonly List<TabDataItem> _rightTabs = new List<TabDataItem>();

        private TerminalSession _session;

        private IntPtr Hwnd => new WindowInteropHelper(this).Handle;
        private DpiScale Dpi => VisualTreeHelper.GetDpi(this);

        public MainWindow()
        {
            InitializeComponent();

            var config = _configService.Load();
            if (config.AllowTransparancy)
            {
                AllowsTransparency = true;
            }

            resizeHint.Visibility = Visibility.Hidden;

            tabBarLeft.DataContext = _leftTabs;
            tabBarRight.DataContext = _rightTabs;

            _leftTabs.Add(new TabDataItem()
            {
                IsActive = true,
                Title = "cmd"
            });
            _leftTabs.Add(new TabDataItem()
            {
                Title = "powershell"
            });
            _leftTabs.Add(new TabDataItem()
            {
                Image = PackIconMaterialKind.Plus
            });
            _rightTabs.Add(new TabDataItem()
            {
                Image = PackIconMaterialKind.Settings
            });
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource.FromHwnd(Hwnd).AddHook(WindowProc);

            StartConsole();
        }

        private void StartConsole()
        {
            var config = _configService.Config;
            var tsize = new TerminalSize(config.Columns, config.Rows);
            var windowSize = GetWindowSizeForBufferSize(tsize);
            Width = windowSize.Width;
            Height = windowSize.Height;

            GetWindowSizeSnap(new Size(Width, Height));

            var profile = ExpandVariables(config.Profile);
            _session = new TerminalSession(tsize, profile);
            _session.TitleChanged += OnSessionTitleChanged;
            txtConsole.Session = _session;
            txtConsole.Focus();
        }

        private void OnSessionTitleChanged(object sender, EventArgs e)
        {
            _leftTabs[0].Title = _session.Title;
        }

        private static Profile ExpandVariables(Profile profile)
        {
            return new Profile()
            {
                Command = ExpandVariables(profile.Command),
                CurrentWorkingDirectory = ExpandVariables(profile.CurrentWorkingDirectory),
                Arguments = profile.Arguments?.Select(x => ExpandVariables(x)).ToArray()
            };
        }

        private static string ExpandVariables(string s)
        {
            var sb = new StringBuilder();
            int index = 0;
            for (;;)
            {
                int start = s.IndexOf('%', index);
                if (start != -1)
                {
                    int end = s.IndexOf('%', start + 1);
                    if (end != -1)
                    {
                        string varName = s.Substring(start + 1, end - start - 1);
                        string varValue = Environment.GetEnvironmentVariable(varName);

                        sb.Append(s.Substring(index, start - index));
                        sb.Append(varValue);

                        index = end + 1;
                        continue;
                    }
                }
                sb.Append(s.Substring(index));
                break;
            }
            return sb.ToString();
        }

        private void TextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    if (AllowsTransparency)
                    {
                        var terminal = txtConsole;
                        const double OpacityDelta = 1 / 32.0;
                        if (e.Delta > 0)
                        {
                            Opacity = Math.Min(Opacity + OpacityDelta, 1);
                        }
                        else
                        {
                            Opacity = Math.Max(Opacity - OpacityDelta, 0.25);
                        }
                        e.Handled = true;
                    }
                }
                else
                {
                    var terminal = txtConsole;
                    const double FontSizeDelta = 2;
                    if (e.Delta > 0)
                    {
                        if (terminal.FontSize < 54)
                        {
                            terminal.FontSize += FontSizeDelta;
                            _charBufferSize = null;
                            FixWindowSize();
                        }
                    }
                    else
                    {
                        if (terminal.FontSize > 8)
                        {
                            terminal.FontSize -= FontSizeDelta;
                            _charBufferSize = null;
                            FixWindowSize();
                        }
                    }
                    e.Handled = true;
                }
            }
        }

        private void FixWindowSize()
        {
            Size fixedSize = GetWindowSizeForBufferSize(_session.Size);
            Width = fixedSize.Width;
            Height = fixedSize.Height;
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg) {
            case WM_SIZE:
            {
                uint widthHeight = (uint)lParam.ToInt32();
                var bounds = new RECT()
                {
                    right = (int)(widthHeight & 0xFFFF),
                    bottom = (int)(widthHeight >> 16),
                };

                ForceWindowRect(bounds);

                resizeHint.IsShowing = true;
                resizeHint.IsShowing = false;
                break;
            }
            case WM_EXITSIZEMOVE:
                resizeHint.IsShowing = false;
                break;
            case WM_SIZING:
            {
                resizeHint.IsShowing = true;

                var bounds = Marshal.PtrToStructure<RECT>(lParam);
                bounds = SnapWindowRect(bounds, wParam.ToInt32());
                Marshal.StructureToPtr(bounds, lParam, fDeleteOld: false);
                break;
            }
            }
            return IntPtr.Zero;
        }

        private void ForceWindowRect(RECT bounds)
        {
            var dpi = Dpi;
            var absoluteSize = new Size(bounds.right - bounds.left, bounds.bottom - bounds.top);
            var scaledSize = new Size(absoluteSize.Width / dpi.DpiScaleX, absoluteSize.Height / dpi.DpiScaleY);
            GetWindowSizeSnap(scaledSize);
        }

        private RECT SnapWindowRect(RECT bounds, int grabDirection)
        {
            var dpi = Dpi;
            var absoluteSize = new Size(bounds.right - bounds.left, bounds.bottom - bounds.top);
            var scaledSize = new Size(absoluteSize.Width / dpi.DpiScaleX, absoluteSize.Height / dpi.DpiScaleY);
            scaledSize = GetWindowSizeSnap(scaledSize);
            absoluteSize = new Size(scaledSize.Width * dpi.DpiScaleX, scaledSize.Height * dpi.DpiScaleY);

            switch (grabDirection) {
            case WMSZ_LEFT:
            case WMSZ_TOPLEFT:
            case WMSZ_BOTTOMLEFT:
                bounds.left = bounds.right - (int)absoluteSize.Width;
                break;
            case WMSZ_RIGHT:
            case WMSZ_TOPRIGHT:
            case WMSZ_BOTTOMRIGHT:
                bounds.right = bounds.left + (int)absoluteSize.Width;
                break;
            }

            switch (grabDirection) {
            case WMSZ_TOP:
            case WMSZ_TOPLEFT:
            case WMSZ_TOPRIGHT:
                bounds.top = bounds.bottom - (int)absoluteSize.Height;
                break;
            case WMSZ_BOTTOM:
            case WMSZ_BOTTOMLEFT:
            case WMSZ_BOTTOMRIGHT:
                bounds.bottom = bounds.top + (int)absoluteSize.Height;
                break;
            }

            return bounds;
        }

        private Size GetWindowSizeSnap(Size size)
        {
            Size charSize = GetBufferCharSize();
            Size consoleOffset = new Size(Math.Max(Width - txtConsole.ActualWidth, 0),
                                          Math.Max(Height - txtConsole.ActualHeight, 0));
            Size newConsoleSize = new Size(Math.Max(size.Width - consoleOffset.Width, 0),
                                           Math.Max(size.Height - consoleOffset.Height, 0));

            int columns = (int)Math.Round(newConsoleSize.Width / charSize.Width);
            int rows = (int)Math.Round(newConsoleSize.Height / charSize.Height);

            columns = Math.Max(columns, MinColumns);
            rows = Math.Max(rows, MinRows);

            var tsize = new TerminalSize(columns, rows);
            if (_session != null)
            {
                _session.Size = tsize;
            }
            resizeHint.Hint = tsize;

            _configService.Config.Columns = tsize.Columns;
            _configService.Config.Rows = tsize.Rows;
            _configService.Save();

            return GetWindowSizeForBufferSize(tsize);
        }

        private Size GetWindowSizeForBufferSize(TerminalSize size)
        {
            Size charSize = GetBufferCharSize();
            Size consoleOffset = new Size(Math.Max(Width - txtConsole.ActualWidth, 0),
                                          Math.Max(Height - txtConsole.ActualHeight, 0));
            Size snappedConsoleSize = new Size(size.Columns * charSize.Width,
                                               size.Rows * charSize.Height);

            Size result = new Size(Math.Ceiling(snappedConsoleSize.Width + consoleOffset.Width) + 2,
                                   Math.Ceiling(snappedConsoleSize.Height + consoleOffset.Height));
            return result;
        }

        private Size GetBufferCharSize()
        {
            if (!_charBufferSize.HasValue)
            {
                _charBufferSize = MeasureString(" ");
            }
            return _charBufferSize.Value;
        }

        private Size MeasureString(string candidate)
        {
            var typeface = new Typeface(txtConsole.FontFamily, txtConsole.FontStyle, txtConsole.FontWeight, txtConsole.FontStretch);
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                txtConsole.FontSize,
                Brushes.Black,
                Dpi.PixelsPerDip);

            var result = new Size(formattedText.WidthIncludingTrailingWhitespace, formattedText.Height);
            Debug.Assert(result.Width > 0);
            Debug.Assert(result.Height > 0);
            return result;
        }
    }
}
