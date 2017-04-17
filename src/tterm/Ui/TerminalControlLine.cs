using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.TextFormatting;
using tterm.Ansi;
using tterm.Terminal;

namespace tterm.Ui
{
    internal class TerminalControlLine : UIElement
    {
        private TerminalTagArray _tags;
        private Typeface _typeface;
        private double _fontSize;
        private TerminalColourHelper _colourHelper;
        private TextLine _textLine;

        public TerminalTagArray Tags
        {
            get => _tags;
            set
            {
                if (_tags != value)
                {
                    _tags = value;
                    InvalidateVisual();
                }
            }
        }

        public Typeface Typeface
        {
            get => _typeface;
            set
            {
                if (_typeface != value)
                {
                    _typeface = value;
                    InvalidateVisual();
                }
            }
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    InvalidateVisual();
                }
            }
        }

        public TerminalColourHelper ColourHelper
        {
            get => _colourHelper;
            set
            {
                if (_colourHelper != value)
                {
                    _colourHelper = value;
                    InvalidateVisual();
                }
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            var textFormatter = TextFormatter.Create();
            var paragraphProperties = new TagTextParagraphProperties(_typeface, _fontSize);
            var textSource = new TagTextSource(_tags, _typeface, _fontSize, _colourHelper);

            _textLine = textFormatter.FormatLine(textSource, 0, 6, paragraphProperties, null);
            _textLine.Draw(dc, new Point(0, 0), InvertAxes.None);
        }

        public int GetColumnAt(Point pos)
        {
            int column = 0;
            if (_textLine != null)
            {
                CharacterHit charHit = _textLine.GetCharacterHitFromDistance(pos.X);
                column = charHit.FirstCharacterIndex;
            }
            return column;
        }

        private class TagTextSource : TextSource
        {
            private readonly TerminalTagArray _tags;
            private readonly Typeface _typeface;
            private readonly double _fontSize;
            private readonly TerminalColourHelper _colourHelper;

            public int Length { get; }

            public TagTextSource(TerminalTagArray tags, Typeface typeface, double fontSize, TerminalColourHelper colourHelper)
            {
                _tags = tags;
                _typeface = typeface;
                _fontSize = fontSize;
                _colourHelper = colourHelper;
                Length = _tags.Sum(x => x.Text.Length);
            }

            public override TextSpan<CultureSpecificCharacterBufferRange> GetPrecedingText(int textSourceCharacterIndexLimit)
            {
                throw new NotSupportedException();
            }

            public override int GetTextEffectCharacterIndexFromTextSourceCharacterIndex(int textSourceCharacterIndex)
            {
                throw new NotSupportedException();
            }

            public override TextRun GetTextRun(int textSourceCharacterIndex)
            {
                if (textSourceCharacterIndex >= Length)
                {
                    return new TextEndOfParagraph(1);
                }

                int chIndex = 0;
                foreach (var tag in _tags)
                {
                    int tagLength = tag.Text.Length;
                    if (chIndex + tagLength > textSourceCharacterIndex)
                    {
                        string text = tag.Text.Substring(textSourceCharacterIndex - chIndex);
                        var properties = GetPropertiesForAttributes(tag.Attributes);
                        var textRun = new TextCharacters(text, properties);
                        return textRun;
                    }
                    chIndex += tagLength;
                }

                throw new Exception("Unable to find a tag for given index.");
            }

            private TextRunProperties GetPropertiesForAttributes(CharAttributes attributes)
            {
                Typeface typeface = _typeface;
                Brush background = _colourHelper.GetBackgroundBrush(attributes.BackgroundColour);
                Brush foreground = _colourHelper.GetForegroundBrush(attributes.ForegroundColour);

                if ((attributes.Flags & 1) != 0)
                {
                    // TODO optimise typeface forks
                    typeface = new Typeface(typeface.FontFamily, typeface.Style, FontWeights.Bold, _typeface.Stretch);
                }

                return new TagTextRunProperties(typeface, _fontSize, foreground, background);
            }
        }

        private class TagTextRunProperties : TextRunProperties
        {
            public override Typeface Typeface { get; }
            public override double FontRenderingEmSize { get; }
            public override double FontHintingEmSize { get; }
            public override TextDecorationCollection TextDecorations { get; }
            public override Brush ForegroundBrush { get; }
            public override Brush BackgroundBrush { get; }
            public override CultureInfo CultureInfo { get; }
            public override TextEffectCollection TextEffects { get; }

            public TagTextRunProperties(Typeface typeface, double fontSize, Brush foreground, Brush background)
            {
                Typeface = typeface;
                FontRenderingEmSize = fontSize;
                FontHintingEmSize = fontSize;
                TextDecorations = new TextDecorationCollection();
                ForegroundBrush = foreground;
                BackgroundBrush = background;
                CultureInfo = CultureInfo.CurrentUICulture;
                TextEffects = new TextEffectCollection();
            }
        }

        private class TagTextParagraphProperties : TextParagraphProperties
        {
            public TagTextParagraphProperties(Typeface typeface, double fontSize)
            {
                DefaultTextRunProperties = new TagTextRunProperties(typeface, fontSize, null, null);
            }

            public override FlowDirection FlowDirection { get; }
            public override TextAlignment TextAlignment { get; }
            public override double LineHeight { get; }
            public override bool FirstLineInParagraph { get; }
            public override TextRunProperties DefaultTextRunProperties { get; }
            public override TextWrapping TextWrapping { get; } = TextWrapping.NoWrap;
            public override TextMarkerProperties TextMarkerProperties { get; }
            public override double Indent { get; }
        }
    }
}
