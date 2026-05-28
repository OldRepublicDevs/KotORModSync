using JetBrains.Annotations;

namespace KOTORModSync.Resource.Generics.GUI
{
    /// <summary>
    /// Enum representing different GUI control types.
    /// </summary>
    [PublicAPI]
    public enum GUIControlType
    {
        Invalid = -1,
        Control = 0,
        Panel = 2,
        ProtoItem = 4,
        Label = 5,
        Button = 6,
        CheckBox = 7,
        Slider = 8,
        ScrollBar = 9,
        Progress = 10,
        ListBox = 11
    }

    /// <summary>
    /// Text alignment options.
    /// </summary>
    [PublicAPI]
    public enum GUIAlignment
    {
        TopLeft = 1,
        TopCenter = 2,
        TopRight = 3,
        CenterLeft = 17,
        Center = 18,
        CenterRight = 19,
        BottomLeft = 33,
        BottomCenter = 34,
        BottomRight = 35
    }
}

