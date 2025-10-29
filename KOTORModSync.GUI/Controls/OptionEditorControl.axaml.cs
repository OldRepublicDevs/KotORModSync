// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

using JetBrains.Annotations;

namespace KOTORModSync.Controls
{
	public partial class OptionEditorControl : UserControl
	{
		public OptionEditorControl()
		{

			AvaloniaXamlLoader.Load( this );
		}

		public event EventHandler<RoutedEventArgs> AddNewOption;
		public event EventHandler<RoutedEventArgs> DeleteOption;
		public event EventHandler<RoutedEventArgs> MoveOptionUp;
		public event EventHandler<RoutedEventArgs> MoveOptionDown;
		public event EventHandler<RoutedEventArgs> AddNewInstruction;
		public event EventHandler<RoutedEventArgs> DeleteInstruction;
		public event EventHandler<RoutedEventArgs> MoveInstructionUp;
		public event EventHandler<RoutedEventArgs> MoveInstructionDown;
		public event EventHandler<RoutedEventArgs> BrowseSourceFiles;
		public event EventHandler<RoutedEventArgs> BrowseSourceFromFolders;
		public event EventHandler<RoutedEventArgs> BrowseDestination;

		private void AddNewOption_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => AddNewOption?.Invoke( this, e );

		private void DeleteOption_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => DeleteOption?.Invoke( this, e );

		private void MoveOptionUp_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => MoveOptionUp?.Invoke( this, e );

		private void MoveOptionDown_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => MoveOptionDown?.Invoke( this, e );

		private void AddNewInstruction_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => AddNewInstruction?.Invoke( this, e );

		private void DeleteInstruction_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => DeleteInstruction?.Invoke( this, e );

		private void MoveInstructionUp_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => MoveInstructionUp?.Invoke( this, e );

		private void MoveInstructionDown_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => MoveInstructionDown?.Invoke( this, e );

		private void BrowseSourceFiles_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => BrowseSourceFiles?.Invoke( this, e );

		private void BrowseSourceFromFolders_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => BrowseSourceFromFolders?.Invoke( this, e );

		private void BrowseDestination_Click( [NotNull] object sender, [NotNull] RoutedEventArgs e ) => BrowseDestination?.Invoke( this, e );
	}
}