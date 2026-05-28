using KOTORModSync;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics
{
    /// <summary>
    /// Stores waypoint data.
    ///
    /// UTW files are GFF-based format files that store waypoint definitions including
    /// map notes, appearance, and location data.
    /// </summary>
    [PublicAPI]
    public sealed class UTW
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:15
        // Original: BINARY_TYPE = ResourceType.UTW
        public static readonly ResourceType BinaryType = ResourceType.UTW;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utw.py:89-107
        // Original: UTW properties initialization
        // Basic UTW properties
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public string Tag { get; set; } = string.Empty;
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public bool HasMapNote { get; set; }
        public bool MapNoteEnabled { get; set; }
        public LocalizedString MapNote { get; set; } = LocalizedString.FromInvalid();
        public int AppearanceId { get; set; }
        public int PaletteId { get; set; }
        public string Comment { get; set; } = string.Empty;
        
        // Deprecated fields
        public string LinkedTo { get; set; } = string.Empty;
        public LocalizedString Description { get; set; } = LocalizedString.FromInvalid();

        public UTW()
        {
        }
    }
}