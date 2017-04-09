using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace tterm.Ansi
{
    internal class WinPty : IDisposable
    {
        #region Native API
#pragma warning disable IDE1006 // Naming Styles
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string path);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int winpty_error_code(IntPtr err);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern string winpty_error_msg(IntPtr err);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void winpty_error_free(IntPtr err);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr winpty_config_new(ulong agentFlags, out IntPtr err);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr winpty_open(IntPtr cfg, out IntPtr err);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void winpty_free(IntPtr wp);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr winpty_spawn_config_new(ulong spawnFlags,
                                                             string appname,
                                                             string cmdline,
                                                             string cwd,
                                                             string env,
                                                             out IntPtr err);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void winpty_spawn_config_free(IntPtr cfg);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool winpty_spawn(IntPtr wp,
                                                IntPtr cfg,
                                                out IntPtr process_handle,
                                                out IntPtr thread_handle,
                                                out int create_process_error,
                                                out IntPtr err);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern string winpty_conin_name(IntPtr wp);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern string winpty_conout_name(IntPtr wp);

        [DllImport("winpty.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern string winpty_conerr_name(IntPtr wp);

#pragma warning restore IDE1006 // Naming Styles
        #endregion

        private bool _disposed;
        private IntPtr _handle;

        public Stream StandardInput { get; private set; }
        public Stream StandardOutput { get; private set; }
        public Stream StandardError { get; private set; }

        static WinPty()
        {
            string platform = Environment.Is64BitProcess ? "x64" : "x86";
            string libPath = String.Format(@"winpty\{0}\winpty.dll", platform);
            IntPtr winptyHandle = LoadLibrary(libPath);
            if (winptyHandle == IntPtr.Zero)
            {
                throw new FileNotFoundException("Unable to find " + libPath);
            }
        }

        public WinPty()
        {
            IntPtr err = IntPtr.Zero;
            IntPtr spawnCfg = IntPtr.Zero;
            try
            {
                IntPtr cfg = winpty_config_new(1 | 4, out err);
                _handle = winpty_open(cfg, out err);
                if (err != IntPtr.Zero)
                {
                    throw new WinPtrException(err);
                }

                string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                spawnCfg = winpty_spawn_config_new(1, @"C:\Windows\System32\cmd.exe", null, homePath, null, out err);
                if (err != IntPtr.Zero)
                {
                    throw new WinPtrException(err);
                }

                StandardInput = CreatePipe(winpty_conin_name(_handle), PipeDirection.Out);
                StandardOutput = CreatePipe(winpty_conout_name(_handle), PipeDirection.In);
                StandardError = CreatePipe(winpty_conerr_name(_handle), PipeDirection.In);

                if (!winpty_spawn(_handle, spawnCfg, out IntPtr process, out IntPtr thread, out int procError, out err))
                {
                    throw new WinPtrException(err);
                }
            }
            finally
            {
                winpty_spawn_config_free(spawnCfg);
                winpty_error_free(err);
            }
        }

        ~WinPty()
        {
            if (!_disposed)
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                StandardInput?.Dispose();
                StandardOutput?.Dispose();
                StandardError?.Dispose();
                winpty_free(_handle);

                StandardInput = null;
                StandardOutput = null;
                StandardError = null;
                _handle = IntPtr.Zero;
            }
        }

        private Stream CreatePipe(string pipeName, PipeDirection direction)
        {
            string serverName = ".";
            if (pipeName.StartsWith("\\"))
            {
                int slash3 = pipeName.IndexOf('\\', 2);
                if (slash3 != -1)
                {
                    serverName = pipeName.Substring(2, slash3 - 2);
                }
                int slash4 = pipeName.IndexOf('\\', slash3 + 1);
                if (slash4 != -1)
                {
                    pipeName = pipeName.Substring(slash4 + 1);
                }
            }

            var pipe = new NamedPipeClientStream(serverName, pipeName, direction);
            pipe.Connect();
            return pipe;
        }

        private class WinPtrException : Exception
        {
            public int Code { get; }

            public WinPtrException(IntPtr err)
                : base(winpty_error_msg(err))
            {
                Code = winpty_error_code(err);
            }
        }
    }
}
