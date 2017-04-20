using System.Collections.Immutable;
using System.Linq;
using tterm.Ansi;

namespace tterm.Terminal
{
    /// <summary>
    /// Represents a single immutable line from a terminal buffer.
    /// </summary>
    internal class TerminalBufferLine
    {
        public int Columns { get; }
        public ImmutableArray<TerminalBufferChar> Buffer { get; }

        public TerminalBufferLine(TerminalBufferChar[] buffer, int index, int length)
        {
            Buffer = buffer.Skip(index).Take(length).ToImmutableArray();
            Columns = length;
        }

        public override string ToString()
        {
            return new string(Buffer.Select(x => x.Char).ToArray());
        }
    }
}
