// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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

		// Event for directory changes
		public event EventHandler<DirectoryChangedEventArgs> DirectoryChangedRequested;

		// Event for Step 2 button click
		public event EventHandler<RoutedEventArgs> LoadInstructionFileRequested;

		// Event for opening settings
		public event EventHandler<RoutedEventArgs> OpenSettingsRequested;

		// Event for scraping downloads
		public event EventHandler<RoutedEventArgs> ScrapeDownloadsRequested;

		// Event for opening mod directory
		public event EventHandler<RoutedEventArgs> OpenModDirectoryRequested;

		// Event for download status button
		public event EventHandler<RoutedEventArgs> DownloadStatusRequested;

		// Event for stopping downloads
		public event EventHandler<RoutedEventArgs> StopDownloadsRequested;

		// Event for validation
		public event EventHandler<RoutedEventArgs> ValidateRequested;

		// Event for previous error navigation
		public event EventHandler<RoutedEventArgs> PrevErrorRequested;

		// Event for next error navigation
		public event EventHandler<RoutedEventArgs> NextErrorRequested;

		// Event for auto-fix button
		public event EventHandler<RoutedEventArgs> AutoFixRequested;

		// Event for jump to mod button
		public event EventHandler<RoutedEventArgs> JumpToModRequested;

		// Event for install button
		public event EventHandler<RoutedEventArgs> InstallRequested;

		// Event for opening output window
		public event EventHandler<RoutedEventArgs> OpenOutputWindowRequested;

		// Event for creating GitHub issue
		public event EventHandler<RoutedEventArgs> CreateGithubIssueRequested;

		// Event for opening sponsor page
		public event EventHandler<RoutedEventArgs> OpenSponsorPageRequested;

		// Event for jumping to current step
		public event EventHandler<RoutedEventArgs> JumpToCurrentStepRequested;

		#endregion

		#region Event Handlers

		[UsedImplicitly]
		private void OnDirectoryChanged(object sender, DirectoryChangedEventArgs e)
		{
			DirectoryChangedRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void Step2Button_Click(object sender, RoutedEventArgs e)
		{
			LoadInstructionFileRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void OpenSettings_Click(object sender, RoutedEventArgs e)
		{
			OpenSettingsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void ScrapeDownloadsButton_Click(object sender, RoutedEventArgs e)
		{
			ScrapeDownloadsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void OpenModDirectoryButton_Click(object sender, RoutedEventArgs e)
		{
			OpenModDirectoryRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void DownloadStatusButton_Click(object sender, RoutedEventArgs e)
		{
			DownloadStatusRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void StopDownloadsButton_Click(object sender, RoutedEventArgs e)
		{
			StopDownloadsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedValidateButton_Click(object sender, RoutedEventArgs e)
		{
			ValidateRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void PrevErrorButton_Click(object sender, RoutedEventArgs e)
		{
			PrevErrorRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void NextErrorButton_Click(object sender, RoutedEventArgs e)
		{
			NextErrorRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void AutoFixButton_Click(object sender, RoutedEventArgs e)
		{
			AutoFixRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void JumpToModButton_Click(object sender, RoutedEventArgs e)
		{
			JumpToModRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void InstallButton_Click(object sender, RoutedEventArgs e)
		{
			InstallRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void OpenOutputWindow_Click(object sender, RoutedEventArgs e)
		{
			OpenOutputWindowRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void CreateGithubIssue_Click(object sender, RoutedEventArgs e)
		{
			CreateGithubIssueRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void OpenSponsorPage_Click(object sender, RoutedEventArgs e)
		{
			OpenSponsorPageRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void JumpToCurrentStep_Click(object sender, RoutedEventArgs e)
		{
			JumpToCurrentStepRequested?.Invoke(sender, e);
		}

		// Handler methods for MainWindow.axaml event bindings
		[UsedImplicitly]
		private void GettingStartedTab_AutoFixRequested(object sender, RoutedEventArgs e)
		{
			AutoFixRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_CreateGithubIssueRequested(object sender, RoutedEventArgs e)
		{
			CreateGithubIssueRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_DirectoryChangedRequested(object sender, DirectoryChangedEventArgs e)
		{
			DirectoryChangedRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_DownloadStatusRequested(object sender, RoutedEventArgs e)
		{
			DownloadStatusRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_InstallRequested(object sender, RoutedEventArgs e)
		{
			InstallRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_JumpToCurrentStepRequested(object sender, RoutedEventArgs e)
		{
			JumpToCurrentStepRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_JumpToModRequested(object sender, RoutedEventArgs e)
		{
			JumpToModRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_LoadInstructionFileRequested(object sender, RoutedEventArgs e)
		{
			LoadInstructionFileRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_NextErrorRequested(object sender, RoutedEventArgs e)
		{
			NextErrorRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_OpenModDirectoryRequested(object sender, RoutedEventArgs e)
		{
			OpenModDirectoryRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_OpenOutputWindowRequested(object sender, RoutedEventArgs e)
		{
			OpenOutputWindowRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_OpenSettingsRequested(object sender, RoutedEventArgs e)
		{
			OpenSettingsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_OpenSponsorPageRequested(object sender, RoutedEventArgs e)
		{
			OpenSponsorPageRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_PrevErrorRequested(object sender, RoutedEventArgs e)
		{
			PrevErrorRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_ScrapeDownloadsRequested(object sender, RoutedEventArgs e)
		{
			ScrapeDownloadsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_StopDownloadsRequested(object sender, RoutedEventArgs e)
		{
			StopDownloadsRequested?.Invoke(sender, e);
		}

		[UsedImplicitly]
		private void GettingStartedTab_ValidateRequested(object sender, RoutedEventArgs e)
		{
			ValidateRequested?.Invoke(sender, e);
		}

		#endregion
	}
}

