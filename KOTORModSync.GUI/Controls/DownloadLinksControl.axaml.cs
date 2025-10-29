// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;
using KOTORModSync.Services;

namespace KOTORModSync.Controls
{
	public partial class DownloadLinksControl : UserControl
	{
		public static readonly StyledProperty<List<string>> DownloadLinksProperty =
			AvaloniaProperty.Register<DownloadLinksControl, List<string>>( nameof( DownloadLinks ), new List<string>() );

		public static readonly StyledProperty<DownloadCacheService> DownloadCacheServiceProperty =
			AvaloniaProperty.Register<DownloadLinksControl, DownloadCacheService>( nameof( DownloadCacheService ) );

		public static readonly StyledProperty<Guid> ComponentGuidProperty =
			AvaloniaProperty.Register<DownloadLinksControl, Guid>( nameof( ComponentGuid ) );

		private bool _isUpdatingFromTextBox;

		public List<string> DownloadLinks
		{
			get => GetValue( DownloadLinksProperty );
			set => SetValue( DownloadLinksProperty, value );
		}

		public DownloadCacheService DownloadCacheService
		{
			get => GetValue( DownloadCacheServiceProperty );
			set => SetValue( DownloadCacheServiceProperty, value );
		}

		public Guid ComponentGuid
		{
			get => GetValue( ComponentGuidProperty );
			set => SetValue( ComponentGuidProperty, value );
		}

		public DownloadLinksControl()
		{
			InitializeComponent();
			UpdateEmptyStateVisibility();
		}

		protected override void OnPropertyChanged( AvaloniaPropertyChangedEventArgs change )
		{
			base.OnPropertyChanged( change );

			if (change.Property == DownloadLinksProperty)
			{

				if (_isUpdatingFromTextBox)
					return;

				UpdateLinksDisplay();
				UpdateEmptyStateVisibility();
				UpdateAllFilenamePanels();
			}
			else if (change.Property == DownloadCacheServiceProperty || change.Property == ComponentGuidProperty)
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
				foreach (var textBox in textBoxes)
				{
					UpdateUrlValidation( textBox );
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error refreshing URL validation" );
			}
		}

		private void UpdateLinksDisplay()
		{
			if (LinksItemsControl?.ItemsSource == DownloadLinks)
				return;
			LinksItemsControl.ItemsSource = DownloadLinks;

		}

		private void UpdateEmptyStateVisibility()
		{
			if (EmptyStateBorder == null)
				return;
			EmptyStateBorder.IsVisible = DownloadLinks == null || DownloadLinks.Count == 0;
		}

		private void AddLink_Click( object sender, RoutedEventArgs e )
		{
			if (DownloadLinks == null)
				DownloadLinks = new List<string>();

			_isUpdatingFromTextBox = false;

			var newList = new List<string>( DownloadLinks ) { string.Empty };
			DownloadLinks = newList;

			// Also update ModLinkFilenames on the component
			var component = GetCurrentComponent();
			if (component != null)
			{
				// Add empty entry for new URL (will be populated when user enters URL)
				SyncDownloadLinksToModLinkFilenames( component, newList );
			}

			Dispatcher.UIThread.Post( () =>
			{
				try
				{
					var textBoxes = this.GetVisualDescendants().OfType<TextBox>().ToList();
					TextBox lastTextBox = textBoxes.LastOrDefault();
					_ = (lastTextBox?.Focus());
				}
				catch (Exception ex)
				{
					Logger.LogException( ex, "Error focusing newly added TextBox in DownloadLinksControl" );
				}
			}, DispatcherPriority.Input );
		}

