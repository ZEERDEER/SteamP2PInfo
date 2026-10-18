using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamP2PInfo.Config
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ConfigCategoryAttribute : Attribute, IConfigUIElement
    {
        public string OptionName { get; }
        public string Tooltip { get; }
        public bool Multicolumn => true;

        public ConfigCategoryAttribute(string categoryName, string tooltip = null)
        {
            OptionName = categoryName;
            Tooltip = tooltip;
        }

        public UIElement CreateUIElement(object source, string path)
        {
            object subConfig = source.GetType()
                .GetProperty(path, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .GetValue(source);

            return ConfigUIBuilder.CreateCard(OptionName, Tooltip, ConfigUIBuilder.CreateRowsPanel(subConfig));
        }
    }
}
