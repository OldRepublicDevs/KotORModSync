// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;

namespace KOTORModSync
{
	internal static class ThemeManager
	{
		private static Uri s_currentStyleUri;
		private static string s_currentTheme = "/Styles/FluentLightStyle.axaml"; // Track current theme

		public static event Action<Uri> StyleChanged;

		public static void UpdateStyle([JetBrains.Annotations.CanBeNull] string stylePath = null)
		{
			// Default to Fluent Light if null or empty
			if (string.IsNullOrWhiteSpace(stylePath))
			{
				stylePath = "/Styles/FluentLightStyle.axaml";
			}

			// Clear ALL existing styles
			Application.Current.Styles.Clear();

			// Handle Fluent Light theme
			if (string.Equals(stylePath, "Fluent.Light", StringComparison.OrdinalIgnoreCase))
			{
				// Redirect to custom FluentLightStyle.axaml
				UpdateStyle("/Styles/FluentLightStyle.axaml");
				return;
			}

			// Handle custom themes (dynamically loaded)
			if (stylePath.EndsWith("KotorStyle.axaml", StringComparison.OrdinalIgnoreCase) || stylePath.EndsWith("Kotor2Style.axaml", StringComparison.OrdinalIgnoreCase) || stylePath.EndsWith("FluentLightStyle.axaml", StringComparison.OrdinalIgnoreCase))
			{
				Uri newUri = new Uri("avares://KOTORModSync" + stylePath);
				s_currentStyleUri = newUri;
				s_currentTheme = stylePath;

				Application.Current.RequestedThemeVariant = ThemeVariant.Light;

				// Load Fluent theme first (provides base control templates)
				var fluentUri = new Uri("avares://Avalonia.Themes.Fluent/FluentTheme.xaml");
				Application.Current.Styles.Add(new StyleInclude(fluentUri) { Source = fluentUri });

				// Then add custom style overrides on top
				var styleUriPath = new Uri("avares://KOTORModSync" + stylePath);
				Application.Current.Styles.Add(new StyleInclude(styleUriPath) { Source = styleUriPath });

				ApplyToAllOpenWindows();
				StyleChanged?.Invoke(s_currentStyleUri);
				return;
			}

			// Fallback to Fluent Light for unknown themes
			UpdateStyle("/Styles/FluentLightStyle.axaml");
		}

		public static void ApplyCurrentToWindow(Window window) => ApplyToWindow(window, s_currentStyleUri);

		public static string GetCurrentStylePath()
		{
			if (!string.IsNullOrEmpty(s_currentTheme))
				return s_currentTheme;

			if (s_currentStyleUri is null)
				return "/Styles/FluentLightStyle.axaml"; // Default to Fluent Light

			string path = s_currentStyleUri.ToString();

			if (path.StartsWith("avares://KOTORModSync", StringComparison.Ordinal))
			{
				return path.Substring("avares://KOTORModSync".Length);
			}
			return "/Styles/FluentLightStyle.axaml"; // Default to Fluent Light
		}

		private static void ApplyToAllOpenWindows()
		{
			if (
				Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
				desktop.Windows != null &&
				desktop.Windows.Count > 0)
			{
				foreach (Window w in desktop.Windows)
				{
					ApplyToWindow(w, s_currentStyleUri);
				}
			}
		}

		private static void ApplyToWindow(Window window, Uri styleUri)
		{
			if (window is null)
				return;

			// CRITICAL: Always set Light theme variant for FluentLightStyle
			window.RequestedThemeVariant = ThemeVariant.Light;

			// Clear all window styles to prevent conflicts
			window.Styles.Clear();

			// ALWAYS apply Fluent base theme + custom style overrides to ensure consistent theming
			var fluentUri = new Uri("avares://Avalonia.Themes.Fluent/FluentTheme.xaml");
			window.Styles.Add(new StyleInclude(fluentUri) { Source = fluentUri });

			// Apply custom style overrides if available
			if (styleUri != null)
			{
				window.Styles.Add(new StyleInclude(styleUri) { Source = styleUri });
			}
			else if (!string.IsNullOrEmpty(s_currentTheme))
			{
				// Fallback to current theme path if styleUri is null
				var currentUri = new Uri("avares://KOTORModSync" + s_currentTheme);
				window.Styles.Add(new StyleInclude(currentUri) { Source = currentUri });
			}
		}
	}
}