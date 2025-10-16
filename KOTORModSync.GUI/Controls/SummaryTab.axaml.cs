// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JetBrains.Annotations;
using KOTORModSync.Core;

namespace KOTORModSync.Controls
{
	[SuppressMessage("ReSharper", "UnusedParameter.Local")]
	public partial class SummaryTab : UserControl
	{
		public static readonly StyledProperty<ModComponent> CurrentComponentProperty =
			AvaloniaProperty.Register<SummaryTab, ModComponent>(nameof(CurrentComponent));

	public static readonly StyledProperty<bool> EditorModeProperty =
		AvaloniaProperty.Register<SummaryTab, bool>(nameof(EditorMode));

	public static readonly StyledProperty<bool> SpoilerFreeModeProperty =
		AvaloniaProperty.Register<SummaryTab, bool>(nameof(SpoilerFreeMode));

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

	public bool SpoilerFreeMode
	{
		get => GetValue(SpoilerFreeModeProperty);
		set => SetValue(SpoilerFreeModeProperty, value);
	}

		public event EventHandler<TappedEventArgs> OpenLinkRequested;
		public event EventHandler<RoutedEventArgs> CopyTextToClipboardRequested;
		public event EventHandler<PointerPressedEventArgs> SummaryOptionPointerPressedRequested;
		public event EventHandler<RoutedEventArgs> CheckBoxChangedRequested;
		public event EventHandler<RoutedEventArgs> JumpToInstructionRequested;

		public SummaryTab()
		{
			InitializeComponent();
			DataContext = this;

			this.PropertyChanged += OnPropertyChanged;
		}

		private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
		{
			if ( e.Property == CurrentComponentProperty )
			{
				UpdateAllFilenamePanels();
			}
		}

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

		public TextBlock GetDescriptionTextBlock() => DescriptionTextBlock;
		public TextBlock GetDirectionsTextBlock() => DirectionsTextBlock;

		private void UpdateAllFilenamePanels()
		{
			if ( CurrentComponent == null || ModLinkRepeater == null )
				return;

			Dispatcher.UIThread.Post(() =>
			{
				try
				{
					var panels = this.GetVisualDescendants().OfType<StackPanel>()
						.Where(sp => sp.Name == "SummaryFilenamesPanel").ToList();

					int urlIndex = 0;
					foreach ( var panel in panels )
					{
						var urls = CurrentComponent.ModLinkFilenames.Keys.ToList();
						if ( urlIndex < urls.Count )
						{
							string url = urls[urlIndex];
							PopulateFilenamesPanel(panel, url);
							urlIndex++;
						}
					}
				}
				catch ( Exception ex )
				{
					Core.Logger.LogException(ex, "Error updating filename panels in SummaryTab");
				}
			}, DispatcherPriority.Background);
		}

		private void PopulateFilenamesPanel(StackPanel panel, string url)
		{
			if ( panel == null || string.IsNullOrWhiteSpace(url) )
				return;

			panel.Children.Clear();

			if ( CurrentComponent == null || CurrentComponent.ModLinkFilenames == null )
				return;

			if ( !CurrentComponent.ModLinkFilenames.TryGetValue(url, out var filenameDict) || filenameDict.Count == 0 )
				return;

			var headerText = new TextBlock
			{
				Text = "Files:",
				FontSize = 10,
				Opacity = 0.6,
				Margin = new Thickness(0, 2, 0, 2)
			};
			panel.Children.Add(headerText);

			foreach ( var filenameEntry in filenameDict )
			{
				string filename = filenameEntry.Key;
				bool? shouldDownload = filenameEntry.Value;

				string statusIcon = shouldDownload == true ? "✓" : shouldDownload == false ? "✗" : "?";
				double opacity = shouldDownload == true ? 1.0 : shouldDownload == false ? 0.5 : 0.7;

				var fileText = new TextBlock
				{
					Text = $"{statusIcon} {filename}",
					FontSize = 11,
					Opacity = opacity,
					Margin = new Thickness(0, 1, 0, 1)
				};

				panel.Children.Add(fileText);
			}
		}
	}
}

