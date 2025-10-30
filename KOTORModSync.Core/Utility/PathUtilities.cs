// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using JetBrains.Annotations;

namespace KOTORModSync.Core.Utility
{

	public static class PathUtilities
	{

		[NotNull]
		public static IEnumerable<string> GetDefaultPathsForMods()
		{
			OSPlatform os = UtilityHelper.GetOperatingSystem();
			List<string> list = new List<string>();
			if (os == OSPlatform.Windows)
			{
				list.AddRange(new[]
				{
					Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
				});
			}
			else if (os == OSPlatform.Linux || os == OSPlatform.OSX)
			{
				list.AddRange(new[]
				{
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
				});


			}
			return list.Where(Directory.Exists).Distinct(StringComparer.Ordinal).ToList();
		}
		private static readonly string[] collection = new[]
				{
					@"C:\Program Files\Steam\steamapps\common\swkotor",
					@"C:\Program Files (x86)\Steam\steamapps\common\swkotor",
					@"C:\Program Files\LucasArts\SWKotOR",
					@"C:\Program Files (x86)\LucasArts\SWKotOR",
					@"C:\GOG Games\Star Wars - KotOR",
					@"C:\Program Files\Steam\steamapps\common\Knights of the Old Republic II",
					@"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II",
					@"C:\Program Files\LucasArts\SWKotOR2",
					@"C:\Program Files (x86)\LucasArts\SWKotOR2",
					@"C:\GOG Games\Star Wars - KotOR2",
				};

		[NotNull]
		public static IEnumerable<string> GetDefaultPathsForGame()
		{
			OSPlatform os = UtilityHelper.GetOperatingSystem();
			List<string> results = new List<string>();
			if (os == OSPlatform.Windows)
			{
				results.AddRange(collection);
			}
			else if (os == OSPlatform.OSX)
			{
				results.AddRange(new[]
				{
					"~/Library/Application Support/Steam/steamapps/common/swkotor/Knights of the Old Republic.app/Contents/Assets",
					"~/Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II/Knights of the Old Republic II.app/Contents/Assets",
					"~/Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II/KOTOR2.app/Contents/GameData/",
				});
			}
			else if (os == OSPlatform.Linux)
			{
				results.AddRange(new[]
				{
					"~/.steam/steam/steamapps/common/swkotor",
					"~/.steam/steam/steamapps/common/Knights of the Old Republic II",
					"~/.local/share/Steam/steamapps/common/swkotor",
					"~/.local/share/Steam/steamapps/common/Knights of the Old Republic II",
				});


			}

			return results.Select(ExpandPath).Where(Directory.Exists).Distinct(StringComparer.Ordinal).ToList();
		}



		[NotNull]
		public static string ExpandPath([CanBeNull] string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return string.Empty;
			string p = Environment.ExpandEnvironmentVariables(path);
			return Path.GetFullPath(p);
		}

		public enum DetectedGame
		{
			Unknown,
			Kotor1,
			Kotor2Legacy,
			Kotor2Aspyr,
		}

		public static DetectedGame DetectGame([CanBeNull] string gamePath)
		{
			if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
				return DetectedGame.Unknown;

			string normalizedPath = ExpandPath(gamePath);

			// Check for KOTOR 1 files
			var kotor1Checks = new[]
			{
				"streamwaves",
				"swkotor.exe",
				"swkotor.ini",
				"rims",
				"utils",
				"32370_install.vdf",
				"miles/mssds3d.m3d",
				"miles/msssoft.m3d",
				"data/party.bif",
				"data/player.bif",
				"modules/global.mod",
				"modules/legal.mod",
				"modules/mainmenu.mod",
			};

			int kotor1Score = kotor1Checks.Count(check => File.Exists(Path.Combine(normalizedPath, check)) || Directory.Exists(Path.Combine(normalizedPath, check)));

			// Check for KOTOR 2 files
			var kotor2Checks = new[]
			{
				"streamvoice",
				"swkotor2.exe",
				"swkotor2.ini",
				"LocalVault",
				"LocalVault/test.bic",
				"LocalVault/testold.bic",
				"miles/binkawin.asi",
				"miles/mssds3d.flt",
				"miles/mssdolby.flt",
				"miles/mssogg.asi",
				"data/Dialogs.bif",
			};

			int kotor2Score = kotor2Checks.Count(check => File.Exists(Path.Combine(normalizedPath, check)) || Directory.Exists(Path.Combine(normalizedPath, check)));

			// Determine base game
			if (kotor1Score > kotor2Score)
				return DetectedGame.Kotor1;

			return kotor2Score > 0 ? DetectKotor2Version(normalizedPath) : DetectedGame.Unknown;
		}

