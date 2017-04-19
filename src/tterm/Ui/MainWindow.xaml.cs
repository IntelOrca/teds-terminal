using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MahApps.Metro.IconPacks;
using tterm.Extensions;
using tterm.Terminal;
using tterm.Ui.Models;

namespace tterm.Ui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : EnhancedWindow
    {
        private const int MinColumns = 52;
        private const int MinRows = 4;
        private const int ReadyDelay = 1000;

        private int _tickInitialised;
        private bool _ready;

        private ConfigurationService _configService = new ConfigurationService();
        private Size? _charBufferSize;
        private readonly ObservableCollection<TabDataItem> _leftTabs = new ObservableCollection<TabDataItem>();
        private readonly List<TabDataItem> _rightTabs = new List<TabDataItem>();

        private TerminalSessionManager _sessionMgr = new TerminalSessionManager();
        private TerminalSession _currentSession;
        private TabDataItem _currentTab;
        private TerminalSize _terminalSize;
        private Profile _defaultProfile;

        public bool Ready
        {
            get
            {
                // HACK Try and find a more reliable way to check if we are ready.
                //      This is to prevent the resize hint from showing at startup.
                if (!_ready)
                {
                    _ready = Environment.TickCount > _tickInitialised + ReadyDelay;
                }
                return _ready;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            var config = _configService.Load();
            if (config.AllowTransparancy)
            {
                AllowsTransparency = true;
            }

            resizeHint.Visibility = Visibility.Hidden;

            tabBarLeft.DataContext = _leftTabs;
            tabBarRight.DataContext = _rightTabs;

            var newSessionTab = new TabDataItem()
            {
                Image = PackIconMaterialKind.Plus
            };
            newSessionTab.Click += NewSessionTab_Click;
            _leftTabs.Add(newSessionTab);
            _rightTabs.Add(new TabDataItem()
            {
                Image = PackIconMaterialKind.Settings
            });
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            _tickInitialised = Environment.TickCount;
        }

        private void NewSessionTab_Click(object sender, EventArgs e)
        {
            CreateSession(_defaultProfile);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            StartConsole();
        }

        private void StartConsole()
        {
            var config = _configService.Config;

            int columns = Math.Max(config.Columns, MinColumns);
            int rows = Math.Max(config.Rows, MinRows);
            _terminalSize = new TerminalSize(columns, rows);
            RenderSize = GetWindowSizeForBufferSize(_terminalSize);

            GetWindowSizeSnap(new Size(Width, Height));

            Profile profile = config.Profile;
            if (profile == null)
            {
                profile = DefaultProfile.Get();
            }
            _defaultProfile = ExpandVariables(profile);
            CreateSession(_defaultProfile);
        }

        private void CreateSession(Profile profile)
        {
            var session = _sessionMgr.CreateSession(_terminalSize, profile);
            session.TitleChanged += OnSessionTitleChanged;
            session.Finished += OnSessionFinished;

            TabDataItem tab = new TabDataItem()
            {
                IsActive = true,
                Title = string.Empty,
                Session = session
            };
            tab.Click += OnSessionTabClick;
            _leftTabs.Insert(_leftTabs.Count - 1, tab);

            ChangeSession(session, tab);
        }

        private void OnSessionTabClick(object sender, EventArgs e)
        {
            var tab = sender as TabDataItem;
            var session = tab.Session;
            ChangeSession(session, tab);
        }

        private void ChangeSession(TerminalSession session, TabDataItem tab)
        {
            if (session != _currentSession)
            {
                if (_currentSession != null)
                {
                    _currentSession.Active = false;
                    _currentTab.IsActive = false;
                }

                _currentSession = session;
                _currentTab = tab;

                if (session != null)
                {
                    session.Active = true;
                    session.Size = _terminalSize;
                    tab.IsActive = true;
                }

                txtConsole.Session = session;
                txtConsole.Focus();
            }
        }

        private void OnSessionTitleChanged(object sender, EventArgs e)
        {
            var session = sender as TerminalSession;
            int index = _sessionMgr.Sessions.IndexOf(session);
            _leftTabs[index].Title = _currentSession.Title;
        }

        private void OnSessionFinished(object sender, EventArgs e)
        {
            var session = sender as TerminalSession;
            int index = _leftTabs.IndexOf(x => x.Session == session);
            _leftTabs.RemoveAt(index);

            var sessions = _sessionMgr.Sessions;
            if (sessions.Count > 0)
            {
                int fallbackIndex = Math.Max(0, index - 1);
                ChangeSession(sessions[fallbackIndex], _leftTabs[fallbackIndex]);
            }
            else
            {
                ChangeSession(null, null);
            }
        }

        private static Profile ExpandVariables(Profile profile)
        {
            return new Profile()
            {
                Command = ExpandVariables(profile.Command),
                CurrentWorkingDirectory = ExpandVariables(profile.CurrentWorkingDirectory),
                Arguments = profile.Arguments?.Select(x => ExpandVariables(x)).ToArray()
            };
        }

        private static string ExpandVariables(string s)
        {
            var sb = new StringBuilder();
            int index = 0;
            for (;;)
            {
                int start = s.IndexOf('%', index);
                if (start != -1)
                {
                    int end = s.IndexOf('%', start + 1);
                    if (end != -1)
                    {
                        string varName = s.Substring(start + 1, end - start - 1);
                        string varValue = Environment.GetEnvironmentVariable(varName);

                        sb.Append(s.Substring(index, start - index));
                        sb.Append(varValue);

                        index = end + 1;
                        continue;
                    }
                }
                sb.Append(s.Substring(index));
                break;
            }
            return sb.ToString();
        }

        private void TextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    if (AllowsTransparency)
                    {
                        var terminal = txtConsole;
                        const double OpacityDelta = 1 / 32.0;
                        if (e.Delta > 0)
                        {
                            Opacity = Math.Min(Opacity + OpacityDelta, 1);
                        }
                        else
                        {
                            Opacity = Math.Max(Opacity - OpacityDelta, 0.25);
                        }
                        e.Handled = true;
                    }
                }
                else
                {
                    var terminal = txtConsole;
                    const double FontSizeDelta = 2;
                    if (e.Delta > 0)
                    {
                        if (terminal.FontSize < 54)
                        {
                            terminal.FontSize += FontSizeDelta;
                            _charBufferSize = null;
                            FixWindowSize();
                        }
                    }
                    else
                    {
                        if (terminal.FontSize > 8)
                        {
                            terminal.FontSize -= FontSizeDelta;
                            _charBufferSize = null;
                            FixWindowSize();
                        }
                    }
                    e.Handled = true;
                }
            }
        }

        private void FixWindowSize()
        {
            Size fixedSize = GetWindowSizeForBufferSize(_currentSession.Size);
            Width = fixedSize.Width;
            Height = fixedSize.Height;
        }

        private Size GetWindowSizeSnap(Size size)
        {
            var tsize = GetBufferSizeForWindowSize(size);
            _terminalSize = tsize;
            if (_currentSession != null)
            {
                _currentSession.Size = tsize;
            }
            resizeHint.Hint = tsize;

            _configService.Config.Columns = tsize.Columns;
            _configService.Config.Rows = tsize.Rows;
            _configService.Save();

            return GetWindowSizeForBufferSize(tsize);
        }

        private TerminalSize GetBufferSizeForWindowSize(Size size)
        {
            Size charSize = GetBufferCharSize();
            Size consoleOffset = new Size(Math.Max(Width - txtConsole.ActualWidth, 0),
                                          Math.Max(Height - txtConsole.ActualHeight, 0));
            Size newConsoleSize = new Size(Math.Max(size.Width - consoleOffset.Width, 0),
                                           Math.Max(size.Height - consoleOffset.Height, 0));

            int columns = (int)Math.Round(newConsoleSize.Width / charSize.Width);
            int rows = (int)Math.Round(newConsoleSize.Height / charSize.Height);

            columns = Math.Max(columns, MinColumns);
            rows = Math.Max(rows, MinRows);

            return new TerminalSize(columns, rows);
        }

        private Size GetWindowSizeForBufferSize(TerminalSize size)
        {
            Size charSize = GetBufferCharSize();
            Size consoleOffset = new Size(Math.Max(Width - txtConsole.ActualWidth, 0),
                                          Math.Max(Height - txtConsole.ActualHeight, 0));
            Size snappedConsoleSize = new Size(size.Columns * charSize.Width,
                                               size.Rows * charSize.Height);

            Size result = new Size(Math.Ceiling(snappedConsoleSize.Width + consoleOffset.Width) + 2,
                                   Math.Ceiling(snappedConsoleSize.Height + consoleOffset.Height));
            return result;
        }

        private Size GetBufferCharSize()
        {
            if (!_charBufferSize.HasValue)
            {
                _charBufferSize = MeasureString(" ");
            }
            return _charBufferSize.Value;
        }

        private Size MeasureString(string candidate)
        {
            var typeface = new Typeface(txtConsole.FontFamily, txtConsole.FontStyle, txtConsole.FontWeight, txtConsole.FontStretch);
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                txtConsole.FontSize,
                Brushes.Black,
                Dpi.PixelsPerDip);

            var result = new Size(formattedText.WidthIncludingTrailingWhitespace, formattedText.Height);
            Debug.Assert(result.Width > 0);
            Debug.Assert(result.Height > 0);
            return result;
        }

        protected override Size GetPreferedSize(Size size)
        {
            return GetWindowSizeSnap(size);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            // Reize the current session buffer
            var tsize = GetBufferSizeForWindowSize(sizeInfo.NewSize);
            _terminalSize = tsize;
            if (_currentSession != null)
            {
                _currentSession.Size = tsize;
            }

            if (Ready)
            {
                // Save configuration
                _configService.Config.Columns = tsize.Columns;
                _configService.Config.Rows = tsize.Rows;
                _configService.Save();

                // Update hint overlay
                resizeHint.Hint = tsize;
                resizeHint.IsShowing = true;
                resizeHint.IsShowing = IsResizing;
            }
        }

        protected override void OnResizeEnded()
        {
            resizeHint.IsShowing = false;
        }
    }
}
