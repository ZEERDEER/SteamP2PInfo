using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.ComponentModel;
using Wpf.Ui.Controls;
using System.Windows.Controls;
using System.Globalization;

namespace SteamP2PInfo.Config
{
    public class OverlayConfig : INotifyPropertyChanged
    {
        public class PingColorRange
        {
            [JsonProperty("threshold")]
            public double Threshold { get; set; }
            [JsonProperty("color")]
            public string Color { get; set; }
        }

        [JsonProperty("enabled")]
        [ConfigBindingElement("Overlay Enabled", typeof(ToggleSwitch), "IsCheckedProperty",
            Tooltip: "Enable/disable the overlay.",
            UIElementProperties: new object[] {
                new object[] { "OnContent", "Yes" },
                new object[] { "OffContent", "No" }
            })]
        public bool Enabled { get; set; }

        [JsonProperty("show_steam_id")]
        [ConfigBindingElement("Show Steam ID", typeof(ToggleSwitch), "IsCheckedProperty",
            Tooltip: "Should account Steam ID 64 be displayed in the overlay?",
            UIElementProperties: new object[] {
                new object[] { "OnContent", "Yes" },
                new object[] { "OffContent", "No" }
            })]
        public bool ShowSteamID { get; set; } = false;

        [JsonProperty("show_connection_quality")]
        [ConfigBindingElement("Show Connection Quality", typeof(ToggleSwitch), "IsCheckedProperty",
            Tooltip: "Display connection quality heuristic from 0 (worst possible) to 1 (perfect).",
            UIElementProperties: new object[] {
                new object[] { "OnContent", "Yes" },
                new object[] { "OffContent", "No" }
            })]
        public bool ShowConnectionQuality { get; set; } = true;

        [JsonProperty("hotkey")]
        [ConfigBindingElement("Overlay Hotkey", typeof(SteamP2PInfo.Controls.HotKeyBox), "ValueProperty",
            Tooltip: "Hotkey to show/hide the overlay.")]
        public int Hotkey { get; set; } = 0;

        [JsonProperty("banner_format")]
        [ConfigBindingElement("Banner Format", typeof(System.Windows.Controls.TextBox), "TextProperty",
            Tooltip: "Format string for the overlay banner text.")]
        public string BannerFormat { get; set; } = "{time:HH:mm:ss}";

        [JsonProperty("font")]
        [ConfigFontSelector("Font", "Font for the text used in the overlay.")]
        public string Font { get; set; } = "Segoe UI, 20.25pt";

        [JsonProperty("x_offset")]
        [ConfigBindingElement("X Offset", typeof(NumberBox), "ValueProperty",
            Tooltip: "Horizontal offset from anchor point as a percentage of window width.",
            UIElementProperties: new object[] {
                new object[] { "Minimum", 0d },
                new object[] { "Maximum", 1d },
                new object[] { "SmallChange", 0.005d },
                new object[] { "MaxDecimalPlaces", 3 }
            })]
        public double XOffset { get; set; } = 0.025;

        [JsonProperty("y_offset")]
        [ConfigBindingElement("Y Offset", typeof(NumberBox), "ValueProperty",
            Tooltip: "Vertical offset from anchor point as a percentage of window width.",
            UIElementProperties: new object[] {
                new object[] { "Minimum", 0d },
                new object[] { "Maximum", 1d },
                new object[] { "SmallChange", 0.005d },
                new object[] { "MaxDecimalPlaces", 3 }
            })]
        public double YOffset { get; set; } = 0.025;

        [JsonProperty("anchor")]
        [ConfigEnumComboBox("Anchor", typeof(OverlayAnchor), 
            Tooltip: "Corner of the game window on which the overlay is anchored.")]
        public OverlayAnchor Anchor { get; set; } = OverlayAnchor.TopRight;

        [JsonProperty("text_color")]
        [ConfigBindingElement("Text Color", typeof(SteamP2PInfo.Controls.ColorBox), "TextProperty",
            Tooltip: "Color of overlay text.")]
        public string TextColor { get; set; } = "#FFFFFFFF";

        [JsonProperty("stroke_color")]
        [ConfigBindingElement("Stroke Color", typeof(SteamP2PInfo.Controls.ColorBox), "TextProperty",
            Tooltip: "Color of overlay text stroke (outline).")]
        public string StrokeColor { get; set; } = "#FF000000";

        [JsonProperty("stroke_width")]
        [ConfigBindingElement("Stroke width", typeof(NumberBox), "ValueProperty",
            Tooltip: "Width of stroke (outline) of the overlay text.",
            UIElementProperties: new object[] {
                new object[] { "Minimum", 0d },
                new object[] { "SmallChange", 0.5d },
                new object[] { "MaxDecimalPlaces", 1 }
            })]
        public double StrokeWidth { get; set; } = 2.0;

        [JsonProperty("ping_colors", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        [ConfigPingColorsPreview("Ping Colors", "Ping color thresholds. Edit the JSON config to change them.")]
        public List<PingColorRange> PingColors { get; set; } = new List<PingColorRange>()
        {
            new PingColorRange { Threshold = 0, Color = "#FF00BFFF" },
            new PingColorRange { Threshold = 50, Color = "#FF7CFC00" },
            new PingColorRange { Threshold = 100, Color = "#FFFFFF00" },
            new PingColorRange { Threshold = 200, Color = "#FFCD5C5C" }
        };

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
