using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tterm.Ansi;

namespace tterm.Terminal
{
    public class TerminalBuffer
    {
        private char[] _buffer;
        private CharAttributes[] _bufferAttributes;

        public int Rows { get; private set; }
        public int Columns { get; private set; }

        public int CursorX { get; set; }
        public int CursorY { get; set; }
        public CharAttributes CurrentCharAttributes { get; set; }

        public TerminalBuffer()
        {
            Initialise(32, 80);
        }

        private void Initialise(int rows, int columns)
        {
            _buffer = new char[rows * columns];
            _bufferAttributes = new CharAttributes[_buffer.Length];
            Rows = rows;
            Columns = columns;
            CursorX = 0;
            CursorY = 0;
            Clear();
        }

        public void SetSize(int rows, int columns)
        {
            if (rows != Rows || Columns != columns)
            {
                var newBuffer = new char[rows * columns];
                var newBufferAttributes = new CharAttributes[newBuffer.Length];
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        if (IsInBuffer(x, y))
                        {
                            int srcIndex = GetBufferIndex(x, y);
                            int dstIndex = x + (y * columns);
                            newBuffer[dstIndex] = _buffer[srcIndex];
                            newBufferAttributes[dstIndex] = _bufferAttributes[srcIndex];
                        }
                    }
                }
                _buffer = newBuffer;
                _bufferAttributes = newBufferAttributes;
                Rows = rows;
                Columns = columns;
            }
        }

        public void Clear()
        {
            ClearBlock(0, 0, Columns - 1, Rows - 1);
        }

        public void ClearBlock(int left, int top, int right, int bottom)
        {
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    _buffer[GetBufferIndex(x, y)] = ' ';
                }
            }
        }

        public void Type(char c)
        {
            if (IsInBuffer(CursorX, CursorY))
            {
                _buffer[GetBufferIndex(CursorX, CursorY)] = c;
                CursorX++;
            }
        }

        internal void Type(string text)
        {
            foreach (char c in text)
            {
                if (IsInBuffer(CursorX, CursorY))
                {
                    int index = GetBufferIndex(CursorX, CursorY);
                    _buffer[index] = c;
                    _bufferAttributes[index] = CurrentCharAttributes;
                    CursorX++;
                }
            }
        }

        public void ShiftUp()
        {
            for (int y = 0; y < Rows - 1; y++)
            {
                Array.Copy(_buffer, (y + 1) * Columns, _buffer, y * Columns, Columns);
                Array.Copy(_bufferAttributes, (y + 1) * Columns, _bufferAttributes, y * Columns, Columns);
            }
        }

        public string GetLine(int y)
        {
            string line = new string(_buffer, GetBufferIndex(0, y), Columns);
            return line;
        }

        public IList<(string Text, CharAttributes Attributes)> GetFormattedLine(int y)
        {
            var buffer = _buffer;
            var bufferAttributes = _bufferAttributes;
            int startIndex = GetBufferIndex(0, y);
            int endIndex = startIndex + Columns;

            var tags = new List<(string, CharAttributes)>();

            // Group sequentially by attribute
            var currentTagStartIndex = startIndex;
            var currentTagAttribute = bufferAttributes[startIndex];
            for (int i = startIndex + 1; i < endIndex; i++)
            {
                var attr = bufferAttributes[i];
                if (attr != currentTagAttribute)
                {
                    string tagText = new string(buffer, currentTagStartIndex, i - currentTagStartIndex);
                    tags.Add((tagText, currentTagAttribute));

                    currentTagStartIndex = i;
                    currentTagAttribute = attr;
                }
            }

            // Last tag
            {
                string tagText = new string(buffer, currentTagStartIndex, endIndex - currentTagStartIndex);
                tags.Add((tagText, currentTagAttribute));
            }
            return tags;
        }

        public bool IsInBuffer(int x, int y)
        {
            return (x >= 0 && x < Columns && y >= 0 && y < Rows);
        }

        private int GetBufferIndex(int x, int y)
        {
            return x + (y * Columns);
        }
    }
}
