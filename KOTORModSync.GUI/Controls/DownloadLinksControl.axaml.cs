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
				UpdateAllFilenamePanels();
			}
			else if ( change.Property == DownloadCacheServiceProperty || change.Property == ComponentGuidProperty )
			{

				RefreshAllUrlValidation();
				UpdateAllFilenamePanels();
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

			textBox.ClearValue(BorderBrushProperty);
			textBox.ClearValue(BorderThicknessProperty);
			ToolTip.SetTip(textBox, null);
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

		private async void ResolveFilenames_Click(object sender, RoutedEventArgs e)
		{
			if ( !(sender is Button button) || !(button.Tag is string url) || string.IsNullOrWhiteSpace(url) )
				return;

			try
			{
				if ( DownloadCacheService?.DownloadManager == null )
				{
					await Logger.LogWarningAsync("[DownloadLinksControl] Download manager not initialized");
					return;
				}

				await Logger.LogAsync($"[DownloadLinksControl] Resolving filenames for URL: {url}");

				var resolved = await DownloadCacheService.DownloadManager.ResolveUrlsToFilenamesAsync(
					new List<string> { url },
					System.Threading.CancellationToken.None);

				if ( resolved.TryGetValue(url, out List<string> filenames) && filenames.Count > 0 )
				{
					await Logger.LogAsync($"[DownloadLinksControl] Resolved {filenames.Count} filename(s) for URL: {url}");

					var component = GetCurrentComponent();
					if ( component != null )
					{
						if ( component.ModLinkFilenames == null )
							component.ModLinkFilenames = new Dictionary<string, Dictionary<string, bool?>>(StringComparer.OrdinalIgnoreCase);

						if ( !component.ModLinkFilenames.TryGetValue(url, out Dictionary<string, bool?> filenameDict) )
						{
							filenameDict = new Dictionary<string, bool?>(StringComparer.OrdinalIgnoreCase);
							component.ModLinkFilenames[url] = filenameDict;
						}

						foreach ( string filename in filenames )
						{
							if ( !string.IsNullOrWhiteSpace(filename) && !filenameDict.ContainsKey(filename) )
							{
								filenameDict[filename] = true;
							}
						}

						UpdateFilenamePanelForUrl(url);
					}
				}
				else
				{
					var component = GetCurrentComponent();
					string componentInfo = component != null ? $" [Component: '{component.Name}']" : "";
					var expectedFilenames = component?.ModLinkFilenames?.TryGetValue(url, out var filenameDict) == true
						? string.Join(", ", filenameDict.Keys)
						: "none";
					await Logger.LogWarningAsync($"[DownloadLinksControl] Failed to resolve filenames for URL: {url}{componentInfo} Expected filename(s): {expectedFilenames}");
				}
			}
			catch ( Exception ex )
			{
				var component = GetCurrentComponent();
				string componentInfo = component != null ? $" [Component: '{component.Name}']" : "";
				Logger.LogException(ex, $"Error resolving filenames for URL: {button.Tag}{componentInfo}");
			}
		}

		private ModComponent GetCurrentComponent()
		{
			if ( ComponentGuid != Guid.Empty && MainConfig.AllComponents != null )
			{
				return MainConfig.AllComponents.FirstOrDefault(c => c.Guid == ComponentGuid);
			}

			return MainConfig.CurrentComponent;
		}

		private void UpdateAllFilenamePanels()
		{
			if ( LinksItemsControl == null || DownloadLinks == null )
				return;

			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					foreach ( string url in DownloadLinks )
					{
						if ( !string.IsNullOrWhiteSpace(url) )
						{
							UpdateFilenamePanelForUrl(url);
						}
					}
				}
				catch ( Exception ex )
				{
					Logger.LogException(ex, "Error updating all filename panels");
				}
			}, DispatcherPriority.Background);
		}

		private void UpdateFilenamePanelForUrl(string url)
		{
			if ( string.IsNullOrWhiteSpace(url) )
				return;

			try
			{
				var borders = this.GetVisualDescendants().OfType<Border>()
					.Where(b => b.Classes.Contains("url-item")).ToList();

				foreach ( var border in borders )
				{
					var textBox = border.GetVisualDescendants().OfType<TextBox>()
						.FirstOrDefault(tb => tb.Text == url);

					if ( textBox != null )
					{
						var filenamesPanel = border.GetVisualDescendants().OfType<StackPanel>()
							.FirstOrDefault(sp => sp.Name == "FilenamesPanel");

						if ( filenamesPanel != null )
						{
							PopulateFilenamesPanel(filenamesPanel, url);
						}
						break;
					}
				}
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Error updating filename panel for URL: {url}");
			}
		}

		private void PopulateFilenamesPanel(StackPanel panel, string url)
		{
			if ( panel == null || string.IsNullOrWhiteSpace(url) )
				return;

			panel.Children.Clear();

			var component = GetCurrentComponent();
			if ( component == null || component.ModLinkFilenames == null )
				return;

			if ( !component.ModLinkFilenames.TryGetValue(url, out Dictionary<string, bool?> filenameDict) ||
				 filenameDict.Count == 0 )
				return;

			var headerText = new TextBlock
			{
				Text = "Resolved Filenames:",
				FontSize = 10,
				Opacity = 0.7,
				Margin = new Thickness(0, 4, 0, 2)
			};
			panel.Children.Add(headerText);

			foreach ( var filenameEntry in filenameDict )
			{
				string filename = filenameEntry.Key;
				bool? shouldDownload = filenameEntry.Value;

				var checkBox = new CheckBox
				{
					Content = filename,
					IsChecked = shouldDownload,
					FontSize = 11,
					Margin = new Thickness(0, 1, 0, 1),
					Tag = new Tuple<string, string>(url, filename)
				};

				checkBox.IsCheckedChanged += FilenameCheckBox_IsCheckedChanged;

				panel.Children.Add(checkBox);
			}
		}

		private void FilenameCheckBox_IsCheckedChanged(object sender, RoutedEventArgs e)
		{
			if ( !(sender is CheckBox checkBox) || !(checkBox.Tag is Tuple<string, string> tag) )
				return;

			string url = tag.Item1;
			string filename = tag.Item2;
			bool shouldDownload = checkBox.IsChecked ?? true;

			var component = GetCurrentComponent();
			if ( component == null || component.ModLinkFilenames == null )
				return;

			if ( component.ModLinkFilenames.TryGetValue(url, out Dictionary<string, bool?> filenameDict) &&
				 filenameDict.ContainsKey(filename) )
			{
				filenameDict[filename] = shouldDownload;
				Logger.LogVerbose($"[DownloadLinksControl] Updated download flag for '{filename}': {shouldDownload}");
			}
		}
	}
}
