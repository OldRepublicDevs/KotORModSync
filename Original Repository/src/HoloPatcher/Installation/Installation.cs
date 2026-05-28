using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Common;
using KOTORModSync.Extract;
using KOTORModSync.Extract.Capsule;
using KOTORModSync.Formats.Capsule;
using KOTORModSync.Resource;
using JetBrains.Annotations;

namespace KOTORModSync.Installation
{

    /// <summary>
    /// Represents a KOTOR/TSL game installation and provides centralized resource access.
    /// Handles resource loading from override, modules, chitin, texture packs, and stream directories.
    /// </summary>
    public class Installation
    {
        private readonly string _path;
        private readonly Game _game;
        private readonly InstallationResourceManager _resourceManager;

        // Hardcoded module names for better UI display
        private static readonly Dictionary<string, string> HardcodedModuleNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["STUNT_00"] = "Ebon Hawk - Cutscene (Vision Sequences)",
            ["STUNT_03A"] = "Leviathan - Cutscene (Destroy Taris)",
            ["STUNT_06"] = "Leviathan - Cutscene (Resume Bombardment)",
            ["STUNT_07"] = "Ebon Hawk - Cutscene (Escape Taris)",
            ["STUNT_12"] = "Leviathan - Cutscene (Calo Nord)",
            ["STUNT_14"] = "Leviathan - Cutscene (Darth Bandon)",
            ["STUNT_16"] = "Ebon Hawk - Cutscene (Leviathan Capture)",
            ["STUNT_18"] = "Unknown World - Cutscene (Bastila Torture)",
            ["STUNT_19"] = "Star Forge - Cutscene (Jawless Malak)",
            ["STUNT_31B"] = "Unknown World - Cutscene (Revan Reveal)",
            ["STUNT_34"] = "Ebon Hawk - Cutscene (Star Forge Arrival)",
            ["STUNT_35"] = "Ebon Hawk - Cutscene (Lehon Crash)",
            ["STUNT_42"] = "Ebon Hawk - Cutscene (LS Dodonna Call)",
            ["STUNT_44"] = "Ebon Hawk - Cutscene (DS Dodonna Call)",
            ["STUNT_50A"] = "Dodonna Flagship - Cutscene (Break In Formation)",
            ["STUNT_51A"] = "Dodonna Flagship - Cutscene (Bastila Against Us)",
            ["STUNT_54A"] = "Dodonna Flagship - Cutscene (Pull Back)",
            ["STUNT_55A"] = "Unknown World - Cutscene (DS Ending)",
            ["STUNT_56A"] = "Dodona Flagship - Cutscene (Star Forge Destroyed)",
            ["STUNT_57"] = "Unknown World - Cutscene (LS Ending)",
            ["001EBO"] = "Ebon Hawk - Interior (Prologue)",
            ["004EBO"] = "Ebon Hawk - Interior (Red Eclipse)",
            ["005EBO"] = "Ebon Hawk - Interior (Escaping Peragus)",
            ["006EBO"] = "Ebon Hawk - Cutscene (After Rebuilt Enclave)",
            ["007EBO"] = "Ebon Hawk - Cutscene (After Goto's Yatch)",
            ["154HAR"] = "Harbinger - Cutscene (Sion Introduction)",
            ["205TEL"] = "Citadel Station - Cutscene (Carth Discussion)",
            ["352NAR"] = "Nar Shaddaa - Cutscene (Goto Introduction)",
            ["853NIH"] = "Ravager - Cutscene (Nihilus Introduction)",
            ["856NIH"] = "Ravager - Cutscene (Sion vs. Nihilus)"
        };

        public string Path => _path;
        public Game Game => _game;
        public InstallationResourceManager Resources => _resourceManager;

        public Installation(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException($"Installation path does not exist: {path}");

            _path = path;
            _game = DetermineGame(path)
                ?? throw new InvalidOperationException($"Could not determine game type for path: {path}");

            _resourceManager = new InstallationResourceManager(path);
        }

