// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Controls
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    public partial class RawTab : UserControl
    {
        public static readonly StyledProperty<ModComponent> CurrentComponentProperty =
            AvaloniaProperty.Register<RawTab, ModComponent>(nameof(CurrentComponent));

        [CanBeNull]
        public ModComponent CurrentComponent
        {
            get => MainConfig.CurrentComponent;
            set
            {
                MainConfig.CurrentComponent = value;
                SetValue(CurrentComponentProperty, value);
                RefreshCurrentFormatContent();
            }
        }

        public event EventHandler<RoutedEventArgs> ApplyEditorChangesRequested;
        public event EventHandler<RoutedEventArgs> GenerateGuidRequested;

        private bool _suppressTextChanged;
        private bool _suppressFormatChanged;
        private string _currentFormat = "toml";

        public RawTab()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void ApplyEditorButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyEditorChangesRequested?.Invoke(this, e);
        }

        private void GenerateGuidButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateGuidRequested?.Invoke(this, e);
        }

        public TextBox GetGuidTextBox() => GuidGeneratedTextBox;

        public TextBox GetRawEditTextBox()
        {
            // Return the currently active format textbox using FindControl for safety
            return string.Equals(_currentFormat, "toml", StringComparison.Ordinal) ? this.FindControl<TextBox>("TomlTextBox") : string.Equals(_currentFormat, "markdown", StringComparison.Ordinal) ? this.FindControl<TextBox>("MarkdownTextBox") : string.Equals(_currentFormat, "yaml", StringComparison.Ordinal) ? this.FindControl<TextBox>("YamlTextBox") : string.Equals(_currentFormat, "json", StringComparison.Ordinal) ? this.FindControl<TextBox>("JsonTextBox") :
                this.FindControl<TextBox>("TomlTextBox");
        }

        /// <summary>
        /// Gets the current format that is active in the RawTab (toml, markdown, yaml, or json)
        /// </summary>
        public string GetCurrentFormat() => _currentFormat;

        private void FormatTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressFormatChanged || !(sender is TabControl tabControl))
            {
                return;
            }

            try
            {
                _suppressFormatChanged = true;

                // Determine which format tab was selected
                if (tabControl.SelectedItem is TabItem selectedTab)
                {
                    string previousFormat = _currentFormat;
                    _currentFormat = selectedTab.Header?.ToString()?.ToLowerInvariant() ?? "toml";

                    Logger.LogVerbose($"[RawTab] Format tab changed from '{previousFormat}' to '{_currentFormat}'");

                    // Regenerate content in the new format
                    RefreshCurrentFormatContent();
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "[RawTab] Error in FormatTabControl_SelectionChanged");
            }
            finally
            {
                _suppressFormatChanged = false;
            }
        }

        private void FormatTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressTextChanged || !(sender is TextBox textBox))
            {
                return;
            }

            try
            {
                // Attempt to deserialize the content when it changes
                string content = textBox.Text;
                if (string.IsNullOrWhiteSpace(content))
                {
                    return;
                }

                // Determine which format this textbox belongs to
                string format = RawTab.GetFormatForTextBox(textBox);
                if (string.IsNullOrEmpty(format))
                {
                    return;
                }

                Logger.LogVerbose($"[RawTab] Text changed in {format} textbox, attempting deserialization");

                // Attempt deserialization (this will validate the syntax)
                IReadOnlyList<ModComponent> components = ModComponentSerializationService.DeserializeModComponentFromString(content, format);
                if (components != null && components.Count > 0)
                {
                    Logger.LogVerbose($"[RawTab] Successfully deserialized {components.Count} component(s) from {format}");
                }
            }
            catch (Exception ex)
            {
                // Don't spam logs for every keystroke - just log at verbose level
                Logger.LogVerbose($"[RawTab] Deserialization failed (may be incomplete): {ex.Message}");
            }
        }

        private static string GetFormatForTextBox(TextBox textBox)
        {
            if (textBox is null)
            {
                return null;
            }

            // Compare by name to avoid reference issues with FindControl
            return string.Equals(textBox.Name, "TomlTextBox", StringComparison.Ordinal) ? "toml" : string.Equals(textBox.Name, "MarkdownTextBox", StringComparison.Ordinal) ? "markdown" : string.Equals(textBox.Name, "YamlTextBox", StringComparison.Ordinal) ? "yaml" : string.Equals(textBox.Name, "JsonTextBox", StringComparison.Ordinal) ? "json" :
                null;
        }

        /// <summary>
        /// Refreshes the content of the currently selected format tab by serializing CurrentComponent
        /// </summary>
        public void RefreshCurrentFormatContent()
        {
            if (CurrentComponent is null)
            {
                return;
            }

            try
            {
                _suppressTextChanged = true;

                Logger.LogVerbose($"[RawTab] Refreshing content for format '{_currentFormat}'");

                // Create a list with just the current component for serialization
                var components = new List<ModComponent> { CurrentComponent };

                // Serialize to the current format using ModComponentSerializationService
                string serializedContent = ModComponentSerializationService.SerializeModComponentAsString(components, _currentFormat);

                // Update the appropriate textbox
                TextBox targetTextBox = GetRawEditTextBox();
                if (targetTextBox != null)
                {
                    targetTextBox.Text = serializedContent;
                    Logger.LogVerbose($"[RawTab] Successfully serialized component '{CurrentComponent.Name}' to {_currentFormat}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"[RawTab] Error serializing component to {_currentFormat}");
            }
            finally
            {
                _suppressTextChanged = false;
            }
        }
    }
}

