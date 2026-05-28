//
// Settings for NCS Decompiler - UI is in NCSDecomp project (Avalonia)
//
using System;
using System.Collections.Generic;
using System.IO;
using JavaSystem = KOTORModSync.Formats.NCS.NCSDecomp.JavaSystem;
using KOTORModSync.Common;

namespace KOTORModSync.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Settings storage for the NCS decompiler.
    /// The actual UI for editing settings is in the NCSDecomp project (SettingsWindow.axaml).
    /// </summary>
    public class Settings : Properties
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Settings.java:48
        // Original: private static final String CONFIG_FILE = "ncsdecomp.conf";
        private static readonly string ConfigFileName = "ncsdecomp.conf";
        private static readonly string LegacyConfigFileName = "dencs.conf";

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Settings.java:377-412
        // Original: public void load() { File configDir = new File(System.getProperty("user.dir"), "config"); File configFile = new File(configDir, CONFIG_FILE); ... }
        public new void Load()
        {
            string userDir = JavaSystem.GetProperty("user.dir");
            string configDir = Path.Combine(userDir, "config");
            string configFile = Path.Combine(configDir, ConfigFileName);
            string configToLoad = configFile;
            if (!System.IO.File.Exists(configToLoad))
            {
                string legacy = Path.Combine(configDir, LegacyConfigFileName);
                if (System.IO.File.Exists(legacy))
                {
                    configToLoad = legacy;
                }
            }

            try
            {
                using (var fis = new FileInputStream(configToLoad))
                {
                    base.Load(fis);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    new NcsFile(ConfigFileName).Create();
                }
                catch (FileNotFoundException var2)
                {
                    var2.PrintStackTrace();
                    JavaSystem.Exit(1);
                }
                catch (IOException var3)
                {
                    var3.PrintStackTrace();
                    JavaSystem.Exit(1);
                }
                Reset();
                Save();
            }

            // Apply loaded settings to static flags (matching Java Settings.load() lines 405-411)
            string gameVariant = GetProperty("Game Variant", "k1").ToLower();
            FileDecompiler.isK2Selected = gameVariant.Equals("k2") || gameVariant.Equals("tsl") || gameVariant.Equals("2");
            FileDecompiler.preferSwitches = bool.Parse(GetProperty("Prefer Switches", "false"));
            FileDecompiler.strictSignatures = bool.Parse(GetProperty("Strict Signatures", "false"));
            string nwnnsscompPath = GetProperty("nwnnsscomp Path", "");
            FileDecompiler.nwnnsscompPath = string.IsNullOrEmpty(nwnnsscompPath) ? null : nwnnsscompPath;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Settings.java:419-429
        // Original: public void save() { File configDir = new File(System.getProperty("user.dir"), "config"); File configFile = new File(configDir, CONFIG_FILE); ... }
        public new void Save()
        {
            try
            {
                string userDir = JavaSystem.GetProperty("user.dir");
                string configDir = Path.Combine(userDir, "config");
                string configFile = Path.Combine(configDir, ConfigFileName);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                using (var fos = new FileOutputStream(configFile))
                {
                    Store(fos, "NCSDecomp Configuration");
                }
            }
            catch (Exception ex)
            {
                ex.PrintStackTrace();
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Settings.java:430-450
        // Original: public void reset() { ... }
        public new void Reset()
        {
            base.Reset();
            // Default output directory: ./ncsdecomp_converted relative to current working directory
            string defaultOutputDir = Path.Combine(JavaSystem.GetProperty("user.dir"), "ncsdecomp_converted");
            SetProperty("Output Directory", defaultOutputDir);
            SetProperty("Open Directory", JavaSystem.GetProperty("user.dir"));
            string defaultNwnnsscompPath = Path.Combine(Path.Combine(JavaSystem.GetProperty("user.dir"), "tools"), "nwnnsscomp.exe");
            SetProperty("nwnnsscomp Path", defaultNwnnsscompPath);
            string defaultK1Path = Path.Combine(Path.Combine(JavaSystem.GetProperty("user.dir"), "tools"), "k1_nwscript.nss");
            string defaultK2Path = Path.Combine(Path.Combine(JavaSystem.GetProperty("user.dir"), "tools"), "tsl_nwscript.nss");
            SetProperty("K1 nwscript Path", defaultK1Path);
            SetProperty("K2 nwscript Path", defaultK2Path);
            SetProperty("Game Variant", "k1");
            SetProperty("Prefer Switches", "false");
            SetProperty("Strict Signatures", "false");
            SetProperty("Overwrite Files", "false");
            SetProperty("Encoding", "Windows-1252");
            SetProperty("File Extension", ".nss");
            SetProperty("Filename Prefix", "");
            SetProperty("Filename Suffix", "");
            SetProperty("Link Scroll Bars", "false");
        }

        /// <summary>
        /// Show settings dialog. This is a no-op in the library -
        /// the actual UI is in the NCSDecomp Avalonia project.
        /// </summary>
        public virtual void Show()
        {
            // No-op in library - UI is in NCSDecomp project
            Console.WriteLine("Settings.Show() called - use NCSDecomp Avalonia app for UI");
        }
    }
}



