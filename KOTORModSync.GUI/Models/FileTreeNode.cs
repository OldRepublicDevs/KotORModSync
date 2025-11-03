// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using JetBrains.Annotations;

namespace KOTORModSync.Models
{
    /// <summary>
    /// Represents a node in the file tree for the ModFilesBrowserDialog.
    /// Can represent an archive, folder, or file.
    /// </summary>
    public class FileTreeNode : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _isExpanded;
        private bool _isIndeterminate;

        public FileTreeNode()
        {
            Children = new ObservableCollection<FileTreeNode>();
        }

        /// <summary>
        /// Display name of the node (file/folder name).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Full path within the archive or filesystem.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// True if this is a directory/folder, false if it's a file.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// True if this node represents an archive file.
        /// </summary>
        public bool IsArchive { get; set; }

        /// <summary>
        /// The archive file path if this node or its parent is from an archive.
        /// </summary>
        public string ArchiveSource { get; set; }

        /// <summary>
        /// Child nodes (files/folders within this directory or archive).
        /// </summary>
        public ObservableCollection<FileTreeNode> Children { get; set; }

        /// <summary>
        /// Parent node reference.
        /// </summary>
        public FileTreeNode Parent { get; set; }

        /// <summary>
        /// Whether this node is checked (selected).
        /// </summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                    
                    // Update children
                    UpdateChildrenCheckedState(value);
                    
                    // Update parent
                    Parent?.UpdateCheckedStateFromChildren();
                }
            }
        }

        /// <summary>
        /// Whether this node shows indeterminate state (some children checked).
        /// </summary>
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set
            {
                if (_isIndeterminate != value)
                {
                    _isIndeterminate = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether this node is expanded in the tree view.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        private void UpdateChildrenCheckedState(bool isChecked)
        {
            foreach (FileTreeNode child in Children)
            {
                child._isChecked = isChecked;
                child.OnPropertyChanged(nameof(IsChecked));
                child.UpdateChildrenCheckedState(isChecked);
            }
        }

        private void UpdateCheckedStateFromChildren()
        {
            if (Children.Count == 0)
            {
                return;
            }

            bool allChecked = true;
            bool anyChecked = false;

            foreach (FileTreeNode child in Children)
            {
                if (child.IsChecked || child.IsIndeterminate)
                {
                    anyChecked = true;
                }

                if (!child.IsChecked)
                {
                    allChecked = false;
                }
            }

            if (allChecked)
            {
                _isChecked = true;
                _isIndeterminate = false;
            }
            else if (anyChecked)
            {
                _isChecked = false;
                _isIndeterminate = true;
            }
            else
            {
                _isChecked = false;
                _isIndeterminate = false;
            }

            OnPropertyChanged(nameof(IsChecked));
            OnPropertyChanged(nameof(IsIndeterminate));

            Parent?.UpdateCheckedStateFromChildren();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

