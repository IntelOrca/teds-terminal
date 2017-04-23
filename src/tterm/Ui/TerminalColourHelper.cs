using System.Collections.Generic;
using System.Windows.Media;

namespace tterm.Ui
{
    internal class TerminalColourHelper
    {
        private readonly Dictionary<int, Brush> _brushDictionary = new Dictionary<int, Brush>();
        private readonly Brush _defaultForegroundBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204));

        public Brush GetForegroundBrush(int id)
        {
            Brush brush = GetBrush(id);
            if (brush == null)
            {
                brush = _defaultForegroundBrush;
            }
            return brush;
        }

        public Brush GetBackgroundBrush(int id)
        {
            return GetBrush(id);
        }

        public Brush GetBrush(int id)
        {
            Brush result = null;
            if (id != 0)
            {
                if (!_brushDictionary.TryGetValue(id, out result))
                {
                    result = new SolidColorBrush(GetColour(id));
                    _brushDictionary.Add(id, result);
                }
            }
            return result;
        }

        public Color GetColour(int id)
        {
            switch (id)
            {
                case SpecialColourIds.Cursor:
                    return Color.FromRgb(204, 204, 204);
                case SpecialColourIds.Selection:
                    return Color.FromRgb(203, 203, 203);
                case SpecialColourIds.Historic:
                case SpecialColourIds.Futuristic:
                    return Color.FromRgb(24, 24, 24);
                default:
                    return (Color)ColorConverter.ConvertFromString(TangoColours[id % 16]);
            }
        }

        // Colors 0-15
        private readonly static string[] TangoColours =
        {
            // dark:
            "#2e3436",
            "#cc0000",
            "#4e9a06",
            "#c4a000",
            "#3465a4",
            "#75507b",
            "#06989a",
            "#d3d7cf",

            // bright:
            "#555753",
            "#ef2929",
            "#8ae234",
            "#fce94f",
            "#729fcf",
            "#ad7fa8",
            "#34e2e2",
            "#eeeeec"
        };
    }
}
