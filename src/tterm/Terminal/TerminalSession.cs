using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using tterm.Ansi;
using tterm.Utility;

namespace tterm.Terminal
{
    internal class TerminalSession : IDisposable
    {
        private readonly WinPty _pty;
        private readonly StreamWriter _ptyWriter;
        private readonly object _bufferSync = new object();
        private bool _disposed;

        public event EventHandler TitleChanged;
        public event EventHandler OutputReceived;
        public event EventHandler BufferSizeChanged;
        public event EventHandler Finished;

        public string Title { get; set; }
        public bool Active { get; set; }
        public bool Connected { get; private set; }
        public bool ErrorOccured { get; private set; }
        public Exception Exception { get; private set; }

        public TerminalBuffer Buffer { get; }

        public TerminalSize Size
        {
            get => Buffer.Size;
            set
            {
                if (Buffer.Size != value)
                {
                    _pty.Size = value;
                    Buffer.Size = value;
                    BufferSizeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public TerminalSession(TerminalSize size, Profile profile)
        {
            Buffer = new TerminalBuffer(size);
            _pty = new WinPty(profile, size);
            _ptyWriter = new StreamWriter(_pty.StandardInput, Encoding.UTF8)
            {
                AutoFlush = true
            };
            RunOutputLoop();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _pty.Dispose();
            }
        }

        private async void RunOutputLoop()
        {
            try
            {
                await ConsoleOutputAsync(_pty.StandardOutput);
            }
            catch (Exception ex)
            {
                SessionErrored(ex);
                return;
            }
            SessionClosed();
        }

        private Task ConsoleOutputAsync(Stream stream)
        {
            return Task.Run(async delegate
            {
                var ansiParser = new AnsiParser();
                var sr = new StreamReader(stream, Encoding.UTF8);
                do
                {
                    int offset = 0;
                    var buffer = new char[1024];
                    int readChars = await sr.ReadAsync(buffer, offset, buffer.Length - offset);
                    if (readChars > 0)
                    {
                        var reader = new ArrayReader<char>(buffer, 0, readChars);
                        var codes = ansiParser.Parse(reader);
                        ReceiveOutput(codes);
                    }
                }
                while (!sr.EndOfStream);
            });
        }

        private void ReceiveOutput(IEnumerable<TerminalCode> codes)
        {
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
                case TerminalCodeType.ResetMode:
                    Buffer.ShowCursor = false;
                    break;
                case TerminalCodeType.SetMode:
                    Buffer.ShowCursor = true;

                    // HACK We want clear to reset the window position but not general typing.
                    //      We therefore reset the window only if the cursor is moved to the top.
                    if (Buffer.CursorY == 0)
                    {
                        Buffer.WindowTop = 0;
                    }
                    break;
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

        private void SessionErrored(Exception ex)
        {
            Connected = false;
            Exception = ex;
            Finished?.Invoke(this, EventArgs.Empty);
        }

        private void SessionClosed()
        {
            _pty.Dispose();

            Connected = false;
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }
}
