using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using tterm.Terminal;

namespace tterm.Ui
{
    /// <summary>
    /// Interaction logic for ResizeHint.xaml
    /// </summary>
    public partial class ResizeHint : UserControl
    {
        private const double HideDelayTime = 1.0;
        private const double FadeTime = 0.5;

        private TerminalSize _size;

        public ResizeHint()
        {
            InitializeComponent();
        }

        public TerminalSize Hint
        {
            get => _size;
            set
            {
                if (value != _size)
                {
                    _size = value;
                    resizeHintText.Text = string.Format("{0} x {1}", _size.Columns, _size.Rows);
                }
            }
        }

        public bool IsShowing
        {
            get => (Opacity != 0);
            set
            {
                if (value)
                {
                    ShowResizeHint();
                }
                else
                {
                    HideResizeHint();
                }
            }
        }

        private void ShowResizeHint()
        {
            BeginAnimation(Border.OpacityProperty, null);
            Opacity = 1;
            Visibility = Visibility.Visible;
        }

        private void HideResizeHint()
        {
            if (Visibility != Visibility.Hidden)
            {
                var duration = new Duration(TimeSpan.FromSeconds(Opacity * FadeTime));
                var animation = new DoubleAnimation(Opacity, 0, duration)
                {
                    BeginTime = TimeSpan.FromSeconds(HideDelayTime)
                };
                animation.Completed += (s, e) =>
                {
                    if (Opacity == 0)
                    {
                        Visibility = Visibility.Hidden;
                    }
                };
                BeginAnimation(Border.OpacityProperty, animation);
            }
        }
    }
}
