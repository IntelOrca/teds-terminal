using tterm.Ansi;

namespace tterm.Terminal
{
    public struct TerminalTag
    {
        public string Text { get; }
        public CharAttributes Attributes { get; }

        public TerminalTag(string text, CharAttributes attributes)
        {
            Text = text;
            Attributes = attributes;
        }

        public TerminalTag Substring(int index)
        {
            return new TerminalTag(Text.Substring(index), Attributes);
        }

        public TerminalTag Substring(int index, int length)
        {
            return new TerminalTag(Text.Substring(index, length), Attributes);
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