		private static DetectedGame DetectKotor2Version(string normalizedPath)
		{
			// Check if it's Aspyr version by looking for Aspyr-specific files
			string overridePath = Path.Combine(normalizedPath, "override");
			if (!Directory.Exists(overridePath))
				return DetectedGame.Kotor2Legacy;

			var aspyrChecks = new[]
			{
				"override/cntrl_ps3_eng.tga",
				"override/cntrl_ps3_fre.tga",
				"override/cntrl_ps3_ger.tga",
				"override/cntrl_ps3_ita.tga",
				"override/cntrl_ps3_spa.tga",
				"override/cntrl_xb360_eng.tga",
				"override/cntrl_xb360_fre.tga",
				"override/cntrl_xb360_ger.tga",
				"override/cntrl_xb360_ita.tga",
				"override/cntrl_xb360_spa.tga",
				"override/cus_button_a.tga",
				"override/cus_button_aps.tga",
				"override/cus_button_b.tga",
				"override/cus_button_bps.tga",
				"override/cus_button_x.tga",
				"override/cus_button_xps.tga",
				"override/cus_button_y.tga",
				"override/cus_button_yps.tga",
				"override/cus_gpad_bg.tga",
				"override/cus_gpad_fper.tga",
				"override/cus_gpad_fper2.tga",
				"override/cus_gpad_gen.tga",
				"override/cus_gpad_gen2.tga",
				"override/cus_gpad_hand.tga",
				"override/cus_gpad_hand2.tga",
				"override/cus_gpad_help.tga",
				"override/cus_gpad_help2.tga",
				"override/cus_gpad_map.tga",
				"override/cus_gpad_map2.tga",
				"override/cus_gpad_save.tga",
				"override/cus_gpad_save2.tga",
				"override/cus_gpad_solo.tga",
				"override/cus_gpad_solo2.tga",
				"override/cus_gpad_solox.tga",
				"override/cus_gpad_solox2.tga",
				"override/cus_gpad_ste.tga",
				"override/cus_gpad_ste2.tga",
				"override/cus_gpad_ste3.tga",
				"override/custom.txt",
				"override/custpnl_p.gui",
				"override/d2xfnt_d16x16b.tga",
				"override/d2xfont16x16b_ps.tga",
				"override/d2xfont16x16b.tga",
				"override/d3xfnt_d16x16b.tga",
				"override/d3xfont16x16b_ps.tga",
				"override/d3xfont16x16b.tga",
				"override/diafnt16x16b_ps.tga",
				"override/dialogfont16x16b.tga",
				"override/equip_p.gui",
				"override/fx_step_splash.MDL",
				"override/gamepad.txt",
				"override/gui_scroll.wav",
				"override/handmaiden.DLG",
				"override/lbl_miscroll_op",
			};

			int aspyrTotal = aspyrChecks.Length;
			int aspyrScore = aspyrChecks.Count(check => File.Exists(Path.Combine(normalizedPath, check)));

			// Use >= 70% of Aspyr-specific files as threshold
			double threshold = aspyrTotal * 0.7;
			return aspyrScore >= threshold ? DetectedGame.Kotor2Aspyr : DetectedGame.Kotor2Legacy;
		}
	}
}