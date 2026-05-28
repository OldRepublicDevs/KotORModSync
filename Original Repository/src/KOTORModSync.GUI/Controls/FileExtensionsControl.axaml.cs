// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

using JetBrains.Annotations;

using KOTORModSync.Core;

namespace KOTORModSync.Controls
{
    public partial class FileExtensionsControl : UserControl
    {
        public static readonly StyledProperty<List<string>> FileExtensionsProperty =
            AvaloniaProperty.Register<FileExtensionsControl, List<string>>(nameof(FileExtensions), new List<string>());

        private bool _isUpdatingFromTextBox;

        public List<string> FileExtensions
        {
            get => GetValue(FileExtensionsProperty);
            set => SetValue(FileExtensionsProperty, value);
        }

        public FileExtensionsControl()
        {

            AvaloniaXamlLoader.Load(this);
            UpdateEmptyStateVisibility();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property != FileExtensionsProperty)
            {
                return;
            }

            if (_isUpdatingFromTextBox)
            {
                return;
            }

            ItemsControl extensionsItemsControl = this.FindControl<ItemsControl>("ExtensionsItemsControl");
            Border emptyStateBorder = this.FindControl<Border>("EmptyStateBorder");
            if (extensionsItemsControl is null || emptyStateBorder is null)
            {
                return;
            }

            UpdateExtensionsDisplay();
            UpdateEmptyStateVisibility();
        }

        private void UpdateExtensionsDisplay()
        {
            ItemsControl extensionsItemsControl = this.FindControl<ItemsControl>("ExtensionsItemsControl");
            if (extensionsItemsControl?.ItemsSource == FileExtensions)
            {
                return;
            }

            if (extensionsItemsControl != null)
            {
                extensionsItemsControl.ItemsSource = FileExtensions;
            }
        }

        private void UpdateEmptyStateVisibility()
        {
            Border emptyStateBorder = this.FindControl<Border>("EmptyStateBorder");
            if (emptyStateBorder is null)
            {
                return;
            }

            emptyStateBorder.IsVisible = FileExtensions is null || FileExtensions.Count == 0;
        }

