using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace SteamP2PInfo.Config
{
    public static class ConfigUIBuilder
    {
        /// <summary>Full editor: a GENERAL card for top-level options plus one card per [ConfigCategory] property.</summary>
        public static StackPanel CreateConfigEditor(object config)
        {
            var panel = new StackPanel();

            var generalRows = CreateRowsPanel(config, skipCategories: true);
            if (generalRows.Children.Count > 0)
                panel.Children.Add(CreateCard("GENERAL", null, generalRows));

            foreach (var prop in GetEditableProperties(config))
            {
                var attr = GetAttribute(prop);
                if (attr is ConfigCategoryAttribute)
                    panel.Children.Add((UIElement)attr.CreateUIElement(config, prop.Name));
            }

            return panel;
        }

        /// <summary>Rows only (label left, control right, hairline separators) — no outer card.</summary>
        public static StackPanel CreateRowsPanel(object config, bool skipCategories = false)
        {
            var rows = new StackPanel();
            bool first = true;

            foreach (var prop in GetEditableProperties(config))
            {
                var attr = GetAttribute(prop);
                if (skipCategories && attr is ConfigCategoryAttribute)
                    continue;

                UIElement element = attr.CreateUIElement(config, prop.Name);

                var row = new Grid { Margin = new Thickness(0, 8, 0, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var label = new TextBlock
                {
                    Text = attr.OptionName,
                    FontSize = 12.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = attr.Tooltip,
                };
                Grid.SetColumn(label, 0);
                row.Children.Add(label);

                element.SetValue(Grid.ColumnProperty, 1);
                if (element is FrameworkElement fe)
                    fe.HorizontalAlignment = HorizontalAlignment.Right;
                row.Children.Add(element);

                if (!first)
                {
                    var separator = new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14FFFFFF")),
                    };
                    rows.Children.Add(separator);
                }
                first = false;
                rows.Children.Add(row);
            }

            return rows;
        }

        /// <summary>Card container matching the approved design: #2B2B2B, radius 6, uppercase header.</summary>
        public static Border CreateCard(string header, string tooltip, UIElement content)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = header,
                FontSize = 10.5,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9A9A9A")),
                Margin = new Thickness(0, 10, 0, 2),
                ToolTip = tooltip,
            });
            stack.Children.Add(content);

            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B2B2B")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 0, 14, 2),
                Margin = new Thickness(0, 0, 0, 10),
                Child = stack,
            };
        }

        private static PropertyInfo[] GetEditableProperties(object config)
        {
            return config.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .Where(p => GetAttribute(p) != null)
                .ToArray();
        }

        private static IConfigUIElement GetAttribute(PropertyInfo property)
        {
            return property.GetCustomAttributes().FirstOrDefault(a => a is IConfigUIElement) as IConfigUIElement;
        }
    }
}
