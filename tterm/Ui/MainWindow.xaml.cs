using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using tterm.Ansi;
using tterm.Extensions;
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
        private WinPty _pty;
        private StreamWriter _ptyWriter;
        private TerminalBuffer _tBuffer = new TerminalBuffer();
        private Size? _charBufferSize;
        private readonly List<TabDataItem> _leftTabs = new List<TabDataItem>();
        private readonly List<TabDataItem> _rightTabs = new List<TabDataItem>();

        private IntPtr Hwnd => new WindowInteropHelper(this).Handle;
        private DpiScale Dpi => VisualTreeHelper.GetDpi(this);

        public MainWindow()
        {
            InitializeComponent();
            txtConsole.Text = "";
            txtConsole.IsReadOnly = true;
            txtConsole.Focus();

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
            GetWindowSizeSnap(new Size(Width, Height));

            _pty = new WinPty();
            _ptyWriter = new StreamWriter(_pty.StandardInput);
            _ptyWriter.AutoFlush = true;

            ConsoleOutputAsync(_pty.StandardOutput);
            ConsoleOutputAsync(_pty.StandardError);
        }

        private void ConsoleOutputAsync(Stream stream)
        {
            var sr = new StreamReader(stream);
            Task.Run(async () =>
            {
                try
                {
                    int readChars;
                    do
                    {
                        var buffer = new char[1024];
                        readChars = await sr.ReadAsync(buffer, 0, buffer.Length);
                        if (readChars > 0)
                        {
                            ReceiveOutput(new ArraySegment<char>(buffer, 0, readChars));
                        }
                    }
                    while (readChars != 0);
                }
                catch (Exception)
                {
                    throw;
                }
            });
        }

        private int ReadDigits(IList<char> buffer, ref int i, ref char c)
        {
            string digits = "";
            while (Char.IsNumber(c = buffer[i]))
            {
                digits += c;
                i++;
            }
            int.TryParse(digits, out int value);
            return value;
        }

        private void ReceiveOutput(ArraySegment<char> buffer)
        {
            var ansiParser = new ANSICodeParser();
            for (int i = 0; i < buffer.Count; i++)
            {
                char c = buffer.Array[buffer.Offset + i];
                if (c == ANSICode.ESC)
                {
                    ANSICode? code = ansiParser.Parse(buffer.Substring(i), out int codeLength);
                    if (code.HasValue)
                    {
                        i += codeLength - 1;
                        ProcessAnsiCode(code.Value);
                    }
                }
                else if (c == '\n')
                {
                    if (_tBuffer.CursorY == _tBuffer.Rows - 1)
                    {
                        _tBuffer.ShiftUp();
                    }
                    else
                    {
                        _tBuffer.CursorY++;
                    }
                }
                else if (c == '\r')
                {
                    _tBuffer.CursorX = 0;
                }
                else
                {
                    _tBuffer.Type(c);
                }
            }
            RefreshUI();
        }

        private void RefreshUI()
        {
            string text = GetBufferAsString();
            Dispatcher.Invoke(() =>
            {
                txtConsole.Text = text;
                txtConsole.SelectionStart = text.Length;
                txtConsole.ScrollToEnd();
            });
        }

        private string GetBufferAsString()
        {
            var sb = new StringBuilder(capacity: 128);
            for (int y = 0; y < _tBuffer.Rows; y++)
            {
                string line = _tBuffer.GetLine(y);
                sb.AppendLine(line);
            }
            return sb.ToString().TrimEnd();
        }

        private void ProcessAnsiCode(ANSICode code)
        {
            switch (code.Type) {
            case ANSICodeType.SetCursorPosition:
                _tBuffer.CursorX = code.Column;
                _tBuffer.CursorY = code.Line;
                break;
            case ANSICodeType.MoveToColumn:
                _tBuffer.CursorX = code.Column;
                break;
            case ANSICodeType.EraseLine:
                if (code.Line == 0)
                {
                    _tBuffer.ClearBlock(_tBuffer.CursorX, _tBuffer.CursorY, _tBuffer.Columns - 1, _tBuffer.CursorY);
                }
                break;
            case ANSICodeType.EraseDisplay:
                _tBuffer.Clear();
                _tBuffer.CursorX = 0;
                _tBuffer.CursorY = 0;
                break;
            case ANSICodeType.Title:
                Dispatcher.Invoke(() =>
                {
                    _leftTabs[0].Title = code.Text;
                });
                break;
            }
        }

        private void TextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                var textBox = (sender as TextBox);
                const double FontSizeDelta = 2;
                if (e.Delta > 0)
                {
                    if (textBox.FontSize < 54)
                    {
                        textBox.FontSize += FontSizeDelta;
                        _charBufferSize = null;
                        FixWindowSize();
                    }
                }
                else
                {
                    if (textBox.FontSize > 8)
                    {
                        textBox.FontSize -= FontSizeDelta;
                        _charBufferSize = null;
                        FixWindowSize();
                    }
                }
                e.Handled = true;
            }
        }

        private void txtConsole_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            string text = string.Empty;
            switch (e.Key)
            {
                case Key.Escape:
                    text = "\u001B\u001B\u001B";
                    break;
                case Key.Back:
                    text = "\u0008";
                    break;
                case Key.Up:
                    text = "\u001BOA";
                    break;
                case Key.Down:
                    text = "\u001BOB";
                    break;
                case Key.Left:
                    text = "\u001BOC";
                    break;
                case Key.Right:
                    text = "\u001BOD";
                    break;
                case Key.Return:
                    text = "\r";
                    break;
                case Key.Space:
                    text = " ";
                    break;
                case Key.Tab:
                    text = "\t";
                    break;
            }
            if (text != string.Empty)
            {
                // txtConsole.Text += text;
                // txtConsole.SelectionStart = txtConsole.Text.Length;
                _ptyWriter.Write(text);
                e.Handled = true;
            }
        }

        private void txtConsole_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // txtConsole.Text += e.Text;
            // txtConsole.SelectionStart = txtConsole.Text.Length;
            _ptyWriter.Write(e.Text);
            e.Handled = true;
        }

        private void FixWindowSize()
        {
            Size fixedSize = GetWindowSizeForBufferSize(_tBuffer.Columns, _tBuffer.Rows);
            Width = fixedSize.Width;
            Height = fixedSize.Height;
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg) {
            case WM_SIZING:
                var bounds = Marshal.PtrToStructure<RECT>(lParam);
                bounds = SnapWindowRect(bounds);
                Marshal.StructureToPtr(bounds, lParam, fDeleteOld: false);
                break;
            }
            return IntPtr.Zero;
        }

        private RECT SnapWindowRect(RECT bounds)
        {
            var dpi = Dpi;
            var absoluteSize = new Size(bounds.right - bounds.left, bounds.bottom - bounds.top);
            var scaledSize = new Size(absoluteSize.Width / dpi.DpiScaleX, absoluteSize.Height / dpi.DpiScaleY);
            scaledSize = GetWindowSizeSnap(scaledSize);
            absoluteSize = new Size(scaledSize.Width * dpi.DpiScaleX, scaledSize.Height * dpi.DpiScaleY);
            bounds.right = bounds.left + (int)absoluteSize.Width;
            bounds.bottom = bounds.top + (int)absoluteSize.Height;
            return bounds;
        }

        private Size GetWindowSizeSnap(Size size)
        {
            Size charSize = GetBufferCharSize();
            Size consoleOffset = new Size(Width - txtConsole.ActualWidth,
                                          Height - txtConsole.ActualHeight);
            Size newConsoleSize = new Size(size.Width - consoleOffset.Width,
                                           size.Height - consoleOffset.Height);

            int columns = (int)Math.Round(newConsoleSize.Width / charSize.Width);
            int rows = (int)Math.Round(newConsoleSize.Height / charSize.Height);

            _tBuffer.SetSize(rows, columns);

            return GetWindowSizeForBufferSize(columns, rows);
        }

        private Size GetWindowSizeForBufferSize(int columns, int rows)
        {
            Size charSize = GetBufferCharSize();
            Size consoleOffset = new Size(Width - txtConsole.ActualWidth,
                                          Height - txtConsole.ActualHeight);
            Size snappedConsoleSize = new Size(columns * charSize.Width,
                                               rows * charSize.Height);

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

            return new Size(formattedText.WidthIncludingTrailingWhitespace, formattedText.Height);
        }
    }
}
