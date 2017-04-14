﻿using System;

namespace tterm.Ansi
{
    public struct CharAttributes : IEquatable<CharAttributes>
    {
        public int Flags { get; set; }
        public int BackgroundColour { get; set; }
        public int ForegroundColour { get; set; }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public bool Equals(CharAttributes other)
        {
            return base.Equals(other);
        }

        public static bool operator ==(CharAttributes a, CharAttributes b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(CharAttributes a, CharAttributes b)
        {
            return !a.Equals(b);
        }
    }
}
