using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using tterm.Ansi;
using tterm.Terminal;

namespace tterm.Ui
{
    public class TerminalControl : StackPanel
    {
        private readonly Dictionary<int, Brush> _brushDictionary = new Dictionary<int, Brush>();
        private readonly List<TextBlock> _textBlocks = new List<TextBlock>();

        private TerminalBuffer _buffer;

        private Brush _foreground;
        private Brush _cursorBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));

        private FontFamily _fontFamily;
        private double _fontSize;
        private FontStyle _fontStyle;
        private FontWeight _fontWeight;
        private FontStretch _fontStretch;

        private int _lastCursorY;

        public TerminalBuffer Buffer
        {
            get => _buffer;
            set
            {
                _buffer = value;
            }
        }

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
        }

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
                FontWeight = _fontWeight
            };
            return textBlock;
        }

        private void EnsureLineCount(int lineCount)
        {
            while (_textBlocks.Count < lineCount)
            {
                var textBlock = CreateTextBlock();
                _textBlocks.Add(textBlock);
                Children.Add(textBlock);
            }
        }

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

        private List<Inline> GetInlines(int y, TerminalTagArray lineTags)
        {
            var inlines = new List<Inline>();
            int x = 0;
            foreach (var tag in lineTags)
            {
                int length = tag.Text.Length;
                if (x <= Buffer.CursorX && y == Buffer.CursorY && x + length > Buffer.CursorX)
                {
                    int offset = Buffer.CursorX - x;
                    var tagA = tag.Substring(0, offset);
                    var tagB = tag.Substring(offset, 1);
                    var tagC = tag.Substring(offset + 1);
                    inlines.Add(CreateInline(tagA));

                    var cursorInline = CreateInline(tagB);
                    cursorInline.Background = _cursorBrush;
                    cursorInline.Foreground = _cursorBrush;
                    inlines.Add(cursorInline);

                    inlines.Add(CreateInline(tagC));
                }
                else
                {
                    inlines.Add(CreateInline(tag));
                }

                x += tag.Text.Length;
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

        #region Events

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            Focus();
            base.OnPreviewMouseDown(e);
        }

        #endregion
    }
}
