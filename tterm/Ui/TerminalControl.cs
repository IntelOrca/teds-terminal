using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using tterm.Ansi;
using tterm.Terminal;
using SelectionMode = tterm.Terminal.SelectionMode;

namespace tterm.Ui
{
    internal class TerminalControl : Canvas
    {
        private readonly Dictionary<int, Brush> _brushDictionary = new Dictionary<int, Brush>();
        private readonly List<TextBlock> _textBlocks = new List<TextBlock>();

        private TerminalSession _session;

        private Brush _foreground;

        private FontFamily _fontFamily;
        private double _fontSize;
        private FontStyle _fontStyle;
        private FontWeight _fontWeight;
        private FontStretch _fontStretch;

        private Size? _charSize;
        private int _lastCursorY;

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
                    }
                    _session = value;
                    _session.OutputReceived += OnOutputReceived;
                }
            }
        }

        public TerminalBuffer Buffer => _session.Buffer;

        public Brush Foreground
        {
            get => _foreground;
        }

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
                    _fontSize = value;
                    foreach (TextBlock textBlock in _textBlocks)
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

        public Size CharSize
        {
            get
            {
                if (!_charSize.HasValue)
                {
                    var charSize = MeasureString(" ");
                    charSize.Height = Math.Ceiling(charSize.Height);
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
            _foreground = new BrushConverter().ConvertFromString("#FFCCCCCC") as Brush;
            _fontFamily = new FontFamily("Consolas");
            _fontSize = 20;
            _fontStyle = FontStyles.Normal;
            _fontWeight = FontWeights.Regular;
            _fontStretch = FontStretches.Normal;
            Focusable = true;
            FocusVisualStyle = null;
            SnapsToDevicePixels = true;
        }

        #region Layout

        private TextBlock CreateTextBlock()
        {
            var textBlock = new TextBlock()
            {
                Background = Background,
                Foreground = _foreground,
                FontFamily = _fontFamily,
                FontSize = _fontSize,
                FontStyle = _fontStyle,
                FontStretch = _fontStretch,
                FontWeight = _fontWeight,
                SnapsToDevicePixels = true
            };
            return textBlock;
        }

        private void EnsureLineCount(int lineCount)
        {
            if (_textBlocks.Count < lineCount)
            {
                while (_textBlocks.Count < lineCount)
                {
                    var textBlock = CreateTextBlock();
                    _textBlocks.Add(textBlock);
                    Children.Add(textBlock);
                }
                AlignTextBlocks();
            }
        }

        private void AlignTextBlocks()
        {
            int y = 0;
            int lineHeight = (int)CharSize.Height;
            for (int i = 0; i < _textBlocks.Count; i++)
            {
                var textBlock = _textBlocks[i];
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

        private TextBlock GetTextBlockAt(Point pos, out int row)
        {
            var ftb = _textBlocks.FirstOrDefault();
            if (ftb != null)
            {
                row = (int)(pos.Y / ftb.ActualHeight);
                if (_textBlocks.Count > row)
                {
                    return _textBlocks[row];
                }
            }
            row = 0;
            return null;
        }

        private TerminalPoint GetBufferCoordinates(Point pos)
        {
            TextBlock tb = GetTextBlockAt(pos, out int row);
            pos = TranslatePoint(pos, tb);
            int col = 0;
            double left = 0;
            var textPointer = tb.ContentStart;
            while (textPointer != null)
            {
                Rect rect = textPointer.GetCharacterRect(LogicalDirection.Forward);
                if (rect.X > left)
                {
                    rect.Width = rect.X - left;
                    rect.X = left;
                    if (pos.X >= rect.Left && pos.X < rect.Right)
                    {
                        break;
                    }

                    left = rect.Right;
                    col++;
                }
                textPointer = textPointer.GetNextInsertionPosition(LogicalDirection.Forward);
            }
            return new TerminalPoint(col, row);
        }

        #endregion

        #region Render

        public void UpdateContent()
        {
            int lineCount = Buffer.Size.Rows;
            EnsureLineCount(lineCount);
            for (int y = 0; y < lineCount; y++)
            {
                UpdateLine(y);
            }
            _lastCursorY = Buffer.CursorY;
        }

        private void UpdateLine(int y)
        {
            var textBlock = _textBlocks[y];

            var lineTags = Buffer.GetFormattedLine(y);
            if (textBlock.Tag != null && y != _lastCursorY && y != Buffer.CursorY)
            {
                if ((TerminalTagArray)textBlock.Tag == lineTags)
                {
                    return;
                }
            }

            var inlines = GetInlines(y, lineTags);
            textBlock.Inlines.Clear();
            textBlock.Inlines.AddRange(inlines);
            textBlock.Tag = lineTags;
        }

        private Inline[] GetInlines(int y, TerminalTagArray lineTags)
        {
            var inlines = new Inline[lineTags.Length];
            int i = 0;
            foreach (var tag in lineTags)
            {
                inlines[i++] = CreateInline(tag);
            }
            return inlines;
        }

        private Run CreateInline(TerminalTag tag)
        {
            var run = new Run(tag.Text)
            {
                Background = GetBackgroundBrush(tag.Attributes.BackgroundColour),
                Foreground = GetForegroundBrush(tag.Attributes.ForegroundColour)
            };
            if ((tag.Attributes.Flags & 1) != 0)
            {
                run.FontWeight = FontWeights.Bold;
            }
            return run;
        }

        private Brush GetBackgroundBrush(int id)
        {
            return GetBrush(id, Background);
        }

        private Brush GetForegroundBrush(int id)
        {
            return GetBrush(id, Foreground);
        }

        private Brush GetBrush(int id, Brush @default)
        {
            Brush result = @default;
            if (id != 0)
            {
                if (!_brushDictionary.TryGetValue(id, out result))
                {
                    result = new SolidColorBrush(GetColour(id));
                    _brushDictionary.Add(id, result);
                }
            }
            return result;
        }

        private Color GetColour(int id)
        {
            return (Color)ColorConverter.ConvertFromString(TangoColours[id % 16]);
        }

        // Colors 0-15
        private readonly static string[] TangoColours =
        {
            // dark:
            "#2e3436",
            "#cc0000",
            "#4e9a06",
            "#c4a000",
            "#3465a4",
            "#75507b",
            "#06989a",
            "#d3d7cf",

            // bright:
            "#555753",
            "#ef2929",
            "#8ae234",
            "#fce94f",
            "#729fcf",
            "#ad7fa8",
            "#34e2e2",
            "#eeeeec"
        };

        #endregion

        #region Selection

        private bool IsSelecting => Buffer.Selection != null;

        private void ClearSelection()
        {
            Buffer.Selection = null;
            UpdateContent();
        }

        private void StartSelectionAt(TerminalPoint startPoint)
        {
            Buffer.Selection = new TerminalSelection(SelectionMode.Block, startPoint, startPoint);
            UpdateContent();
        }

        private void EndSelectionAt(TerminalPoint endPoint)
        {
            if (Buffer.Selection != null)
            {
                var startPoint = Buffer.Selection.Start;
                Buffer.Selection = new TerminalSelection(SelectionMode.Block, startPoint, endPoint);
                UpdateContent();
            }
        }

        #endregion

        #region Events

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            AlignTextBlocks();
            return base.ArrangeOverride(arrangeSize);
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            Focus();
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                var point = GetBufferCoordinates(pos);
                StartSelectionAt(point);
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
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                var point = GetBufferCoordinates(pos);
                EndSelectionAt(point);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
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
                case Key.Return:
                    text = C0.CR.ToString();
                    break;
                case Key.Space:
                    text = " ";
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
            Dispatcher.Invoke(UpdateContent);
        }

        #endregion
    }
}
