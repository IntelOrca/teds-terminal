using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tterm.Ansi
{
    internal struct ANSICode
    {
        public const char ESC = '\u001B';

        public ANSICodeType Type { get; }
        public int Line { get; }
        public int Column { get; }
        public string Text { get; }

        public ANSICode(ANSICodeType type)
        {
            Type = type;
            Line = 0;
            Column = 0;
            Text = null;
        }

        public ANSICode(ANSICodeType type, string text)
        {
            Type = type;
            Line = 0;
            Column = 0;
            Text = text;
        }

        public ANSICode(ANSICodeType type, int line, int column)
        {
            Type = type;
            Line = line;
            Column = column;
            Text = null;
        }
    }
}
