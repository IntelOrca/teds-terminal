namespace tterm.Terminal
{
    internal class TerminalSelection
    {
        public SelectionMode Mode { get; }
        public TerminalPoint Start { get; }
        public TerminalPoint End { get; }

        public TerminalSelection(SelectionMode mode, TerminalPoint start, TerminalPoint end)
        {
            Mode = mode;
            Start = start;
            End = end;
        }
    }
}
