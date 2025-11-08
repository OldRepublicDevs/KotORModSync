// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Core.Parsing;

namespace KOTORModSync.Dialogs
{
    public partial class RegexImportDialog : Window
    {
        public RegexImportDialogViewModel ViewModel { get; private set; }
        public bool LoadSuccessful { get; private set; }
        private readonly SelectableTextBlock _previewTextBox;
        private readonly Func<Task<bool>> _confirmationCallback;
        private bool _mouseDownForWindowMoving;
        private PointerPoint _originalPoint;
        private Control _currentlyHighlightedTextBox;

        public RegexImportDialog()
        {
            InitializeComponent();
            ViewModel = null;
            LoadSuccessful = false;

            PointerPressed += InputElement_OnPointerPressed;
            PointerMoved += InputElement_OnPointerMoved;
            PointerReleased += InputElement_OnPointerReleased;
            PointerExited += InputElement_OnPointerReleased;

            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);
        }

        public RegexImportDialog(
            [NotNull] string markdown,
            [CanBeNull] MarkdownImportProfile initialProfile = null,
            [CanBeNull] Func<Task<bool>> confirmationCallback = null
        ) : this()
        {
            InitializeComponent();
            ViewModel = new RegexImportDialogViewModel(markdown, initialProfile ?? MarkdownImportProfile.CreateDefault());
            DataContext = ViewModel;
            LoadSuccessful = false;
            _confirmationCallback = confirmationCallback;

            _previewTextBox = this.FindControl<SelectableTextBlock>("PreviewTextBox");
            if (_previewTextBox != null)
            {
                _previewTextBox.PointerMoved += PreviewTextBox_PointerMoved;
                _previewTextBox.PointerExited += PreviewTextBox_PointerExited;

                // Subscribe to ViewModel HighlightedPreview updates
                ViewModel.PropertyChanged += (s, e) =>
                {
                    if (string.Equals(e.PropertyName, nameof(RegexImportDialogViewModel.HighlightedPreview), StringComparison.Ordinal))
                    {
                        UpdatePreviewInlines();
                    }
                };
                UpdatePreviewInlines();
            }

            // Subscribe to ViewModel events
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Add Ctrl+F shortcut
            AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);

