using System;

namespace tterm.Terminal
{
    public struct TerminalPoint : IEquatable<TerminalPoint>
    {
        public int Column { get; set; }
        public int Row { get; set; }

        public TerminalPoint(int col, int row)
        {
            Column = col;
            Row = row;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public bool Equals(TerminalPoint other)
        {
            return Column == other.Column &&
                   Row == other.Row;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Column}, {Row}";
        }

        public static bool operator ==(TerminalPoint a, TerminalPoint b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(TerminalPoint a, TerminalPoint b)
        {
            return !a.Equals(b);
        }
    }
}
