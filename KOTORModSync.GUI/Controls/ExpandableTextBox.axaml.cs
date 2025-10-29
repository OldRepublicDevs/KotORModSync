// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

using JetBrains.Annotations;

namespace KOTORModSync.Controls
{
	public partial class ExpandableTextBox : UserControl
	{
		public static readonly StyledProperty<string> TextProperty =
			AvaloniaProperty.Register<ExpandableTextBox, string>(
				nameof( Text ),
				defaultBindingMode: Avalonia.Data.BindingMode.TwoWay );

		public static readonly StyledProperty<string> WatermarkProperty =
			AvaloniaProperty.Register<ExpandableTextBox, string>( nameof( Watermark ) );

		public string Text
		{
			get => GetValue( TextProperty );
			set => SetValue( TextProperty, value );
		}

		public string Watermark
		{
			get => GetValue( WatermarkProperty );
			set => SetValue( WatermarkProperty, value );
		}

		public ExpandableTextBox()
		{
			InitializeComponent();
		}

		private void SingleLineTextBox_PointerPressed( [CanBeNull] object sender, [NotNull] PointerPressedEventArgs e )
		{
			ExpandEditor();
		}

		private void ExpandEditor()
		{
			SingleLineTextBox.IsVisible = false;
			FullEditorTextBox.IsVisible = true;
			_ = FullEditorTextBox.Focus();
			FullEditorTextBox.SelectAll();
		}

		private void CollapseEditor()
		{
			FullEditorTextBox.IsVisible = false;
			SingleLineTextBox.IsVisible = true;
		}

		private void FullEditorTextBox_LostFocus( [CanBeNull] object sender, [NotNull] RoutedEventArgs e )
		{
			CollapseEditor();
		}

		private void FullEditorTextBox_KeyDown( [CanBeNull] object sender, [NotNull] KeyEventArgs e )
		{
			if (e.Key == Key.Escape)
			{
				CollapseEditor();
				e.Handled = true;
			}
			else if (e.Key == Key.Enter && e.KeyModifiers.HasFlag( KeyModifiers.Control ))
			{
				CollapseEditor();
				e.Handled = true;
			}
		}
	}
}