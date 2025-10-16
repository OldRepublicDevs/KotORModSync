// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JetBrains.Annotations;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Controls
{
	[SuppressMessage("ReSharper", "UnusedParameter.Local")]
	public partial class EditorTab : UserControl
	{
		public static readonly StyledProperty<ModComponent> CurrentComponentProperty =
			AvaloniaProperty.Register<EditorTab, ModComponent>(nameof(CurrentComponent));

		public static readonly StyledProperty<List<string>> TierOptionsProperty =
			AvaloniaProperty.Register<EditorTab, List<string>>(nameof(TierOptions));

		public static readonly StyledProperty<DownloadCacheService> DownloadCacheServiceProperty =
			AvaloniaProperty.Register<EditorTab, DownloadCacheService>(nameof(DownloadCacheService));

		public static readonly StyledProperty<ModManagementService> ModManagementServiceProperty =
			AvaloniaProperty.Register<EditorTab, ModManagementService>(nameof(ModManagementService));

		[CanBeNull]
		public ModComponent CurrentComponent
		{
			get => MainConfig.CurrentComponent;
			set
			{
				MainConfig.CurrentComponent = value;
				SetValue(CurrentComponentProperty, value);
			}
		}

		[CanBeNull]
		public List<string> TierOptions
		{
			get => GetValue(TierOptionsProperty);
			set => SetValue(TierOptionsProperty, value);
		}

		[CanBeNull]
		public DownloadCacheService DownloadCacheService
		{
			get => GetValue(DownloadCacheServiceProperty);
			set => SetValue(DownloadCacheServiceProperty, value);
		}

		[CanBeNull]
		public ModManagementService ModManagementService
		{
			get => GetValue(ModManagementServiceProperty);
			set => SetValue(ModManagementServiceProperty, value);
		}

		public event EventHandler<RoutedEventArgs> ExpandAllSectionsRequested;
		public event EventHandler<RoutedEventArgs> CollapseAllSectionsRequested;
		public event EventHandler<RoutedEventArgs> AutoGenerateInstructionsRequested;
		public event EventHandler<RoutedEventArgs> AddNewInstructionRequested;
		public event EventHandler<RoutedEventArgs> DeleteInstructionRequested;
		public event EventHandler<RoutedEventArgs> BrowseDestinationRequested;
		public event EventHandler<RoutedEventArgs> BrowseSourceFilesRequested;
		public event EventHandler<RoutedEventArgs> BrowseSourceFromFoldersRequested;

		private Button _autoGenerateButton;
		public event EventHandler<RoutedEventArgs> MoveInstructionUpRequested;
		public event EventHandler<RoutedEventArgs> MoveInstructionDownRequested;
		public event EventHandler<RoutedEventArgs> AddNewOptionRequested;
		public event EventHandler<RoutedEventArgs> DeleteOptionRequested;
		public event EventHandler<RoutedEventArgs> MoveOptionUpRequested;
		public event EventHandler<RoutedEventArgs> MoveOptionDownRequested;
		public event EventHandler<Core.Services.Validation.PathValidationResult> JumpToBlockingInstructionRequested;

		public EditorTab()
		{
			InitializeComponent();
			DataContext = this;
			_autoGenerateButton = this.FindControl<Button>("AutoGenerateButton");
		}

		private void ExpandAllSections_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				BasicInfoExpander.IsExpanded = true;
				DescriptionExpander.IsExpanded = true;
				DependenciesExpander.IsExpanded = true;
				InstructionsExpander.IsExpanded = true;
				OptionsExpander.IsExpanded = true;
				ExpandAllSectionsRequested?.Invoke(sender, e);
			}
			catch ( Exception ex )
			{
				Core.Logger.LogException(ex, "Error expanding all sections");
			}
		}

		private void CollapseAllSections_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				BasicInfoExpander.IsExpanded = false;
				DescriptionExpander.IsExpanded = false;
				DependenciesExpander.IsExpanded = false;
				InstructionsExpander.IsExpanded = false;
				OptionsExpander.IsExpanded = false;
				CollapseAllSectionsRequested?.Invoke(sender, e);
			}
			catch ( Exception ex )
			{
				Core.Logger.LogException(ex, "Error collapsing all sections");
			}
		}

		private void AutoGenerateInstructions_Click(object sender, RoutedEventArgs e)
		{
			AutoGenerateInstructionsRequested?.Invoke(sender, e);
		}

		private void AddNewInstruction_Click(object sender, RoutedEventArgs e)
		{
			AddNewInstructionRequested?.Invoke(sender, e);
		}

		private void DeleteInstruction_Click(object sender, RoutedEventArgs e)
		{
			DeleteInstructionRequested?.Invoke(sender, e);
		}

		private void BrowseDestination_Click(object sender, RoutedEventArgs e)
		{
			BrowseDestinationRequested?.Invoke(sender, e);
		}

		private void BrowseSourceFiles_Click(object sender, RoutedEventArgs e)
		{
			BrowseSourceFilesRequested?.Invoke(sender, e);
		}

		private void BrowseSourceFromFolders_Click(object sender, RoutedEventArgs e)
		{
			BrowseSourceFromFoldersRequested?.Invoke(sender, e);
		}

		private void MoveInstructionUp_Click(object sender, RoutedEventArgs e)
		{
			MoveInstructionUpRequested?.Invoke(sender, e);
		}

		private void MoveInstructionDown_Click(object sender, RoutedEventArgs e)
		{
			MoveInstructionDownRequested?.Invoke(sender, e);
		}

		private void AddNewOption_Click(object sender, RoutedEventArgs e)
		{
			AddNewOptionRequested?.Invoke(sender, e);
		}

		private void DeleteOption_Click(object sender, RoutedEventArgs e)
		{
			DeleteOptionRequested?.Invoke(sender, e);
		}

		private void MoveOptionUp_Click(object sender, RoutedEventArgs e)
		{
			MoveOptionUpRequested?.Invoke(sender, e);
		}

		private void MoveOptionDown_Click(object sender, RoutedEventArgs e)
		{
			MoveOptionDownRequested?.Invoke(sender, e);
		}

		private void JumpToBlockingInstruction_Handler(object sender, Core.Services.Validation.PathValidationResult e)
		{
			JumpToBlockingInstructionRequested?.Invoke(sender, e);
		}
	}
}

