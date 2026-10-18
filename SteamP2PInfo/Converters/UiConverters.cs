using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Steamworks;

namespace SteamP2PInfo.Converters
{
    /// <summary>Hex string ("#AARRGGBB" or "#RRGGBB") to SolidColorBrush. ConverterParameter optionally overrides the alpha (0-255).</summary>
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(value as string ?? "#FFFFFFFF");
                if (parameter != null && byte.TryParse(parameter.ToString(), out byte alpha))
                    color = Color.FromArgb(alpha, color.R, color.G, color.B);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Connection quality (0..1) to a pixel width on a 44px bar.</summary>
    public class QualityWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double q = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return Math.Max(0, Math.Min(1, q)) * 44;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>SteamID to a stable avatar fallback color from a fixed palette.</summary>
    public class AvatarColorConverter : IValueConverter
    {
        private static readonly Color[] Palette =
        {
            (Color)ColorConverter.ConvertFromString("#3E6E9E"),
            (Color)ColorConverter.ConvertFromString("#5C4A8C"),
            (Color)ColorConverter.ConvertFromString("#8C6B3A"),
            (Color)ColorConverter.ConvertFromString("#3A7C6B"),
            (Color)ColorConverter.ConvertFromString("#7C3A4E"),
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ulong id = value is CSteamID sid ? sid.m_SteamID : 0;
            var brush = new SolidColorBrush(Palette[(int)(id % (ulong)Palette.Length)]);
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>Persona name to a single uppercase initial.</summary>
    public class InitialConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string name = value as string;
            return string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    /// <summary>SteamID to the peer's Steam avatar, or null (fallback circle shows through).</summary>
    public class AvatarImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is CSteamID sid ? SteamAvatar.Get(sid) : null;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
