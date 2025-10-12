// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Controls
{
	public partial class DownloadLinksControl : UserControl
	{
		public static readonly StyledProperty<List<string>> DownloadLinksProperty =
			AvaloniaProperty.Register<DownloadLinksControl, List<string>>(nameof(DownloadLinks), new List<string>());

		public static readonly StyledProperty<DownloadCacheService> DownloadCacheServiceProperty =
			AvaloniaProperty.Register<DownloadLinksControl, DownloadCacheService>(nameof(DownloadCacheService));

		public static readonly StyledProperty<Guid> ComponentGuidProperty =
			AvaloniaProperty.Register<DownloadLinksControl, Guid>(nameof(ComponentGuid));

		private bool _isUpdatingFromTextBox; // Re-entrancy guard

		public List<string> DownloadLinks
		{
			get => GetValue(DownloadLinksProperty);
			set => SetValue(DownloadLinksProperty, value);
		}

		public DownloadCacheService DownloadCacheService
		{
			get => GetValue(DownloadCacheServiceProperty);
			set => SetValue(DownloadCacheServiceProperty, value);
		}

		public Guid ComponentGuid
		{
			get => GetValue(ComponentGuidProperty);
			set => SetValue(ComponentGuidProperty, value);
		}

		public DownloadLinksControl()
		{
			InitializeComponent();
			UpdateEmptyStateVisibility();
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);

			if ( change.Property == DownloadLinksProperty )
			{
				// Don't update display if we're in the middle of updating from a TextBox
				if ( _isUpdatingFromTextBox )
					return;

				UpdateLinksDisplay();
				UpdateEmptyStateVisibility();
			}
			else if ( change.Property == DownloadCacheServiceProperty || change.Property == ComponentGuidProperty )
			{
				// Refresh validation when cache service or component changes
				RefreshAllUrlValidation();
			}
		}

		private void RefreshAllUrlValidation()
		{
			try
			{
				var textBoxes = this.GetVisualDescendants().OfType<TextBox>().ToList();
				foreach ( var textBox in textBoxes )
				{
					UpdateUrlValidation(textBox);
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error refreshing URL validation");
			}
		}

		private void UpdateLinksDisplay()
		{
			if ( LinksItemsControl?.ItemsSource == DownloadLinks )
				return;
			LinksItemsControl.ItemsSource = DownloadLinks;

			// Note: URL validation will be handled by individual TextBox TextChanged events
			// This avoids potential deadlocks during UI layout
		}

		private void UpdateEmptyStateVisibility()
		{
			if ( EmptyStateBorder == null )
				return;
			EmptyStateBorder.IsVisible = DownloadLinks == null || DownloadLinks.Count == 0;
		}

		private void AddLink_Click(object sender, RoutedEventArgs e)
		{
			if ( DownloadLinks == null )
				DownloadLinks = new List<string>();

			// User-initiated action, allow full update cycle
			_isUpdatingFromTextBox = false;

			// Create a new list to ensure property change notification
			var newList = new List<string>(DownloadLinks) { string.Empty };
			DownloadLinks = newList;

			// Focus the newly added textbox
			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					var textBoxes = this.GetVisualDescendants().OfType<TextBox>().ToList();
					TextBox lastTextBox = textBoxes.LastOrDefault();
					_ = (lastTextBox?.Focus());
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex, "Error focusing newly added TextBox in DownloadLinksControl");
				}
			}, DispatcherPriority.Input);
		}

		private void RemoveLink_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is Button button) || DownloadLinks == null )
				return;

			// Find the parent Grid, then the Border to get the associated TextBox
			if ( !(button.Parent is Grid parentGrid) )
				return;

			TextBox textBox = parentGrid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
			if ( textBox == null )
				return;

			int index = GetTextBoxIndex(textBox);
			if ( index < 0 || index >= DownloadLinks.Count )
				return;

			// User-initiated action, allow full update cycle
			_isUpdatingFromTextBox = false;

			// Create a new list to ensure property change notification
			var newList = new List<string>(DownloadLinks);
			newList.RemoveAt(index);
			DownloadLinks = newList;
		}

		private void OpenLink_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is Button button) || !(button.Tag is string url) || string.IsNullOrWhiteSpace(url) )
				return;
			try
			{
				// Ensure the URL has a protocol
				if ( !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
					!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) )
				{
					url = "https://" + url;
				}

				var processInfo = new ProcessStartInfo
				{
					FileName = url,
					UseShellExecute = true
				};
				_ = Process.Start(processInfo);
			}
			catch ( Exception ex )
			{
				// Could show an error message to the user here
				Logger.LogException(ex, $"Failed to open URL: {ex.Message}");
			}
		}

		private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if ( !(sender is TextBox textBox) || DownloadLinks == null )
				return;
			// Find the index of this textbox in the list
			int index = GetTextBoxIndex(textBox);
			if ( index < 0 || index >= DownloadLinks.Count )
				return;

			string newText = textBox.Text ?? string.Empty;

			// Only update if the value actually changed to prevent infinite loop
			if ( DownloadLinks[index] == newText )
			{
				UpdateUrlValidation(textBox);
				return;
			}

			// Set re-entrancy guard to prevent OnPropertyChanged from updating display
			_isUpdatingFromTextBox = true;
			try
			{
				// Create a new list to ensure property change notification
				var newList = new List<string>(DownloadLinks)
				{
					[index] = newText
				};
				DownloadLinks = newList;
			}
			finally
			{
				_isUpdatingFromTextBox = false;
			}

			UpdateUrlValidation(textBox);
		}

		private int GetTextBoxIndex(TextBox textBox)
		{
			if ( !(LinksItemsControl?.ItemsSource is List<string> links) || textBox == null )
				return -1;
			try
			{
				var textBoxes = this.GetVisualDescendants().OfType<TextBox>().ToList();
				int index = textBoxes.IndexOf(textBox);
				return index >= 0 && index < links.Count ? index : -1;
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Error getting TextBox index in DownloadLinksControl");
				return -1;
			}
		}

		private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
		{
			switch ( e.Key )
			{
				case Key.Enter:
					// Add a new link when Enter is pressed
					AddLink_Click(sender, e);
					e.Handled = true;
					break;
				case Key.Delete when sender is TextBox deleteTextBox &&
									 DownloadLinks != null && string.IsNullOrWhiteSpace(deleteTextBox.Text):
					{
						// Remove empty link when Delete is pressed on empty textbox
						int index = GetTextBoxIndex(deleteTextBox);
						if ( index >= 0 && index < DownloadLinks.Count )
						{
							// User-initiated action, allow full update cycle
							_isUpdatingFromTextBox = false;

							// Create a new list to ensure property change notification
							var newList = new List<string>(DownloadLinks);
							newList.RemoveAt(index);
							DownloadLinks = newList;
						}
						e.Handled = true;
						break;
					}
			}
		}

		private void UpdateUrlValidation(TextBox textBox)
		{
			if ( textBox == null )
				return;

			// Only validate URLs when in EditorMode
			if ( !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) || !mainWindow.EditorMode )
			{
				// Reset to default styling when not in EditorMode
				textBox.ClearValue(TextBox.BorderBrushProperty);
				textBox.ClearValue(TextBox.BorderThicknessProperty);
				ToolTip.SetTip(textBox, null);
				return;
			}

			string url = textBox.Text?.Trim() ?? string.Empty;

			// Update visual styling based on validation
			if ( string.IsNullOrWhiteSpace(url) )
			{
				// Reset to default styling for empty URLs
				textBox.ClearValue(BorderBrushProperty);
				textBox.ClearValue(BorderThicknessProperty);
				ToolTip.SetTip(textBox, null);
				return;
			}

			// Check URL format first
			bool isValidFormat = DownloadLinksControl.IsValidUrl(url);

			if ( !isValidFormat )
			{
				// Invalid URL format - red border
				textBox.BorderBrush = ThemeResourceHelper.UrlValidationInvalidBrush;
				textBox.BorderThickness = new Thickness(2);
				ToolTip.SetTip(textBox, $"Invalid URL format: {url}");
				return;
			}

			// Check if the URL is downloaded/cached using DownloadCacheService
			bool isDownloaded = false;
			if ( DownloadCacheService != null && ComponentGuid != Guid.Empty )
			{
				try
				{
					isDownloaded = DownloadCacheService.IsCached(ComponentGuid, url);
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex, "Error checking download cache status");
				}
			}

			if ( isDownloaded )
			{
				// Valid URL and downloaded - green border
				string archiveName = DownloadCacheService?.GetArchiveName(ComponentGuid, url) ?? "unknown";
				textBox.BorderBrush = ThemeResourceHelper.UrlValidationValidBrush;
				textBox.BorderThickness = new Thickness(2);
				ToolTip.SetTip(textBox, $"✅ Downloaded: {archiveName}");
			}
			else
			{
				// Valid URL format but not downloaded - orange/yellow border
				textBox.BorderBrush = ThemeResourceHelper.UrlValidationWarningBrush;
				textBox.BorderThickness = new Thickness(2);
				ToolTip.SetTip(textBox, $"⚠️ Valid URL but not downloaded yet");
			}
		}

		/// <summary>
		/// Checks if a string is a valid URL
		/// </summary>
		private static bool IsValidUrl(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			// Basic URL validation
			if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri uri) )
				return false;

			// Check if it's HTTP or HTTPS
			if ( uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps )
				return false;

			// Check if it has a valid host
			if ( string.IsNullOrWhiteSpace(uri.Host) )
				return false;

			return true;
		}
	}
}
