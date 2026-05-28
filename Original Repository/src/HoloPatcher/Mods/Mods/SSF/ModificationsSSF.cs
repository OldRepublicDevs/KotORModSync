using System;
using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Formats.SSF;
using KOTORModSync.Logger;
using KOTORModSync.Memory;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Mods.SSF
{

    /// <summary>
    /// Represents a single SSF sound modification.
    /// 1:1 port from Python ModifySSF in pykotor/tslpatcher/mods/ssf.py
    /// </summary>
    public class ModifySSF
    {
        public SSFSound Sound { get; set; }
        public TokenUsage Stringref { get; set; }

        public ModifySSF(SSFSound sound, TokenUsage stringref)
        {
            Sound = sound;
            Stringref = stringref;
        }

        public void Apply(Formats.SSF.SSF ssf, PatcherMemory memory)
        {
            ssf.SetData(Sound, int.Parse(Stringref.Value(memory)));
        }
    }

    /// <summary>
    /// SSF modification algorithms for TSLPatcher/HoloPatcher.
    /// 
    /// This module implements SSF modification logic for applying patches from changes.ini files.
    /// Handles sound set entry modifications and memory token resolution.
    /// 
    /// References:
    /// ----------
    ///     vendor/TSLPatcher/TSLPatcher.pl - Perl SSF modification logic (likely unfinished)
    ///     vendor/Kotor.NET/Kotor.NET.Patcher/ - Incomplete C# patcher
    /// </summary>

    /// <summary>
    /// Container for SSF (sound set file) modifications.
    /// 1:1 port from Python ModificationsSSF in pykotor/tslpatcher/mods/ssf.py
    /// </summary>
    public class ModificationsSSF : PatcherModifications
    {
        public new const string DEFAULT_DESTINATION = PatcherModifications.DEFAULT_DESTINATION;
        public static string DefaultDestination => DEFAULT_DESTINATION;

        public List<ModifySSF> Modifiers { get; set; }

        public ModificationsSSF(string filename, bool replace, [CanBeNull] List<ModifySSF> modifiers = null)
            : base(filename, replace)
        {
            ReplaceFile = replace;
            Modifiers = modifiers ?? new List<ModifySSF>();
        }

        public override object PatchResource(
            byte[] source,
            PatcherMemory memory,
            PatchLogger logger,
            Game game)
        {
            var reader = new SSFBinaryReader(source);
            Formats.SSF.SSF ssf = reader.Load();
            Apply(ssf, memory, logger, game);

            var writer = new SSFBinaryWriter(ssf);
            return writer.Write();
        }

        public override void Apply(
            object mutableData,
            PatcherMemory memory,
            PatchLogger logger,
            Game game)
        {
            if (mutableData is Formats.SSF.SSF ssf)
            {
                foreach (ModifySSF modifier in Modifiers)
                {
                    modifier.Apply(ssf, memory);
                }
            }
            else
            {
                logger.AddError($"Expected SSF object for ModificationsSSF, but got {mutableData.GetType().Name}");
            }
        }
    }
}