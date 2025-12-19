// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using Avalonia.Controls;
using Avalonia.Interactivity;

using JetBrains.Annotations;

namespace KOTORModSync.Controls
{
    public partial class GettingStartedTab : UserControl
    {
        public GettingStartedTab()
        {
            InitializeComponent();
        }

        #region Routed Events

        public event EventHandler<DirectoryChangedEventArgs> DirectoryChangedRequested;

        public event EventHandler<RoutedEventArgs> LoadInstructionFileRequested;

        public event EventHandler<RoutedEventArgs> OpenSettingsRequested;

        public event EventHandler<RoutedEventArgs> ScrapeDownloadsRequested;

        public event EventHandler<RoutedEventArgs> OpenModDirectoryRequested;

        public event EventHandler<RoutedEventArgs> DownloadStatusRequested;

        public event EventHandler<RoutedEventArgs> StopDownloadsRequested;

        public event EventHandler<RoutedEventArgs> ValidateRequested;

        public event EventHandler<RoutedEventArgs> PrevErrorRequested;

        public event EventHandler<RoutedEventArgs> NextErrorRequested;

        public event EventHandler<RoutedEventArgs> AutoFixRequested;

        public event EventHandler<RoutedEventArgs> JumpToModRequested;

        public event EventHandler<RoutedEventArgs> InstallRequested;

        public event EventHandler<RoutedEventArgs> OpenOutputWindowRequested;

        public event EventHandler<RoutedEventArgs> CreateGithubIssueRequested;

        public event EventHandler<RoutedEventArgs> OpenSponsorPageRequested;

        public event EventHandler<RoutedEventArgs> JumpToCurrentStepRequested;

        #endregion

        #region Event Handlers

        [UsedImplicitly]
        private void OnDirectoryChanged(object sender, DirectoryChangedEventArgs e)
        {
            DirectoryChangedRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void Step2Button_Click(object sender, RoutedEventArgs e)
        {
            LoadInstructionFileRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void ScrapeDownloadsButton_Click(object sender, RoutedEventArgs e)
        {
            ScrapeDownloadsRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void OpenModDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            OpenModDirectoryRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void DownloadStatusButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadStatusRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void StopDownloadsButton_Click(object sender, RoutedEventArgs e)
        {
            StopDownloadsRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedValidateButton_Click(object sender, RoutedEventArgs e)
        {
            ValidateRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void PrevErrorButton_Click(object sender, RoutedEventArgs e)
        {
            PrevErrorRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void NextErrorButton_Click(object sender, RoutedEventArgs e)
        {
            NextErrorRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void AutoFixButton_Click(object sender, RoutedEventArgs e)
        {
            AutoFixRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void JumpToModButton_Click(object sender, RoutedEventArgs e)
        {
            JumpToModRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            InstallRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void OpenOutputWindow_Click(object sender, RoutedEventArgs e)
        {
            OpenOutputWindowRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void CreateGithubIssue_Click(object sender, RoutedEventArgs e)
        {
            CreateGithubIssueRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void OpenSponsorPage_Click(object sender, RoutedEventArgs e)
        {
            OpenSponsorPageRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void JumpToCurrentStep_Click(object sender, RoutedEventArgs e)
        {
            JumpToCurrentStepRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_AutoFixRequested(object sender, RoutedEventArgs e)
        {
            AutoFixRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_CreateGithubIssueRequested(object sender, RoutedEventArgs e)
        {
            CreateGithubIssueRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_DirectoryChangedRequested(object sender, DirectoryChangedEventArgs e)
        {
            DirectoryChangedRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_DownloadStatusRequested(object sender, RoutedEventArgs e)
        {
            DownloadStatusRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_InstallRequested(object sender, RoutedEventArgs e)
        {
            InstallRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_JumpToCurrentStepRequested(object sender, RoutedEventArgs e)
        {
            JumpToCurrentStepRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_JumpToModRequested(object sender, RoutedEventArgs e)
        {
            JumpToModRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_LoadInstructionFileRequested(object sender, RoutedEventArgs e)
        {
            LoadInstructionFileRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_NextErrorRequested(object sender, RoutedEventArgs e)
        {
            NextErrorRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_OpenModDirectoryRequested(object sender, RoutedEventArgs e)
        {
            OpenModDirectoryRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_OpenOutputWindowRequested(object sender, RoutedEventArgs e)
        {
            OpenOutputWindowRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_OpenSettingsRequested(object sender, RoutedEventArgs e)
        {
            OpenSettingsRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_OpenSponsorPageRequested(object sender, RoutedEventArgs e)
        {
            OpenSponsorPageRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_PrevErrorRequested(object sender, RoutedEventArgs e)
        {
            PrevErrorRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_ScrapeDownloadsRequested(object sender, RoutedEventArgs e)
        {
            ScrapeDownloadsRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_StopDownloadsRequested(object sender, RoutedEventArgs e)
        {
            StopDownloadsRequested?.Invoke(this, e);
        }

        [UsedImplicitly]
        private void GettingStartedTab_ValidateRequested(object sender, RoutedEventArgs e)
        {
            ValidateRequested?.Invoke(this, e);
        }

        #endregion
    }
}
