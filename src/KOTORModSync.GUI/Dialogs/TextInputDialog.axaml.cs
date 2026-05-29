// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using JetBrains.Annotations;
using KOTORModSync.Services;

namespace KOTORModSync.Dialogs
{
    public partial class TextInputDialog : Window
    {
        public static readonly AvaloniaProperty PromptTextProperty =
            AvaloniaProperty.Register<TextInputDialog, string>(nameof(PromptText));

        public TextInputDialog()
        {
            InitializeComponent();
            ThemeManager.ApplyCurrentToWindow(this);
        }

        [CanBeNull]
        public string PromptText
        {
            get => GetValue(PromptTextProperty) as string;
            set => SetValue(PromptTextProperty, value);
        }

        [CanBeNull]
        public string ResultText { get; private set; }

        public static async Task<string> ShowTextInputDialogAsync(
            [NotNull] Window parentWindow,
            [CanBeNull] string prompt,
            [CanBeNull] string title = "Input",
            [CanBeNull] string defaultText = null)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new TextInputDialog
                {
                    Title = title,
                    PromptText = prompt,
                    Topmost = true,
                };

                if (dialog.InputTextBox != null && !string.IsNullOrEmpty(defaultText))
                {
                    dialog.InputTextBox.Text = defaultText;
                }

                bool? accepted = await dialog.ShowDialog<bool?>(parentWindow);
                return accepted == true ? dialog.ResultText : null;
            });
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResultText = InputTextBox?.Text?.Trim();
            Close(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close(false);

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close(false);
    }
}
