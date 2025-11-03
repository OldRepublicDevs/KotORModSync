// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Models;
using KOTORModSync.Services;

namespace KOTORModSync.Dialogs
{
    public partial class ModFilesBrowserDialog : Window, INotifyPropertyChanged
    {
        private bool _mouseDownForWindowMoving;
        private PointerPoint _originalPoint;
        private ObservableCollection<FileTreeNode> _rootNodes;

        public ModFilesBrowserDialog()
        {
            InitializeComponent();
            DataContext = this;
            ThemeManager.ApplyCurrentToWindow(this);

            PointerPressed += InputElement_OnPointerPressed;
            PointerMoved += InputElement_OnPointerMoved;
            PointerReleased += InputElement_OnPointerReleased;
            PointerExited += InputElement_OnPointerReleased;

            RootNodes = new ObservableCollection<FileTreeNode>();
        }

        public ObservableCollection<FileTreeNode> RootNodes
        {
            get => _rootNodes;
            set
            {
                if (_rootNodes != value)
                {
                    _rootNodes = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The selected file paths from the dialog (after user clicks Select).
        /// </summary>
        public List<string> SelectedPaths { get; private set; }

        /// <summary>
        /// Whether the user confirmed the selection.
        /// </summary>
        public bool UserConfirmed { get; private set; }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <summary>
        /// Shows the dialog and allows user to browse mod files/archives.
        /// </summary>
        public static async Task<ModFilesBrowserDialog> ShowBrowserDialogAsync(
            [NotNull] Window parentWindow,
            [NotNull] ModComponent component)
        {
            if (parentWindow == null)
            {
                throw new ArgumentNullException(nameof(parentWindow));
            }

            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            ModFilesBrowserDialog dialog = null;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                dialog = new ModFilesBrowserDialog();

                // Build file tree from component's ResourceRegistry
                var enumerationService = new ArchiveEnumerationService();
                dialog.RootNodes = await enumerationService.BuildFileTreeFromComponentAsync(component);

                // Expand root level by default
                foreach (FileTreeNode node in dialog.RootNodes)
                {
                    node.IsExpanded = true;
                }

                await dialog.ShowDialog(parentWindow);
            });

            return dialog;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SelectedPaths = CollectSelectedPaths();
                UserConfirmed = true;
                Close();
            }
            catch (Exception ex)
            {
                _ = Logger.LogExceptionAsync(ex, "[ModFilesBrowserDialog] Error collecting selected paths");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            UserConfirmed = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Treat close button like cancel
            CancelButton_Click(sender, e);
        }

        private List<string> CollectSelectedPaths()
        {
            var selectedPaths = new List<string>();
            CollectSelectedPathsRecursive(RootNodes, selectedPaths);
            return selectedPaths;
        }

        private void CollectSelectedPathsRecursive(IEnumerable<FileTreeNode> nodes, List<string> selectedPaths)
        {
            foreach (FileTreeNode node in nodes)
            {
                if (node.IsChecked)
                {
                    // Format path appropriately
                    if (node.IsArchive)
                    {
                        // Archive file itself
                        selectedPaths.Add($"<<modDirectory>>\\{node.Name}");
                    }
                    else if (!string.IsNullOrEmpty(node.ArchiveSource))
                    {
                        // File inside an archive
                        string archiveName = System.IO.Path.GetFileName(node.ArchiveSource);
                        selectedPaths.Add($"<<modDirectory>>\\{archiveName}\\{node.Path}");
                    }
                    else
                    {
                        // Regular file
                        selectedPaths.Add($"<<modDirectory>>\\{node.Name}");
                    }
                }

                // Recursively collect from children if parent is not fully selected
                if (node.Children.Count > 0 && !node.IsChecked)
                {
                    CollectSelectedPathsRecursive(node.Children, selectedPaths);
                }
            }
        }

        private void InputElement_OnPointerMoved([NotNull] object sender, [NotNull] PointerEventArgs e)
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

        private void InputElement_OnPointerPressed([NotNull] object sender, [NotNull] PointerEventArgs e)
        {
            if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
            {
                return;
            }

            _mouseDownForWindowMoving = true;
            _originalPoint = e.GetCurrentPoint(this);
        }

        private void InputElement_OnPointerReleased([NotNull] object sender, [NotNull] PointerEventArgs e) =>
            _mouseDownForWindowMoving = false;

        public new event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