            PointerPressed += InputElement_OnPointerPressed;
            PointerMoved += InputElement_OnPointerMoved;
            PointerReleased += InputElement_OnPointerReleased;
            PointerExited += InputElement_OnPointerReleased;

            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "ShowFindDialog", StringComparison.Ordinal))
            {
                ShowFindDialog();
            }
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void OnResetDefaults(object sender, RoutedEventArgs e) => ViewModel?.ResetDefaults();

        private void OnCancel(object sender, RoutedEventArgs e) => Close();

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void ToggleMaximizeButton_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private async void OnLoad(object sender, RoutedEventArgs e)
        {

            if (_confirmationCallback != null)


            {
                bool confirmed = await _confirmationCallback();
                if (!confirmed)
                {
                    return;
                }
            }

            LoadSuccessful = true;
            Close();
        }


        private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_mouseDownForWindowMoving)
            {
                return;
            }

            PointerPoint currentPoint = e.GetCurrentPoint(this);
            Position = new PixelPoint(
                Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
                Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
            );
        }

        private void InputElement_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
            {
                return;
            }

            if (ShouldIgnorePointerForWindowDrag(e))
            {
                return;
            }

            _mouseDownForWindowMoving = true;
            _originalPoint = e.GetCurrentPoint(this);
        }

        private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) =>
            _mouseDownForWindowMoving = false;

        private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
        {

            if (!(e.Source is Visual source))
            {
                return false;
            }

            Visual current = source;
            while (current != null && current != this)
            {
                switch (current)
                {

                    case Button _:
                    case TextBox _:
                    case ComboBox _:
                    case ListBox _:
                    case MenuItem _:
                    case Menu _:
                    case Expander _:
                    case Slider _:
                    case TabControl _:
                    case TabItem _:
                    case ProgressBar _:
                    case ScrollViewer _:

                    case Control control when control.ContextMenu?.IsOpen == true:
                        return true;
                    case Control control when control.ContextFlyout?.IsOpen == true:
                        return true;
                    default:
                        current = current.GetVisualParent();
                        break;
                }
            }

            return false;
        }

        private void OnRegexPatternChanged(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                ViewModel.UpdatePreviewFromTextBox(textBox);
            }
        }

        private void OnRegexTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (
                (e.Key == Key.Enter || e.Key == Key.Tab)
                && sender is TextBox textBox)
            {
                ViewModel.UpdatePreviewFromTextBox(textBox);
            }
        }


        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                ViewModel?.FindCommand.Execute(null);
            }
        }

        private void PreviewTextBox_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_previewTextBox == null || ViewModel == null)
            {
                return;
            }

            Point position = e.GetPosition(_previewTextBox);
            int caretPosition = GetCharacterIndexFromPoint(_previewTextBox, position);

            if (caretPosition >= 0)
            {
                string groupName = ViewModel.GetGroupNameForPosition(caretPosition);
                if (!string.IsNullOrEmpty(groupName))
                {
                    HighlightTextBoxForGroup(groupName);

                    // Show component info tooltip
                    string componentInfo = ViewModel.GetComponentInfoForPosition(caretPosition);
                    if (!string.IsNullOrEmpty(componentInfo))
                    {
                        ToolTip.SetTip(_previewTextBox, componentInfo);
                    }
                    else
                    {
                        ToolTip.SetTip(_previewTextBox, null);
                    }
                    return;
                }
            }

            ClearTextBoxHighlight();
            ToolTip.SetTip(_previewTextBox, null);
        }

        private void PreviewTextBox_PointerExited(object sender, PointerEventArgs e)
        {
            ClearTextBoxHighlight();
            ToolTip.SetTip(_previewTextBox, null);
        }

        private void UpdatePreviewInlines()
        {
            if (_previewTextBox == null || ViewModel?.HighlightedPreview == null)
            {
                return;
            }

            _previewTextBox.Inlines?.Clear();
            foreach (Inline inline in ViewModel.HighlightedPreview)
            {
                _previewTextBox.Inlines?.Add(inline);
            }
        }

        private static int GetCharacterIndexFromPoint(SelectableTextBlock textBox, Avalonia.Point point)
        {
            // Approximate character position based on font metrics
            // This is a simplified version - exact hit testing would require TextPointer API
            try
            {
                // Get text from inlines
                var textBuilder = new System.Text.StringBuilder();
                if (textBox.Inlines != null)
                {
                    foreach (Inline inline in textBox.Inlines)
                    {
                        if (inline is Run run)
                        {
                            textBuilder.Append(run.Text ?? "");
                        }
                    }
                }
                string text = textBuilder.ToString();
                if (string.IsNullOrEmpty(text))
                {
                    return -1;
                }

                // Get approximate character based on scroll position and click location
                int scrollOffset = (int)textBox.GetValue(ScrollViewer.OffsetProperty).Y;
                double lineHeight = textBox.FontSize * 1.2; // Approximate line height
                int lineIndex = Math.Max(0, (int)((point.Y + scrollOffset) / lineHeight));

                string[] lines = text.Split('\n');
                if (lineIndex >= lines.Length)
                {
                    return text.Length - 1;
                }

                int charIndex = 0;
                for (int i = 0; i < lineIndex && i < lines.Length; i++)
                {
                    charIndex += lines[i].Length + 1; // +1 for newline
                }

                // Approximate character within line based on X position
                if (lineIndex < lines.Length)
                {
                    double charWidth = textBox.FontSize * 0.6; // Approximate monospace char width
                    int charInLine = Math.Max(0, (int)(point.X / charWidth));
                    charIndex += Math.Min(charInLine, lines[lineIndex].Length);
                }

                return Math.Min(charIndex, text.Length - 1);
            }
            catch
            {
                return -1;
            }
        }

        private void HighlightTextBoxForGroup(string groupName)
        {
            // Use the type name as required by the compiler error.
            string textBoxName = RegexImportDialogViewModel.GetTextBoxNameForGroupName(groupName);
            if (string.IsNullOrEmpty(textBoxName))
            {
                return;
            }

            // Find the Simple tab
            TabItem simpleTab = this.FindControl<TabItem>("SimpleTab");
            if (simpleTab == null)
            {
                return;
            }

            // Find all TextBoxes in the Simple tab
            System.Collections.Generic.IEnumerable<TextBox> textBoxes = simpleTab.GetVisualDescendants().OfType<TextBox>()
                .Where(tb => tb.Name != null && tb.Name.IndexOf(textBoxName, StringComparison.OrdinalIgnoreCase) >= 0);

            TextBox textBox = textBoxes.FirstOrDefault();
            if (textBox != null && _currentlyHighlightedTextBox != textBox)
            {
                ClearTextBoxHighlight();
                _currentlyHighlightedTextBox = textBox;
                textBox.BorderBrush = Brushes.Yellow;
                textBox.BorderThickness = new Thickness(2);
            }
        }

        private void ClearTextBoxHighlight()
        {
            if (_currentlyHighlightedTextBox is TextBox textBox)
            {
                textBox.BorderBrush = null;
                textBox.BorderThickness = new Thickness(1);
                _currentlyHighlightedTextBox = null;
            }
        }

        private void ShowFindDialog()
        {
            if (_previewTextBox == null)
            {
                return;
            }

            // Note: Find functionality requires TextBox with Text property
            // SelectableTextBlock doesn't support find, so we skip it for now
            Logger.LogVerbose("Find dialog not yet supported for highlighted preview");

            // Create a simple find panel at the top of the preview area
            Border findPanel = CreateFindPanel();
            Border previewBorder = this.FindControl<Border>("PreviewBorder");
            if (previewBorder?.Child is Grid previewGrid)
            {
                // Check if find panel already exists
                Border existingPanel = previewGrid.Children.OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "FindPanel", StringComparison.Ordinal));
                if (existingPanel != null)
                {
                    TextBox findTextBox = existingPanel.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
                    findTextBox?.Focus();
                    findTextBox?.SelectAll();
                    return;
                }

                // Insert find panel at the top
                findPanel.Name = "FindPanel";
                Grid.SetRow(findPanel, 0);
                Grid.SetRowSpan(previewGrid.Children[previewGrid.Children.Count - 1], 1); // Adjust preview textbox row span
                previewGrid.Children.Insert(0, findPanel);

                TextBox findTextBox2 = findPanel.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
                findTextBox2?.Focus();
            }
        }

        private Border CreateFindPanel()
        {
            var findTextBox = new TextBox
            {
                Watermark = "Find in preview...",
                MinWidth = 200,
            };

            var findNextButton = new Button
            {
                Content = "Next",
                Margin = new Thickness(4, 0, 0, 0),
            };

            var findPrevButton = new Button
            {
                Content = "Prev",
                Margin = new Thickness(4, 0, 0, 0),
            };

            var closeButton = new Button
            {
                Content = "X",
                Margin = new Thickness(4, 0, 0, 0),
                Width = 30,
            };

            findTextBox.TextChanged += (s, e) => RegexImportDialog.PerformFind(findTextBox.Text);
            findNextButton.Click += (s, e) => RegexImportDialog.PerformFind(findTextBox.Text);
            findPrevButton.Click += (s, e) => RegexImportDialog.PerformFind(findTextBox.Text);
            closeButton.Click += (s, e) => CloseFindPanel();

            var panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Thickness(8, 4, 8, 4),
                Children =
                {
                    new TextBlock { Text = "Find:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) },
                    findTextBox,
                    findNextButton,
                    findPrevButton,
                    closeButton
                },
            };

            return new Border
            {
                Child = panel,
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
        }

        private static void PerformFind(string searchText)
        {
            // Find functionality not implemented for SelectableTextBlock
            // Would need to switch to TextBox for full find/selection support
            if (string.IsNullOrEmpty(searchText))
            {
                return;
            }

            Logger.LogVerbose($"Find '{searchText}' not supported in highlight preview mode");
        }

        private void CloseFindPanel()
        {
            Border previewBorder = this.FindControl<Border>("PreviewBorder");
            if (previewBorder?.Child is Grid previewGrid)
            {
                Border findPanel = previewGrid.Children.OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "FindPanel", StringComparison.Ordinal));
                if (findPanel != null)
                {
                    previewGrid.Children.Remove(findPanel);
                }
            }
        }

        private void OnPreviewTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CloseFindPanel();
            }
        }
    }
}
