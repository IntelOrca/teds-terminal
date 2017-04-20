using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Windows;
using tterm.Ansi;

namespace tterm.Terminal
{
    internal class TerminalBuffer
    {
        private const int MaxHistorySize = 1024;

        private TerminalBufferChar[] _buffer;
        private TerminalSize _size;
        private readonly List<TerminalBufferLine> _history = new List<TerminalBufferLine>();

        public bool ShowCursor { get; set; }
        public int CursorX { get; set; }
        public int CursorY { get; set; }
        public CharAttributes CurrentCharAttributes { get; set; }
        public TerminalSelection Selection { get; set; }

        public TerminalBuffer(TerminalSize size)
        {
            Initialise(size);
        }

        private void Initialise(TerminalSize size)
        {
            _buffer = new TerminalBufferChar[size.Rows * size.Columns];
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
                    var srcSize = _size;
                    var dstSize = value;

                    var srcBuffer = _buffer;
                    var dstBuffer = new TerminalBufferChar[dstSize.Rows * dstSize.Columns];

                    int srcLeft = 0;
                    int srcRight = Math.Min(srcSize.Columns, dstSize.Columns) - 1;
                    int srcTop = Math.Max(0, CursorY - dstSize.Rows + 1);
                    int srcBottom = srcTop + Math.Min(srcSize.Rows, dstSize.Rows) - 1;
                    int dstLeft = 0;
                    int dstTop = 0;

                    CopyBufferToBuffer(srcBuffer, srcSize, srcLeft, srcTop, srcRight, srcBottom,
                                       dstBuffer, dstSize, dstLeft, dstTop);

                    _buffer = dstBuffer;
                    _size = dstSize;

                    CursorY = Math.Min(CursorY, _size.Rows - 1);
                }
            }
        }

        public void Clear()
        {
            ClearBlock(0, 0, _size.Columns - 1, _size.Rows - 1);
        }

        public void ClearBlock(int left, int top, int right, int bottom)
        {
            left = Math.Max(0, left);
            top = Math.Max(0, top);
            right = Math.Min(right, _size.Columns - 1);
            bottom = Math.Min(bottom, _size.Rows - 1);

            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    int index = GetBufferIndex(x, y);
                    _buffer[index] = new TerminalBufferChar(' ', default(CharAttributes));
                }
            }
        }

        public void Type(char c)
        {
            if (IsInBuffer(CursorX, CursorY))
            {
                int index = GetBufferIndex(CursorX, CursorY);
                _buffer[index] = new TerminalBufferChar(c, CurrentCharAttributes);
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
                    _buffer[index] = new TerminalBufferChar(c, CurrentCharAttributes);
                    CursorX++;
                }
            }
        }

        public void ShiftUp()
        {
            var topLine = GetBufferLine(0);
            AddHistory(topLine);

            for (int y = 0; y < _size.Rows - 1; y++)
            {
                Array.Copy(_buffer, (y + 1) * _size.Columns, _buffer, y * _size.Columns, _size.Columns);
            }
        }

        public string GetText(int y)
        {
            return GetText(0, y, _size.Columns);
        }

        public string[] GetText(int left, int top, int right, int bottom)
        {
            int width = right - left + 1;
            int height = bottom - top + 1;
            string[] result = new string[height];
            for (int i = 0; i < height; i++)
            {
                result[i] = GetText(left, top + i, width);
            }
            return result;
        }

        public string GetText(int x, int y, int length)
        {
            if (x + length > _size.Columns)
            {
                throw new ArgumentException("Range overflows line length.", nameof(length));
            }

            int index = GetBufferIndex(x, y);
            string line = GetTextAtIndex(index, length);
            return line;
        }

        public string GetTextAtIndex(int index, int length)
        {
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = _buffer[index + i].Char;
            }
            string line = new string(chars);
            return line;
        }

        public string[] GetSelectionText()
        {
            var selection = Selection;
            int left = Math.Min(selection.Start.Column, selection.End.Column);
            int right = Math.Max(selection.Start.Column, selection.End.Column);
            int top = Math.Min(selection.Start.Row, selection.End.Row);
            int bottom = Math.Max(selection.Start.Row, selection.End.Row);
            return GetText(left, top, right, bottom);
        }

        public void CopySelection()
        {
            string[] selectionText = GetSelectionText();
            string text = String.Join(Environment.NewLine, selectionText);
            var dataObject = new DataObject();
            dataObject.SetText(text);
            dataObject.SetData("MSDEVColumnSelect", true);
            Clipboard.SetDataObject(dataObject, copy: true);
        }

        public TerminalBufferLine GetBufferLine(int y)
        {
            int startIndex = GetBufferIndex(0, y);
            var bufferLine = new TerminalBufferLine(_buffer, startIndex, _size.Columns);
            return bufferLine;
        }

        public TerminalTagArray GetFormattedLine(int y)
        {
            var buffer = _buffer;
            int startIndex = GetBufferIndex(0, y);
            int endIndex = startIndex + _size.Columns;

            var tags = ImmutableArray.CreateBuilder<TerminalTag>(initialCapacity: 8);

            // Group sequentially by attribute
            var currentTagStartIndex = startIndex;
            var currentTagAttribute = GetAttributesAt(0, y, startIndex);
            for (int i = startIndex + 1; i < endIndex; i++)
            {
                int x = i - startIndex;
                char c = buffer[i].Char;
                var attr = GetAttributesAt(x, y, i);
                if (!CanContinueTag(currentTagAttribute, attr, c))
                {
                    string tagText = GetTextAtIndex(currentTagStartIndex, i - currentTagStartIndex);
                    tags.Add(new TerminalTag(tagText, currentTagAttribute));

                    currentTagStartIndex = i;
                    currentTagAttribute = attr;
                }
            }

            // Last tag
            {
                string tagText = GetTextAtIndex(currentTagStartIndex, endIndex - currentTagStartIndex);
                tags.Add(new TerminalTag(tagText, currentTagAttribute));
            }
            return new TerminalTagArray(tags.ToImmutable());
        }

        private CharAttributes GetAttributesAt(int x, int y, int index)
        {
            CharAttributes attr = _buffer[index].Attributes;
            if (ShowCursor && x == CursorX && y == CursorY)
            {
                attr.BackgroundColour = 15;
            }
            else if (IsPointInSelection(x, y))
            {
                attr.BackgroundColour = 8;
            }
            return attr;
        }

        private bool IsPointInSelection(int x, int y)
        {
            var selection = Selection;
            if (selection == null)
            {
                return false;
            }
            else
            {
                int left = Math.Min(selection.Start.Column, selection.End.Column);
                int right = Math.Max(selection.Start.Column, selection.End.Column);
                int top = Math.Min(selection.Start.Row, selection.End.Row);
                int bottom = Math.Max(selection.Start.Row, selection.End.Row);
                return (x >= left && x <= right &&
                        y >= top && y <= bottom);
            }
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

        private TerminalPoint GetBufferPoint(int index)
        {
            return new TerminalPoint(index % _size.Columns, index % _size.Columns);
        }

        private void AddHistory(TerminalBufferLine line)
        {
            if (_history.Count >= MaxHistorySize)
            {
                _history.RemoveAt(0);
            }
            _history.Add(line);
        }

        private static void CopyBufferToBuffer(TerminalBufferChar[] srcBuffer, TerminalSize srcSize, int srcLeft, int srcTop, int srcRight, int srcBottom,
                                               TerminalBufferChar[] dstBuffer, TerminalSize dstSize, int dstLeft, int dstTop)
        {
            int cols = srcRight - srcLeft + 1;
            int rows = srcBottom - srcTop + 1;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int srcX = srcLeft + x;
                    int srcY = srcTop + y;
                    int dstX = dstLeft + x;
                    int dstY = dstTop + y;
                    int srcIndex = srcX + (srcY * srcSize.Columns);
                    int dstIndex = dstX + (dstY * dstSize.Columns);
                    dstBuffer[dstIndex] = srcBuffer[srcIndex];
                }
            }
        }
    }
}
