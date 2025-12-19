using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Extract;
using KOTORModSync.Installation;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Diff
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:34-63
    // Original: @dataclass class TLKModificationWithSource:
    /// <summary>
    /// Wrapper that associates a TLK modification with its source path.
    /// </summary>
    public class TLKModificationWithSource
    {
        public Mods.TLK.ModificationsTLK Modification { get; set; }
        public object SourcePath { get; set; } // Installation or Path
        public int SourceIndex { get; set; }
        public bool IsInstallation { get; set; }
        public Dictionary<int, int> StrrefMappings { get; set; } = new Dictionary<int, int>(); // old_strref -> token_id
        public string SourceFilepath { get; set; } // Base installation TLK path for reference finding
        public Installation.Installation SourceInstallation { get; set; } // Base installation for reference finding

        public TLKModificationWithSource(
            Mods.TLK.ModificationsTLK modification,
            object sourcePath,
            int sourceIndex,
            bool isInstallation = false)
        {
            Modification = modification;
            SourcePath = sourcePath;
            SourceIndex = sourceIndex;
            IsInstallation = isInstallation;
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:65-75
    // Original: @dataclass class ResolvedResource:
    /// <summary>
    /// A resource resolved through the game's priority order.
    /// </summary>
    public class ResolvedResource
    {
        public ResourceIdentifier Identifier { get; set; }
        public byte[] Data { get; set; }
        public string SourceLocation { get; set; } // Human-readable description
        public string LocationType { get; set; } // Type of location (Override, Modules, Chitin, etc.)
        public string Filepath { get; set; } // Full path to the file
        public Dictionary<string, List<string>> AllLocations { get; set; } // All locations where resource was found

        public ResolvedResource(
            ResourceIdentifier identifier,
            byte[] data,
            string sourceLocation,
            string locationType,
            string filepath,
            Dictionary<string, List<string>> allLocations = null)
        {
            Identifier = identifier;
            Data = data;
            SourceLocation = sourceLocation;
            LocationType = locationType;
            Filepath = filepath;
            AllLocations = allLocations ?? new Dictionary<string, List<string>>();
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:77-83
    // Original: def get_location_display_name(location_type: str | None) -> str:
    /// <summary>
    /// Get human-readable name for a location type.
    /// </summary>
    public static class Resolution
    {
        public static string GetLocationDisplayName(string locationType)
        {
            if (string.IsNullOrEmpty(locationType))
            {
                return "Not Found";
            }
            return locationType;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:431-446
        // Original: def collect_all_resource_identifiers(installation: Installation) -> set[ResourceIdentifier]:
        /// <summary>
        /// Collect all unique resource identifiers from an installation.
        /// </summary>
        public static HashSet<ResourceIdentifier> CollectAllResourceIdentifiers(Installation.Installation installation)
        {
            HashSet<ResourceIdentifier> identifiers = new HashSet<ResourceIdentifier>();
            foreach (FileResource fileResource in GetAllResources(installation))
            {
                identifiers.Add(fileResource.Identifier);
            }
            return identifiers;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:449-465
        // Original: def build_resource_index(installation: Installation) -> dict[ResourceIdentifier, list[FileResource]]:
        /// <summary>
        /// Build an index mapping ResourceIdentifier to all FileResource instances.
        /// This dramatically improves performance by avoiding O(n) scans for each resource.
        /// </summary>
        public static Dictionary<ResourceIdentifier, List<FileResource>> BuildResourceIndex(Installation.Installation installation)
        {
            Dictionary<ResourceIdentifier, List<FileResource>> index = new Dictionary<ResourceIdentifier, List<FileResource>>();
            foreach (FileResource fileResource in GetAllResources(installation))
            {
                ResourceIdentifier ident = fileResource.Identifier;
                if (!index.ContainsKey(ident))
                {
                    index[ident] = new List<FileResource>();
                }
                index[ident].Add(fileResource);
            }
            return index;
        }

        /// <summary>
        /// Get all resources from an installation (chitin, core, override, modules).
        /// </summary>
        private static IEnumerable<FileResource> GetAllResources(Installation.Installation installation)
        {
            var allResources = new List<FileResource>();
            allResources.AddRange(installation.ChitinResources());
            allResources.AddRange(installation.CoreResources());
            allResources.AddRange(installation.OverrideResources());
            // TODO: Add module resources when available
            return allResources;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:496-536
        // Original: def determine_tslpatcher_destination(location_a: str | None, location_b: str | None, filepath_b: Path | None) -> str:
        /// <summary>
        /// Determine the appropriate TSLPatcher destination based on source locations.
        /// </summary>
        public static string DetermineTslpatcherDestination(string locationA, string locationB, string filepathB)
        {
            // If resource is in Override, destination is Override
            if (locationB == "Override folder")
            {
                return "Override";
            }

            // If resource is in a module
            if (!string.IsNullOrEmpty(locationB) && locationB.Contains("Modules") && !string.IsNullOrEmpty(filepathB))
            {
                string filepathStr = filepathB.ToLowerInvariant();

                // Check if it's in a .mod file
                if (filepathStr.Contains(".mod") || filepathStr.Contains(".erf"))
                {
                    // Extract module filename
                    string moduleName = System.IO.Path.GetFileName(filepathB);
                    return $"modules\\{moduleName}";
                }

                // It's in a .rim - need to redirect to corresponding .mod
                if (filepathStr.Contains(".rim"))
                {
                    // Extract module root using Module.NameToRoot
                    string moduleRoot = Module.NameToRoot(filepathB);
                    return $"modules\\{moduleRoot}.mod";
                }
            }

            // Default to Override for safety
            return "Override";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py:86-309
        // Original: def resolve_resource_in_installation(...) -> ResolvedResource:
        /// <summary>
        /// Resolve a resource in an installation using game priority order.
        /// Resolution order (ONLY applies to Override/Modules/Chitin):
        /// 1. Override folder (highest priority)
        /// 2. Modules (.mod files)
        /// 3. Modules (.rim/_s.rim/_dlg.erf files - composite loading)
        /// 4. Chitin BIFs (lowest priority)
        /// </summary>
        public static ResolvedResource ResolveResourceInInstallation(
            Installation.Installation installation,
            ResourceIdentifier identifier,
            Action<string> logFunc = null,
            bool verbose = true,
            Dictionary<ResourceIdentifier, List<FileResource>> resourceIndex = null)
        {
            if (logFunc == null)
            {
                logFunc = _ => { };
            }

            // Find all instances of this resource across the installation
            List<string> overrideFiles = new List<string>();
            List<string> moduleModFiles = new List<string>();
            List<string> moduleRimFiles = new List<string>();
            List<string> chitinFiles = new List<string>();

            // Store FileResource instances for data retrieval
            Dictionary<string, FileResource> resourceInstances = new Dictionary<string, FileResource>();

            try
            {
                // Use index if provided (O(1) lookup), otherwise iterate (O(n) scan)
                List<FileResource> fileResources;
                if (resourceIndex != null && resourceIndex.TryGetValue(identifier, out List<FileResource> indexedResources))
                {
                    fileResources = indexedResources;
                }
                else
                {
                    // Fallback to iteration if no index provided
                    fileResources = GetAllResources(installation).Where(fr => fr.Identifier.Equals(identifier)).ToList();
                }

                // Categorize all instances by location
                string installRoot = installation.Path;

                // Group module files by their basename for proper composite handling
                Dictionary<string, List<Tuple<string, FileResource>>> moduleGroups = new Dictionary<string, List<Tuple<string, FileResource>>>();

                foreach (FileResource fileResource in fileResources)
                {
                    string filepath = fileResource.FilePath;
                    string[] pathParts = filepath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

                    // Store for data retrieval later
                    resourceInstances[filepath] = fileResource;

                    // Categorize by location (ONLY resolution-order locations)
                    bool isOverride = pathParts.Any(p => p.Equals("override", StringComparison.OrdinalIgnoreCase));
                    bool isModules = pathParts.Any(p => p.Equals("modules", StringComparison.OrdinalIgnoreCase));
                    bool isData = pathParts.Any(p => p.Equals("data", StringComparison.OrdinalIgnoreCase));
                    bool isBif = filepath.EndsWith(".bif", StringComparison.OrdinalIgnoreCase);

                    if (isOverride)
                    {
                        overrideFiles.Add(filepath);
                    }
                    else if (isModules)
                    {
                        // Group by module basename to handle composite loading correctly
                        try
                        {
                            string moduleRoot = Module.NameToRoot(filepath);
                            if (!moduleGroups.ContainsKey(moduleRoot))
                            {
                                moduleGroups[moduleRoot] = new List<Tuple<string, FileResource>>();
                            }
                            moduleGroups[moduleRoot].Add(Tuple.Create(filepath, fileResource));
                        }
                        catch (Exception e)
                        {
                            logFunc($"Warning: Could not determine module root for {filepath}: {e}");
                            // Fallback: add to rim files without grouping
                            if (filepath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                            {
                                moduleModFiles.Add(filepath);
                            }
                            else
                            {
                                moduleRimFiles.Add(filepath);
                            }
                        }
                    }
                    else if (isData || isBif)
                    {
                        chitinFiles.Add(filepath);
                    }
                    else if (Path.GetDirectoryName(filepath) == installRoot || Path.GetDirectoryName(filepath) == null)
                    {
                        // Files directly in installation root (like dialog.tlk, chitin.key, etc.)
                        // Treat as Override priority since they're loose files at root level
                        overrideFiles.Add(filepath);
                    }
                }

                // Within each module basename group, apply composite loading priority
                // Priority within a group: .mod > .rim > _s.rim > _dlg.erf
                int GetCompositePriority(string filepath)
                {
                    string nameLower = Path.GetFileName(filepath).ToLowerInvariant();
                    if (nameLower.EndsWith(".mod"))
                    {
                        return 0; // Highest priority
                    }
                    if (nameLower.EndsWith(".rim") && !nameLower.EndsWith("_s.rim"))
                    {
                        return 1;
                    }
                    if (nameLower.EndsWith("_s.rim"))
                    {
                        return 2;
                    }
                    if (nameLower.EndsWith("_dlg.erf"))
                    {
                        return 3;
                    }
                    return 4; // Other files
                }

                // Process each module group and pick the winner
                foreach (KeyValuePair<string, List<Tuple<string, FileResource>>> moduleGroup in moduleGroups)
                {
                    if (moduleGroup.Value.Count == 0)
                    {
                        logFunc($"Warning: Empty module group for basename {moduleGroup.Key}");
                        continue;
                    }

                    // Sort by composite priority and pick the winner
                    List<Tuple<string, FileResource>> sortedFiles = moduleGroup.Value.OrderBy(x => GetCompositePriority(x.Item1)).ToList();
                    string winnerPath = sortedFiles[0].Item1;

                    // Add winner to appropriate category
                    if (winnerPath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                    {
                        moduleModFiles.Add(winnerPath);
                        continue;
                    }
                    moduleRimFiles.Add(winnerPath);
                }

                // Apply resolution order: Override > .mod > .rim > Chitin
                string chosenFilepath = null;
                string locationType = null;

                if (overrideFiles.Count > 0)
                {
                    chosenFilepath = overrideFiles[0];
                    // Check if it's actually in Override folder or root
                    string[] pathParts = chosenFilepath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                    if (pathParts.Any(p => p.Equals("override", StringComparison.OrdinalIgnoreCase)))
                    {
                        locationType = "Override folder";
                    }
                    else
                    {
                        locationType = "Installation root";
                    }
                }
                else if (moduleModFiles.Count > 0)
                {
                    chosenFilepath = moduleModFiles[0];
                    locationType = "Modules (.mod)";
                }
                else if (moduleRimFiles.Count > 0)
                {
                    // Use first .rim file found (composite loading handled elsewhere)
                    chosenFilepath = moduleRimFiles[0];
                    locationType = "Modules (.rim)";
                }
                else if (chitinFiles.Count > 0)
                {
                    chosenFilepath = chitinFiles[0];
                    locationType = "Chitin BIFs";
                }

                if (string.IsNullOrEmpty(chosenFilepath))
                {
                    return new ResolvedResource(
                        identifier,
                        null,
                        "Not found in installation",
                        null,
                        null,
                        null);
                }

                // Read the data from the chosen location (O(1) lookup with stored instances)
                byte[] data = null;
                if (resourceInstances.TryGetValue(chosenFilepath, out FileResource fileResourceInstance))
                {
                    data = fileResourceInstance.Data();
                }

                if (data == null)
                {
                    return new ResolvedResource(
                        identifier,
                        null,
                        $"Found but couldn't read: {chosenFilepath}",
                        locationType,
                        chosenFilepath,
                        null);
                }

                // Create human-readable source description
                string sourceDesc;
                try
                {
                    string relPath = Path.GetRelativePath(installRoot, chosenFilepath);
                    sourceDesc = $"{locationType}: {relPath}";
                }
                catch
                {
                    sourceDesc = $"{locationType}: {chosenFilepath}";
                }

                // Store all found locations for combined logging
                Dictionary<string, List<string>> allLocs = new Dictionary<string, List<string>>
                {
                    { "Override folder", overrideFiles },
                    { "Modules (.mod)", moduleModFiles },
                    { "Modules (.rim/_s.rim/._dlg.erf)", moduleRimFiles },
                    { "Chitin BIFs", chitinFiles }
                };

                return new ResolvedResource(
                    identifier,
                    data,
                    sourceDesc,
                    locationType,
                    chosenFilepath,
                    allLocs);
            }
            catch (Exception e)
            {
                logFunc($"[Error] Failed to resolve {identifier}: {e.GetType().Name}: {e}");
                return new ResolvedResource(
                    identifier,
                    null,
                    $"Error: {e.GetType().Name}: {e}",
                    null,
                    null,
                    null);
            }
        }
    }
}
