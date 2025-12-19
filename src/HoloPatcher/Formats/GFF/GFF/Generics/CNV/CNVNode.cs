using System;
using System.Collections.Generic;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.CNV
{
    /// <summary>
    /// Represents a node in the conversation graph (either CNVEntry or CNVReply).
    /// </summary>
    /// <remarks>
    /// CNV Node:
    /// - Eclipse Engine conversation format (Dragon Age, )
    /// - Base class for conversation entries (NPC lines) and replies (player options)
    /// - Similar structure to DLG nodes but adapted for Eclipse conversation system
    /// </remarks>
    [PublicAPI]
    public abstract class CNVNode : IEquatable<CNVNode>
    {
        // Matching pattern from DLGNode
        // Original: Base node class for Eclipse conversation format
        protected readonly int _hashCache;

        /// <summary>
        /// Comment/note for this conversation node.
        /// </summary>
        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// Links to other conversation nodes.
        /// </summary>
        public List<CNVLink> Links { get; set; } = new List<CNVLink>();

        /// <summary>
        /// Index in the conversation list.
        /// </summary>
        public int ListIndex { get; set; } = -1;

        // Camera settings
        /// <summary>
        /// Camera angle for this conversation node.
        /// </summary>
        public int CameraAngle { get; set; }

        /// <summary>
        /// Camera animation ID.
        /// </summary>
        public int? CameraAnim { get; set; }

        /// <summary>
        /// Camera ID.
        /// </summary>
        public int? CameraId { get; set; }

        /// <summary>
        /// Camera field of view.
        /// </summary>
        public float? CameraFov { get; set; }

        /// <summary>
        /// Camera height.
        /// </summary>
        public float? CameraHeight { get; set; }

        /// <summary>
        /// Camera effect.
        /// </summary>
        public int? CameraEffect { get; set; }

        // Timing
        /// <summary>
        /// Delay before this node executes (-1 = no delay).
        /// </summary>
        public int Delay { get; set; } = -1;

        /// <summary>
        /// Fade type for transitions.
        /// </summary>
        public int FadeType { get; set; }

        /// <summary>
        /// Fade color for transitions.
        /// </summary>
        public Color FadeColor { get; set; }

        /// <summary>
        /// Fade delay in seconds.
        /// </summary>
        public float? FadeDelay { get; set; }

        /// <summary>
        /// Fade length in seconds.
        /// </summary>
        public float? FadeLength { get; set; }

        // Content
        /// <summary>
        /// Localized text for this conversation node.
        /// </summary>
        public LocalizedString Text { get; set; } = LocalizedString.FromInvalid();

        /// <summary>
        /// Script to execute when this node is reached.
        /// </summary>
        public ResRef Script1 { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Secondary script (Eclipse-specific).
        /// </summary>
        public ResRef Script2 { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Sound effect ResRef.
        /// </summary>
        public ResRef Sound { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Sound exists flag.
        /// </summary>
        public int SoundExists { get; set; }

        /// <summary>
        /// Voice-over ResRef.
        /// </summary>
        public ResRef VoResRef { get; set; } = ResRef.FromBlank();

        /// <summary>
        /// Wait flags for timing control.
        /// </summary>
        public int WaitFlags { get; set; }

        // Script parameters (Eclipse-specific)
        /// <summary>
        /// Script 1 parameter 1.
        /// </summary>
        public int Script1Param1 { get; set; }

        /// <summary>
        /// Script 1 parameter 2.
        /// </summary>
        public int Script1Param2 { get; set; }

        /// <summary>
        /// Script 1 parameter 3.
        /// </summary>
        public int Script1Param3 { get; set; }

        /// <summary>
        /// Script 1 parameter 4.
        /// </summary>
        public int Script1Param4 { get; set; }

        /// <summary>
        /// Script 1 parameter 5.
        /// </summary>
        public int Script1Param5 { get; set; }

        /// <summary>
        /// Script 1 parameter 6 (string).
        /// </summary>
        public string Script1Param6 { get; set; } = string.Empty;

        /// <summary>
        /// Script 2 parameter 1.
        /// </summary>
        public int Script2Param1 { get; set; }

        /// <summary>
        /// Script 2 parameter 2.
        /// </summary>
        public int Script2Param2 { get; set; }

        /// <summary>
        /// Script 2 parameter 3.
        /// </summary>
        public int Script2Param3 { get; set; }

        /// <summary>
        /// Script 2 parameter 4.
        /// </summary>
        public int Script2Param4 { get; set; }

        /// <summary>
        /// Script 2 parameter 5.
        /// </summary>
        public int Script2Param5 { get; set; }

        /// <summary>
        /// Script 2 parameter 6 (string).
        /// </summary>
        public string Script2Param6 { get; set; } = string.Empty;

        // Quest/Plot
        /// <summary>
        /// Quest identifier.
        /// </summary>
        public string Quest { get; set; } = string.Empty;

        /// <summary>
        /// Quest entry index.
        /// </summary>
        public int? QuestEntry { get; set; } = 0;

        /// <summary>
        /// Plot index.
        /// </summary>
        public int PlotIndex { get; set; }

        /// <summary>
        /// Plot XP percentage.
        /// </summary>
        public float PlotXpPercentage { get; set; } = 1.0f;

        // Animation
        /// <summary>
        /// List of animations for this node.
        /// </summary>
        public List<CNVAnimation> Animations { get; set; } = new List<CNVAnimation>();

        /// <summary>
        /// Emotion ID for facial expression.
        /// </summary>
        public int EmotionId { get; set; }

        /// <summary>
        /// Facial animation ID.
        /// </summary>
        public int FacialId { get; set; }

        // Other
        /// <summary>
        /// Listener tag (character who listens to this line).
        /// </summary>
        public string Listener { get; set; } = string.Empty;

        /// <summary>
        /// Target height for camera positioning.
        /// </summary>
        public float? TargetHeight { get; set; }

        // Eclipse-specific fields
        /// <summary>
        /// Node ID (Eclipse-specific).
        /// </summary>
        public int NodeId { get; set; }

        /// <summary>
        /// Whether this node is unskippable.
        /// </summary>
        public bool Unskippable { get; set; }

        /// <summary>
        /// Whether voice-over text was changed.
        /// </summary>
        public bool VoTextChanged { get; set; }

        protected CNVNode()
        {
            _hashCache = Guid.NewGuid().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is CNVNode other && Equals(other);
        }

        public bool Equals(CNVNode other)
        {
            if (other == null || GetType() != other.GetType()) return false;
            return _hashCache == other._hashCache;
        }

        public override int GetHashCode()
        {
            return _hashCache;
        }

        /// <summary>
        /// Gets the path string for this node in the conversation tree.
        /// </summary>
        public string Path()
        {
            string nodeListDisplay = this is CNVEntry ? "EntryList" : "ReplyList";
            return $"{nodeListDisplay}\\{ListIndex}";
        }
    }

    /// <summary>
    /// Replies are nodes that are responses by the player.
    /// </summary>
    [PublicAPI]
    public sealed class CNVReply : CNVNode
    {
        // Matching pattern from DLGReply
        // Original: Player response node in Eclipse conversation
        public CNVReply() : base()
        {
        }
    }

    /// <summary>
    /// Entries are nodes that are responses by NPCs.
    /// </summary>
    [PublicAPI]
    public sealed class CNVEntry : CNVNode
    {
        // Matching pattern from DLGEntry
        // Original: NPC line node in Eclipse conversation
        /// <summary>
        /// Speaker tag (character who speaks this line).
        /// </summary>
        public string Speaker { get; set; } = string.Empty;

        public CNVEntry() : base()
        {
        }

        /// <summary>
        /// Animation ID (alias for CameraAnim).
        /// </summary>
        public int? AnimationId
        {
            get => CameraAnim;
            set => CameraAnim = value;
        }
    }
}

