using System;
using System.Collections.Generic;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.DLG
{
    /// <summary>
    /// Represents a node in the dialog graph (either DLGEntry or DLGReply).
    /// </summary>
    [PublicAPI]
    public abstract class DLGNode : IEquatable<DLGNode>
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:22
        // Original: class DLGNode:
        protected readonly int _hashCache;

        public string Comment { get; set; } = string.Empty;
        public List<DLGLink> Links { get; set; } = new List<DLGLink>();
        public int ListIndex { get; set; } = -1;

        // Camera settings
        public int CameraAngle { get; set; }
        public int? CameraAnim { get; set; }
        public int? CameraId { get; set; }
        public float? CameraFov { get; set; }
        public float? CameraHeight { get; set; }
        public int? CameraEffect { get; set; }

        // Timing
        public int Delay { get; set; } = -1;
        public int FadeType { get; set; }
        public Color FadeColor { get; set; }
        public float? FadeDelay { get; set; }
        public float? FadeLength { get; set; }

        // Content
        public LocalizedString Text { get; set; } = LocalizedString.FromInvalid();
        public ResRef Script1 { get; set; } = ResRef.FromBlank();
        public ResRef Script2 { get; set; } = ResRef.FromBlank();
        public ResRef Sound { get; set; } = ResRef.FromBlank();
        public int SoundExists { get; set; }
        public ResRef VoResRef { get; set; } = ResRef.FromBlank();
        public int WaitFlags { get; set; }

        // Script parameters (KotOR 2)
        public int Script1Param1 { get; set; }
        public int Script1Param2 { get; set; }
        public int Script1Param3 { get; set; }
        public int Script1Param4 { get; set; }
        public int Script1Param5 { get; set; }
        public string Script1Param6 { get; set; } = string.Empty;
        public int Script2Param1 { get; set; }
        public int Script2Param2 { get; set; }
        public int Script2Param3 { get; set; }
        public int Script2Param4 { get; set; }
        public int Script2Param5 { get; set; }
        public string Script2Param6 { get; set; } = string.Empty;

        // Quest/Plot
        public string Quest { get; set; } = string.Empty;
        public int? QuestEntry { get; set; } = 0;
        public int PlotIndex { get; set; }
        public float PlotXpPercentage { get; set; } = 1.0f;

        // Animation
        public List<DLGAnimation> Animations { get; set; } = new List<DLGAnimation>();
        public int EmotionId { get; set; }
        public int FacialId { get; set; }

        // Other
        public string Listener { get; set; } = string.Empty;
        public float? TargetHeight { get; set; }

        // KotOR 2
        public int AlienRaceNode { get; set; }
        public int NodeId { get; set; }
        public int PostProcNode { get; set; }
        public bool Unskippable { get; set; }
        public bool RecordNoVoOverride { get; set; }
        public bool RecordVo { get; set; }
        public bool VoTextChanged { get; set; }

        protected DLGNode()
        {
            _hashCache = Guid.NewGuid().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DLGNode other && Equals(other);
        }

        public bool Equals(DLGNode other)
        {
            if (other == null || GetType() != other.GetType()) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }

        public string Path()
        {
            string nodeListDisplay = this is DLGEntry ? "EntryList" : "ReplyList";
            return $"{nodeListDisplay}\\{ListIndex}";
        }
    }

    /// <summary>
    /// Replies are nodes that are responses by the player.
    /// </summary>
    [PublicAPI]
    public sealed class DLGReply : DLGNode
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:488
        // Original: class DLGReply(DLGNode):
        public DLGReply() : base()
        {
        }
    }

    /// <summary>
    /// Entries are nodes that are responses by NPCs.
    /// </summary>
    [PublicAPI]
    public sealed class DLGEntry : DLGNode
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/dlg/nodes.py:502
        // Original: class DLGEntry(DLGNode):
        public string Speaker { get; set; } = string.Empty;

        public DLGEntry() : base()
        {
        }

        public int? AnimationId
        {
            get => CameraAnim;
            set => CameraAnim = value;
        }
    }
}
