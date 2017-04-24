using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;

namespace tterm.Terminal
{
    internal class TerminalSessionManager
    {
        private readonly List<TerminalSession> _sessions = new List<TerminalSession>();

        public IList<TerminalSession> Sessions
        {
            get => new ReadOnlyCollection<TerminalSession>(_sessions);
        }

        public TerminalSession CreateSession(TerminalSize size, Profile profile)
        {
            PrepareTTermEnvironment(profile);

            var session = new TerminalSession(size, profile);
            session.Finished += OnSessionFinished;

            _sessions.Add(session);
            return session;
        }

        private void PrepareTTermEnvironment(Profile profile)
        {
            var env = profile.EnvironmentVariables;

            // Force our own environment variable so shells know we are in a tterm session
            int pid = Process.GetCurrentProcess().Id;
            env[EnvironmentVariables.TTERM] = pid.ToString();

            // Add assembly directory to PATH so tterm can be launched from the shell
            var app = Application.Current as App;
            env[EnvironmentVariables.PATH] = app.AssemblyDirectory + ";" + env[EnvironmentVariables.PATH];
        }

        private void OnSessionFinished(object sender, EventArgs e)
        {
            var session = sender as TerminalSession;
            _sessions.Remove(session);
        }
    }
}
