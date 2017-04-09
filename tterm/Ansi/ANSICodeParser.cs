using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tterm.Utility;

namespace tterm.Ansi
{
    internal class ANSICodeParser
    {
        public ANSICode? Parse(ArraySegment<char> buffer, out int codeLength)
        {
            int num1, num2;

            codeLength = 0;
            ANSICode? result = null;
            var reader = new ArrayReader<char>(buffer);
            if (reader.TryRead(out char c) && c == ANSICode.ESC)
            {
                if (reader.TryRead(out c))
                {
                    if (c == '[')
                    {
                        if (TryReadNumber(reader, out num1))
                        {
                            if (reader.TryRead(out c))
                            {
                                switch (c)
                                {
                                case ';':
                                    if (TryReadNumber(reader, out num2))
                                    {
                                        if (reader.TryRead(out c) && (c == 'H' || c == 'f'))
                                        {
                                            result = new ANSICode(ANSICodeType.SetCursorPosition, num1, num2);
                                        }
                                    }
                                    break;
                                case 'm':
                                    result = new ANSICode(ANSICodeType.SetGraphicsMode);
                                    break;
                                case 'K':
                                    result = new ANSICode(ANSICodeType.EraseLine);
                                    break;
                                case 'A':
                                    result = new ANSICode(ANSICodeType.CursorUp, num1, 0);
                                    break;
                                case 'G':
                                    result = new ANSICode(ANSICodeType.MoveToColumn, 0, num1 - 1);
                                    break;
                                case 'J':
                                    result = new ANSICode(ANSICodeType.EraseDisplay);
                                    break;
                                default:
                                    codeLength--;
                                    break;
                                }
                            }
                        }
                        else if (reader.TryRead(out c))
                        {
                            if (c == '?')
                            {
                                if (TryReadNumber(reader, out num1))
                                {
                                    if (reader.TryRead(out c))
                                    {
                                        if (c == 'h' || c == 'l')
                                        {
                                            result = new ANSICode(ANSICodeType.SetMode);
                                        }
                                        else
                                        {
                                            codeLength--;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                codeLength--;
                            }
                        }
                    }
                    else if (c == ']')
                    {
                        if (TryReadNumber(reader, out num1))
                        {
                            if (num1 == 0)
                            {
                                if (reader.TryRead(out c))
                                {
                                    if (c == ';')
                                    {
                                        string title = "";
                                        while (reader.TryRead(out c) && c != '\a')
                                        {
                                            title += c;
                                        }
                                        result = new ANSICode(ANSICodeType.Title, title);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            codeLength += buffer.Count - reader.RemainingLength;
            return result;
        }

        private bool TryReadNumber(ArrayReader<char> reader, out int value)
        {
            value = 0;
            string digits = "";
            while (reader.TryPeek(out char c) && Char.IsNumber(c))
            {
                digits += reader.Read();
                value = int.Parse(digits);
            }
            return digits.Length != 0;
        }
    }
}
