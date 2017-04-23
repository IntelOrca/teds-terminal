using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using tterm.Ansi;
using tterm.Terminal;
using SelectionMode = tterm.Terminal.SelectionMode;

namespace tterm.Ui
{
    internal class TerminalControl : Canvas
    {
        private const int UpdateTimerIntervalMs = 50;
        private const int UpdateTimerTriggerMs = 50;
        private const int UpdateTimerTriggerIntervalCount = 5;

        private readonly TerminalColourHelper _colourHelper = new TerminalColourHelper();
        private readonly List<TerminalControlLine> _lines = new List<TerminalControlLine>();

        private TerminalSession _session;

        private FontFamily _fontFamily;
        private double _fontSize;
        private FontStyle _fontStyle;
        private FontWeight _fontWeight;
        private FontStretch _fontStretch;

        private Size? _charSize;

        private int _lastCursorY;

        private readonly DispatcherTimer _updateTimer;
        private readonly object _updateRequestIntervalsSync = new object();
        private readonly int[] _updateRequestIntervals = new int[UpdateTimerTriggerIntervalCount];
        private int _updateRequestIntervalsIndex;
        private int _lastUpdateTick;
        private int _updateAvailable;

        private int _focusTick;

        public TerminalSession Session
        {
            get => _session;
            set
            {
                if (_session != value)
                {
                    if (_session != null)
                    {
                        _session.OutputReceived -= OnOutputReceived;
                        _session.BufferSizeChanged -= OnBufferSizeChanged;
                    }
                    _session = value;
                    if (value != null)
                    {
                        _session.OutputReceived += OnOutputReceived;
                        _session.BufferSizeChanged += OnBufferSizeChanged;
                    }
                    UpdateContent();
                }
            }
        }

        public TerminalBuffer Buffer => _session?.Buffer;

