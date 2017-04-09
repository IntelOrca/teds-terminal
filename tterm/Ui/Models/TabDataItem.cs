using System.ComponentModel;
using MahApps.Metro.IconPacks;
using PropertyChanged;

namespace tterm.Ui.Models
{
    [ImplementPropertyChanged]
    internal class TabDataItem
    {
        public bool IsActive { get; set; }
        public string Title { get; set; }
        public PackIconMaterialKind Image { get; set; }

        public bool IsImage => (Title == null);

    }
}
