using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tterm.Ansi
{
    public struct CharAttributes : IEquatable<CharAttributes>
    {
        public int Flags { get; set; }
        public int BackgroundColour { get; set; }
        public int ForegroundColour { get; set; }

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
