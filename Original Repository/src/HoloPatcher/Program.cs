using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using KOTORModSync;
using KOTORModSync.Logger;
using HoloPatcher.UI;
using HoloPatcher.UI.ViewModels;
using JetBrains.Annotations;

namespace Andastra.Patcher
{

    class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Parse command line arguments
            CommandLineArgs cmdlineArgs = ParseArgs(args);

            // Determine if we should run in CLI mode
            bool forceCli = cmdlineArgs.Install || cmdlineArgs.Uninstall || cmdlineArgs.Validate;

            if (forceCli)
            {
                // CLI mode explicitly requested - no GUI
                ExecuteCli(cmdlineArgs);
            }
            else
            {
                // GUI mode by default - try GUI, fall back gracefully if unavailable
                try
                {
                    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                }
                catch (Exception ex)
                {
                    // If GUI is unavailable, print error and exit gracefully
                    Console.Error.WriteLine($"[Warning] Display driver not available, cannot run in GUI mode: {ex.Message}");
                    Console.Error.WriteLine("[Info] Use --help to see CLI options");
                    Environment.Exit(0);
                }
            }
        }

        private static CommandLineArgs ParseArgs(string[] args)
        {
            var result = new CommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--game-dir" when i + 1 < args.Length:
                        result.GameDir = args[++i];
                        break;
                    case "--tslpatchdata" when i + 1 < args.Length:
                        result.TslPatchData = args[++i];
                        break;
                    case "--namespace-option-index" when i + 1 < args.Length:
                        if (int.TryParse(args[++i], out int index))
                        {
                            result.NamespaceOptionIndex = index;
                        }
                        break;
                    case "--console":
                        result.Console = true;
                        break;
                    case "--uninstall":
                        result.Uninstall = true;
                        break;
                    case "--install":
                        result.Install = true;
                        break;
                    case "--validate":
                        result.Validate = true;
                        break;
                    case "--help":
                        result.Help = true;
                        break;
                }
            }

            // Handle positional arguments (game_dir tslpatchdata [namespace_option_index])
            int positionalCount = args.Count(a => !a.StartsWith("--"));
            if (positionalCount >= 2)
            {
                string[] positional = args.Where(a => !a.StartsWith("--")).ToArray();
                result.GameDir = positional[0];
                result.TslPatchData = positional[1];
                if (positionalCount >= 3 && int.TryParse(positional[2], out int posIndex))
                {
                    result.NamespaceOptionIndex = posIndex;
                }
            }

            return result;
        }

        private static void ExecuteCli(CommandLineArgs args)
        {
            var logger = new PatchLogger();
            logger.VerboseLogged += (s, l) => Console.WriteLine($"[VERBOSE] {l.Message}");
            logger.NoteLogged += (s, l) => Console.WriteLine($"[NOTE] {l.Message}");
            logger.WarningLogged += (s, l) => Console.WriteLine($"[WARNING] {l.Message}");
            logger.ErrorLogged += (s, l) => Console.Error.WriteLine($"[ERROR] {l.Message}");

            // Load mod
            if (string.IsNullOrEmpty(args.TslPatchData))
            {
                Console.Error.WriteLine("[Error] No mod path specified. Use --tslpatchdata <path>");
                Environment.Exit((int)Core.ExitCode.NumberOfArgs);
                return;
            }

            Core.ModInfo modInfo;
            try
            {
                modInfo = Core.LoadMod(args.TslPatchData);
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine($"[Error] Failed to load mod: {ex.Message}");
                Environment.Exit((int)Core.ExitCode.NamespacesIniNotFound);
                return;
            }

            // Select namespace
            string selectedNamespace;
            if (args.NamespaceOptionIndex.HasValue)
            {
                if (args.NamespaceOptionIndex.Value >= modInfo.Namespaces.Count)
                {
                    Console.Error.WriteLine($"[Error] Namespace index {args.NamespaceOptionIndex.Value} out of range (max: {modInfo.Namespaces.Count - 1})");
                    Environment.Exit((int)Core.ExitCode.NamespaceIndexOutOfRange);
                    return;
                }
                selectedNamespace = modInfo.Namespaces[args.NamespaceOptionIndex.Value].Name;
            }
            else
            {
                selectedNamespace = modInfo.Namespaces[0].Name;
            }

            // Validate game path
            if (string.IsNullOrEmpty(args.GameDir))
            {
                Console.Error.WriteLine("[Error] No game directory specified. Use --game-dir <path>");
                Environment.Exit((int)Core.ExitCode.NumberOfArgs);
                return;
            }

            string gamePath;
            try
            {
                gamePath = Core.ValidateGameDirectory(args.GameDir);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"[Error] Invalid game directory: {ex.GetType().Name}: {ex.Message}");
                Environment.Exit((int)Core.ExitCode.NumberOfArgs);
                return;
            }

            // Validate paths
            if (!Core.ValidateInstallPaths(modInfo.ModPath, gamePath))
            {
                Console.Error.WriteLine("[Error] Invalid mod or game paths");
                Environment.Exit((int)Core.ExitCode.NumberOfArgs);
                return;
            }

            // Check which operation to perform
            int numActions = (args.Install ? 1 : 0) + (args.Uninstall ? 1 : 0) + (args.Validate ? 1 : 0);
            if (numActions > 1)
            {
                Console.Error.WriteLine("[Error] Cannot run more than one of [--install, --uninstall, --validate]");
                Environment.Exit((int)Core.ExitCode.NumberOfArgs);
                return;
            }
            if (numActions == 0)
            {
                Console.Error.WriteLine("[Error] Must specify one of [--install, --uninstall, --validate] for CLI mode");
                Environment.Exit((int)Core.ExitCode.NumberOfArgs);
                return;
            }

            // Execute the requested operation
            try
            {
                if (args.Install)
                {
                    Console.WriteLine($"[Info] Installing mod from {modInfo.ModPath} to {gamePath}");
                    var cancellationToken = new CancellationToken();
                    Core.InstallResult result = Core.InstallMod(
                        modInfo.ModPath,
                        gamePath,
                        modInfo.Namespaces,
                        selectedNamespace,
                        logger,
                        cancellationToken);

                    Console.WriteLine($"[Info] Install completed: {result.NumErrors} errors, {result.NumWarnings} warnings, {result.NumPatches} patches");
                    Console.WriteLine($"[Info] Install time: {Core.FormatInstallTime(result.InstallTime)}");

                    if (result.NumErrors > 0)
                    {
                        Environment.Exit((int)Core.ExitCode.InstallCompletedWithErrors);
                    }
                    Environment.Exit((int)Core.ExitCode.Success);
                }
                else if (args.Uninstall)
                {
                    Console.WriteLine($"[Info] Uninstalling mod from {gamePath}");
                    bool fullyRan = Core.UninstallMod(modInfo.ModPath, gamePath, logger);
                    if (fullyRan)
                    {
                        Console.WriteLine("[Info] Uninstall completed successfully");
                    }
                    else
                    {
                        Console.WriteLine("[Warning] Uninstall completed with warnings");
                    }
                    Environment.Exit((int)Core.ExitCode.Success);
                }
                else if (args.Validate)
                {
                    Console.WriteLine("[Info] Validating mod configuration");
                    Core.ValidateConfig(modInfo.ModPath, modInfo.Namespaces, selectedNamespace, logger);
                    Console.WriteLine("[Info] Validation completed successfully");
                    Environment.Exit((int)Core.ExitCode.Success);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Error] {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                Environment.Exit((int)Core.ExitCode.ExceptionDuringInstall);
            }
        }

        private class CommandLineArgs
        {
            [CanBeNull]
            public string GameDir { get; set; }
            [CanBeNull]
            public string TslPatchData { get; set; }
            public int? NamespaceOptionIndex { get; set; }
            public bool Console { get; set; }
            public bool Uninstall { get; set; }
            public bool Install { get; set; }
            public bool Validate { get; set; }
            public bool Help { get; set; }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                //.WithInterFont()
                .LogToTrace();
    }
}