		private void RemoveLink_Click( object sender, RoutedEventArgs e )
		{
			if (!(sender is Button button) || DownloadLinks == null)
				return;

			if (!(button.Parent is Grid parentGrid))
				return;

			TextBox textBox = parentGrid.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
			if (textBox == null)
				return;

			int index = GetTextBoxIndex( textBox );
			if (index < 0 || index >= DownloadLinks.Count)
				return;

			// Get the URL being removed
			string urlToRemove = DownloadLinks[index];

			_isUpdatingFromTextBox = false;

			var newList = new List<string>( DownloadLinks );
			newList.RemoveAt( index );
			DownloadLinks = newList;

			// Also remove from ModLinkFilenames
			var component = GetCurrentComponent();
			if (component != null && !string.IsNullOrWhiteSpace( urlToRemove ))
			{
				component.ModLinkFilenames.Remove( urlToRemove );
			}
		}

		private void OpenLink_Click( object sender, RoutedEventArgs e )
		{
			if (!(sender is Button button) || !(button.Tag is string url) || string.IsNullOrWhiteSpace( url ))
				return;
			try
			{

				if (!url.StartsWith( "http://", StringComparison.OrdinalIgnoreCase ) &&
					!url.StartsWith( "https://", StringComparison.OrdinalIgnoreCase ))
				{
					url = "https://" + url;
				}

				var processInfo = new ProcessStartInfo
				{
					FileName = url,
					UseShellExecute = true
				};
				_ = Process.Start( processInfo );
			}
			catch (Exception ex)
			{

				Logger.LogException( ex, $"Failed to open URL: {ex.Message}" );
			}
		}

		private void UrlTextBox_TextChanged( object sender, TextChangedEventArgs e )
		{
			if (!(sender is TextBox textBox) || DownloadLinks == null)
				return;

			int index = GetTextBoxIndex( textBox );
			if (index < 0 || index >= DownloadLinks.Count)
				return;

			string oldUrl = DownloadLinks[index];
			string newText = textBox.Text ?? string.Empty;

			if (string.Equals( oldUrl, newText, StringComparison.Ordinal ))
			{
				UpdateUrlValidation( textBox );
				return;
			}

			_isUpdatingFromTextBox = true;
			try
			{

				var newList = new List<string>( DownloadLinks )
				{
					[index] = newText
				};
				DownloadLinks = newList;

				// Update ModLinkFilenames on component
				var component = GetCurrentComponent();
				if (component != null)
				{
					// Remove old URL entry if it existed
					if (!string.IsNullOrWhiteSpace( oldUrl ) && component.ModLinkFilenames.ContainsKey( oldUrl ))
					{
						var oldFilenames = component.ModLinkFilenames[oldUrl];
						component.ModLinkFilenames.Remove( oldUrl );

						// If new URL is valid, transfer the filenames to it
						if (!string.IsNullOrWhiteSpace( newText ))
						{
							component.ModLinkFilenames[newText] = oldFilenames;
						}
					}
					else if (!string.IsNullOrWhiteSpace( newText ) && !component.ModLinkFilenames.ContainsKey( newText ))
					{
						// New URL without previous entry
						component.ModLinkFilenames[newText] = new Dictionary<string, bool?>( StringComparer.OrdinalIgnoreCase );
					}
				}
			}
			finally
			{
				_isUpdatingFromTextBox = false;
			}

			UpdateUrlValidation( textBox );
		}

		private int GetTextBoxIndex( TextBox textBox )
		{
			if (!(LinksItemsControl?.ItemsSource is List<string> links) || textBox == null)
				return -1;
			try
			{
				var textBoxes = this.GetVisualDescendants().OfType<TextBox>().ToList();
				int index = textBoxes.IndexOf( textBox );
				return index >= 0 && index < links.Count ? index : -1;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error getting TextBox index in DownloadLinksControl" );
				return -1;
			}
		}