        private void AddExtension_Click(object sender, RoutedEventArgs e)
        {
            if (FileExtensions is null)
            {
                FileExtensions = new List<string>();
            }

            _isUpdatingFromTextBox = false;

            var newList = new List<string>(FileExtensions) { string.Empty };
            FileExtensions = newList;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var textBoxes = this.GetVisualDescendants().OfType<TextBox>().ToList();
                    TextBox lastTextBox = textBoxes.LastOrDefault();
                    _ = (lastTextBox?.Focus());
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Error focusing newly added TextBox in FileExtensionsControl");
                }
            }, DispatcherPriority.Input);
        }

        private void RemoveExtension_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || FileExtensions is null)
            {
                return;
            }

            if (!(button.Parent is Grid parentGrid))
            {
                return;
            }

            TextBox textBox = parentGrid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            if (textBox is null)
            {
                return;
            }

            int index = GetTextBoxIndex(textBox);
            if (index < 0 || index >= FileExtensions.Count)
            {
                return;
            }

            _isUpdatingFromTextBox = false;

            var newList = new List<string>(FileExtensions);
            newList.RemoveAt(index);
            FileExtensions = newList;
        }

        private void ExtensionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!(sender is TextBox textBox) || FileExtensions is null)
            {
                return;
            }

            int index = GetTextBoxIndex(textBox);
            if (index < 0 || index >= FileExtensions.Count)
            {
                return;
            }

            string newText = textBox.Text ?? string.Empty;

            if (string.Equals(FileExtensions[index], newText, StringComparison.Ordinal))
            {
                UpdateExtensionValidation(textBox);
                return;
            }

            _isUpdatingFromTextBox = true;
            try
            {

                var newList = new List<string>(FileExtensions)
                {
                    [index] = newText,
                };
                FileExtensions = newList;
            }
            finally
            {
                _isUpdatingFromTextBox = false;
            }

            UpdateExtensionValidation(textBox);
        }

        private int GetTextBoxIndex(TextBox textBox)
        {
            ItemsControl extensionsItemsControl = this.FindControl<ItemsControl>("ExtensionsItemsControl");
            if (extensionsItemsControl is null || !(extensionsItemsControl.ItemsSource is List<string> extensions) || textBox is null)
            {
                return -1;
            }

            try
            {
                var textBoxes = this.GetVisualDescendants().OfType<TextBox>().ToList();
                int index = textBoxes.IndexOf(textBox);
                return index >= 0 && index < extensions.Count ? index : -1;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error getting TextBox index in FileExtensionsControl");
                return -1;
            }
        }

        private void ExtensionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:

                    AddExtension_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.Delete when sender is TextBox deleteTextBox &&
                                     FileExtensions != null && string.IsNullOrWhiteSpace(deleteTextBox.Text):
                    {

                        int index = GetTextBoxIndex(deleteTextBox);
                        if (index >= 0 && index < FileExtensions.Count)
                        {

                            _isUpdatingFromTextBox = false;

                            var newList = new List<string>(FileExtensions);
                            newList.RemoveAt(index);
                            FileExtensions = newList;
                        }
                        e.Handled = true;
                        break;
                    }
            }
        }

        private static void UpdateExtensionValidation(TextBox textBox)
        {
            if (textBox is null)
            {
                return;
            }

            string extension = textBox.Text?.Trim() ?? string.Empty;
            bool isValid = string.IsNullOrWhiteSpace(extension) || IsValidExtension(extension);

            if (string.IsNullOrWhiteSpace(extension))
            {

                textBox.ClearValue(BorderBrushProperty);
                textBox.ClearValue(BorderThicknessProperty);
                ToolTip.SetTip(textBox, value: null);
            }
            else if (isValid)
            {

                textBox.BorderBrush = ThemeResourceHelper.UrlValidationValidBrush;
                textBox.BorderThickness = new Thickness(1);
                ToolTip.SetTip(textBox, "Valid file extension");
            }
            else
            {

                textBox.BorderBrush = ThemeResourceHelper.UrlValidationInvalidBrush;
                textBox.BorderThickness = new Thickness(2);
                ToolTip.SetTip(textBox, $"Invalid file extension: {extension}");
            }
        }

        private static bool IsValidExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            if (extension[0] != '.')
            {
                return false;
            }

            string extensionWithoutDot = extension.Substring(1);
            if (extensionWithoutDot.Length == 0)
            {
                return false;
            }

            foreach (char c in extensionWithoutDot)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        public void SetExtensions([NotNull] IEnumerable<string> extensions)
        {
            List<string> extensionsList = extensions?.ToList() ?? new List<string>();
            Logger.LogVerbose($"FileExtensionsControl.SetExtensions called with {extensionsList.Count} extensions: [{string.Join(", ", extensionsList)}]");

            ItemsControl extensionsItemsControl = this.FindControl<ItemsControl>("ExtensionsItemsControl");
            Border emptyStateBorder = this.FindControl<Border>("EmptyStateBorder");
            if (extensionsItemsControl is null || emptyStateBorder is null)
            {
                Logger.LogVerbose("FileExtensionsControl not fully loaded yet, deferring SetExtensions");

                Loaded += (sender, e) =>
                {
                    Logger.LogVerbose($"FileExtensionsControl loaded, now setting {extensionsList.Count} extensions");
                    FileExtensions = extensionsList;
                };
                return;
            }

            Logger.LogVerbose("FileExtensionsControl already loaded, setting extensions immediately");
            FileExtensions = extensionsList;
        }

        public List<string> GetValidExtensions()
        {
            if (FileExtensions is null)
            {
                return new List<string>();
            }

            var validExtensions = new List<string>();
            foreach (string extension in FileExtensions)
            {
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    string cleanedExtension = extension.Trim();
                    if (!cleanedExtension.StartsWith(".", StringComparison.Ordinal))
                    {
                        cleanedExtension = "." + cleanedExtension;
                    }
                    validExtensions.Add(cleanedExtension);
                }
            }
            return validExtensions;
        }
    }
}
