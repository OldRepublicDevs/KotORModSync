// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.


using System;
using KOTORModSync.Core;

namespace KOTORModSync.Services
{

	public class ThemeService
	{

		public static void ApplyTheme(string stylePath)
		{
			try
			{
				if ( string.IsNullOrEmpty(stylePath) )
				{
					Logger.LogWarning("Cannot apply theme: style path is null or empty");
					return;
				}

				ThemeManager.UpdateStyle(stylePath);
				Logger.LogVerbose($"Applied theme: {stylePath}");
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, $"Failed to apply theme: {stylePath}");
			}
		}

		public static string GetCurrentTheme()
		{
			try
			{
				return ThemeManager.GetCurrentStylePath();
			}
			catch ( Exception ex )
			{
				Logger.LogException(ex, "Failed to get current theme");
				return null;
			}
		}
	}
}

