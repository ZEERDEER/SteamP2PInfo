using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamP2PInfo.Config
{
    /// <summary>Read-only preview of the ping color thresholds (editing stays JSON-only for now).</summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ConfigPingColorsPreviewAttribute : Attribute, IConfigUIElement
    {
        public string OptionName { get; }
        public string Tooltip { get; }
        public bool Multicolumn => false;

        public ConfigPingColorsPreviewAttribute(string optionName, string tooltip = null)
        {
            OptionName = optionName;
            Tooltip = tooltip;
        }

        public UIElement CreateUIElement(object source, string path)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var ranges = source.GetType()
                .GetProperty(path, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                ?.GetValue(source) as IEnumerable<OverlayConfig.PingColorRange>;

            if (ranges != null)
            {
                foreach (var range in ranges)
                {
                    var dot = new Border
                    {
                        Width = 10,
                        Height = 10,
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(range.Color)),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var text = new TextBlock
                    {
                        Text = range.Threshold + "+",
                        FontSize = 10.5,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9A9A9A")),
                        Margin = new Thickness(5, 0, 12, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    panel.Children.Add(dot);
                    panel.Children.Add(text);
                }
            }

            return panel;
        }
    }
}
