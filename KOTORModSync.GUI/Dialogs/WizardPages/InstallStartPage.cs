// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs.WizardPages
{
    public class InstallStartPage : IWizardPage
    {
        public string Title => "Ready to Install";
        public string Subtitle => "Review your selections and begin installation";
        public Control Content { get; }
        public bool CanNavigateBack => true;
        public bool CanNavigateForward => true;
        public bool CanCancel => true;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1450:Private fields only used as local variables in methods should become local variables", Justification = "<Pending>")]
        private readonly List<ModComponent> _allComponents;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public InstallStartPage([NotNull][ItemNotNull] List<ModComponent> allComponents)
        {
            _allComponents = allComponents ?? throw new ArgumentNullException(nameof(allComponents));

            var panel = new StackPanel
            {
                Spacing = 16
            };

            panel.Children.Add(new TextBlock
            {
                Text = "🚀 Ready to Begin Installation",
                FontSize = 24,
                FontWeight = FontWeight.Bold
            });

            panel.Children.Add(new TextBlock
            {
                Text = "Review your mod selection below. Click 'Next' to begin the installation process.",
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            });

            // Installation summary
            var selectedMods = _allComponents.Where(c => c.IsSelected && !c.WidescreenOnly).ToList();

            var summaryBorder = new Border
            {
                Padding = new Avalonia.Thickness(16),
                CornerRadius = new Avalonia.CornerRadius(8)
            };

            var summaryPanel = new StackPanel
            {
                Spacing = 12
            };

            summaryPanel.Children.Add(new TextBlock
            {
                Text = $"📦 {selectedMods.Count} mods selected for installation",
                FontSize = 16,
                FontWeight = FontWeight.SemiBold
            });

            // Show mod list
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 250
            };

            var modListPanel = new StackPanel
            {
                Spacing = 4
            };

            foreach (var mod in selectedMods)
            {
                modListPanel.Children.Add(new TextBlock
                {
                    Text = $"• {mod.Name}",
                    FontSize = 12
                });
            }

            scrollViewer.Content = modListPanel;
            summaryPanel.Children.Add(scrollViewer);

            summaryBorder.Child = summaryPanel;
            panel.Children.Add(summaryBorder);

            // Warning
            panel.Children.Add(new Border
            {
                Padding = new Avalonia.Thickness(12),
                CornerRadius = new Avalonia.CornerRadius(4),
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "⚠️ Important",
                            FontSize = 16,
                            FontWeight = FontWeight.Bold
                        },
                        new TextBlock
                        {
                            Text = "• The installation process cannot be fully reversed",
                            FontSize = 13
                        },
                        new TextBlock
                        {
                            Text = "• Checkpoints will be created so you can roll back if needed",
                            FontSize = 13
                        },
                        new TextBlock
                        {
                            Text = "• Do not close the application during installation",
                            FontSize = 13
                        }
                    }
                }
            });

            Content = panel;
        }

        public Task OnNavigatedToAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task OnNavigatingFromAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult((true, (string)null));
        }
    }
}

