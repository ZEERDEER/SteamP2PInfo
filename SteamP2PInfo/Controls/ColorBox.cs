using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamP2PInfo.Controls
{
    /// <summary>
    /// Hex color editor for the config UI: a TextBox holding a "#AARRGGBB" (or "#RRGGBB") string
    /// that tints itself to the parsed color. The value round-trips as the same string the config
    /// JSON stores, so no value converter is involved.
    /// </summary>
    public class ColorBox : TextBox
    {
        private static readonly Brush InvalidBorder = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));

        public ColorBox()
        {
            MinWidth = 120;
            FontFamily = new FontFamily("Consolas");
            ApplyPreview();
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            ApplyPreview();
        }

        private void ApplyPreview()
        {
            if (TryParse(Text, out Color color))
            {
                BorderBrush = new SolidColorBrush(color);
                Background = new SolidColorBrush(Color.FromArgb(36, color.R, color.G, color.B));
            }
            else
            {
                BorderBrush = InvalidBorder;
                Background = Brushes.Transparent;
            }
        }

        private static bool TryParse(string text, out Color color)
        {
            color = Colors.White;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                color = (Color)ColorConverter.ConvertFromString(text.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
