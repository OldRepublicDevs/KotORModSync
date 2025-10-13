// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Controls
{
	public partial class ScreenshotDisplayControl : UserControl
	{
		public static readonly StyledProperty<string> ScreenshotDataProperty =
			AvaloniaProperty.Register<ScreenshotDisplayControl, string>(nameof(ScreenshotData));

		public string ScreenshotData
		{
			get => GetValue(ScreenshotDataProperty);
			set => SetValue(ScreenshotDataProperty, value);
		}

		public ScreenshotDisplayControl()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if ( change.Property == ScreenshotDataProperty )
			{
				UpdateScreenshotDisplay();
			}
		}

		private void UpdateScreenshotDisplay()
		{
			var imageItemsControl = this.FindControl<ItemsControl>("ImageItemsControl");
			var fallbackTextBlock = this.FindControl<TextBlock>("FallbackTextBlock");

			if ( imageItemsControl == null || fallbackTextBlock == null )
				return;

			if ( string.IsNullOrWhiteSpace(ScreenshotData) )
			{
				imageItemsControl.ItemsSource = null;
				imageItemsControl.IsVisible = false;
				fallbackTextBlock.IsVisible = false;
				return;
			}

			// Split by newlines and filter empty entries
			var lines = ScreenshotData
				.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
				.Select(line => line.Trim())
				.Where(line => !string.IsNullOrWhiteSpace(line))
				.ToList();

			var imageUrls = new List<string>();
			var nonImageContent = new List<string>();

			foreach ( var line in lines )
			{
				if ( IsImageUrl(line) )
				{
					imageUrls.Add(line);
				}
				else
				{
					nonImageContent.Add(line);
				}
			}

			// Display images if we have any
			if ( imageUrls.Count > 0 )
			{
				imageItemsControl.ItemsSource = imageUrls;
				imageItemsControl.IsVisible = true;
			}
			else
			{
				imageItemsControl.ItemsSource = null;
				imageItemsControl.IsVisible = false;
			}

			// Display fallback text if we have non-image content
			if ( nonImageContent.Count > 0 )
			{
				fallbackTextBlock.Text = string.Join(Environment.NewLine, nonImageContent);
				fallbackTextBlock.IsVisible = true;
			}
			else
			{
				fallbackTextBlock.Text = string.Empty;
				fallbackTextBlock.IsVisible = false;
			}
		}

		private static bool IsImageUrl(string text)
		{
			if ( string.IsNullOrWhiteSpace(text) )
				return false;

			// Check if it's a valid URL
			if ( !Uri.TryCreate(text, UriKind.Absolute, out Uri uri) )
				return false;

			// Check if it has http or https scheme
			if ( uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps )
				return false;

			// Check if it has a common image extension
			var path = uri.AbsolutePath.ToLowerInvariant();
			return path.EndsWith(".jpg") ||
				   path.EndsWith(".jpeg") ||
				   path.EndsWith(".png") ||
				   path.EndsWith(".gif") ||
				   path.EndsWith(".bmp") ||
				   path.EndsWith(".webp");
		}

		private void ImageUrl_Tapped(object sender, TappedEventArgs e)
		{
			try
			{
				if ( sender is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text) )
				{
					UrlUtilities.OpenUrl(textBlock.Text);
				}
			}
			catch ( Exception ex )
			{
				Core.Logger.LogException(ex, "Failed to open screenshot URL");
			}
		}
	}
}

