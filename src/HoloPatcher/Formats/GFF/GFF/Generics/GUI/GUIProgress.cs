using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.GUI
{
    /// <summary>
    /// Progress bar properties for GUI controls.
    /// </summary>
    [PublicAPI]
    public class GUIProgress
    {
        public ResRef Corner { get; set; } = ResRef.FromBlank();
        public ResRef Edge { get; set; } = ResRef.FromBlank();
        public ResRef Fill { get; set; } = ResRef.FromBlank();
        public int FillStyle { get; set; }
        public int Dimension { get; set; }
        public int InnerOffset { get; set; }
        public Color Color { get; set; }
        public int? Pulsing { get; set; }
        public int? InnerOffsetY { get; set; }
        public int? StartFromLeft { get; set; }

        public GUIProgress()
        {
        }
    }

    /// <summary>
    /// Selected state properties for GUI controls.
    /// </summary>
    [PublicAPI]
    public class GUISelected
    {
        public ResRef Corner { get; set; } = ResRef.FromBlank();
        public ResRef Edge { get; set; } = ResRef.FromBlank();
        public ResRef Fill { get; set; } = ResRef.FromBlank();
        public int FillStyle { get; set; }
        public int Dimension { get; set; }
        public int? InnerOffset { get; set; }
        public Color Color { get; set; }
        public int? Pulsing { get; set; }
        public int? InnerOffsetY { get; set; }

        public GUISelected()
        {
        }
    }

    /// <summary>
    /// Highlight selected state properties for GUI controls.
    /// </summary>
    [PublicAPI]
    public class GUIHilightSelected
    {
        public ResRef Corner { get; set; } = ResRef.FromBlank();
        public ResRef Edge { get; set; } = ResRef.FromBlank();
        public ResRef Fill { get; set; } = ResRef.FromBlank();
        public int FillStyle { get; set; }
        public int Dimension { get; set; }
        public int? InnerOffset { get; set; }
        public Color Color { get; set; }
        public int? Pulsing { get; set; }
        public int? InnerOffsetY { get; set; }

        public GUIHilightSelected()
        {
        }
    }
}
