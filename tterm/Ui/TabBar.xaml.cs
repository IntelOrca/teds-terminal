using System.Windows;
using System.Windows.Controls;
using tterm.Ui.Models;

namespace tterm.Ui
{
    public partial class TabBar : UserControl
    {
        public TabBar()
        {
            InitializeComponent();
        }

        private void OnTabClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var tabDataItem = button.DataContext as TabDataItem;
            if (tabDataItem != null)
            {
                tabDataItem.RaiseClickEvent();
            }
        }
    }
}
