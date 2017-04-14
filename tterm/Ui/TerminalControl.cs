using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using tterm.Ansi;
using tterm.Terminal;

namespace tterm.Ui
{
    public class TerminalControl : TextBlock
    {
        private TerminalBuffer _buffer;

        public TerminalBuffer Buffer
        {
            get => _buffer;
            set
            {
                _buffer = value;
            }
        }

        static TerminalControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TerminalControl), new FrameworkPropertyMetadata(typeof(TerminalControl)));
        }

        public TerminalControl()
        {
            FontFamily = new FontFamily("Consolas");
            FontSize = 20;
            Background = new BrushConverter().ConvertFromString("#FF1E1E1E") as Brush;
            Foreground = new BrushConverter().ConvertFromString("#FFCCCCCC") as Brush;
            Focusable = true;
            FocusVisualStyle = null;
        }

        public void UpdateContent()
        {
            var inlines = new List<Inline>();
            var buffer = Buffer;
            for (int y = 0; y < buffer.Size.Rows; y++)
            {
                int x = 0;
                var lineTags = buffer.GetFormattedLine(y);
                foreach (var tag in lineTags)
                {
                    int length = tag.Text.Length;
                    if (x <= buffer.CursorX && y == buffer.CursorY && x + length > buffer.CursorX)
                    {
                        int offset = buffer.CursorX - x;
                        var tagA = tag.Substring(0, offset);
                        var tagB = tag.Substring(offset, 1);
                        var tagC = tag.Substring(offset + 1);
                        inlines.Add(CreateInline(tagA));

                        var cursorBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));
                        var cursorInline = CreateInline(tagB);
                        cursorInline.Background = cursorBrush;
                        cursorInline.Foreground = cursorBrush;
                        inlines.Add(cursorInline);

                        inlines.Add(CreateInline(tagC));
                    }
                    else
                    {
                        inlines.Add(CreateInline(tag));
                    }

                    x += tag.Text.Length;
                }
                inlines.Add(new LineBreak());
            }

            Inlines.Clear();
            Inlines.AddRange(inlines);
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
            Brush result = Background;
            if (id != 0)
            {
                result = new SolidColorBrush(GetColour(id));
            }
            return result;
        }

        private Brush GetForegroundBrush(int id)
        {
            Brush result = Foreground;
            if (id != 0)
            {
                result = new SolidColorBrush(GetColour(id));
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
    }
}
