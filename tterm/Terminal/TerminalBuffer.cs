using System;
using System.Collections.Immutable;
using tterm.Ansi;

namespace tterm.Terminal
{
    public class TerminalBuffer
    {
        private char[] _buffer;
        private CharAttributes[] _bufferAttributes;
        private TerminalSize _size;

        public int CursorX { get; set; }
        public int CursorY { get; set; }
        public CharAttributes CurrentCharAttributes { get; set; }

        public TerminalBuffer(TerminalSize size)
        {
            Initialise(size);
        }

        private void Initialise(TerminalSize size)
        {
            _buffer = new char[size.Rows * size.Columns];
            _bufferAttributes = new CharAttributes[_buffer.Length];
            _size = size;
            CursorX = 0;
            CursorY = 0;
            Clear();
        }

        public TerminalSize Size
        {
            get => _size;
            set
            {
                if (value != _size)
                {
                    var newBuffer = new char[value.Rows * value.Columns];
                    var newBufferAttributes = new CharAttributes[newBuffer.Length];
                    for (int y = 0; y < value.Rows; y++)
                    {
                        for (int x = 0; x < value.Columns; x++)
                        {
                            if (IsInBuffer(x, y))
                            {
                                int srcIndex = GetBufferIndex(x, y);
                                int dstIndex = x + (y * value.Columns);
                                newBuffer[dstIndex] = _buffer[srcIndex];
                                newBufferAttributes[dstIndex] = _bufferAttributes[srcIndex];
                            }
                        }
                    }
                    _buffer = newBuffer;
                    _bufferAttributes = newBufferAttributes;
                    _size = value;
                }
            }
        }

        public void Clear()
        {
            ClearBlock(0, 0, _size.Columns - 1, _size.Rows - 1);
        }

        public void ClearBlock(int left, int top, int right, int bottom)
        {
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    int index = GetBufferIndex(x, y);
                    _buffer[index] = ' ';
                    _bufferAttributes[index] = default(CharAttributes);
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
            for (int y = 0; y < _size.Rows - 1; y++)
            {
                Array.Copy(_buffer, (y + 1) * _size.Columns, _buffer, y * _size.Columns, _size.Columns);
                Array.Copy(_bufferAttributes, (y + 1) * _size.Columns, _bufferAttributes, y * _size.Columns, _size.Columns);
            }
        }

        public string GetLine(int y)
        {
            string line = new string(_buffer, GetBufferIndex(0, y), _size.Columns);
            return line;
        }

        public TerminalTagArray GetFormattedLine(int y)
        {
            var buffer = _buffer;
            var bufferAttributes = _bufferAttributes;
            int startIndex = GetBufferIndex(0, y);
            int endIndex = startIndex + _size.Columns;

            var tags = ImmutableArray.CreateBuilder<TerminalTag>(initialCapacity: 8);

            // Group sequentially by attribute
            var currentTagStartIndex = startIndex;
            var currentTagAttribute = bufferAttributes[startIndex];
            for (int i = startIndex + 1; i < endIndex; i++)
            {
                var attr = bufferAttributes[i];
                if (!CanContinueTag(currentTagAttribute, attr, buffer[i]))
                {
                    string tagText = new string(buffer, currentTagStartIndex, i - currentTagStartIndex);
                    tags.Add(new TerminalTag(tagText, currentTagAttribute));

                    currentTagStartIndex = i;
                    currentTagAttribute = attr;
                }
            }

            // Last tag
            {
                string tagText = new string(buffer, currentTagStartIndex, endIndex - currentTagStartIndex);
                tags.Add(new TerminalTag(tagText, currentTagAttribute));
            }
            return new TerminalTagArray(tags.ToImmutable());
        }

        private static bool CanContinueTag(CharAttributes previous, CharAttributes next, char nextC)
        {
            if (nextC == ' ')
            {
                return previous.BackgroundColour == next.BackgroundColour;
            }
            else
            {
                return previous == next;
            }
        }

        public bool IsInBuffer(int x, int y)
        {
            return (x >= 0 && x < _size.Columns && y >= 0 && y < _size.Rows);
        }

        private int GetBufferIndex(int x, int y)
        {
            return x + (y * _size.Columns);
        }
    }
}
