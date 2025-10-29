// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;

using KOTORModSync.Core;

namespace KOTORModSync.Services
{
	public enum ThemeType
	{
		FluentLight,
		KOTOR,
		KOTOR2,
	}

	public class ThemeService
	{
		public static void ApplyTheme(string stylePath)
		{
			try
			{
				if (string.IsNullOrEmpty(stylePath))
				{
					Logger.LogWarning("Cannot apply theme: style path is null or empty");
					return;
				}

				ThemeManager.UpdateStyle(stylePath);
				Logger.LogVerbose($"Applied theme: {stylePath}");
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, $"Failed to apply theme: {stylePath}");
			}
		}

		public static void ApplyTheme(ThemeType themeType)
		{
			string stylePath = GetStylePathForTheme(themeType);
			ApplyTheme(stylePath);
		}

		public static string GetCurrentTheme()
		{
			try
			{
				return ThemeManager.GetCurrentStylePath();
			}
			catch (Exception ex)
			{
				Logger.LogException(ex, "Failed to get current theme");
				return null;
			}
		}

		public static ThemeType GetCurrentThemeType()
		{
			string currentTheme = GetCurrentTheme();
			if (string.IsNullOrEmpty(currentTheme))
				return ThemeType.FluentLight; // Default

			if (currentTheme.Equals("Fluent.Light", StringComparison.OrdinalIgnoreCase))
				return ThemeType.FluentLight;
			if (currentTheme.Contains("Kotor2Style"))
				return ThemeType.KOTOR2;
			if (currentTheme.Contains("KotorStyle"))
				return ThemeType.KOTOR;

			return ThemeType.FluentLight; // Default
		}

		public static string GetStylePathForTheme(ThemeType themeType)
		{
			switch (themeType)
			{
				case ThemeType.FluentLight:
					return "Fluent.Light";
				case ThemeType.KOTOR:
					return "/Styles/KotorStyle.axaml";
				case ThemeType.KOTOR2:
					return "/Styles/Kotor2Style.axaml";
				default:
					return "Fluent.Light";
			}
		}
	}
}