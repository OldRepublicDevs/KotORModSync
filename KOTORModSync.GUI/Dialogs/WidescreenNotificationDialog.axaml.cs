// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using KOTORModSync.Converters;
using KOTORModSync.Core;

namespace KOTORModSync.Dialogs
{
	public partial class WidescreenNotificationDialog : Window
	{
		public bool DontShowAgain { get; private set; }
		public bool UserCancelled { get; private set; }

		public WidescreenNotificationDialog()
		{
			InitializeComponent();
		}

		public WidescreenNotificationDialog( string widescreenContent ) : this()
		{
			LoadContent( widescreenContent );
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load( this );
		}

		private void LoadContent( string widescreenContent )
		{
			Dispatcher.UIThread.Post( () =>
			{
				try
				{
					var contentTextBlock = this.FindControl<TextBlock>( "ContentTextBlock" );
					if (contentTextBlock != null && !string.IsNullOrWhiteSpace( widescreenContent ))
					{

						var renderedTextBlock = MarkdownRenderer.RenderToTextBlock(
							widescreenContent,
							url => Core.Utility.UrlUtilities.OpenUrl( url )
						);

						if (renderedTextBlock?.Inlines != null)
						{
							contentTextBlock.Inlines.Clear();
							contentTextBlock.Inlines.AddRange( renderedTextBlock.Inlines );


							contentTextBlock.PointerPressed += ( sender, e ) =>
							{
								try
								{
									if (sender is TextBlock tb)
									{
										string fullText = GetTextBlockText( tb );
										if (!string.IsNullOrEmpty( fullText ))
										{
											var linkPattern = @"🔗([^🔗]+)🔗";
											var match = System.Text.RegularExpressions.Regex.Match( fullText, linkPattern );
											if (match.Success)
											{
												string url = match.Groups[1].Value;
												Core.Utility.UrlUtilities.OpenUrl( url );
												e.Handled = true;
											}
										}
									}
								}
								catch (Exception ex)
								{
									Logger.LogError( $"Error handling link click: {ex.Message}" );
								}
							};
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogError( $"Error loading widescreen content: {ex.Message}" );
				}
			} );
		}

		private static string GetTextBlockText( TextBlock textBlock )
		{
			if (textBlock.Inlines == null || textBlock.Inlines.Count == 0)
				return textBlock.Text ?? string.Empty;

			var text = new System.Text.StringBuilder();
			foreach (var inline in textBlock.Inlines)
			{
				if (inline is Avalonia.Controls.Documents.Run run)
					text.Append( run.Text );
			}
			return text.ToString();
		}

		private void ContinueButton_Click( object sender, RoutedEventArgs e )
		{
			var dontShowCheckBox = this.FindControl<CheckBox>( "DontShowAgainCheckBox" );
			DontShowAgain = dontShowCheckBox?.IsChecked == true;
			UserCancelled = false;
			Close( true );
		}

		private void CancelButton_Click( object sender, RoutedEventArgs e )
		{
			UserCancelled = true;
			Close( false );
		}
	}
}