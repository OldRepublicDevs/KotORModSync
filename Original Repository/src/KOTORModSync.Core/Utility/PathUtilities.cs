// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using JetBrains.Annotations;
using KOTORModSync.Core.FileSystemUtils;

namespace KOTORModSync.Core.Utility
{

    public static class PathUtilities
    {

        [NotNull]
        public static IEnumerable<string> GetDefaultPathsForMods()
        {
            OSPlatform os = UtilityHelper.GetOperatingSystem();
            var list = new List<string>();
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
            var results = new List<string>();
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
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

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
            {
                return DetectedGame.Unknown;
            }

            string normalizedPath = ExpandPath(gamePath);

            // Check for KOTOR 1 files
            string[] kotor1Checks = new[]
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
            string[] kotor2Checks = new[]
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
            {
                return DetectedGame.Kotor1;
            }

            return kotor2Score > 0 ? DetectKotor2Version(normalizedPath) : DetectedGame.Unknown;
        }

        private static DetectedGame DetectKotor2Version(string normalizedPath)
        {
            // Check if it's Aspyr version by looking for Aspyr-specific files
            string overridePath = Path.Combine(normalizedPath, "override");
            if (!Directory.Exists(overridePath))
            {
                return DetectedGame.Kotor2Legacy;
            }

            string[] aspyrChecks = new[]
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

        private const int EvidenceLimit = 6;

        private static readonly IReadOnlyDictionary<GameInstallVariant, IReadOnlyList<string>> DetailedSignatureMap =
            new Dictionary<GameInstallVariant, IReadOnlyList<string>>
            {
                {
                    GameInstallVariant.PcKotor1,
                    new[]
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
                    }
                },
                {
                    GameInstallVariant.PcKotor2,
                    new[]
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
                    }
                },
                {
                    GameInstallVariant.XboxKotor1,
                    new[]
                    {
                        "01_SS_Repair01.ini",
                        "swpatch.ini",
                        "dataxbox/_newbif.bif",
                        "rimsxbox",
                        "players.erf",
                        "downloader.xbe",
                        "rimsxbox/manm28ad_adx.rim",
                        "rimsxbox/miniglobal.rim",
                        "rimsxbox/miniglobaldx.rim",
                        "rimsxbox/STUNT_56a_a.rim",
                        "rimsxbox/STUNT_56a_adx.rim",
                        "rimsxbox/STUNT_57_adx.rim",
                        "rimsxbox/subglobal.rim",
                        "rimsxbox/subglobaldx.rim",
                        "rimsxbox/unk_m44ac_adx.rim",
                        "rimsxbox/M12ab_adx.rim",
                        "rimsxbox/mainmenu.rim",
                        "rimsxbox/mainmenudx.rim",
                    }
                },
                {
                    GameInstallVariant.XboxKotor2,
                    new[]
                    {
                        "combat.erf",
                        "effects.erf",
                        "footsteps.erf",
                        "footsteps.rim",
                        "SWRC",
                        "weapons.ERF",
                        "SuperModels/smseta.erf",
                        "SuperModels/smsetb.erf",
                        "SuperModels/smsetc.erf",
                        "SWRC/System/Subtitles_Epilogue.int",
                        "SWRC/System/Subtitles_YYY_06.int",
                        "SWRC/System/SWRepublicCommando.int",
                        "SWRC/System/System.ini",
                        "SWRC/System/UDebugMenu.u",
                        "SWRC/System/UnrealEd.int",
                        "SWRC/System/UnrealEd.u",
                        "SWRC/System/User.ini",
                        "SWRC/System/UWeb.int",
                        "SWRC/System/Window.int",
                        "SWRC/System/WinDrv.int",
                        "SWRC/System/Xbox",
                        "SWRC/System/XboxLive.int",
                        "SWRC/System/XGame.u",
                        "SWRC/System/XGameList.int",
                        "SWRC/System/XGames.int",
                        "SWRC/System/XInterface.u",
                        "SWRC/System/XInterfaceMP.u",
                        "SWRC/System/XMapList.int",
                        "SWRC/System/XMaps.int",
                        "SWRC/System/YYY_TitleCard.int",
                        "SWRC/System/Xbox/Engine.int",
                        "SWRC/System/Xbox/XboxLive.int",
                        "SWRC/Textures/GUIContent.utx",
                    }
                },
                {
                    GameInstallVariant.IosKotor1,
                    new[]
                    {
                        "override/ios_action_bg.tga",
                        "override/ios_action_bg2.tga",
                        "override/ios_action_x.tga",
                        "override/ios_action_x2.tga",
                        "override/ios_button_a.tga",
                        "override/ios_button_x.tga",
                        "override/ios_button_y.tga",
                        "override/ios_edit_box.tga",
                        "override/ios_enemy_plus.tga",
                        "override/ios_gpad_bg.tga",
                        "override/ios_gpad_gen.tga",
                        "override/ios_gpad_gen2.tga",
                        "override/ios_gpad_help.tga",
                        "override/ios_gpad_help2.tga",
                        "override/ios_gpad_map.tga",
                        "override/ios_gpad_map2.tga",
                        "override/ios_gpad_save.tga",
                        "override/ios_gpad_save2.tga",
                        "override/ios_gpad_solo.tga",
                        "override/ios_gpad_solo2.tga",
                        "override/ios_gpad_solox.tga",
                        "override/ios_gpad_solox2.tga",
                        "override/ios_gpad_ste.tga",
                        "override/ios_gpad_ste2.tga",
                        "override/ios_gpad_ste3.tga",
                        "override/ios_help.tga",
                        "override/ios_help2.tga",
                        "override/ios_help_1.tga",
                        "KOTOR",
                        "KOTOR.entitlements",
                        "kotorios-Info.plist",
                        "AppIcon29x29.png",
                        "AppIcon50x50@2x~ipad.png",
                        "AppIcon50x50~ipad.png",
                    }
                },
                {
                    GameInstallVariant.IosKotor2,
                    new[]
                    {
                        "override/ios_mfi_deu.tga",
                        "override/ios_mfi_eng.tga",
                        "override/ios_mfi_esp.tga",
                        "override/ios_mfi_fre.tga",
                        "override/ios_mfi_ita.tga",
                        "override/ios_self_box_r.tga",
                        "override/ios_self_expand2.tga",
                        "override/ipho_forfeit.tga",
                        "override/ipho_forfeit2.tga",
                        "override/kotor2logon.tga",
                        "override/lbl_miscroll_open_f.tga",
                        "override/lbl_miscroll_open_f2.tga",
                        "override/ydialog.gui",
                        "KOTOR II",
                        "KOTOR2-Icon-20-Apple.png",
                        "KOTOR2-Icon-29-Apple.png",
                        "KOTOR2-Icon-40-Apple.png",
                        "KOTOR2-Icon-58-apple.png",
                        "KOTOR2-Icon-60-apple.png",
                        "KOTOR2-Icon-76-apple.png",
                        "KOTOR2-Icon-80-apple.png",
                        "KOTOR2_LaunchScreen.storyboardc",
                        "KOTOR2_LaunchScreen.storyboardc/Info.plist",
                        "GoogleService-Info.plist",
                    }
                },
                { GameInstallVariant.AndroidKotor1, Array.Empty<string>() },
                { GameInstallVariant.AndroidKotor2, Array.Empty<string>() },
            };

