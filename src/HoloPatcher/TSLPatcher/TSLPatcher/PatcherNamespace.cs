namespace KOTORModSync.Namespaces
{

    /// <summary>
    /// Represents a namespace entry from namespaces.ini
    /// </summary>
    public class PatcherNamespace
    {
        public const string DefaultIniFilename = "changes.ini";
        public const string DefaultInfoFilename = "info.rtf";

        public string NamespaceId { get; set; } = string.Empty;
        public string IniFilename { get; set; } = DefaultIniFilename;
        public string InfoFilename { get; set; } = DefaultInfoFilename;
        public string DataFolderPath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public PatcherNamespace()
        {
        }

        public PatcherNamespace(string iniFilename, string infoFilename)
        {
            IniFilename = iniFilename;
            InfoFilename = infoFilename;
        }

        public string ChangesFilePath()
        {
            return System.IO.Path.Combine(DataFolderPath, IniFilename);
        }

        public string RtfFilePath()
        {
            return System.IO.Path.Combine(DataFolderPath, InfoFilename);
        }

        public static PatcherNamespace FromDefault()
        {
            return new PatcherNamespace
            {
                IniFilename = DefaultIniFilename,
                InfoFilename = DefaultInfoFilename
            };
        }

        public override string ToString() => $"{NamespaceId}: {Name}";
    }
}

