// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace KOTORModSync.Dialogs
{
    /// <summary>
    /// Tracks the state of a wizard page for navigation purposes
    /// </summary>
    public class WizardPageInfo : INotifyPropertyChanged
    {
        private bool _isCompleted;
        private bool _isCurrent;
        private bool _isAccessible;

        public IWizardPage Page { get; }
        public int PageIndex { get; }

        /// <summary>
        /// Gets the display title for this page in the navigation
        /// </summary>
        public string DisplayTitle => Page.Title;

        /// <summary>
        /// Whether this page has been completed (validated and navigated past)
        /// </summary>
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted != value)
                {
                    _isCompleted = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether this is the current active page
        /// </summary>
        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                if (_isCurrent != value)
                {
                    _isCurrent = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Whether this page can be navigated to (is completed or is next in sequence)
        /// </summary>
        public bool IsAccessible
        {
            get => _isAccessible;
            set
            {
                if (_isAccessible != value)
                {
                    _isAccessible = value;
                    OnPropertyChanged();
                }
            }
        }

        public WizardPageInfo(IWizardPage page, int pageIndex)
        {
            Page = page;
            PageIndex = pageIndex;
            _isCompleted = false;
            _isCurrent = false;
            _isAccessible = pageIndex == 0; // Only first page is accessible initially
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