        public static DetailedGameDetectionSummary AnalyzeGameDirectoryDetailed([CanBeNull] string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return DetailedGameDetectionSummary.Empty;
            }

            var root = new CaseAwarePath(path).Resolve();
            var results = new List<DetailedGameDetectionResult>();

            foreach (KeyValuePair<GameInstallVariant, IReadOnlyList<string>> signature in DetailedSignatureMap)
            {
                results.Add(ScoreSignatures(root, signature.Key, signature.Value));
            }

            if (results.Count == 0)
            {
                return DetailedGameDetectionSummary.Empty;
            }

            int maxMatches = results.Max(r => r.Matches);
            if (maxMatches <= 0)
            {
                return new DetailedGameDetectionSummary(GameInstallVariant.Unknown, isTie: false, bestResult: null, results);
            }

            bool tie = results.Count(r => r.Matches == maxMatches) > 1;
            DetailedGameDetectionResult best = tie ? null : results.First(r => r.Matches == maxMatches);

            if (best != null && best.Variant == GameInstallVariant.PcKotor2)
            {
                DetectedGame k2Version = DetectKotor2Version(root.ToString());
                GameInstallVariant resolved = k2Version == DetectedGame.Kotor2Aspyr
                    ? GameInstallVariant.PcKotor2Aspyr
                    : GameInstallVariant.PcKotor2Legacy;

                best = new DetailedGameDetectionResult(
                    resolved,
                    best.Matches,
                    best.TotalChecks,
                    best.MatchedEvidence,
                    best.MissingEvidence);
            }