        /// <summary>
        /// Determines the game type from an installation path.
        /// </summary>
        public static Game? DetermineGame(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
                return null;

            // Check for swkotor2.exe (TSL)
            string tsl64Exe = System.IO.Path.Combine(installPath, "swkotor2.exe");
            string tsl32Exe = System.IO.Path.Combine(installPath, "SWKOTOR2.EXE");

            if (File.Exists(tsl64Exe) || File.Exists(tsl32Exe))
                return Game.TSL;

            // Check for swkotor.exe (K1)
            string k164Exe = System.IO.Path.Combine(installPath, "swkotor.exe");
            string k132Exe = System.IO.Path.Combine(installPath, "SWKOTOR.EXE");

            if (File.Exists(k164Exe) || File.Exists(k132Exe))
                return Game.K1;

            // Check for chitin.key as fallback indicator
            string chitinPath = System.IO.Path.Combine(installPath, "chitin.key");
            if (File.Exists(chitinPath))
            {
                // Try to guess based on module files
                string modulesPath = GetModulesPath(installPath);
                if (Directory.Exists(modulesPath))
                {
                    // TSL has modules like 001EBO, 004EBO
                    // K1 has modules like danm13, danm14
                    var modules = Directory.GetFiles(modulesPath, "*.rim")
                        .Concat(Directory.GetFiles(modulesPath, "*.mod"))
                        .Select(System.IO.Path.GetFileNameWithoutExtension)
                        .ToList();

                    if (modules.Any(m => m?.StartsWith("001", StringComparison.OrdinalIgnoreCase) == true ||
                                         m?.StartsWith("004", StringComparison.OrdinalIgnoreCase) == true))
                        return Game.TSL;

                    if (modules.Any(m => m?.StartsWith("dan", StringComparison.OrdinalIgnoreCase) == true ||
                                         m?.StartsWith("tar", StringComparison.OrdinalIgnoreCase) == true))
                        return Game.K1;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the override directory path for an installation.
        /// </summary>
        public static string GetOverridePath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "Override");
        }

        /// <summary>
        /// Gets the modules directory path for an installation.
        /// </summary>
        public static string GetModulesPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "modules");
        }

        /// <summary>
        /// Gets the Packages directory path for an installation ( games).
        /// </summary>
        public static string GetPackagesPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "Packages");
        }

        /// <summary>
        /// Gets the data directory path for an installation (contains BIF files).
        /// </summary>
        public static string GetDataPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "data");
        }

        /// <summary>
        /// Gets the chitin.key file path for an installation.
        /// </summary>
        public static string GetChitinPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "chitin.key");
        }

        /// <summary>
        /// Gets the texture packs directory path for an installation.
        /// </summary>
        public static string GetTexturePacksPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "TexturePacks");
        }

        /// <summary>
        /// Gets the StreamMusic directory path for an installation.
        /// </summary>
        public static string GetStreamMusicPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "StreamMusic");
        }

        /// <summary>
        /// Gets the StreamSounds directory path for an installation.
        /// </summary>
        public static string GetStreamSoundsPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "StreamSounds");
        }

        /// <summary>
        /// Gets the StreamVoice directory path for a TSL installation.
        /// </summary>
        public static string GetStreamVoicePath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "StreamVoice");
        }

        /// <summary>
        /// Gets the StreamWaves directory path for a K1 installation.
        /// </summary>
        public static string GetStreamWavesPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "StreamWaves");
        }

        /// <summary>
        /// Gets the lips directory path for an installation.
        /// </summary>
        public static string GetLipsPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "lips");
        }

        /// <summary>
        /// Gets the rims directory path for an installation (TSL only).
        /// </summary>
        public static string GetRimsPath(string installPath)
        {
            return System.IO.Path.Combine(installPath, "rims");
        }

        /// <summary>
        /// Extracts the module root name from a module filename.
        /// Example: "danm13.rim" -> "danm13", "danm13_s.rim" -> "danm13"
        /// </summary>
        public static string GetModuleRoot(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
                return string.Empty;

            // Remove extension
            string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(moduleName);

            // Remove suffix like _s, _dlg, etc.
            int underscoreIndex = nameWithoutExt.IndexOf('_');
            if (underscoreIndex > 0)
            {
                return nameWithoutExt.Substring(0, underscoreIndex);
            }

            return nameWithoutExt;
        }

        /// <summary>
        /// Gets the display name for a module, using hardcoded names when available.
        /// </summary>
        public static string GetModuleDisplayName(string moduleRoot)
        {
            // Can be null if displayName not found
            if (HardcodedModuleNames.TryGetValue(moduleRoot, out string displayName))
            {
                return displayName;
            }

            return moduleRoot;
        }

        /// <summary>
        /// Looks up a single resource by name and type.
        /// </summary>
        [CanBeNull]
        public ResourceResult Resource(
            string resname,
            ResourceType restype,
            [CanBeNull] SearchLocation[] searchOrder = null,
            [CanBeNull] string moduleRoot = null)
        {
            return _resourceManager.LookupResource(resname, restype, searchOrder, moduleRoot);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1807-1843
        // Original: def texture(self, resname: str, order: Sequence[SearchLocation] | None = None, *, capsules: Sequence[Capsule] | None = None, folders: list[Path] | None = None, logger: Callable[[str], None] | None = None) -> TPC | None:
        /// <summary>
        /// Returns a TPC object loaded from a resource with the specified name.
        /// </summary>
        [CanBeNull]
        public Formats.TPC.TPC Texture(
            string resname,
            [CanBeNull] SearchLocation[] searchOrder = null)
        {
            if (string.IsNullOrWhiteSpace(resname))
            {
                return null;
            }

            // Default search order for textures
            if (searchOrder == null || searchOrder.Length == 0)
            {
                searchOrder = new[]
                {
                    SearchLocation.CUSTOM_FOLDERS,
                    SearchLocation.OVERRIDE,
                    SearchLocation.CUSTOM_MODULES,
                    SearchLocation.TEXTURES_TPA,
                    SearchLocation.CHITIN
                };
            }

            // Try TPC first, then TGA
            ResourceResult result = Resource(resname, ResourceType.TPC, searchOrder);
            if (result == null)
            {
                result = Resource(resname, ResourceType.TGA, searchOrder);
            }

            if (result == null || result.Data == null)
            {
                return null;
            }

            try
            {
                return Formats.TPC.TPCAuto.ReadTpc(result.Data);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Locates all instances of a resource across the installation.
        /// </summary>
        public List<LocationResult> Locate(
            string resname,
            ResourceType restype,
            [CanBeNull] SearchLocation[] searchOrder = null,
            [CanBeNull] string moduleRoot = null)
        {
            return _resourceManager.LocateResource(resname, restype, searchOrder, moduleRoot);
        }

        /// <summary>
        /// Gets all module roots available in the installation.
        /// </summary>
        public List<string> GetModuleRoots()
        {
            string modulesPath = GetModulesPath(_path);
            if (!Directory.Exists(modulesPath))
            {
                return new List<string>();
            }

            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in Directory.GetFiles(modulesPath))
            {
                string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".rim" || ext == ".mod" || ext == ".erf")
                {
                    string root = GetModuleRoot(System.IO.Path.GetFileName(file));
                    if (!string.IsNullOrEmpty(root))
                    {
                        roots.Add(root);
                    }
                }
            }

            return roots.OrderBy(r => r).ToList();
        }

        /// <summary>
        /// Gets all module files for a specific module root.
        /// </summary>
        public List<string> GetModuleFiles(string moduleRoot)
        {
            string modulesPath = GetModulesPath(_path);
            if (!Directory.Exists(modulesPath))
            {
                return new List<string>();
            }

            var files = new List<string>();

            foreach (string file in Directory.GetFiles(modulesPath))
            {
                string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (ext == ".rim" || ext == ".mod" || ext == ".erf")
                {
                    string root = GetModuleRoot(System.IO.Path.GetFileName(file));
                    if (root.Equals(moduleRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(file);
                    }
                }
            }

            return files;
        }

        /// <summary>
        /// Clears all cached resources, forcing a reload on next access.
        /// </summary>
        public void ClearCache()
        {
            _resourceManager.ClearCache();
        }

        /// <summary>
        /// Reloads a specific module's resources.
        /// </summary>
        public void ReloadModule(string moduleName)
        {
            _resourceManager.ReloadModule(moduleName);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:723-730
        // Original: def module_path(self) -> Path:
        /// <summary>
        /// Returns the path to modules folder of the Installation. This method maintains the case of the foldername.
        /// </summary>
        public string ModulePath()
        {
            return GetModulesPath(_path);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:732-739
        // Original: def override_path(self) -> Path:
        /// <summary>
        /// Returns the path to override folder of the Installation. This method maintains the case of the foldername.
        /// </summary>
        public string OverridePath()
        {
            return GetOverridePath(_path);
        }

        /// <summary>
        /// Returns the path to Packages folder of the Installation ( series).
        /// This method maintains the case of the foldername.
        /// </summary>
        public string PackagePath()
        {
            return GetPackagesPath(_path);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:935-942
        // Original: def chitin_resources(self) -> list[FileResource]:
        /// <summary>
        /// Returns a shallow copy of the list of FileResources stored in the Chitin linked to the Installation.
        /// </summary>
        public List<FileResource> ChitinResources()
        {
            return _resourceManager.GetChitinResources();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:944-946
        // Original: def core_resources(self) -> list[FileResource]:
        /// <summary>
        /// Similar to chitin_resources, but also return the resources in patch.erf if exists and the installation is Game.K1.
        /// </summary>
        public List<FileResource> CoreResources()
        {
            var results = new List<FileResource>();
            results.AddRange(ChitinResources());
            results.AddRange(_resourceManager.GetPatchErfResources(_game));
            return results;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1030-1039
        // Original: def override_list(self) -> list[str]:
        /// <summary>
        /// Returns the list of subdirectories located in override folder linked to the Installation.
        /// </summary>
        public List<string> OverrideList()
        {
            string overridePath = GetOverridePath(_path);
            if (!Directory.Exists(overridePath))
            {
                return new List<string>();
            }

            var subdirs = new List<string>();
            foreach (string dir in Directory.GetDirectories(overridePath))
            {
                subdirs.Add(System.IO.Path.GetFileName(dir));
            }

            return subdirs;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1041-1059
        // Original: def override_resources(self, directory: str | None = None) -> list[FileResource]:
        /// <summary>
        /// Returns a list of FileResources stored in the specified subdirectory located in the 'override' folder linked to the Installation.
        /// </summary>
        public List<FileResource> OverrideResources(string directory = null)
        {
            string overridePath = GetOverridePath(_path);
            if (!Directory.Exists(overridePath))
            {
                return new List<FileResource>();
            }

            var results = new List<FileResource>();
            string searchPath = string.IsNullOrEmpty(directory) ? overridePath : System.IO.Path.Combine(overridePath, directory);

            if (!Directory.Exists(searchPath))
            {
                return results;
            }

            // Recursively search for all resource files
            foreach (string file in Directory.GetFiles(searchPath, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    ResourceIdentifier identifier = ResourceIdentifier.FromPath(file);
                    if (identifier.ResType != ResourceType.INVALID && !identifier.ResType.IsInvalid)
                    {
                        var fileInfo = new FileInfo(file);
                        results.Add(new FileResource(
                            identifier.ResName,
                            identifier.ResType,
                            (int)fileInfo.Length,
                            0,
                            file
                        ));
                    }
                }
                catch
                {
                    // Skip files that can't be parsed as resources
                }
            }

            return results;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1366-1560
        // Original: def locations(self, queries: list[ResourceIdentifier], order: list[SearchLocation] | None = None, *, capsules: Sequence[LazyCapsule] | None = None, folders: list[Path] | None = None, module_root: str | None = None, logger: Callable[[str], None] | None = None) -> dict[ResourceIdentifier, list[LocationResult]]:
        /// <summary>
        /// Returns a dictionary mapping the items provided in the queries argument to a list of locations for that respective resource.
        /// </summary>
        public Dictionary<ResourceIdentifier, List<LocationResult>> Locations(
            List<ResourceIdentifier> queries,
            SearchLocation[] order = null,
            List<LazyCapsule> capsules = null,
            List<string> folders = null,
            string moduleRoot = null)
        {
            if (queries == null || queries.Count == 0)
            {
                return new Dictionary<ResourceIdentifier, List<LocationResult>>();
            }

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/installation.py:1297-1360
            // Original: if order is an empty list, return empty results. If None, use default order.
            // If order is an empty array (not null), return empty results immediately
            if (order != null && order.Length == 0)
            {
                var emptyResults = new Dictionary<ResourceIdentifier, List<LocationResult>>();
                foreach (ResourceIdentifier query in queries)
                {
                    emptyResults[query] = new List<LocationResult>();
                }
                return emptyResults;
            }

            // Default search order if not specified (null)
            if (order is null)
            {
                order = new[]
                {
                    SearchLocation.CUSTOM_FOLDERS,
                    SearchLocation.OVERRIDE,
                    SearchLocation.CUSTOM_MODULES,
                    SearchLocation.MODULES,
                    SearchLocation.CHITIN,
                };
            }

            capsules = capsules ?? new List<LazyCapsule>();
            folders = folders ?? new List<string>();

            var locations = new Dictionary<ResourceIdentifier, List<LocationResult>>();
            foreach (ResourceIdentifier query in queries)
            {
                locations[query] = new List<LocationResult>();
            }

            // Search each location in order
            foreach (SearchLocation location in order)
            {
                foreach (ResourceIdentifier query in queries)
                {
                    List<LocationResult> found = Locate(query.ResName, query.ResType, new[] { location }, moduleRoot);
                    if (found.Count > 0)
                    {
                        locations[query].AddRange(found);
                    }
                }
            }

            // Search custom capsules
            if (capsules.Count > 0)
            {
                foreach (LazyCapsule capsule in capsules)
                {
                    foreach (ResourceIdentifier query in queries)
                    {
                        FileResource resource = capsule.GetResourceInfo(query.ResName, query.ResType);
                        if (resource != null)
                        {
                            var locationResult = new LocationResult(resource.FilePath, resource.Offset, resource.Size);
                            locationResult.SetFileResource(resource);
                            locations[query].Add(locationResult);
                        }
                    }
                }
            }

            // Search custom folders
            if (folders.Count > 0)
            {
                foreach (string folder in folders)
                {
                    if (!Directory.Exists(folder))
                    {
                        continue;
                    }

                    foreach (string file in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            ResourceIdentifier identifier = ResourceIdentifier.FromPath(file);
                            if (queries.Contains(identifier))
                            {
                                var fileInfo = new FileInfo(file);
                                var fileResource = new FileResource(
                                    identifier.ResName,
                                    identifier.ResType,
                                    (int)fileInfo.Length,
                                    0,
                                    file
                                );
                                var locationResult = new LocationResult(file, 0, (int)fileInfo.Length);
                                locationResult.SetFileResource(fileResource);
                                locations[identifier].Add(locationResult);
                            }
                        }
                        catch
                        {
                            // Skip files that can't be parsed
                        }
                    }
                }
            }

            return locations;
        }
    }
}
