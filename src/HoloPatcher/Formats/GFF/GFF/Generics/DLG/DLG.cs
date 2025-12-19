using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.DLG
{
    /// <summary>
    /// Type of computer interface for dialog.
    /// </summary>
    [PublicAPI]
    public enum DLGComputerType
    {
        Modern = 0,
        Ancient = 1
    }

    /// <summary>
    /// Type of conversation for dialog.
    /// </summary>
    [PublicAPI]
    public enum DLGConversationType
    {
        Human = 0,
        Computer = 1,
        Other = 2,
        Unknown = 3
    }

    /// <summary>
    /// Stores dialog data.
    ///
    /// DLG files are GFF-based format files that store dialog trees with entries, replies,
    /// links, and conversation metadata.
    /// </summary>
    [PublicAPI]
    public sealed class DLG
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:36
        // Original: class DLG:
        public static readonly ResourceType BinaryType = ResourceType.DLG;

        public List<DLGLink> Starters { get; set; } = new List<DLGLink>();
        public List<DLGStunt> Stunts { get; set; } = new List<DLGStunt>();

        // Dialog metadata
        public ResRef AmbientTrack { get; set; } = ResRef.FromBlank();
        public int AnimatedCut { get; set; }
        public ResRef CameraModel { get; set; } = ResRef.FromBlank();
        public DLGComputerType ComputerType { get; set; } = DLGComputerType.Modern;
        public DLGConversationType ConversationType { get; set; } = DLGConversationType.Human;
        public ResRef OnAbort { get; set; } = ResRef.FromBlank();
        public ResRef OnEnd { get; set; } = ResRef.FromBlank();
        public int WordCount { get; set; }
        public bool OldHitCheck { get; set; }
        public bool Skippable { get; set; }
        public bool UnequipItems { get; set; }
        public bool UnequipHands { get; set; }
        public string VoId { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;

        // KotOR 2
        public int AlienRaceOwner { get; set; }
        public int NextNodeId { get; set; }
        public int PostProcOwner { get; set; }
        public int RecordNoVo { get; set; }

        // Deprecated
        public int DelayEntry { get; set; }
        public int DelayReply { get; set; }

        public DLG()
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:307
        // Original: def all_entries(self, *, as_sorted: bool = False) -> list[DLGEntry]:
        public List<DLGEntry> AllEntries(bool asSorted = false)
        {
            List<DLGEntry> entries = _AllEntries();
            if (!asSorted)
            {
                return entries;
            }
            return entries.OrderBy(e => e.ListIndex == -1).ThenBy(e => e.ListIndex).ToList();
        }

        private List<DLGEntry> _AllEntries(List<DLGLink> links = null, HashSet<DLGEntry> seenEntries = null)
        {
            List<DLGEntry> entries = new List<DLGEntry>();
            links = links ?? Starters;
            seenEntries = seenEntries ?? new HashSet<DLGEntry>();

            foreach (DLGLink link in links)
            {
                DLGNode entry = link.Node;
                if (entry == null || seenEntries.Contains(entry as DLGEntry))
                {
                    continue;
                }
                if (!(entry is DLGEntry dlgEntry))
                {
                    continue;
                }
                entries.Add(dlgEntry);
                seenEntries.Add(dlgEntry);
                foreach (DLGLink replyLink in entry.Links)
                {
                    DLGNode reply = replyLink.Node;
                    if (reply != null)
                    {
                        entries.AddRange(_AllEntries(reply.Links, seenEntries));
                    }
                }
            }

            return entries;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/base.py:363
        // Original: def all_replies(self, *, as_sorted: bool = False) -> list[DLGReply]:
        public List<DLGReply> AllReplies(bool asSorted = false)
        {
            List<DLGReply> replies = _AllReplies();
            if (!asSorted)
            {
                return replies;
            }
            return replies.OrderBy(r => r.ListIndex == -1).ThenBy(r => r.ListIndex).ToList();
        }

        private List<DLGReply> _AllReplies(List<DLGLink> links = null, List<DLGReply> seenReplies = null)
        {
            List<DLGReply> replies = new List<DLGReply>();
            links = links ?? Starters.Where(l => l.Node != null).SelectMany(l => l.Node.Links).ToList();
            seenReplies = seenReplies ?? new List<DLGReply>();

            foreach (DLGLink link in links)
            {
                DLGNode reply = link.Node;
                if (seenReplies.Contains(reply as DLGReply))
                {
                    continue;
                }
                if (!(reply is DLGReply dlgReply))
                {
                    continue;
                }
                replies.Add(dlgReply);
                seenReplies.Add(dlgReply);
                foreach (DLGLink entryLink in reply.Links)
                {
                    DLGNode entry = entryLink.Node;
                    if (entry != null)
                    {
                        replies.AddRange(_AllReplies(entry.Links, seenReplies));
                    }
                }
            }

            return replies;
        }
    }
}