		private void UrlTextBox_KeyDown( object sender, KeyEventArgs e )
		{
			switch (e.Key)
			{
				case Key.Enter:

					AddLink_Click( sender, e );
					e.Handled = true;
					break;
				case Key.Delete when sender is TextBox deleteTextBox &&
									 DownloadLinks != null && string.IsNullOrWhiteSpace( deleteTextBox.Text ):
					{

						int index = GetTextBoxIndex( deleteTextBox );
						if (index >= 0 && index < DownloadLinks.Count)
						{

							_isUpdatingFromTextBox = false;

							var newList = new List<string>( DownloadLinks );
							newList.RemoveAt( index );
							DownloadLinks = newList;
						}
						e.Handled = true;
						break;
					}
			}
		}

		private void UpdateUrlValidation( TextBox textBox )
		{
			if (textBox == null)
				return;

			if (!(this.FindAncestorOfType<Window>() is MainWindow mainWindow) || !mainWindow.EditorMode)
			{
				textBox.ClearValue( TextBox.BorderBrushProperty );
				textBox.ClearValue( TextBox.BorderThicknessProperty );
				ToolTip.SetTip( textBox, null );
				return;
			}

			string url = textBox.Text?.Trim() ?? string.Empty;

			if (string.IsNullOrWhiteSpace( url ))
			{
				textBox.ClearValue( BorderBrushProperty );
				textBox.ClearValue( BorderThicknessProperty );
				ToolTip.SetTip( textBox, null );
				return;
			}

			bool isValidFormat = DownloadLinksControl.IsValidUrl( url );

			if (!isValidFormat)
			{
				textBox.BorderBrush = ThemeResourceHelper.UrlValidationInvalidBrush;
				textBox.BorderThickness = new Thickness( 2 );
				ToolTip.SetTip( textBox, $"Invalid URL format: {url}" );
				return;
			}

			textBox.ClearValue( BorderBrushProperty );
			textBox.ClearValue( BorderThicknessProperty );
			ToolTip.SetTip( textBox, null );
		}

		private static bool IsValidUrl( string url )
		{
			if (string.IsNullOrWhiteSpace( url ))
				return false;

			if (!Uri.TryCreate( url, UriKind.Absolute, out Uri uri ))
				return false;

			if (!string.Equals( uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal ) && !string.Equals( uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal ))
				return false;

			if (string.IsNullOrWhiteSpace( uri.Host ))
				return false;

			return true;
		}

		private async void ResolveFilenames_Click( object sender, RoutedEventArgs e )
		{
			if (!(sender is Button button) || !(button.Tag is string url) || string.IsNullOrWhiteSpace( url ))
				return;

			try
			{
				if (DownloadCacheService?.DownloadManager == null)


				{
					await Logger.LogWarningAsync( "[DownloadLinksControl] Download manager not initialized" ).ConfigureAwait( false );
					return;
				}

				await Logger.LogAsync( $"[DownloadLinksControl] Resolving filenames for URL: {url}" ).ConfigureAwait( false );

				using (var cts = new CancellationTokenSource( TimeSpan.FromMinutes( 5 ) ))
				{
					var resolved = await DownloadCacheService.DownloadManager.ResolveUrlsToFilenamesAsync(
						new List<string> { url },
						cts.Token ).ConfigureAwait( false );

					if (resolved.TryGetValue( url, out List<string> filenames ) && filenames.Count > 0)


					{
						await Logger.LogAsync( $"[DownloadLinksControl] Resolved {filenames.Count} filename(s) for URL: {url}" ).ConfigureAwait( false );

						ModComponent component = GetCurrentComponent();
						if (component != null)
						{
							if (!component.ModLinkFilenames.TryGetValue( url, out Dictionary<string, bool?> filenameDict ))
							{
								filenameDict = new Dictionary<string, bool?>( StringComparer.OrdinalIgnoreCase );
								component.ModLinkFilenames[url] = filenameDict;
							}

							foreach (string filename in filenames)
							{
								if (!string.IsNullOrWhiteSpace( filename ) && !filenameDict.ContainsKey( filename ))
								{
									filenameDict[filename] = true;
								}
							}

							UpdateFilenamePanelForUrl( url );
						}
					}
					else
					{
						var component = GetCurrentComponent();
						string componentInfo = component != null ? $" [Component: '{component.Name}']" : "";
						var expectedFilenames = component?.ModLinkFilenames.TryGetValue( url, out var filenameDict ) == true
							? string.Join( ", ", filenameDict.Keys )
							: "none";
						await Logger.LogWarningAsync( $"[DownloadLinksControl] Failed to resolve filenames for URL: {url}{componentInfo} Expected filename(s): {expectedFilenames}" ).ConfigureAwait( false );
					}
				}
			}
			catch (Exception ex)
			{
				var component = GetCurrentComponent();
				string componentInfo = component != null ? $" [Component: '{component.Name}']" : "";
				await Logger.LogExceptionAsync( ex, $"Error resolving filenames for URL: {button.Tag}{componentInfo}" ).ConfigureAwait( false );
			}
		}

