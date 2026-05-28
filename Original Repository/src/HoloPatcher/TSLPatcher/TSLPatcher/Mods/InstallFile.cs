using System;
using KOTORModSync;
using KOTORModSync.Logger;
using KOTORModSync.Memory;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Mods
{

    /// <summary>
    /// Represents a file to be installed/copied during patching.
    /// 1:1 port from Python InstallFile in pykotor/tslpatcher/mods/install.py
    /// </summary>
    public class InstallFile : PatcherModifications
    {
        public InstallFile(
            string filename,
            bool? replaceExisting = null,
            [CanBeNull] string destination = null)
            : base(filename, replaceExisting, destination)
        {
            Action = "Copy ";
            SkipIfNotReplace = true;
        }

        /// <summary>
        /// HACK(th3w1zard1): organize this into PatcherModifications class later, this is only used for nwscript.nss currently.
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Destination, SaveAs, ReplaceFile);
        }

        public override object PatchResource(
            byte[] source,
            PatcherMemory memory,
            PatchLogger logger,
            Game game)
        {
            Apply(source, memory, logger, game);
            // Python: with BinaryReader.from_auto(source) as reader: return reader.read_all()
            return source;
        }

        public override void Apply(
            object mutableData,
            PatcherMemory memory,
            PatchLogger logger,
            Game game)
        {
            // InstallFile doesn't modify the file, just copies it
        }
    }
}