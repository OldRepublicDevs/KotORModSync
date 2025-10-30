// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Controls;

using KOTORModSync.Controls;
using KOTORModSync.Core;
using KOTORModSync.Models;

namespace KOTORModSync.Services
{

	public class SettingsService
	{
		private readonly MainConfig _mainConfig;
		private readonly Window _parentWindow;

		public SettingsService( MainConfig mainConfig, Window parentWindow )
		{
			_mainConfig = mainConfig ?? throw new ArgumentNullException( nameof( mainConfig ) );
			_parentWindow = parentWindow ?? throw new ArgumentNullException( nameof( parentWindow ) );
		}

		public (AppSettings Settings, string Theme) LoadSettings()
		{
			try
			{
				AppSettings settings = SettingsManager.LoadSettings();
				settings.ApplyToMainConfig( _mainConfig, out string theme );

				Logger.LogVerbose( "Settings loaded successfully" );
				return (settings, theme);
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Failed to load settings" );
				return (new AppSettings(), null);
			}
		}

		public void SaveSettings( string currentTheme )
		{
			try
			{
				var settings = AppSettings.FromCurrentState( _mainConfig, currentTheme );
				SettingsManager.SaveSettings( settings );

				Logger.LogVerbose( "Settings saved successfully" );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Failed to save settings" );
			}
		}

		public static void UpdateDirectoryPickersFromSettings(
			AppSettings settings,
			Func<string, DirectoryPickerControl> findControl )
		{
			try
			{

				if (!string.IsNullOrEmpty( settings.SourcePath ))
				{
					DirectoryPickerControl modPicker = findControl( "ModDirectoryPicker" );
					DirectoryPickerControl step1ModPicker = findControl( "Step1ModDirectoryPicker" );

					SettingsService.UpdateDirectoryPickerWithPath( modPicker, settings.SourcePath );
					SettingsService.UpdateDirectoryPickerWithPath( step1ModPicker, settings.SourcePath );
				}

				if (!string.IsNullOrEmpty( settings.DestinationPath ))
				{
					DirectoryPickerControl kotorPicker = findControl( "KotorDirectoryPicker" );
					DirectoryPickerControl step1KotorPicker = findControl( "Step1KotorDirectoryPicker" );

					SettingsService.UpdateDirectoryPickerWithPath( kotorPicker, settings.DestinationPath );
					SettingsService.UpdateDirectoryPickerWithPath( step1KotorPicker, settings.DestinationPath );
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Failed to update directory pickers from settings" );
			}
		}

		private static void UpdateDirectoryPickerWithPath( DirectoryPickerControl picker, string path )
		{
			if (picker is null || string.IsNullOrEmpty( path ))
				return;

			try
			{
				picker.SetCurrentPathFromSettings( path );

				ComboBox comboBox = picker.FindControl<ComboBox>( "PathSuggestions" );
				if (comboBox != null)
				{
					var currentItems = (comboBox.ItemsSource as IEnumerable<string>)?.ToList() ?? new List<string>();
					if (!currentItems.Contains( path, StringComparer.Ordinal ))
					{
						currentItems.Insert( 0, path );
						if (currentItems.Count > 20)
							currentItems = currentItems.Take( 20 ).ToList();
						comboBox.ItemsSource = currentItems;
					}

					comboBox.SelectedItem = path;
				}
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, $"Failed to update directory picker with path: {path}" );
			}
		}

		public static void SyncDirectoryPickers(
			DirectoryPickerType pickerType,
			string path,
			Func<string, DirectoryPickerControl> findControl )
		{
			try
			{
				var allPickers = new List<DirectoryPickerControl>();

				if (pickerType == DirectoryPickerType.ModDirectory)
				{
					DirectoryPickerControl mainPicker = findControl( "ModDirectoryPicker" );
					DirectoryPickerControl step1Picker = findControl( "Step1ModDirectoryPicker" );

					if (mainPicker != null) allPickers.Add( mainPicker );
					if (step1Picker != null) allPickers.Add( step1Picker );
				}
				else if (pickerType == DirectoryPickerType.KotorDirectory)
				{
					DirectoryPickerControl mainPicker = findControl( "KotorDirectoryPicker" );
					DirectoryPickerControl step1Picker = findControl( "Step1KotorDirectoryPicker" );

					if (mainPicker != null) allPickers.Add( mainPicker );
					if (step1Picker != null) allPickers.Add( step1Picker );
				}

				foreach (DirectoryPickerControl picker in allPickers)
					picker.SetCurrentPathFromSettings( path );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error synchronizing directory pickers" );
			}
		}

		public void InitializeDirectoryPickers( Func<string, DirectoryPickerControl> findControl )
		{
			try
			{
				DirectoryPickerControl modPicker = findControl( "ModDirectoryPicker" );
				DirectoryPickerControl kotorPicker = findControl( "KotorDirectoryPicker" );
				DirectoryPickerControl step1ModPicker = findControl( "Step1ModDirectoryPicker" );
				DirectoryPickerControl step1KotorPicker = findControl( "Step1KotorDirectoryPicker" );

				if (modPicker != null && _mainConfig.sourcePath != null)
					modPicker.SetCurrentPathFromSettings( _mainConfig.sourcePath.FullName );

				if (kotorPicker != null && _mainConfig.destinationPath != null)
					kotorPicker.SetCurrentPathFromSettings( _mainConfig.destinationPath.FullName );

				if (step1ModPicker != null && _mainConfig.sourcePath != null)
					step1ModPicker.SetCurrentPathFromSettings( _mainConfig.sourcePath.FullName );

				if (step1KotorPicker != null && _mainConfig.destinationPath != null)
					step1KotorPicker.SetCurrentPathFromSettings( _mainConfig.destinationPath.FullName );
			}
			catch (Exception ex)
			{
				Logger.LogException( ex, "Error initializing directory pickers" );
			}
		}
	}
}