		private ModComponent GetCurrentComponent()
		{
			if (ComponentGuid != Guid.Empty)
			{
				return MainConfig.AllComponents.FirstOrDefault( c => c.Guid == ComponentGuid );
			}

			return MainConfig.CurrentComponent;
		}

		private void UpdateAllFilenamePanels()
		{
			if (LinksItemsControl == null || DownloadLinks == null)
				return;

			Dispatcher.UIThread.Post( () =>
			{
				try
				{
					foreach (string url in DownloadLinks)
					{
						if (!string.IsNullOrWhiteSpace( url ))
						{
							UpdateFilenamePanelForUrl( url );
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogException( ex, "Error updating all filename panels" );
				}
			}, DispatcherPriority.Background );
		}

		private void UpdateFilenamePanelForUrl( string url )
		{
			if (string.IsNullOrWhiteSpace( url ))
				return;

			try
			{
				var borders = this.GetVisualDescendants().OfType<Border>()


					.Where( b => b.Classes.Contains( "url-item", StringComparer.Ordinal ) ).ToList();

				foreach (var border in borders)
				{
					var textBox = border.GetVisualDescendants().OfType<TextBox>()
						.FirstOrDefault( tb => string.Equals( tb.Text, url, StringComparison.Ordinal ) );

					if (textBox != null)
					{
						var filenamesPanel = border.GetVisualDescendants().OfType<StackPanel>()
							.FirstOrDefault( sp => string.Equals( sp.Name, "FilenamesPanel", StringComparison.Ordinal ) );

						if (filenamesPanel != null)
						{
							PopulateFilenamesPanel( filenamesPanel, url );
						}
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, $"Error updating filename panel for URL: {url}" );
			}
		}

		private void PopulateFilenamesPanel( StackPanel panel, string url )
		{
			if (panel == null || string.IsNullOrWhiteSpace( url ))
				return;

			panel.Children.Clear();

			var component = GetCurrentComponent();
			if (component == null)
				return;

			if (!component.ModLinkFilenames.TryGetValue( url, out Dictionary<string, bool?> filenameDict ))
			{
				// URL exists but no filename dictionary - create an empty one
				filenameDict = new Dictionary<string, bool?>( StringComparer.OrdinalIgnoreCase );
				component.ModLinkFilenames[url] = filenameDict;
			}

			if (filenameDict.Count == 0)
			{
				var emptyText = new TextBlock
				{
					Text = "No filenames added yet. Click 'Add Filename Manually' or '📥' to resolve from URL.",
					FontSize = 10,
					Opacity = 0.6,
					TextWrapping = Avalonia.Media.TextWrapping.Wrap,
					Margin = new Thickness( 0, 2, 0, 2 )
				};
				panel.Children.Add( emptyText );
				return;
			}

			var headerText = new TextBlock
			{
				Text = $"Filenames ({filenameDict.Count}):",
				FontSize = 10,
				Opacity = 0.7,
				Margin = new Thickness( 0, 4, 0, 2 )
			};
			panel.Children.Add( headerText );

			var helpText = new TextBlock
			{
				Text = "☑ = Download | ☐ = Skip | ▣ = Auto-detect",
				FontSize = 9,
				Opacity = 0.5,
				Margin = new Thickness( 0, 0, 0, 4 )
			};
			panel.Children.Add( helpText );

			foreach (var filenameEntry in filenameDict)
			{
				string filename = filenameEntry.Key;
				bool? shouldDownload = filenameEntry.Value;

				var fileGrid = new Grid
				{
					ColumnDefinitions = new ColumnDefinitions( "*,Auto" ),
					Margin = new Thickness( 0, 1, 0, 1 )
				};

				var checkBox = new CheckBox
				{
					Content = filename,
					IsChecked = shouldDownload,
					IsThreeState = true,
					FontSize = 11,
					Tag = new Tuple<string, string>( url, filename )
				};
				checkBox.IsCheckedChanged += FilenameCheckBox_IsCheckedChanged;

				var removeButton = new Button
				{
					Content = "❌",
					FontSize = 9,
					Padding = new Thickness( 4, 2 ),
					Margin = new Thickness( 4, 0, 0, 0 ),
					Tag = new Tuple<string, string>( url, filename )
				};
				ToolTip.SetTip( removeButton, $"Remove {filename}" );
				removeButton.Click += RemoveFilename_Click;

				Grid.SetColumn( checkBox, 0 );
				Grid.SetColumn( removeButton, 1 );

				fileGrid.Children.Add( checkBox );
				fileGrid.Children.Add( removeButton );

				panel.Children.Add( fileGrid );
			}
		}

		private void FilenameCheckBox_IsCheckedChanged( object sender, RoutedEventArgs e )
		{
			if (!(sender is CheckBox checkBox) || !(checkBox.Tag is Tuple<string, string> tag))
				return;

			string url = tag.Item1;
			string filename = tag.Item2;
			bool shouldDownload = checkBox.IsChecked ?? true;

			var component = GetCurrentComponent();
			if (component == null)
				return;

			if (component.ModLinkFilenames.TryGetValue( url, out Dictionary<string, bool?> filenameDict ) &&
				 filenameDict.ContainsKey( filename ))
			{
				filenameDict[filename] = shouldDownload;
				Logger.LogVerbose( $"[DownloadLinksControl] Updated download flag for '{filename}': {shouldDownload}" );
			}
		}

		/// <summary>
		/// Syncs the DownloadLinks list to the component's ModLinkFilenames dictionary.
		/// Ensures ModLinkFilenames contains all URLs from DownloadLinks.
		/// </summary>
		private void SyncDownloadLinksToModLinkFilenames( ModComponent component, List<string> urlList )
		{
			if (component == null || urlList == null)
				return;

			// Add any new URLs to ModLinkFilenames
			foreach (string url in urlList)
			{
				if (!string.IsNullOrWhiteSpace( url ) && !component.ModLinkFilenames.ContainsKey( url ))
				{
					component.ModLinkFilenames[url] = new Dictionary<string, bool?>( StringComparer.OrdinalIgnoreCase );
					Logger.LogVerbose( $"[DownloadLinksControl] Added new URL to ModLinkFilenames: {url}" );
				}
			}

			// Remove URLs that are no longer in the list
			var urlsToRemove = component.ModLinkFilenames.Keys


				.Where( url => !urlList.Contains( url, StringComparer.Ordinal ) )
				.ToList();

			foreach (string url in urlsToRemove)
			{
				component.ModLinkFilenames.Remove( url );
				Logger.LogVerbose( $"[DownloadLinksControl] Removed URL from ModLinkFilenames: {url}" );
			}
		}

		private async void AddFilename_Click( object sender, RoutedEventArgs e )
		{
			if (!(sender is Button button) || !(button.Tag is string url))
				return;

			if (string.IsNullOrWhiteSpace( url ))
				return;

			var component = GetCurrentComponent();
			if (component == null)
				return;

			// Prompt user for filename
			try
			{
				// Create a simple input dialog
				var dialog = new Window
				{
					Title = "Add Filename",
					Width = 500,
					Height = 200,
					WindowStartupLocation = WindowStartupLocation.CenterOwner,
					CanResize = false
				};

				var stackPanel = new StackPanel
				{
					Margin = new Thickness( 16 ),
					Spacing = 12
				};

				var messageText = new TextBlock
				{
					Text = $"Enter filename to add to URL:\n{url}",
					TextWrapping = Avalonia.Media.TextWrapping.Wrap
				};

				var inputBox = new TextBox
				{
					Watermark = "example_mod_v1.0.zip"
				};

				var buttonPanel = new StackPanel
				{
					Orientation = Avalonia.Layout.Orientation.Horizontal,
					HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
					Spacing = 8
				};

				var okButton = new Button { Content = "Add", Width = 80 };
				var cancelButton = new Button { Content = "Cancel", Width = 80 };

				string result = null;

				okButton.Click += ( s, args ) =>
				{
					result = inputBox.Text;
					dialog.Close();
				};

				cancelButton.Click += ( s, args ) =>
				{
					result = null;
					dialog.Close();
				};

				buttonPanel.Children.Add( okButton );
				buttonPanel.Children.Add( cancelButton );

				stackPanel.Children.Add( messageText );
				stackPanel.Children.Add( inputBox );
				stackPanel.Children.Add( buttonPanel );

				dialog.Content = stackPanel;



				await dialog.ShowDialog( this.GetVisualRoot() as Window ).ConfigureAwait( false );

				string filename = result;

				if (string.IsNullOrWhiteSpace( filename ))
					return;

				if (!component.ModLinkFilenames.TryGetValue( url, out Dictionary<string, bool?> filenameDict ))
				{
					filenameDict = new Dictionary<string, bool?>( StringComparer.OrdinalIgnoreCase );
					component.ModLinkFilenames[url] = filenameDict;
				}

				if (filenameDict.ContainsKey( filename ))
				{
					Logger.LogWarning( $"[DownloadLinksControl] Filename already exists: {filename}" );
					return;
				}

				// Add with null (auto-detect) by default
				filenameDict[filename] = null;
				Logger.LogVerbose( $"[DownloadLinksControl] Added filename '{filename}' to URL '{url}'" );

				// Refresh the panel
				UpdateFilenamePanelForUrl( url );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error adding filename" );
			}
		}

		private void RemoveFilename_Click( object sender, RoutedEventArgs e )
		{
			if (!(sender is Button button) || !(button.Tag is Tuple<string, string> tag))
				return;

			string url = tag.Item1;
			string filename = tag.Item2;

			var component = GetCurrentComponent();
			if (component == null)
				return;

			if (component.ModLinkFilenames.TryGetValue( url, out Dictionary<string, bool?> filenameDict ))
			{
				if (filenameDict.Remove( filename ))
				{
					Logger.LogVerbose( $"[DownloadLinksControl] Removed filename '{filename}' from URL '{url}'" );
					UpdateFilenamePanelForUrl( url );
				}
			}
		}

		private async void DownloadMod_Click( object sender, RoutedEventArgs e )
		{
			var component = GetCurrentComponent();
			if (component == null)
				return;

			try
			{
				Logger.LogVerbose( $"[DownloadLinksControl] Download button clicked for: {component.Name}" );

				// Get the main window to call the download method
				if (this.FindAncestorOfType<MainWindow>() is MainWindow mainWindow)
				{
					await DownloadOrchestrationService.DownloadModFromUrlAsync( component.ModLinkFilenames.First().Key, component );
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, $"Error downloading mod from DownloadLinksControl: {component.Name}" );
			}
		}
	}
}