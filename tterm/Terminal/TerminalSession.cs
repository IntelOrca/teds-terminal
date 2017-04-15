using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using tterm.Ansi;
using tterm.Extensions;
using tterm.Terminal;
using tterm.Ui.Models;
using tterm.Utility;

namespace tterm.Terminal
{
    internal class TerminalSession
    {
        private readonly WinPty _pty;
        private readonly StreamWriter _ptyWriter;
        private readonly object _bufferSync = new object();

        public event EventHandler TitleChanged;
        public event EventHandler OutputReceived;

        public string Title { get; set; }
        public bool Active { get; set; }

        public TerminalBuffer Buffer { get; }

        public TerminalSize Size
        {
            get => Buffer.Size;
            set
            {
                _pty.Size = value;
                Buffer.Size = value;
            }
        }

        public TerminalSession(TerminalSize size, Profile profile)
        {
            Buffer = new TerminalBuffer(size);
            _pty = new WinPty(profile, size);
            _ptyWriter = new StreamWriter(_pty.StandardInput)
            {
                AutoFlush = true
            };
            ConsoleOutputAsync(_pty.StandardOutput);
        }

        private void ConsoleOutputAsync(Stream stream)
        {
            var sr = new StreamReader(stream);
            Task.Run(async () =>
            {
                try
                {
                    int readChars;
                    do
                    {
                        int offset = 0;
                        var buffer = new char[1024];
                        readChars = await sr.ReadAsync(buffer, offset, buffer.Length - offset);
                        if (readChars > 0)
                        {
                            var segment = new ArraySegment<char>(buffer, 0, readChars);
                            var reader = new ArrayReader<char>(segment);
                            ReceiveOutput(reader);

                            Array.Copy(buffer, reader.Offset, buffer, 0, reader.RemainingLength);
                            offset = reader.RemainingLength;
                        }
                    }
                    while (readChars != 0);
                }
                catch (Exception)
                {
                    throw;
                }
            });
        }

        private void ReceiveOutput(ArrayReader<char> reader)
        {
            var ansiParser = new AnsiParser();
            var codes = ansiParser.Parse(reader);
            lock (_bufferSync)
            {
                foreach (var code in codes)
                {
                    ProcessTerminalCode(code);
                }
            }
            OutputReceived?.Invoke(this, EventArgs.Empty);
        }

        private void ProcessTerminalCode(TerminalCode code)
        {
            switch (code.Type)
            {
                case TerminalCodeType.Text:
                    Buffer.Type(code.Text);
                    break;
                case TerminalCodeType.LineFeed:
                    if (Buffer.CursorY == Buffer.Size.Rows - 1)
                    {
                        Buffer.ShiftUp();
                    }
                    else
                    {
                        Buffer.CursorY++;
                    }
                    break;
                case TerminalCodeType.CarriageReturn:
                    Buffer.CursorX = 0;
                    break;
                case TerminalCodeType.CharAttributes:
                    Buffer.CurrentCharAttributes = code.CharAttributes;
                    break;
                case TerminalCodeType.CursorPosition:
                    Buffer.CursorX = code.Column;
                    Buffer.CursorY = code.Line;
                    break;
                case TerminalCodeType.CursorUp:
                    Buffer.CursorY -= code.Line;
                    break;
                case TerminalCodeType.CursorCharAbsolute:
                    Buffer.CursorX = code.Column;
                    break;
                case TerminalCodeType.EraseInLine:
                    if (code.Line == 0)
                    {
                        Buffer.ClearBlock(Buffer.CursorX, Buffer.CursorY, Buffer.Size.Columns - 1, Buffer.CursorY);
                    }
                    break;
                case TerminalCodeType.EraseInDisplay:
                    Buffer.Clear();
                    Buffer.CursorX = 0;
                    Buffer.CursorY = 0;
                    break;
                case TerminalCodeType.SetTitle:
                    Title = code.Text;
                    TitleChanged?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        public void Write(string text)
        {
            _ptyWriter.Write(text);
        }

        public void Paste()
        {
            string text = Clipboard.GetText();
            if (!String.IsNullOrEmpty(text))
            {
                Write(text);
            }
        }
    }
}
