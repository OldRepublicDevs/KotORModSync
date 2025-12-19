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
using Avalonia.VisualTree;

using JetBrains.Annotations;

using KOTORModSync.Core;

namespace KOTORModSync.Dialogs
{
    public partial class ValidationIssueDetailsDialog : Window
    {
        private readonly ValidationIssue _validationIssue;
        private bool _mouseDownForWindowMoving;
        private PointerPoint _originalPoint;

        public ValidationIssueDetailsDialog() => InitializeComponent();

        public ValidationIssueDetailsDialog(ValidationIssue issue) : this()
        {
            _validationIssue = issue ?? throw new ArgumentNullException(nameof(issue));
            LoadDetails();
            WireUpEvents();

            PointerPressed += InputElement_OnPointerPressed;
            PointerMoved += InputElement_OnPointerMoved;
            PointerReleased += InputElement_OnPointerReleased;
            PointerExited += InputElement_OnPointerReleased;

            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        private void LoadDetails()
        {
            if (_validationIssue is null)
            {
                return;
            }

            TextBlock issueIconText = this.FindControl<TextBlock>("IssueIconText");
            if (issueIconText != null)
            {
                issueIconText.Text = _validationIssue.Icon ?? "❓";
            }

            TextBlock modNameText = this.FindControl<TextBlock>("ModNameText");
            if (modNameText != null)
            {
                modNameText.Text = _validationIssue.ModName ?? "Unknown Mod";
            }

            TextBlock issueTypeText = this.FindControl<TextBlock>("IssueTypeText");
            if (issueTypeText != null)
            {
                issueTypeText.Text = $"Issue Type: {_validationIssue.IssueType ?? "Unknown"}";
            }

            TextBlock descriptionText = this.FindControl<TextBlock>("DescriptionText");
            if (descriptionText != null)
            {
                descriptionText.Text = _validationIssue.Description ?? "No description available.";
            }

            TextBlock categoryText = this.FindControl<TextBlock>("CategoryText");
            if (categoryText != null && _validationIssue.VfsIssue != null)
            {
                categoryText.Text = _validationIssue.VfsIssue.Category ?? "Unknown";
            }

            TextBlock severityText = this.FindControl<TextBlock>("SeverityText");
            if (severityText != null && _validationIssue.VfsIssue != null)
            {
                severityText.Text = _validationIssue.VfsIssue.Severity.ToString();
            }

            TextBlock affectedPathText = this.FindControl<TextBlock>("AffectedPathText");
            if (affectedPathText != null && _validationIssue.VfsIssue != null)
            {
                affectedPathText.Text = _validationIssue.VfsIssue.AffectedPath ?? "N/A";
            }

            TextBlock instructionText = this.FindControl<TextBlock>("InstructionText");
            if (instructionText != null && _validationIssue.VfsIssue != null && _validationIssue.VfsIssue.AffectedInstruction != null)
            {
                Instruction instr = _validationIssue.VfsIssue.AffectedInstruction;
                string instrText = $"Action: {instr.Action}";
                if (instr.Source != null && instr.Source.Count > 0)
                {
                    instrText += $"\nSource: {string.Join(", ", instr.Source)}";
                }
                if (!string.IsNullOrEmpty(instr.Destination))
                {
                    instrText += $"\nDestination: {instr.Destination}";
                }
                instructionText.Text = instrText;
            }
            else if (instructionText != null)
            {
                instructionText.Text = "N/A";
            }

            Border solutionSection = this.FindControl<Border>("SolutionSection");
            TextBlock solutionText = this.FindControl<TextBlock>("SolutionText");
            if (solutionSection != null && solutionText != null)
            {
                if (!string.IsNullOrWhiteSpace(_validationIssue.Solution))
                {
                    solutionText.Text = _validationIssue.Solution;
                    solutionSection.IsVisible = true;
                }
                else
                {
                    solutionSection.IsVisible = false;
                }
            }

            TextBox logsTextBox = this.FindControl<TextBox>("LogsTextBox");
            if (logsTextBox != null && _validationIssue.Component != null)
            {
                // Generate logs based on VFS issue and component
                var logLines = new List<string>();
                logLines.Add($"=== Validation Log for: {_validationIssue.ModName} ===");
                logLines.Add("");

                if (_validationIssue.AllVfsIssues != null && _validationIssue.AllVfsIssues.Count > 0)
                {
                    logLines.Add($"=== Found {_validationIssue.AllVfsIssues.Count} Validation Issue(s) ===");
                    logLines.Add("");

                    for (int i = 0; i < _validationIssue.AllVfsIssues.Count; i++)
                    {
                        Core.Services.FileSystem.ValidationIssue vfsIssue = _validationIssue.AllVfsIssues[i];
                        logLines.Add($"--- Issue #{i + 1} ---");
                        logLines.Add($"Timestamp: {vfsIssue.Timestamp:yyyy-MM-dd HH:mm:ss}");
                        logLines.Add($"Severity: {vfsIssue.Severity}");
                        logLines.Add($"Category: {vfsIssue.Category}");
                        logLines.Add($"Message: {vfsIssue.Message}");
                        if (!string.IsNullOrEmpty(vfsIssue.AffectedPath))
                        {
                            logLines.Add($"Affected Path: {vfsIssue.AffectedPath}");
                        }
                        if (vfsIssue.InstructionIndex > 0)
                        {
                            logLines.Add($"Instruction Index: {vfsIssue.InstructionIndex}");
                        }
                        if (vfsIssue.AffectedInstruction != null)
                        {
                            logLines.Add($"Instruction Action: {vfsIssue.AffectedInstruction.Action}");
                            if (vfsIssue.AffectedInstruction.Source != null && vfsIssue.AffectedInstruction.Source.Count > 0)
                            {
                                logLines.Add($"Instruction Source: {string.Join(", ", vfsIssue.AffectedInstruction.Source)}");
                            }
                        }
                        logLines.Add("");
                    }
                }
                else if (_validationIssue.VfsIssue != null)
                {
                    Core.Services.FileSystem.ValidationIssue vfsIssue = _validationIssue.VfsIssue;
                    logLines.Add($"Timestamp: {vfsIssue.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    logLines.Add($"Severity: {vfsIssue.Severity}");
                    logLines.Add($"Category: {vfsIssue.Category}");
                    logLines.Add($"Message: {vfsIssue.Message}");
                    if (!string.IsNullOrEmpty(vfsIssue.AffectedPath))
                    {
                        logLines.Add($"Affected Path: {vfsIssue.AffectedPath}");
                    }
                    if (vfsIssue.InstructionIndex > 0)
                    {
                        logLines.Add($"Instruction Index: {vfsIssue.InstructionIndex}");
                    }
                    logLines.Add("");
                }

                logLines.Add("=== Component Information ===");
                if (_validationIssue.Component != null)
                {
                    logLines.Add($"Name: {_validationIssue.Component.Name}");
                    logLines.Add($"GUID: {_validationIssue.Component.Guid}");
                    logLines.Add($"Instructions Count: {_validationIssue.Component.Instructions.Count}");
                    logLines.Add("");
                }

                logLines.Add("=== VFS Execution Summary ===");
                logLines.Add("This validation was performed using VirtualFileSystemProvider");
                logLines.Add("which simulates the installation by executing all instructions");
                logLines.Add("with a dry-run file system. Any errors or warnings detected");
                logLines.Add("during this simulation are shown above.");

                logsTextBox.Text = string.Join(Environment.NewLine, logLines);
            }

            Border additionalIssuesSection = this.FindControl<Border>("AdditionalIssuesSection");
            TextBlock additionalIssuesText = this.FindControl<TextBlock>("AdditionalIssuesText");
            if (
                additionalIssuesSection != null
                && additionalIssuesText != null
                && _validationIssue.AllVfsIssues != null
                && _validationIssue.AllVfsIssues.Count > 1)
            {
                additionalIssuesText.Text = $"This component has {_validationIssue.AllVfsIssues.Count} total validation issues. See the logs section above for details on all issues.";
                additionalIssuesSection.IsVisible = true;
            }
        }

        private void WireUpEvents()
        {
            Button closeButton = this.FindControl<Button>("CloseButton");
            if (closeButton != null)
            {
                closeButton.Click += CloseButton_Click;
            }
        }

        [UsedImplicitly]
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

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
    }
}

