using JetBrains.Annotations;

namespace KOTORModSync.Resource.Generics.GUI
{
    /// <summary>
    /// Represents movement navigation between controls.
    /// </summary>
    [PublicAPI]
    public class GUIMoveTo
    {
        public int Up { get; set; } = -1;
        public int Down { get; set; } = -1;
        public int Left { get; set; } = -1;
        public int Right { get; set; } = -1;

        public GUIMoveTo()
        {
        }
    }
}

