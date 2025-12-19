using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.GUI
{
    /// <summary>
    /// Direction properties for scrollbars.
    /// </summary>
    [PublicAPI]
    public class GUIScrollbarDir
    {
        public ResRef Image { get; set; } = ResRef.FromBlank();
        public int Alignment { get; set; }
        public int? FlipStyle { get; set; }
        public int? DrawStyle { get; set; }
        public float? Rotate { get; set; }

        public GUIScrollbarDir()
        {
        }
    }

    /// <summary>
    /// Thumb control in a scrollbar.
    /// </summary>
    [PublicAPI]
    public class GUIScrollbarThumb
    {
        public ResRef Image { get; set; } = ResRef.FromBlank();
        public int Alignment { get; set; }
        public int? FlipStyle { get; set; }
        public int? DrawStyle { get; set; }
        public float? Rotate { get; set; }

        public GUIScrollbarThumb()
        {
        }
    }

    /// <summary>
    /// Scrollbar control in a GUI.
    /// </summary>
    [PublicAPI]
    public class GUIScrollbar : GUIControl
    {
        public int Alignment { get; set; }
        public bool Horizontal { get; set; }
        public int VisibleValue { get; set; }
        public int? CurrentValue { get; set; }
        public GUIScrollbarThumb GuiThumb { get; set; }
        public GUIScrollbarDir GuiDirection { get; set; }

        public GUIScrollbar() : base()
        {
            GuiType = GUIControlType.ScrollBar;
        }
    }
}
