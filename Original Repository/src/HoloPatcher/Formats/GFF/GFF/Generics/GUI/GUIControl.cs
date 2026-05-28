using System.Collections.Generic;
using System.Numerics;
using KOTORModSync;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.GUI
{
    /// <summary>
    /// Base class for all GUI controls.
    /// </summary>
    [PublicAPI]
    public class GUIControl
    {
        private Vector2 _position = new Vector2(0, 0);
        private Vector2 _size = new Vector2(0, 0);

        public GUIControlType GuiType { get; set; } = GUIControlType.Control;
        public int? Id { get; set; }
        public string Tag { get; set; }
        public Vector4 Extent { get; set; } = new Vector4(0, 0, 0, 0);
        public GUIBorder Border { get; set; }
        public Color Color { get; set; }
        public GUIBorder Hilight { get; set; }
        public string ParentTag { get; set; }
        public int? ParentId { get; set; }
        public bool? Locked { get; set; }
        public GUIText GuiText { get; set; }
        public ResRef Font { get; set; } = ResRef.FromBlank();
        public List<GUIControl> Children { get; set; } = new List<GUIControl>();
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
        public GUIMoveTo Moveto { get; set; }
        public GUIScrollbar Scrollbar { get; set; }
        public int? MaxValue { get; set; }
        public int? Padding { get; set; }
        public int? Looping { get; set; }
        public int? LeftScrollbar { get; set; }
        public int? DrawMode { get; set; }
        public GUISelected Selected { get; set; }
        public GUIHilightSelected HilightSelected { get; set; }
        public int? IsSelected { get; set; }
        public int? CurrentValue { get; set; }
        public GUIProgress Progress { get; set; }
        public int? StartFromLeft { get; set; }
        public GUIScrollbarThumb Thumb { get; set; }

        public Vector2 Position
        {
            get => _position;
            set
            {
                _position = value;
                Extent = new Vector4(value.X, value.Y, _size.X, _size.Y);
            }
        }

        public Vector2 Size
        {
            get => _size;
            set
            {
                _size = value;
                Extent = new Vector4(_position.X, _position.Y, value.X, value.Y);
            }
        }

        public GUIControl()
        {
        }
    }

    /// <summary>
    /// Button control in a GUI.
    /// </summary>
    [PublicAPI]
    public class GUIButton : GUIControl
    {
        public string Text { get; set; }
        public Color TextColor { get; set; } = new Color(0, 0, 0, 0);
        public int? Pulsing { get; set; }

        public GUIButton() : base()
        {
            GuiType = GUIControlType.Button;
        }
    }

    /// <summary>
    /// Label control in a GUI.
    /// </summary>
    [PublicAPI]
    public class GUILabel : GUIControl
    {
        public string Text { get; set; } = string.Empty;
        public Color TextColor { get; set; } = new Color(0, 0, 0, 0);
        public bool Editable { get; set; }
        public int Alignment { get; set; }

        public GUILabel() : base()
        {
            GuiType = GUIControlType.Label;
        }
    }

    /// <summary>
    /// Slider control in a GUI.
    /// </summary>
    [PublicAPI]
    public class GUISlider : GUIControl
    {
        public float Value { get; set; }
        public float MinValue { get; set; }
        public float MaxValue { get; set; } = 100.0f;
        public string Direction { get; set; } = "horizontal";

        public GUISlider() : base()
        {
            GuiType = GUIControlType.Slider;
        }
    }

    /// <summary>
    /// Panel control in a GUI.
    /// </summary>
    [PublicAPI]
    public class GUIPanel : GUIControl
    {
        public ResRef BackgroundTexture { get; set; }
        public ResRef BorderTexture { get; set; }
        public float Alpha { get; set; } = 1.0f;

        public GUIPanel() : base()
        {
            GuiType = GUIControlType.Panel;
        }
    }

    /// <summary>
    /// List box control in a GUI.
    /// </summary>
    [PublicAPI]
    public class GUIListBox : GUIControl
    {
        public GUIProtoItem ProtoItem { get; set; }
        public GUIScrollbar ScrollBar { get; set; }
        public int Padding { get; set; } = 5;
        public bool Looping { get; set; } = true;

        public GUIListBox() : base()
        {
            GuiType = GUIControlType.ListBox;
        }
    }

    /// <summary>
    /// Checkbox control in a GUI.
    /// </summary>
    [PublicAPI]
    public class GUICheckBox : GUIControl
    {
        public int? IsSelected { get; set; }

        public GUICheckBox() : base()
        {
            GuiType = GUIControlType.CheckBox;
        }
    }

    /// <summary>
    /// Prototype item control in a GUI.
    /// </summary>
    [PublicAPI]
    public class GUIProtoItem : GUIControl
    {
        public int? Pulsing { get; set; }

        public GUIProtoItem() : base()
        {
            GuiType = GUIControlType.ProtoItem;
        }
    }

    /// <summary>
    /// Progress bar control in a GUI.
    /// </summary>
    [PublicAPI]
    public class GUIProgressBar : GUIControl
    {
        public float MaxValue { get; set; } = 100.0f;
        public int CurrentValue { get; set; }
        public ResRef ProgressFillTexture { get; set; } = ResRef.FromBlank();
        public GUIBorder ProgressBorder { get; set; }
        public int StartFromLeft { get; set; } = 1;
        public float? Progress { get; set; }

        public GUIProgressBar() : base()
        {
            GuiType = GUIControlType.Progress;
        }

        public void SetValue(int value)
        {
            if (value < 0 || value > 100)
            {
                throw new System.ArgumentException($"Progress bar value must be between 0-100, got {value}");
            }
            Progress = value;
        }
    }
}
