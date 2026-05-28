using System;
using System.Collections.Generic;
using System.Linq;
using KOTORModSync;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.CNV
{
    /// <summary>
    /// Stores conversation data for Eclipse Engine games (Dragon Age, ).
    /// </summary>
    /// <remarks>
    /// CNV files are GFF-based format files that store conversation trees with entries, replies,
    /// links, and conversation metadata. Used by Eclipse Engine games (Dragon Age Origins,
    /// Dragon Age 2,  1,  2).
    /// 
    /// CNV Format:
    /// - GFF format with "CNV " signature
    /// - Similar structure to DLG but adapted for Eclipse conversation system
    /// - Used by Eclipse Engine games (daorigins.exe, DragonAge2.exe, , )
    /// - Conversation system uses message passing and UnrealScript-based architecture
    /// 
    /// Differences from DLG:
    /// - Eclipse-specific conversation metadata
    /// - Different field names and organization
    /// - Adapted for Eclipse's conversation UI and flow
    /// </remarks>
    [PublicAPI]
    public sealed class CNV
    {
        // Matching pattern from DLG
        // Original: CNV conversation format for Eclipse Engine
        public static readonly ResourceType BinaryType = ResourceType.CNV;

        /// <summary>
        /// Starting links (entry points into the conversation tree).
        /// </summary>
        public List<CNVLink> Starters { get; set; } = new List<CNVLink>();

        /// <summary>
        /// Stunt model references (for special conversation animations).
        /// </summary>
        public List<CNVStunt> Stunts { get; set; } = new List<CNVStunt>();

        // Conversation metadata
        /// <summary>
        /// Background music track ResRef.
        /// </summary>
        public ResRef AmbientTrack { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Animated cutscene flag.
        /// </summary>
        public int AnimatedCut { get; set; }

        /// <summary>
        /// Camera model ResRef for cutscenes.
        /// </summary>
        public ResRef CameraModel { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Type of computer interface (0=Modern, 1=Ancient).
        /// </summary>
        public CNVComputerType ComputerType { get; set; } = CNVComputerType.Modern;

        /// <summary>
        /// Type of conversation (0=Human, 1=Computer, 2=Other).
        /// </summary>
        public CNVConversationType ConversationType { get; set; } = CNVConversationType.Human;

        /// <summary>
        /// Script to run when conversation ends normally.
        /// </summary>
        public ResRef OnEnd { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Script to run when conversation is aborted.
        /// </summary>
        public ResRef OnAbort { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Total word count (unused).
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// Legacy hit check flag (unused).
        /// </summary>
        public bool OldHitCheck { get; set; }

        /// <summary>
        /// Whether conversation can be skipped.
        /// </summary>
        public bool Skippable { get; set; }

        /// <summary>
        /// Whether to unequip all items during conversation.
        /// </summary>
        public bool UnequipItems { get; set; }

        /// <summary>
        /// Whether to unequip hand items during conversation.
        /// </summary>
        public bool UnequipHands { get; set; }

        /// <summary>
        /// Voice-over identifier string.
        /// </summary>
        public string VoId { get; set; } = string.Empty;

        /// <summary>
        /// Comment/note for the conversation.
        /// </summary>
        public string Comment { get; set; } = string.Empty;

        // Eclipse-specific fields
        /// <summary>
        /// Next available node ID for new nodes (Eclipse-specific).
        /// </summary>
        public int NextNodeId { get; set; }

        // Deprecated fields
        /// <summary>
        /// Delay before conversation starts (deprecated, not used by engine).
        /// </summary>
        public int DelayEntry { get; set; }

        /// <summary>
        /// Delay before player reply options appear (deprecated, not used by engine).
        /// </summary>
        public int DelayReply { get; set; }

        public CNV()
        {
        }

        // Matching pattern from DLG.AllEntries
        /// <summary>
        /// Gets all entry nodes in the conversation tree.
        /// </summary>
        /// <param name="asSorted">Whether to return entries sorted by list index.</param>
        /// <returns>List of all CNVEntry nodes.</returns>
        public List<CNVEntry> AllEntries(bool asSorted = false)
        {
            List<CNVEntry> entries = _AllEntries();
            if (!asSorted)
            {
                return entries;
            }
            return entries.OrderBy(e => e.ListIndex == -1).ThenBy(e => e.ListIndex).ToList();
        }

        private List<CNVEntry> _AllEntries(List<CNVLink> links = null, HashSet<CNVEntry> seenEntries = null)
        {
            List<CNVEntry> entries = new List<CNVEntry>();
            links = links ?? Starters;
            seenEntries = seenEntries ?? new HashSet<CNVEntry>();

            foreach (CNVLink link in links)
            {
                CNVNode entry = link.Node;
                if (entry == null || seenEntries.Contains(entry as CNVEntry))
                {
                    continue;
                }
                if (!(entry is CNVEntry cnvEntry))
                {
                    continue;
                }
                entries.Add(cnvEntry);
                seenEntries.Add(cnvEntry);
                foreach (CNVLink replyLink in entry.Links)
                {
                    CNVNode reply = replyLink.Node;
                    if (reply != null)
                    {
                        entries.AddRange(_AllEntries(reply.Links, seenEntries));
                    }
                }
            }

            return entries;
        }

        // Matching pattern from DLG.AllReplies
        /// <summary>
        /// Gets all reply nodes in the conversation tree.
        /// </summary>
        /// <param name="asSorted">Whether to return replies sorted by list index.</param>
        /// <returns>List of all CNVReply nodes.</returns>
        public List<CNVReply> AllReplies(bool asSorted = false)
        {
            List<CNVReply> replies = _AllReplies();
            if (!asSorted)
            {
                return replies;
            }
            return replies.OrderBy(r => r.ListIndex == -1).ThenBy(r => r.ListIndex).ToList();
        }

        private List<CNVReply> _AllReplies(List<CNVLink> links = null, List<CNVReply> seenReplies = null)
        {
            List<CNVReply> replies = new List<CNVReply>();
            links = links ?? Starters.Where(l => l.Node != null).SelectMany(l => l.Node.Links).ToList();
            seenReplies = seenReplies ?? new List<CNVReply>();

            foreach (CNVLink link in links)
            {
                CNVNode reply = link.Node;
                if (seenReplies.Contains(reply as CNVReply))
                {
                    continue;
                }
                if (!(reply is CNVReply cnvReply))
                {
                    continue;
                }
                replies.Add(cnvReply);
                seenReplies.Add(cnvReply);
                foreach (CNVLink entryLink in reply.Links)
                {
                    CNVNode entry = entryLink.Node;
                    if (entry != null)
                    {
                        replies.AddRange(_AllReplies(entry.Links, seenReplies));
                    }
                }
            }

            return replies;
        }
    }

    /// <summary>
    /// Type of computer interface for conversation.
    /// </summary>
    [PublicAPI]
    public enum CNVComputerType
    {
        Modern = 0,
        Ancient = 1
    }

    /// <summary>
    /// Type of conversation.
    /// </summary>
    [PublicAPI]
    public enum CNVConversationType
    {
        Human = 0,
        Computer = 1,
        Other = 2,
        Unknown = 3
    }

    /// <summary>
    /// Represents a stunt model in a conversation.
    /// </summary>
    [PublicAPI]
    public sealed class CNVStunt : IEquatable<CNVStunt>
    {
        // Matching pattern from DLGStunt
        // Original: Stunt model reference for Eclipse conversation
        private readonly int _hashCache;

        /// <summary>
        /// Participant tag (character who performs the stunt).
        /// </summary>
        public string Participant { get; set; } = string.Empty;

        /// <summary>
        /// Stunt model ResRef.
        /// </summary>
        public ResRef StuntModel { get; set; } = ResRef.FromBlank();

        public CNVStunt()
        {
            _hashCache = Guid.NewGuid().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is CNVStunt other && Equals(other);
        }

        public bool Equals(CNVStunt other)
        {
            if (other == null) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }
    }
}

