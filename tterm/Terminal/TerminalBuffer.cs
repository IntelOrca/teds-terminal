using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tterm.Terminal
{
    internal class TerminalBuffer
    {
        private char[] _buffer;

        public int Rows { get; private set; }
        public int Columns { get; private set; }

        public int CursorX { get; set; }
        public int CursorY { get; set; }

        public TerminalBuffer()
        {
            Initialise(32, 80);
        }

        private void Initialise(int rows, int columns)
        {
            _buffer = new char[rows * columns];
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
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        if (IsInBuffer(x, y))
                        {
                            newBuffer[x + (y * columns)] = _buffer[GetBufferIndex(x, y)];
                        }
                    }
                }
                _buffer = newBuffer;
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

        public void ShiftUp()
        {
            for (int y = 0; y < Rows - 1; y++)
            {
                Array.Copy(_buffer, (y + 1) * Columns, _buffer, y * Columns, Columns);
            }
        }

        public string GetLine(int y)
        {
            string line = new string(_buffer, GetBufferIndex(0, y), Columns);
            return line;
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
