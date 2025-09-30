// Copyright 2021-2025 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;

namespace KOTORModSync
{
	internal static class ThemeManager
	{
		private static Uri s_currentStyleUri;

		public static event Action<Uri> StyleChanged;

		public static void UpdateStyle([JetBrains.Annotations.CanBeNull] string stylePath)
		{
			Uri newUri = null;
			if ( !string.IsNullOrEmpty(stylePath) )
				newUri = new Uri("avares://KOTORModSync" + stylePath);

			s_currentStyleUri = newUri;
			ApplyToAllOpenWindows();
			StyleChanged?.Invoke(s_currentStyleUri);
			// Remove any previous custom styles (but keep FluentTheme)
			//Styles.Clear();
			//Styles.Add(new FluentTheme());
			//Styles.Clear();
			for ( int i = Application.Current.Styles.Count - 1; i >= 0; i-- )
			{
				if ( Application.Current.Styles[i] is StyleInclude styleInclude &&
					styleInclude.Source != null &&
					styleInclude.Source.ToString().Contains("/Styles/") )
				{
					Application.Current.Styles.RemoveAt(i);
				}
			}
			if ( !string.IsNullOrEmpty(stylePath) )
			{
				// Apply the selected style dynamically
				var styleUriPath = new Uri("avares://KOTORModSync" + stylePath);
				Application.Current.Styles.Add(new StyleInclude(styleUriPath) { Source = styleUriPath });
			}
		}

		public static void ApplyCurrentToWindow(Window window) => ApplyToWindow(window, s_currentStyleUri);

		public static string GetCurrentStylePath()
		{
			if ( s_currentStyleUri == null )
				return "/Styles/KotorStyle.axaml";

			string path = s_currentStyleUri.ToString();
			// Extract just the path part after "avares://KOTORModSync"
			if ( path.StartsWith("avares://KOTORModSync") )
			{
				return path.Substring("avares://KOTORModSync".Length);
			}
			return "/Styles/KotorStyle.axaml";
		}

		private static void ApplyToAllOpenWindows()
		{
			if ( Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.Windows != null && desktop.Windows.Count > 0 )
			{
				foreach ( Window w in desktop.Windows )
				{
					ApplyToWindow(w, s_currentStyleUri);
				}
			}
		}

		private static void ApplyToWindow(Window window, Uri styleUri)
		{
			if ( window is null )
				return;

			// Remove any previous custom style includes from this window (keep FluentTheme)
			for ( int i = window.Styles.Count - 1; i >= 0; i-- )
			{
				if ( window.Styles[i] is StyleInclude si
					&& si.Source != null
					&& si.Source.ToString().Contains("/Styles/") )
				{
					window.Styles.RemoveAt(i);
				}
			}

			if ( styleUri != null )
			{
				if ( !window.Styles.OfType<StyleInclude>().Any(s => s.Source == styleUri) )
				{
					window.Styles.Add(new StyleInclude(styleUri) { Source = styleUri });
				}
			}
		}
	}
}


