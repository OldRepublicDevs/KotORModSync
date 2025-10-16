// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Controls
{
	[SuppressMessage("ReSharper", "UnusedParameter.Local")]
	public partial class RawTab : UserControl
	{
		public static readonly StyledProperty<ModComponent> CurrentComponentProperty =
			AvaloniaProperty.Register<RawTab, ModComponent>(nameof(CurrentComponent));

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

		public event EventHandler<RoutedEventArgs> ApplyEditorChangesRequested;
		public event EventHandler<RoutedEventArgs> GenerateGuidRequested;

		public RawTab()
		{
			InitializeComponent();
			DataContext = this;
		}

		private void ApplyEditorButton_Click(object sender, RoutedEventArgs e)
		{
			ApplyEditorChangesRequested?.Invoke(sender, e);
		}

		private void GenerateGuidButton_Click(object sender, RoutedEventArgs e)
		{
			GenerateGuidRequested?.Invoke(sender, e);
		}

		public TextBox GetGuidTextBox() => GuidGeneratedTextBox;

		public TextBox GetRawEditTextBox() => RawEditTextBox;
	}
}

