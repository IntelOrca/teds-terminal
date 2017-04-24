using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Newtonsoft.Json;
using tterm.Extensions;
using static tterm.Native.Win32;

namespace tterm
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public string AssemblyPath { get; } = Assembly.GetEntryAssembly().Location;
        public string AssemblyDirectory { get; } = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        protected override void OnStartup(StartupEventArgs e)
        {
            string[] args = e.Args;
            int sessionPid = GetTTERMSessionPid();
            if (sessionPid != 0 || args.Contains("--fork"))
            {
                Fork(sessionPid);
                Shutdown();
            }
        }

        private int GetTTERMSessionPid()
        {
            string ttermValue = Environment.GetEnvironmentVariable(EnvironmentVariables.TTERM);
            int.TryParse(ttermValue, out int pid);
            return pid;
        }

        private void Fork(int sessionPid)
        {
            Process callProcess = null;
            if (sessionPid != 0)
            {
                callProcess = Process.GetProcessById(sessionPid);
            }
            if (callProcess != null)
            {
                var currentProcess = Process.GetCurrentProcess();
                int currentPID = currentProcess.Id;
                callProcess = Process
                    .GetProcessesByName("tterm")
                    .Where(x => x.Id != currentPID)
                    .FirstOrDefault();
            }
            if (callProcess != null)
            {
                IntPtr hwnd = callProcess.MainWindowHandle;

                string forkData = GetForkData();
                IntPtr forkDataPtr = IntPtr.Zero;
                IntPtr dataPtr = IntPtr.Zero;
                try
                {
                    forkDataPtr = Marshal.StringToHGlobalUni(forkData);
                    dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<COPYDATASTRUCT>());
                    var data = new COPYDATASTRUCT()
                    {
                        dwData = new IntPtr(1),
                        cbData = forkData.Length * 2 + 2,
                        lpData = forkDataPtr
                    };
                    Marshal.StructureToPtr(data, dataPtr, false);
                    SendMessage(hwnd, WM_COPYDATA, IntPtr.Zero, dataPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(dataPtr);
                    Marshal.FreeHGlobal(forkDataPtr);
                }
            }
        }

        private string GetForkData()
        {
            var forkData = new ForkData()
            {
                CurrentWorkingDirectory = Environment.CurrentDirectory,
                Environment = Environment.GetEnvironmentVariables().ToGeneric<string, string>()
            };
            return JsonConvert.SerializeObject(forkData);
        }

        public void StartNewInstance()
        {
            Process.Start(AssemblyPath);
        }
    }
}
