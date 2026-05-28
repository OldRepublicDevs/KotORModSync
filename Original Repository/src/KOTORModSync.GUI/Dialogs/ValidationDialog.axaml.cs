// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using JetBrains.Annotations;

using KOTORModSync.Core;

namespace KOTORModSync.Dialogs
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public class ValidationIssue
    {
        public string Icon { get; set; }
        public string ModName { get; set; }
        public string IssueType { get; set; }
        public string Description { get; set; }
        public string Solution { get; set; }
        public bool HasSolution => !string.IsNullOrEmpty(Solution);

        // Link to actual VFS validation issues for details
        public Core.Services.FileSystem.ValidationIssue VfsIssue { get; set; }
        public IReadOnlyList<Core.Services.FileSystem.ValidationIssue> AllVfsIssues { get; set; }
        public ModComponent Component { get; set; }
    }

    public partial class ValidationDialog : Window
    {
        private readonly Action _openOutputCallback;
        private bool _mouseDownForWindowMoving;
        private PointerPoint _originalPoint;

        public ValidationDialog()
        {
            AvaloniaXamlLoader.Load(this);
            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);

            PointerPressed += InputElement_OnPointerPressed;
            PointerMoved += InputElement_OnPointerMoved;
            PointerReleased += InputElement_OnPointerReleased;
            PointerExited += InputElement_OnPointerReleased;
        }

        public ValidationDialog(bool success, string summaryMessage, List<ValidationIssue> modIssues = null,
                               List<string> systemIssues = null, Action openOutputCallback = null)
        {
            AvaloniaXamlLoader.Load(this);
            _openOutputCallback = openOutputCallback;

            InitializeDialog(success, summaryMessage, modIssues, systemIssues);

            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        private void InitializeDialog(bool success, string summaryMessage, List<ValidationIssue> modIssues, List<string> systemIssues)
        {

            Border summaryBorder = this.FindControl<Border>("SummaryBorder");
            TextBlock summaryIcon = this.FindControl<TextBlock>("SummaryIcon");
            TextBlock summaryText = this.FindControl<TextBlock>("SummaryText");
            TextBlock summaryDetails = this.FindControl<TextBlock>("SummaryDetails");
            StackPanel issuesPanel = this.FindControl<StackPanel>("IssuesPanel");
            StackPanel systemIssuesPanel = this.FindControl<StackPanel>("SystemIssuesPanel");
            ItemsControl systemIssuesList = this.FindControl<ItemsControl>("SystemIssuesList");
            StackPanel modIssuesPanel = this.FindControl<StackPanel>("ModIssuesPanel");
            ItemsControl modIssuesList = this.FindControl<ItemsControl>("ModIssuesList");
            Button openOutputButton = this.FindControl<Button>("OpenOutputButton");

            if (summaryIcon is null || summaryText is null || summaryDetails is null || summaryBorder is null)
            {
                return;
            }

            if (success)
            {
                summaryIcon.Text = "✅";
                summaryText.Text = "Validation Successful!";
                summaryDetails.Text = summaryMessage;
                summaryBorder.Background = new SolidColorBrush(Color.Parse("#1B4D3E"));
            }
            else
            {
                summaryIcon.Text = "❌";
                summaryText.Text = "Validation Failed";
                summaryDetails.Text = summaryMessage;
                summaryBorder.Background = new SolidColorBrush(Color.Parse("#4D1B1B"));
                if (issuesPanel != null)
                {
                    issuesPanel.IsVisible = true;
                }

                if (openOutputButton != null)
                {
                    openOutputButton.IsVisible = true;
                }

                if (systemIssues != null && systemIssues.Count > 0 && systemIssuesPanel != null && systemIssuesList != null)
                {
                    systemIssuesPanel.IsVisible = true;
                    var systemIssuesChildren = new List<Control>();
                    foreach (string issue in systemIssues)
                    {
                        systemIssuesChildren.Add(new Border
                        {
                            Classes = { "summary-card" },
                            Padding = new Avalonia.Thickness(12),
                            Margin = new Avalonia.Thickness(0, 4),
                            CornerRadius = new Avalonia.CornerRadius(6),
                            Child = new TextBlock
                            {
                                Text = issue,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            },
                        });
                    }
                    systemIssuesList.ItemsSource = systemIssuesChildren;
                }

                if (modIssues != null && modIssues.Count > 0 && modIssuesPanel != null && modIssuesList != null)
                {
                    modIssuesPanel.IsVisible = true;
                    modIssuesList.ItemsSource = new ObservableCollection<ValidationIssue>(modIssues);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenOutput_Click(object sender, RoutedEventArgs e)
        {
            _openOutputCallback?.Invoke();
            // Don't close the dialog - just open the output window
        }

        [UsedImplicitly]
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        [UsedImplicitly]
        private void ToggleMaximizeButton_Click([NotNull] object sender, [NotNull] RoutedEventArgs e)
        {
            if (!(sender is Button maximizeButton))
            {
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                maximizeButton.Content = "▢";
            }
            else
            {
                WindowState = WindowState.Maximized;
                maximizeButton.Content = "▣";
            }
        }

        private void ValidationIssueItem_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.ClickCount != 2)
            {
                return;
            }

            if (!(sender is Border border) || !(border.DataContext is ValidationIssue issue))
            {
                return;
            }

            try
            {
                // Show validation issue details dialog
                var detailsDialog = new ValidationIssueDetailsDialog(issue);
                _ = detailsDialog.ShowDialog(this);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to show validation issue details: {ex.Message}");
            }
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

        public static async Task<bool> ShowValidationDialog(
            Window parent,
            bool success,
            string summaryMessage,
            List<ValidationIssue> modIssues = null,
            List<string> systemIssues = null,
            Action openOutputCallback = null)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new ValidationDialog(success, summaryMessage, modIssues, systemIssues, openOutputCallback);


                await dialog.ShowDialog(parent);
            });
            return true;
        }
    }
}