        public FontFamily FontFamily
        {
            get => _fontFamily;
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _charSize = null;
                    _fontSize = value;
                    foreach (var textBlock in _lines)
                    {
                        textBlock.FontSize = value;
                    }
                }
            }
        }

        public FontStyle FontStyle
        {
            get => _fontStyle;
        }

        public FontWeight FontWeight
        {
            get => _fontWeight;
        }

        public FontStretch FontStretch
        {
            get => _fontStretch;
        }

        private DpiScale Dpi => VisualTreeHelper.GetDpi(this);

        private bool IsSessionAvailable => _session != null;

        public Size CharSize
        {
            get
            {
                if (!_charSize.HasValue)
                {
                    var charSize = MeasureString(" ");
                    charSize.Height = Math.Floor(charSize.Height);
                    _charSize = charSize;
                }
                return _charSize.Value;
            }
        }

        static TerminalControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TerminalControl), new FrameworkPropertyMetadata(typeof(TerminalControl)));
        }

        public TerminalControl()
        {
            Background = new BrushConverter().ConvertFromString("#FF1E1E1E") as Brush;
            _fontFamily = new FontFamily("Consolas");
            _fontSize = 20;
            _fontStyle = FontStyles.Normal;
            _fontWeight = FontWeights.Regular;
            _fontStretch = FontStretches.Normal;
            Focusable = true;
            FocusVisualStyle = null;
            SnapsToDevicePixels = true;
            ClipToBounds = true;

            _updateTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(UpdateTimerIntervalMs),
                DispatcherPriority.Background,
                OnTimeControlledUpdate,
                Dispatcher);
            _updateTimer.Stop();
        }

        #region Layout

        private TerminalControlLine CreateLine()
        {
            var line = new TerminalControlLine()
            {
                Typeface = new Typeface(_fontFamily, _fontStyle, _fontWeight, _fontStretch),
                FontSize = _fontSize,
                ColourHelper = _colourHelper,
                SnapsToDevicePixels = true
            };
            return line;
        }

        private void SetLineCount(int lineCount)
        {
            if (_lines.Count < lineCount)
            {
                while (_lines.Count < lineCount)
                {
                    var textBlock = CreateLine();
                    _lines.Add(textBlock);
                    Children.Add(textBlock);
                }
                AlignTextBlocks();
            }
            else if (_lines.Count > lineCount)
            {
                int removeIndex = lineCount;
                int removeCount = _lines.Count - lineCount;
                _lines.RemoveRange(removeIndex, removeCount);
                Children.RemoveRange(removeIndex, removeCount);
            }
        }

        private void AlignTextBlocks()
        {
            int y = 0;
            int lineHeight = (int)CharSize.Height;
            for (int i = 0; i < _lines.Count; i++)
            {
                var textBlock = _lines[i];
                Canvas.SetTop(textBlock, y);
                Canvas.SetBottom(textBlock, y + lineHeight);
                Canvas.SetLeft(textBlock, 0);
                Canvas.SetRight(textBlock, ActualWidth);
                y += lineHeight;
            }
        }

        private Size MeasureString(string candidate)
        {
            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Brushes.Black,
                Dpi.PixelsPerDip);

            var result = new Size(formattedText.WidthIncludingTrailingWhitespace, formattedText.Height);
            Debug.Assert(result.Width > 0);
            Debug.Assert(result.Height > 0);
            return result;
        }

        private TerminalControlLine GetLineAt(Point pos, out int row)
        {
            TerminalControlLine result = null;
            row = (int)(pos.Y / CharSize.Height);
            if (_lines.Count > row)
            {
                result = _lines[row];
            }
            return result;
        }

        private TerminalPoint? GetBufferCoordinates(Point pos)
        {
            TerminalPoint? result = null;
            TerminalControlLine line = GetLineAt(pos, out int row);
            if (line != null)
            {
                pos = TranslatePoint(pos, line);
                int col = line.GetColumnAt(pos);
                result = new TerminalPoint(col, row);
            }
            return result;
        }

        #endregion

        #region Render

        public void UpdateContent()
        {
            Interlocked.Increment(ref _updateAvailable);

            // If we are getting a lot of update requests, off load it to our update timer for a more
            // steady refresh rate that doesn't slow the UI down
            bool heavyLoad = false;
            lock (_updateRequestIntervalsSync)
            {
                int currentTick = Environment.TickCount;
                int interval = currentTick - _lastUpdateTick;
                int[] intervals = _updateRequestIntervals;
                int measureLength = intervals.Length;
                int intervalsIndex = _updateRequestIntervalsIndex;
                intervals[intervalsIndex] = interval;
                intervalsIndex++;
                if (intervalsIndex == measureLength)
                {
                    intervalsIndex = 0;
                }
                _updateRequestIntervalsIndex = intervalsIndex;
                _lastUpdateTick = currentTick;

                int total = 0;
                for (int i = 0; i < intervals.Length; i++)
                {
                    total += intervals[i];
                }
                heavyLoad = (total < UpdateTimerTriggerMs);
            }

            if (heavyLoad && !IsSelecting)
            {
                _updateTimer.IsEnabled = true;
            }
            else
            {
                _updateTimer.IsEnabled = false;
                UpdateContentControlled();
            }
        }

        public void UpdateContentControlled()
        {
            int updateAvailable = Interlocked.Exchange(ref _updateAvailable, 0);
            if (updateAvailable == 0)
            {
                return;
            }
            UpdateContentForced();
        }

        public void UpdateContentForced()
        {
            if (IsSessionAvailable)
            {
                int lineCount = Buffer.Size.Rows;
                var lineTags = new TerminalTagArray[lineCount];
                for (int y = 0; y < lineCount; y++)
                {
                    lineTags[y] = Buffer.GetFormattedLine(y);
                }

                Dispatcher.InvokeAsync(() =>
                {
                    _lastCursorY = Buffer.CursorY;
                    SetLineCount(lineCount);
                    for (int y = 0; y < lineCount; y++)
                    {
                        _lines[y].Tags = lineTags[y];
                    }

                    _lastUpdateTick = Environment.TickCount;
                });
            }
            else
            {
                Dispatcher.InvokeAsync(() =>
                {
                    _lines.Clear();
                    Children.Clear();
                });
            }
        }

        #endregion

        #region Selection

        private bool IsSelecting => Buffer.Selection != null;

        private void ClearSelection()
        {
            Buffer.Selection = null;
            UpdateContentForced();
        }

        private void StartSelectionAt(TerminalPoint startPoint)
        {
            Buffer.Selection = new TerminalSelection(SelectionMode.Block, startPoint, startPoint);
            UpdateContentForced();
        }

        private void EndSelectionAt(TerminalPoint endPoint)
        {
            if (Buffer.Selection != null)
            {
                var startPoint = Buffer.Selection.Start;
                Buffer.Selection = new TerminalSelection(SelectionMode.Block, startPoint, endPoint);
                UpdateContentForced();
            }
        }

        #endregion

        #region Events

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            _charSize = null;
            AlignTextBlocks();
            return base.ArrangeOverride(arrangeSize);
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            _focusTick = e.Timestamp;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            if (!IsSessionAvailable)
            {
                return;
            }

            Focus();

            // Prevent selection if we have just gained focus of the control
            if (e.Timestamp - _focusTick < 100)
            {
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                var point = GetBufferCoordinates(pos);
                if (point.HasValue)
                {
                    StartSelectionAt(point.Value);
                }
            }
            else if (e.MiddleButton == MouseButtonState.Pressed ||
                     e.RightButton == MouseButtonState.Pressed)
            {
                if (Buffer.Selection != null)
                {
                    Buffer.CopySelection();
                    ClearSelection();
                }
                else
                {
                    _session.Paste();
                }
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            if (!IsSessionAvailable)
            {
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed && IsSelecting)
            {
                var pos = e.GetPosition(this);
                var point = GetBufferCoordinates(pos);
                if (point.HasValue)
                {
                    EndSelectionAt(point.Value);
                }
            }
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                double delta = -e.Delta / 40.0;
                int offset = (int)delta;
                if (offset < 0 && Buffer.WindowTop > 0 && Buffer.WindowTop + offset < 0)
                {
                    offset = -Buffer.WindowTop;
                }
                else if (offset > 0 && Buffer.WindowTop < 0 && Buffer.WindowTop + offset > 0)
                {
                    offset = -Buffer.WindowTop;
                }
                Buffer.Scroll(offset);
                UpdateContentForced();
                e.Handled = true;
            }
            else
            {
                base.OnPreviewMouseWheel(e);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (!IsSessionAvailable)
            {
                return;
            }

            ModifierKeys modifiers = e.KeyboardDevice.Modifiers;
            int modCode = 0;
            if (modifiers.HasFlag(ModifierKeys.Shift)) modCode |= 1;
            if (modifiers.HasFlag(ModifierKeys.Alt)) modCode |= 2;
            if (modifiers.HasFlag(ModifierKeys.Control)) modCode |= 4;
            if (modifiers.HasFlag(ModifierKeys.Windows)) modCode |= 8;

            if (IsSelecting)
            {
                ClearSelection();
                if (e.Key == Key.Escape)
                {
                    return;
                }
            }

            string text = string.Empty;
            switch (e.Key)
            {
                case Key.Escape:
                    text = $"{C0.ESC}{C0.ESC}{C0.ESC}";
                    break;
                case Key.Back:
                    text = modifiers.HasFlag(ModifierKeys.Shift) ?
                        C0.BS.ToString() :
                        C0.DEL.ToString();
                    break;
                case Key.Delete:
                    text = (modCode == 0) ?
                        $"{C0.ESC}[3~" :
                        $"{C0.ESC}[3;{modCode + 1}~";
                    break;
                case Key.Tab:
                    text = modifiers.HasFlag(ModifierKeys.Shift) ?
                        $"{C0.ESC}[Z" :
                        C0.HT.ToString();
                    break;
                case Key.Up:
                    text = Construct(1, 'A');
                    break;
                case Key.Down:
                    text = Construct(1, 'B');
                    break;
                case Key.Right:
                    text = Construct(1, 'C');
                    break;
                case Key.Left:
                    text = Construct(1, 'D');
                    break;
                case Key.Home:
                    text = Construct(1, 'H');
                    break;
                case Key.End:
                    text = Construct(1, 'F');
                    break;
                case Key.PageUp:
                    text = $"{C0.ESC}[5~";
                    break;
                case Key.PageDown:
                    text = $"{C0.ESC}[6~";
                    break;
                case Key.Return:
                    text = C0.CR.ToString();
                    break;
                case Key.Space:
                    text = " ";
                    break;
                case Key.F1:
                    text = Construct(1, 'P');
                    break;
                case Key.F2:
                    text = Construct(1, 'Q');
                    break;
                case Key.F3:
                    text = Construct(1, 'R');
                    break;
                case Key.F4:
                    text = Construct(1, 'S');
                    break;
                case Key.F5:
                    text = (modCode == 0) ?
                        $"{C0.ESC}[15~" :
                        $"{C0.ESC}[15;{modCode + 1}~";
                    break;
                case Key.F6:
                    text = (modCode == 0) ?
                        $"{C0.ESC}[17~" :
                        $"{C0.ESC}[17;{modCode + 1}~";
                    break;
                case Key.F7:
                    text = (modCode == 0) ?
                        $"{C0.ESC}[18~" :
                        $"{C0.ESC}[18;{modCode + 1}~";
                    break;
                case Key.F8:
                    text = (modCode == 0) ?
                        $"{C0.ESC}[19~" :
                        $"{C0.ESC}[19;{modCode + 1}~";
                    break;
                case Key.F9:
                    text = (modCode == 0) ?
                        $"{C0.ESC}[20~" :
                        $"{C0.ESC}[20;{modCode + 1}~";
                    break;
                case Key.F10:
                    text = (modCode == 0) ?
                        $"{C0.ESC}[21~" :
                        $"{C0.ESC}[21;{modCode + 1}~";
                    break;
                case Key.F11:
                    text = (modCode == 0) ?
                        $"{C0.ESC}[23~" :
                        $"{C0.ESC}[23;{modCode + 1}~";
                    break;
                case Key.F12:
                    text = (modCode == 0) ?
                        $"{C0.ESC}[24~" :
                        $"{C0.ESC}[24;{modCode + 1}~";
                    break;
            }
            if (text != string.Empty)
            {
                _session.Write(text);
                e.Handled = true;
            }

            string Construct(int a, char c)
            {
                return (modCode == 0) ?
                    $"{C0.ESC}O{c}" :
                    $"{C0.ESC}[{a};{modCode + 1}{c}";
            }
        }

        protected override void OnPreviewTextInput(TextCompositionEventArgs e)
        {
            if (!IsSessionAvailable)
            {
                return;
            }

            string text = e.Text;
            if (string.IsNullOrEmpty(text))
            {
                text = e.ControlText;
            }
            _session.Write(text);
            e.Handled = true;
        }

        private void OnOutputReceived(object sender, EventArgs e)
        {
            UpdateContent();
        }

        private void OnBufferSizeChanged(object sender, EventArgs e)
        {
            UpdateContent();
        }

        private void OnTimeControlledUpdate(object sender, EventArgs e)
        {
            UpdateContentControlled();
        }

        #endregion
    }
}
