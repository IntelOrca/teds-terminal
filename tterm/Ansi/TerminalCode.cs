using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tterm.Ansi
{
    internal struct TerminalCode
    {
        public TerminalCodeType Type { get; }
        public int Line { get; }
        public int Column { get; }
        public string Text { get; }

        public TerminalCode(TerminalCodeType type)
        {
            Type = type;
            Line = 0;
            Column = 0;
            Text = null;
        }

        public TerminalCode(TerminalCodeType type, string text)
        {
            Type = type;
            Line = 0;
            Column = 0;
            Text = text;
        }

        public TerminalCode(TerminalCodeType type, int line, int column)
        {
            Type = type;
            Line = line;
            Column = column;
            Text = null;
        }

        public override string ToString()
        {
            if (Type == TerminalCodeType.Text)
            {
                return Text;
            }
            else
            {
                return Type.ToString();
            }
        }
    }
}
