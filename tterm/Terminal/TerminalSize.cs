﻿using System;

namespace tterm.Terminal
{
    /// <summary>
    /// Represents a terminal size in columns and rows.
    /// </summary>
    public struct TerminalSize : IEquatable<TerminalSize>
    {
        public int Columns { get; }
        public int Rows { get; }

        public TerminalSize(int columns, int rows)
        {
            Columns = columns;
            Rows = rows;
        }

        public bool Equals(TerminalSize other)
        {
            return base.Equals(other);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Columns} x {Rows}";
        }

        public static bool operator ==(TerminalSize a, TerminalSize b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(TerminalSize a, TerminalSize b)
        {
            return !a.Equals(b);
        }
    }
}
