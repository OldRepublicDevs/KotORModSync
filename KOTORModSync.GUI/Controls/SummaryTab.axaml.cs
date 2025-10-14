// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Controls
{
	[SuppressMessage("ReSharper", "UnusedParameter.Local")]
	public partial class SummaryTab : UserControl
	{
		// Dependency properties
		public static readonly StyledProperty<ModComponent> CurrentComponentProperty =
			AvaloniaProperty.Register<SummaryTab, ModComponent>(nameof(CurrentComponent));

		public static readonly StyledProperty<bool> EditorModeProperty =
			AvaloniaProperty.Register<SummaryTab, bool>(nameof(EditorMode));

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

		public bool EditorMode
		{
			get => GetValue(EditorModeProperty);
			set => SetValue(EditorModeProperty, value);
		}

		// Event handlers that will be routed to MainWindow
		public event EventHandler<TappedEventArgs> OpenLinkRequested;
		public event EventHandler<RoutedEventArgs> CopyTextToClipboardRequested;
		public event EventHandler<PointerPressedEventArgs> SummaryOptionPointerPressedRequested;
		public event EventHandler<RoutedEventArgs> CheckBoxChangedRequested;
		public event EventHandler<RoutedEventArgs> JumpToInstructionRequested;

		public SummaryTab()
		{
			InitializeComponent();
			DataContext = this;
		}

		// Event handler forwarders
		private void OpenLink_Tapped(object sender, TappedEventArgs e)
		{
			OpenLinkRequested?.Invoke(sender, e);
		}

		private void CopyTextToClipboard_Click(object sender, RoutedEventArgs e)
		{
			CopyTextToClipboardRequested?.Invoke(sender, e);
		}

		private void SummaryOptionBorder_PointerPressed(object sender, PointerPressedEventArgs e)
		{
			SummaryOptionPointerPressedRequested?.Invoke(sender, e);
		}

		private void OnCheckBoxChanged(object sender, RoutedEventArgs e)
		{
			CheckBoxChangedRequested?.Invoke(sender, e);
		}

		private void JumpToInstruction_Click(object sender, RoutedEventArgs e)
		{
			JumpToInstructionRequested?.Invoke(sender, e);
		}

		// Public accessors for TextBlocks so MainWindow can access them for markdown rendering
		public TextBlock GetDescriptionTextBlock() => DescriptionTextBlock;
		public TextBlock GetDirectionsTextBlock() => DirectionsTextBlock;
	}
}

