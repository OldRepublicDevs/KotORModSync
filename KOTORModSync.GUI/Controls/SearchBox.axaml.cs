// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.Controls
{
	public partial class SearchBox : UserControl
	{
		public static readonly StyledProperty<string> SearchTextProperty =
			AvaloniaProperty.Register<SearchBox, string>(nameof(SearchText), string.Empty);

		public static readonly StyledProperty<string> WatermarkProperty =
			AvaloniaProperty.Register<SearchBox, string>(nameof(Watermark), "Search by name or author...");

		public string SearchText
		{
			get => GetValue(SearchTextProperty);
			set => SetValue(SearchTextProperty, value);
		}

		public string Watermark
		{
			get => GetValue(WatermarkProperty);
			set => SetValue(WatermarkProperty, value);
		}

		private TextBox _searchTextBox;
		private Button _clearButton;

		public SearchBox()
		{
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);

			_searchTextBox = this.FindControl<TextBox>("SearchTextBox");
			_clearButton = this.FindControl<Button>("ClearButton");

			if ( _searchTextBox != null )
			{
				_searchTextBox.PropertyChanged += (s, e) =>
				{
					if ( e.Property == TextBox.TextProperty )
					{
						UpdateClearButtonVisibility();
					}
				};
			}

			_ = this.GetObservable(WatermarkProperty).Subscribe(new AnonymousObserver<string>(UpdateWatermark));

			if ( _clearButton != null )
			{
				_clearButton.Click += (s, e) =>
				{
					SearchText = string.Empty;
					if ( _searchTextBox != null )
					{
						_searchTextBox.Text = string.Empty;
						_ = _searchTextBox.Focus();
					}
				};
			}

			UpdateClearButtonVisibility();
		}

		private void UpdateClearButtonVisibility()
		{
			if ( _clearButton != null && _searchTextBox != null )
			{
				_clearButton.IsVisible = !string.IsNullOrEmpty(_searchTextBox.Text);
			}
		}

		private void UpdateWatermark(string watermark)
		{
			if ( _searchTextBox != null )
			{
				_searchTextBox.Watermark = watermark;
			}
		}
	}

	internal class AnonymousObserver<T> : IObserver<T>
	{
		private readonly Action<T> _onNext;

		public AnonymousObserver(Action<T> onNext) => _onNext = onNext;

		public void OnCompleted() { }
		public void OnError(Exception error) { }
		public void OnNext(T value) => _onNext(value);
	}
}

