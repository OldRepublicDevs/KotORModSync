// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace KOTORModSync
{
	internal static class ThemeManager
	{
		private static Uri s_currentStyleUri;
		private static string s_currentTheme = "Fluent.Light"; // Track current theme

		public static event Action<Uri> StyleChanged;

		public static void UpdateStyle([JetBrains.Annotations.CanBeNull] string stylePath)
		{
			// Default to Fluent Light if null or empty
			if (string.IsNullOrEmpty(stylePath))
			{
				stylePath = "Fluent.Light";
			}

			// Clear ALL existing styles
			Application.Current.Styles.Clear();

			// Handle Fluent Light theme
			if (string.Equals(stylePath, "Fluent.Light", StringComparison.OrdinalIgnoreCase))
			{
				s_currentStyleUri = null;
				s_currentTheme = "Fluent.Light";

				Application.Current.RequestedThemeVariant = ThemeVariant.Light;

				// Add Fluent theme
				var fluentUri = new Uri("avares://Avalonia.Themes.Fluent/FluentTheme.xaml");
				Application.Current.Styles.Add(new StyleInclude(fluentUri) { Source = fluentUri });

				ApplyToAllOpenWindows();
				StyleChanged?.Invoke(s_currentStyleUri);
				return;
			}

			// Handle KOTOR themes (dynamically loaded)
			if (stylePath.Contains("KotorStyle") || stylePath.Contains("Kotor2Style"))
			{
				Uri newUri = new Uri("avares://KOTORModSync" + stylePath);
				s_currentStyleUri = newUri;
				s_currentTheme = stylePath;

				Application.Current.RequestedThemeVariant = ThemeVariant.Light;

				// Load Fluent theme first (provides base control templates)
				var fluentUri = new Uri("avares://Avalonia.Themes.Fluent/FluentTheme.xaml");
				Application.Current.Styles.Add(new StyleInclude(fluentUri) { Source = fluentUri });

				// Then add KOTOR style overrides on top
				var styleUriPath = new Uri("avares://KOTORModSync" + stylePath);
				Application.Current.Styles.Add(new StyleInclude(styleUriPath) { Source = styleUriPath });

				ApplyToAllOpenWindows();
				StyleChanged?.Invoke(s_currentStyleUri);
				return;
			}

			// Fallback to Fluent Light for unknown themes
			UpdateStyle("Fluent.Light");
		}

		public static void ApplyCurrentToWindow(Window window) => ApplyToWindow(window, s_currentStyleUri);

		public static string GetCurrentStylePath()
		{
			if (!string.IsNullOrEmpty(s_currentTheme))
				return s_currentTheme;

			if (s_currentStyleUri == null)
				return "Fluent.Light"; // Default to Fluent Light

			string path = s_currentStyleUri.ToString();

			if (path.StartsWith("avares://KOTORModSync", StringComparison.Ordinal))
			{
				return path.Substring("avares://KOTORModSync".Length);
			}
			return "Fluent.Light"; // Default to Fluent Light
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

			// Set the window's RequestedThemeVariant to match Application's
			window.RequestedThemeVariant = Application.Current.RequestedThemeVariant;

			// Clear all window styles
			window.Styles.Clear();

			// Apply window-level styles based on current theme
			if (s_currentTheme.Contains("KotorStyle") || s_currentTheme.Contains("Kotor2Style"))
			{
				// KOTOR themes: Add Fluent base + KOTOR overrides
				var fluentUri = new Uri("avares://Avalonia.Themes.Fluent/FluentTheme.xaml");
				window.Styles.Add(new StyleInclude(fluentUri) { Source = fluentUri });

				if (styleUri != null)
				{
					window.Styles.Add(new StyleInclude(styleUri) { Source = styleUri });
				}
			}
			// Fluent theme is already added at Application.Current.Styles level, window inherits it
		}
	}
}