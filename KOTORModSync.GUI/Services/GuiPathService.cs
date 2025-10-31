// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using KOTORModSync.Core;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Services
{

	public class GuiPathService
	{
		private readonly MainConfig _mainConfig;
		private readonly ComponentSelectionService _componentSelectionService;

		public GuiPathService( MainConfig mainConfig, ComponentSelectionService componentSelectionService = null )
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException( nameof( mainConfig ) );
			_componentSelectionService = componentSelectionService;
		}

		public bool TryApplySourcePath( string text, Action<string> onPathSet = null )
		{
			try
			{
				string p = PathUtilities.ExpandPath( text );
				if (string.IsNullOrWhiteSpace( p ) || !Directory.Exists( p ))
					return false;

				_mainConfig.sourcePath = new DirectoryInfo( p );

				Action callback = onPathSet != null ? (Action)(() => onPathSet( p )) : null;
                FileSystemService.SetupModDirectoryWatcher( p, ( changedFile ) => callback?.Invoke() );

				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException( ex );
				return false;
			}
		}

	public bool TryApplyDestinationPath( string text )
	{
		try
		{
			string p = PathUtilities.ExpandPath( text );
			if (string.IsNullOrWhiteSpace( p ) || !Directory.Exists( p ))
				return false;

			_mainConfig.destinationPath = new DirectoryInfo( p );
			
			// Detect game version after setting destination path
			_componentSelectionService?.DetectGameVersion();
			
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogException( ex );
			return false;
		}
	}

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
        public static void UpdatePathSuggestions( TextBox input, ComboBox combo, ref CancellationTokenSource cts )
		{
			try
			{
				cts?.Cancel();
				cts = new CancellationTokenSource();
				CancellationToken token = cts.Token;
				string typed = input.Text ?? string.Empty;

				_ = Task.Run<IList<string>>( () =>
				{
					var results = new List<string>();
					string expanded = PathUtilities.ExpandPath( typed );

					if (string.IsNullOrWhiteSpace( expanded ))
					{

						if (string.Equals( input.Name, "ModPathInput", StringComparison.Ordinal ))
							return PathUtilities.GetDefaultPathsForMods().ToList();
						if (string.Equals( input.Name, "InstallPathInput", StringComparison.Ordinal ))
							return PathUtilities.GetDefaultPathsForGame().ToList();
						return results;
					}

					string normalized = expanded;


					bool endsWithSep = normalized.EndsWith( Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal );

					bool isRootDir = IsRootDirectory( normalized );
					if (isRootDir)
					{
						normalized = NormalizeRootDirectory( normalized );
					}

					string baseDir;
					string fragment;

					if (isRootDir)
					{
						baseDir = normalized;
						fragment = string.Empty;
					}
					else
					{
						baseDir = endsWithSep ? normalized : Path.GetDirectoryName( normalized );
						if (string.IsNullOrEmpty( baseDir ))
							baseDir = Path.GetPathRoot( normalized );
						fragment = endsWithSep ? string.Empty : Path.GetFileName( normalized );
					}

					if (!string.IsNullOrEmpty( baseDir ) && Directory.Exists( baseDir ))
					{
						IEnumerable<string> dirs = GetDirectoriesInPath( baseDir );

						if (string.IsNullOrEmpty( fragment ))
						{
							results.AddRange( dirs );
						}
						else
						{
							results.AddRange( dirs.Where( d =>
								Path.GetFileName( d ).IndexOf( fragment, StringComparison.OrdinalIgnoreCase ) >= 0 ) );
						}
					}

					return results;
				}, token ).ContinueWith( t =>
				{
					if (token.IsCancellationRequested || t.IsFaulted)
						return;

					Dispatcher.UIThread.Post( () =>
					{
						UpdateComboBoxItemsSource( combo, input, t.Result );
					} );
				}, token );
			}
			catch (Exception ex)
			{
				Logger.LogVerbose( $"Error updating path suggestions: {ex.Message}" );
			}
		}

		public static async Task AddToRecentModsAsync( string path )
		{
			try
			{
				if (string.IsNullOrWhiteSpace( path ) || !Directory.Exists( path ))
					return;

				string appData = Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData );
				string folder = Path.Combine( appData, "KOTORModSync" );
				string file = Path.Combine( folder, "recent_mod_dirs.txt" );
				_ = Directory.CreateDirectory( folder );



				List<string> existing = await LoadRecentModDirectoriesAsync( file ).ConfigureAwait(true);

				AddToRecentDirectories( path, existing, maxCount: 20 );

				await SaveRecentModDirectoriesAsync( existing, file ).ConfigureAwait(true);
			}
			catch (Exception ex)
			{
				await Logger.LogVerboseAsync( $"Error adding to recent mods: {ex.Message}" ).ConfigureAwait( false );
			}
		}

		public static async Task<List<string>> LoadRecentModDirectoriesAsync( string filePath )
		{
			var result = new List<string>();

			try
			{
				if (!File.Exists( filePath ))
					return result;



				string[] lines = await Task.Run( () => File.ReadAllLines( filePath ) ).ConfigureAwait(true);
				result.AddRange( lines.Where( line => !string.IsNullOrWhiteSpace( line ) && Directory.Exists( line ) ) );
			}
			catch (Exception ex)
			{
				await Logger.LogVerboseAsync( $"Error loading recent mod directories: {ex.Message}" ).ConfigureAwait( false );
			}

			return result;
		}

		public static async Task SaveRecentModDirectoriesAsync( List<string> directories, string filePath )
		{
			try
			{

				await Task.Run( () => File.WriteAllLines( filePath, directories ) ).ConfigureAwait(true);
			}
			catch (Exception ex)
			{
				await Logger.LogVerboseAsync( $"Error saving recent mod directories: {ex.Message}" ).ConfigureAwait( false );
			}
		}

		public static void AddToRecentDirectories( string path, List<string> existing, int maxCount = 20 )
		{

			_ = existing.Remove( path );

			existing.Insert( 0, path );

			if (existing.Count > maxCount)
			{
				existing.RemoveRange( maxCount, existing.Count - maxCount );
			}
		}

		#region Private Helper Methods

		private static bool IsRootDirectory( string normalized )
		{
			if (UtilityHelper.GetOperatingSystem() == OSPlatform.Windows)
			{

				if (normalized.Length >= 2 && normalized[1] == ':' &&
					(normalized.Length == 2 || (normalized.Length == 3 && normalized[2] == Path.DirectorySeparatorChar)))
				{
					return true;
				}
			}
			else
			{

				if (string.Equals( normalized, "/", StringComparison.Ordinal ) || normalized.EndsWith( ":/", StringComparison.Ordinal ))
					return true;
			}

			return false;
		}

		private static string NormalizeRootDirectory( string normalized )
		{
			if (UtilityHelper.GetOperatingSystem() == OSPlatform.Windows)
			{
				return normalized.TrimEnd( Path.DirectorySeparatorChar ) + Path.DirectorySeparatorChar;
			}
			return normalized;
		}

		private static IEnumerable<string> GetDirectoriesInPath( string baseDir )
		{
			try
			{
				return Directory.EnumerateDirectories( baseDir );
			}
			catch (Exception ex)
			{
				Logger.LogVerbose( $"Failed to enumerate directories in {baseDir}: {ex.Message}" );
				return Enumerable.Empty<string>();
			}
		}

		private static void UpdateComboBoxItemsSource( ComboBox combo, TextBox input, IList<string> newResults )
		{
			if (!Dispatcher.UIThread.CheckAccess())
			{
				Dispatcher.UIThread.Post(() => UpdateComboBoxItemsSource(combo, input, newResults), DispatcherPriority.Normal);
				return;
			}
			try
			{

				if (string.Equals( combo.Name, "ModPathSuggestions", StringComparison.Ordinal ) && combo.ItemsSource is IEnumerable<string> existingItems)
				{
					var resultsToShow = newResults.ToList();

					foreach (string item in existingItems)
					{
						if (!resultsToShow.Contains( item, StringComparer.Ordinal ) && Directory.Exists( item ))
							resultsToShow.Add( item );
					}

					var current = (combo.ItemsSource as IEnumerable<string>)?.ToList();
					if (current is null || !current.SequenceEqual( resultsToShow, StringComparer.Ordinal ))
						combo.ItemsSource = resultsToShow;
				}
				else
				{
					var current = (combo.ItemsSource as IEnumerable<string>)?.ToList();
					if (current is null || !current.SequenceEqual( newResults, StringComparer.Ordinal ))
						combo.ItemsSource = newResults;
				}

				if (newResults.Count > 0 && input.IsKeyboardFocusWithin)
					combo.IsDropDownOpen = true;
			}
			catch (Exception ex)
			{
				Logger.LogVerbose( $"Error updating combobox items source: {ex.Message}" );
			}
		}

		#endregion
	}
}