            var normalizedResults = results
                .Select(r =>
                    r.Variant != GameInstallVariant.PcKotor2
                        ? r
                        : new DetailedGameDetectionResult(
                            best?.Variant ?? GameInstallVariant.PcKotor2,
                            r.Matches,
                            r.TotalChecks,
                            r.MatchedEvidence,
                            r.MissingEvidence))
                .ToList();

            return new DetailedGameDetectionSummary(
                best?.Variant ?? GameInstallVariant.Unknown,
                tie,
                best,
                normalizedResults);
        }

        private static DetailedGameDetectionResult ScoreSignatures(
            CaseAwarePath root,
            GameInstallVariant identity,
            IReadOnlyList<string> signatures)
        {
            if (signatures is null || signatures.Count == 0)
            {
                return new DetailedGameDetectionResult(identity, 0, 0, Array.Empty<string>(), Array.Empty<string>());
            }

            int matches = 0;
            var matched = new List<string>(EvidenceLimit);
            var missing = new List<string>(EvidenceLimit);

            foreach (string signature in signatures)
            {
                bool exists = SignatureExists(root, signature);
                if (exists)
                {
                    matches++;
                    if (matched.Count < EvidenceLimit)
                    {
                        matched.Add(signature);
                    }
                }
                else if (missing.Count < EvidenceLimit)
                {
                    missing.Add(signature);
                }
            }

            return new DetailedGameDetectionResult(identity, matches, signatures.Count, matched, missing);
        }

        private static bool SignatureExists(CaseAwarePath root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string normalized = relativePath.Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            CaseAwarePath candidate = root.JoinPath(normalized);
            string fullPath = candidate.ToString();

            return Directory.Exists(fullPath) || File.Exists(fullPath);
        }

        public enum GameInstallVariant
        {
            Unknown,
            PcKotor1,
            PcKotor2,
            PcKotor2Legacy,
            PcKotor2Aspyr,
            XboxKotor1,
            XboxKotor2,
            IosKotor1,
            IosKotor2,
            AndroidKotor1,
            AndroidKotor2,
        }

        public sealed class DetailedGameDetectionResult
        {
            public DetailedGameDetectionResult(
                GameInstallVariant variant,
                int matches,
                int totalChecks,
                IReadOnlyList<string> matchedEvidence,
                IReadOnlyList<string> missingEvidence)
            {
                Variant = variant;
                Matches = matches;
                TotalChecks = totalChecks;
                MatchedEvidence = matchedEvidence;
                MissingEvidence = missingEvidence;
            }

            public GameInstallVariant Variant { get; }
            public int Matches { get; }
            public int TotalChecks { get; }
            public IReadOnlyList<string> MatchedEvidence { get; }
            public IReadOnlyList<string> MissingEvidence { get; }
            public double Confidence => TotalChecks == 0 ? 0 : (double)Matches / TotalChecks;
        }

        public sealed class DetailedGameDetectionSummary
        {
            public static readonly DetailedGameDetectionSummary Empty =
                new DetailedGameDetectionSummary(GameInstallVariant.Unknown, isTie: false, bestResult: null, Array.Empty<DetailedGameDetectionResult>());

            public DetailedGameDetectionSummary(
                GameInstallVariant identity,
                bool isTie,
                DetailedGameDetectionResult bestResult,
                IReadOnlyList<DetailedGameDetectionResult> allResults)
            {
                Identity = identity;
                IsTie = isTie;
                BestResult = bestResult;
                AllResults = allResults;
            }

            public GameInstallVariant Identity { get; }
            public bool IsTie { get; }
            public DetailedGameDetectionResult BestResult { get; }
            public IReadOnlyList<DetailedGameDetectionResult> AllResults { get; }
        }
    }
}

