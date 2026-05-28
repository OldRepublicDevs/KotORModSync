using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace HoloPatcher.UI.Views.Dialogs
{
    /// <summary>
    /// Simple utility dialog that mirrors tkinter's multi-option message boxes.
    /// </summary>
    public sealed class ChoiceDialog : Window
    {
        public ChoiceDialog(string title, string message, IReadOnlyList<string> options)
        {
            if (options is null || options.Count == 0)
            {
                throw new ArgumentException("At least one option is required.", nameof(options));
            }

            Title = title;
            Width = 420;
            Height = 220;
            MinWidth = 360;
            MinHeight = 180;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Content = BuildContent(message, options);
        }

        private Control BuildContent(string message, IReadOnlyList<string> options)
        {
            var root = new DockPanel
            {
                Margin = new Thickness(16)
            };

            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };

            DockPanel.SetDock(textBlock, Dock.Top);
            root.Children.Add(textBlock);

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8
            };

            foreach (string option in options)
            {
                var button = new Button
                {
                    Content = option,
                    MinWidth = 80
                };
                button.Click += (sender, e) => Close(option);
                buttonsPanel.Children.Add(button);
            }

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 80
            };
            cancelButton.Click += (sender, e) => Close(null);
            buttonsPanel.Children.Add(cancelButton);

            DockPanel.SetDock(buttonsPanel, Dock.Bottom);
            root.Children.Add(buttonsPanel);

            return root;
        }
    }
}

