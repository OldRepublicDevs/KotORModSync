using KOTORModSync;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    /// <summary>
    /// Stores trigger data.
    ///
    /// UTT files are GFF-based format files that store trigger definitions including
    /// trap mechanics, script hooks, and activation settings.
    /// </summary>
    [PublicAPI]
    public sealed class UTT
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:16
        // Original: BINARY_TYPE = ResourceType.UTT
        public static readonly ResourceType BinaryType = ResourceType.UTT;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utt.py:171-209
        // Original: UTT properties initialization
        // Basic UTT properties
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public string Tag { get; set; } = string.Empty;
        public bool AutoRemoveKey { get; set; } = true;
        public int FactionId { get; set; }
        public int Cursor { get; set; }
        public float HighlightHeight { get; set; }
        public string KeyName { get; set; } = string.Empty;
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public int TypeId { get; set; }
        public bool TrapDetectable { get; set; }
        public int TrapDetectDc { get; set; }
        public bool TrapDisarmable { get; set; }
        public int TrapDisarmDc { get; set; }
        public bool IsTrap { get; set; }
        public int TrapType { get; set; }
        public bool TrapOnce { get; set; }
        public ResRef OnDisarmScript { get; set; } = ResRef.FromBlank();
        public ResRef OnTrapTriggeredScript { get; set; } = ResRef.FromBlank();
        public ResRef OnClickScript { get; set; } = ResRef.FromBlank();
        public ResRef OnHeartbeatScript { get; set; } = ResRef.FromBlank();
        public ResRef OnEnterScript { get; set; } = ResRef.FromBlank();
        public ResRef OnExitScript { get; set; } = ResRef.FromBlank();
        public ResRef OnUserDefinedScript { get; set; } = ResRef.FromBlank();
        public string Comment { get; set; } = string.Empty;
        
        // Deprecated fields
        public int PortraitId { get; set; }
        public int LoadscreenId { get; set; }
        public int PaletteId { get; set; }

        public UTT()
        {
        }
    }
}