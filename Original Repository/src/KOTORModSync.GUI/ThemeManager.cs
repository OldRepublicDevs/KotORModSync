// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

namespace KOTORModSync
{
    internal static class ThemeManager
    {
        private const string DefaultThemePath = "/Styles/LightStyle.axaml";
        private static readonly object ThemeCacheLock = new object();
        private static string[] s_cachedThemePaths;

        private static Uri s_currentStyleUri;
        private static string s_currentTheme = DefaultThemePath; // Track current theme

        public static event Action<Uri> StyleChanged;

        public static void UpdateStyle([JetBrains.Annotations.CanBeNull] string stylePath = null)
        {
            string normalizedPath = NormalizeStylePath(stylePath);

            if (string.Equals(s_currentTheme, normalizedPath, StringComparison.OrdinalIgnoreCase) && s_currentStyleUri != null)
            {
                return;
            }

            s_currentTheme = normalizedPath;
            s_currentStyleUri = new Uri("avares://KOTORModSync" + normalizedPath);

            // Ensure all UI operations happen on UI thread
            if (Dispatcher.UIThread.CheckAccess())
            {
                ApplyStyleInternal(normalizedPath);
            }
            else
            {
                Dispatcher.UIThread.Post(() => ApplyStyleInternal(normalizedPath), DispatcherPriority.Normal);
            }
        }


        private static void ApplyStyleInternal(string stylePath)
        {
            // Clear ALL existing styles
            Application.Current.Styles.Clear();

            Application.Current.RequestedThemeVariant = ThemeVariant.Light;

            // Load Fluent theme first (provides base control templates)
            var fluentUri = new Uri("avares://Avalonia.Themes.Fluent/FluentTheme.xaml");
            Application.Current.Styles.Add(new StyleInclude(fluentUri) { Source = fluentUri });

            // Then add custom style overrides on top
            var styleUriPath = new Uri("avares://KOTORModSync" + stylePath);
            Application.Current.Styles.Add(new StyleInclude(styleUriPath) { Source = styleUriPath });

            // Apply to all open windows
            ApplyToAllOpenWindows();
            StyleChanged?.Invoke(s_currentStyleUri);
        }

        private static string NormalizeStylePath(string stylePath)
        {
            if (string.IsNullOrWhiteSpace(stylePath))
            {
                return DefaultThemePath;
            }

            stylePath = stylePath.Trim();

            if (string.Equals(stylePath, "Fluent.Light", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultThemePath;
            }

            stylePath = stylePath.Replace('\\', '/');

            const string assemblyPrefix = "avares://KOTORModSync";
            if (stylePath.StartsWith(assemblyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                stylePath = stylePath.Substring(assemblyPrefix.Length);
            }
            else if (stylePath.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                var parsedUri = new Uri(stylePath);
                stylePath = parsedUri.AbsolutePath;
            }

            if (stylePath.EndsWith("FluentLightStyle.axaml", StringComparison.OrdinalIgnoreCase))
            {
                stylePath = DefaultThemePath;
            }
            else if (!stylePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
            {
                stylePath += ".axaml";
            }

            if (!stylePath.StartsWith("/", StringComparison.Ordinal))
            {
                if (stylePath.StartsWith("Styles/", StringComparison.OrdinalIgnoreCase))
                {
                    stylePath = "/" + stylePath;
                }
                else
                {
                    stylePath = "/Styles/" + stylePath;
                }
            }

            return stylePath;
        }

        public static IReadOnlyList<string> GetAvailableThemePaths(bool forceRefresh = false)
        {
            if (!forceRefresh && s_cachedThemePaths != null)
            {
                return s_cachedThemePaths;
            }

            lock (ThemeCacheLock)
            {
                if (!forceRefresh && s_cachedThemePaths != null)
                {
                    return s_cachedThemePaths;
                }

                var themes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    DefaultThemePath,
                };

                try
                {
                    var baseUri = new Uri("avares://KOTORModSync/Styles/");
                    foreach (Uri assetUri in AssetLoader.GetAssets(baseUri, baseUri: null))
                    {
                        string normalized = NormalizeStylePath(assetUri.ToString());
                        themes.Add(normalized);
                    }
                }
                catch
                {
                    // If asset enumeration fails, fall back to whatever we have cached/default.
                }

                s_cachedThemePaths = themes
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return s_cachedThemePaths;
            }
        }

        public static void ApplyCurrentToWindow(Window window)
        {
            if (window is null)
            {
                return;
            }

            // Ensure UI operations happen on UI thread
            if (Dispatcher.UIThread.CheckAccess())
            {
                ApplyToWindow(window, s_currentStyleUri);
            }
            else
            {
                Dispatcher.UIThread.Post(() => ApplyToWindow(window, s_currentStyleUri), DispatcherPriority.Normal);
            }
        }

        public static string GetCurrentStylePath()
        {
            if (!string.IsNullOrEmpty(s_currentTheme))
            {
                return s_currentTheme;
            }

            if (s_currentStyleUri is null)
            {
                return "/Styles/LightStyle.axaml"; // Default to Light Style
            }

            string path = s_currentStyleUri.ToString();

            if (path.StartsWith("avares://KOTORModSync", StringComparison.Ordinal))
            {
                return path.Substring("avares://KOTORModSync".Length);
            }
            return "/Styles/LightStyle.axaml"; // Default to Light Style
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
            {
                return;
            }

            // CRITICAL: Always set Light theme variant for LightStyle
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
