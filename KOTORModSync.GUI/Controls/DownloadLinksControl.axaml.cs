// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
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

		private bool _isUpdatingFromTextBox;

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

				if ( _isUpdatingFromTextBox )
					return;

				UpdateLinksDisplay();
				UpdateEmptyStateVisibility();
			}
			else if ( change.Property == DownloadCacheServiceProperty || change.Property == ComponentGuidProperty )
			{

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

			_isUpdatingFromTextBox = false;

			var newList = new List<string>(DownloadLinks) { string.Empty };
			DownloadLinks = newList;

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

			if ( !(button.Parent is Grid parentGrid) )
				return;

			TextBox textBox = parentGrid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
			if ( textBox == null )
				return;

			int index = GetTextBoxIndex(textBox);
			if ( index < 0 || index >= DownloadLinks.Count )
				return;

			_isUpdatingFromTextBox = false;

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

				Logger.LogException(ex, $"Failed to open URL: {ex.Message}");
			}
		}

		private void UrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if ( !(sender is TextBox textBox) || DownloadLinks == null )
				return;

			int index = GetTextBoxIndex(textBox);
			if ( index < 0 || index >= DownloadLinks.Count )
				return;

			string newText = textBox.Text ?? string.Empty;

			if ( DownloadLinks[index] == newText )
			{
				UpdateUrlValidation(textBox);
				return;
			}

			_isUpdatingFromTextBox = true;
			try
			{

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

					AddLink_Click(sender, e);
					e.Handled = true;
					break;
				case Key.Delete when sender is TextBox deleteTextBox &&
									 DownloadLinks != null && string.IsNullOrWhiteSpace(deleteTextBox.Text):
					{

						int index = GetTextBoxIndex(deleteTextBox);
						if ( index >= 0 && index < DownloadLinks.Count )
						{

							_isUpdatingFromTextBox = false;

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

			if ( !(this.FindAncestorOfType<Window>() is MainWindow mainWindow) || !mainWindow.EditorMode )
			{

				textBox.ClearValue(TextBox.BorderBrushProperty);
				textBox.ClearValue(TextBox.BorderThicknessProperty);
				ToolTip.SetTip(textBox, null);
				return;
			}

			string url = textBox.Text?.Trim() ?? string.Empty;

			if ( string.IsNullOrWhiteSpace(url) )
			{

				textBox.ClearValue(BorderBrushProperty);
				textBox.ClearValue(BorderThicknessProperty);
				ToolTip.SetTip(textBox, null);
				return;
			}

			bool isValidFormat = DownloadLinksControl.IsValidUrl(url);

			if ( !isValidFormat )
			{

				textBox.BorderBrush = ThemeResourceHelper.UrlValidationInvalidBrush;
				textBox.BorderThickness = new Thickness(2);
				ToolTip.SetTip(textBox, $"Invalid URL format: {url}");
				return;
			}

			bool hasResolvedFilename = false;
			string archiveName = null;
			if ( DownloadCacheService != null && ComponentGuid != Guid.Empty )
			{
				try
				{

					if ( DownloadCacheService.TryGetEntry(ComponentGuid, url, out DownloadCacheEntry entry) )
					{

						if ( !string.IsNullOrWhiteSpace(entry.ArchiveName) )
						{
							hasResolvedFilename = true;
							archiveName = entry.ArchiveName;
						}
					}
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex, "Error checking cached filename for URL");
				}
			}

			if ( hasResolvedFilename )
			{

				textBox.BorderBrush = ThemeResourceHelper.UrlValidationValidBrush;
				textBox.BorderThickness = new Thickness(2);
				ToolTip.SetTip(textBox, $"✅ Resolves to: {archiveName}");
			}
			else
			{

				textBox.BorderBrush = ThemeResourceHelper.UrlValidationInvalidBrush;
				textBox.BorderThickness = new Thickness(2);
				ToolTip.SetTip(textBox, $"❌ Cannot resolve to filename (not cached)");
			}
		}

		private static bool IsValidUrl(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return false;

			if ( !Uri.TryCreate(url, UriKind.Absolute, out Uri uri) )
				return false;

			if ( uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps )
				return false;

			if ( string.IsNullOrWhiteSpace(uri.Host) )
				return false;

			return true;
		}
	}
}
