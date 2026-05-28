using System;
using System.Numerics;
using System.Collections.Generic;
using KOTORModSync;
using KOTORModSync.Formats.GFF;
using KOTORModSync.Resource;
using JetBrains.Annotations;
using KOTORModSync.Common;

namespace KOTORModSync.Resource.Generics.GUI
{
    /// <summary>
    /// Reads KOTOR GUI files from GFF format.
    /// Based on PyKotor wiki documentation: vendor/PyKotor/wiki/GFF-GUI.md
    /// Reference: vendor/reone GUI loading, vendor/KotOR.js GUI system
    /// </summary>
    [PublicAPI]
    public class GUIReader
    {
        private GFF _gff;

        public GUIReader(byte[] data)
        {
            var reader = new GFFBinaryReader(data);
            _gff = reader.Load();
        }

        public GUIReader(string filepath)
        {
            var reader = new GFFBinaryReader(filepath);
            _gff = reader.Load();
        }

        /// <summary>
        /// Loads the GUI from the GFF data.
        /// </summary>
        public GUI Load()
        {
            var gui = new GUI();

            // Read root GUI properties
            gui.Tag = _gff.Root.GetString("Tag") ?? string.Empty;

            // Read top-level controls list
            if (_gff.Root.TryGetList("CONTROLS", out var controlsList))
            {
                foreach (var controlStruct in controlsList)
                {
                    var control = LoadControl(controlStruct, null);
                    if (control != null)
                    {
                        gui.Controls.Add(control);
                    }
                }
            }

            // Set the first control as root if available (some GUIs use this pattern)
            if (gui.Controls.Count > 0)
            {
                gui.Root = gui.Controls[0];
            }

            return gui;
        }

        /// <summary>
        /// Recursively loads a GUI control from a GFF struct.
        /// </summary>
        private GUIControl LoadControl(GFFStruct gffStruct, GUIControl parent)
        {
            // Read control type
            int controlType = gffStruct.Exists("CONTROLTYPE") ? gffStruct.GetInt32("CONTROLTYPE") : 0;

            // Create appropriate control subclass
            GUIControl control = CreateControlByType((GUIControlType)controlType);

            // Load common properties
            control.GuiType = (GUIControlType)controlType;
            control.Id = gffStruct.Exists("ID") ? (int?)gffStruct.GetInt32("ID") : null;
            control.Tag = gffStruct.GetString("TAG") ?? string.Empty;
            control.ParentTag = gffStruct.GetString("Obj_Parent");
            control.ParentId = gffStruct.Exists("Obj_ParentID") ? (int?)gffStruct.GetInt32("Obj_ParentID") : null;
            control.Locked = gffStruct.Exists("Obj_Locked") ? (bool?)(gffStruct.GetUInt8("Obj_Locked") != 0) : null;

            // Load extent (position and size)
            if (gffStruct.TryGetStruct("EXTENT", out var extentStruct))
            {
                int left = extentStruct.Exists("LEFT") ? extentStruct.GetInt32("LEFT") : 0;
                int top = extentStruct.Exists("TOP") ? extentStruct.GetInt32("TOP") : 0;
                int width = extentStruct.Exists("WIDTH") ? extentStruct.GetInt32("WIDTH") : 0;
                int height = extentStruct.Exists("HEIGHT") ? extentStruct.GetInt32("HEIGHT") : 0;

                control.Extent = new Vector4(left, top, width, height);
                control.Position = new Vector2(left, top);
                control.Size = new Vector2(width, height);
            }

            // Load color
            if (gffStruct.Exists("COLOR"))
            {
                var vec = gffStruct.GetVector3("COLOR");
                control.Color = new Color(vec.X, vec.Y, vec.Z, 1.0f);
            }

            // Alpha is stored as a float (0.0-1.0)
            // Store it in Properties for now since GUIControl doesn't have an Alpha field
            if (gffStruct.Exists("ALPHA"))
            {
                control.Properties["ALPHA"] = gffStruct.GetSingle("ALPHA");
            }

            // Load border
            if (gffStruct.TryGetStruct("BORDER", out var borderStruct))
            {
                control.Border = LoadBorder(borderStruct);
            }

            // Load highlight
            if (gffStruct.TryGetStruct("HILIGHT", out var hilightStruct))
            {
                control.Hilight = LoadBorder(hilightStruct);
            }

            // Load selected state
            if (gffStruct.TryGetStruct("SELECTED", out var selectedStruct))
            {
                control.Selected = LoadGUISelected(selectedStruct);
            }

            // Load highlight+selected state
            if (gffStruct.TryGetStruct("HILIGHTSELECTED", out var hilightSelectedStruct))
            {
                control.HilightSelected = LoadGUIHilightSelected(hilightSelectedStruct);
            }

            // Load text
            if (gffStruct.TryGetStruct("TEXT", out var textStruct))
            {
                control.GuiText = LoadText(textStruct);
            }

            // Load navigation (MOVETO)
            if (gffStruct.TryGetStruct("MOVETO", out var movetoStruct))
            {
                control.Moveto = LoadMoveTo(movetoStruct);
            }

            // Load control-specific properties
            LoadControlSpecificProperties(control, gffStruct);

            // Recursively load child controls
            if (gffStruct.TryGetList("CONTROLS", out var childControlsList))
            {
                foreach (var childStruct in childControlsList)
                {
                    var childControl = LoadControl(childStruct, control);
                    if (childControl != null)
                    {
                        control.Children.Add(childControl);
                    }
                }
            }

            return control;
        }

        /// <summary>
        /// Creates the appropriate GUIControl subclass based on control type.
        /// </summary>
        private GUIControl CreateControlByType(GUIControlType type)
        {
            switch (type)
            {
                case GUIControlType.Button:
                    return new GUIButton();
                case GUIControlType.Label:
                    return new GUILabel();
                case GUIControlType.Slider:
                    return new GUISlider();
                case GUIControlType.Panel:
                    return new GUIPanel();
                case GUIControlType.ListBox:
                    return new GUIListBox();
                case GUIControlType.CheckBox:
                    return new GUICheckBox();
                case GUIControlType.ProtoItem:
                    return new GUIProtoItem();
                case GUIControlType.Progress:
                    return new GUIProgressBar();
                case GUIControlType.ScrollBar:
                case GUIControlType.Control:
                case GUIControlType.Invalid:
                default:
                    return new GUIControl();
            }
        }

        /// <summary>
        /// Loads border properties from a GFF struct.
        /// </summary>
        private GUIBorder LoadBorder(GFFStruct borderStruct)
        {
            var border = new GUIBorder();

            border.Corner = borderStruct.GetResRef("CORNER");
            border.Edge = borderStruct.GetResRef("EDGE");
            border.Fill = borderStruct.GetResRef("FILL");
            border.FillStyle = borderStruct.Exists("FILLSTYLE") ? borderStruct.GetInt32("FILLSTYLE") : -1;
            border.Dimension = borderStruct.Exists("DIMENSION") ? borderStruct.GetInt32("DIMENSION") : 0;
            border.InnerOffset = borderStruct.Exists("INNEROFFSET") ? (int?)borderStruct.GetInt32("INNEROFFSET") : null;
            border.InnerOffsetY = borderStruct.Exists("INNEROFFSETY") ? (int?)borderStruct.GetInt32("INNEROFFSETY") : null;

            if (borderStruct.Exists("COLOR"))
            {
                var vec = borderStruct.GetVector3("COLOR");
                border.Color = new Color(vec.X, vec.Y, vec.Z, 1.0f);
            }

            border.Pulsing = borderStruct.Exists("PULSING") ? (int?)borderStruct.GetUInt8("PULSING") : null;

            return border;
        }

        /// <summary>
        /// Loads text properties from a GFF struct.
        /// </summary>
        private GUIText LoadText(GFFStruct textStruct)
        {
            var text = new GUIText();

            text.Text = textStruct.GetString("TEXT") ?? string.Empty;
            text.StrRef = textStruct.Exists("STRREF") ? (int)textStruct.GetUInt32("STRREF") : -1;
            text.Font = textStruct.GetResRef("FONT");
            text.Alignment = textStruct.Exists("ALIGNMENT") ? (int)textStruct.GetUInt32("ALIGNMENT") : 18; // 18 = Center

            if (textStruct.Exists("COLOR"))
            {
                var vec = textStruct.GetVector3("COLOR");
                text.Color = new Color(vec.X, vec.Y, vec.Z, 1.0f);
            }
            else
            {
                // Default KotOR 1 text color: cyan
                text.Color = new Color(0.0f, 0.659f, 0.980f, 1.0f);
            }

            text.Pulsing = textStruct.Exists("PULSING") ? (int?)textStruct.GetUInt8("PULSING") : null;

            return text;
        }

        /// <summary>
        /// Loads navigation (MOVETO) properties.
        /// </summary>
        private GUIMoveTo LoadMoveTo(GFFStruct movetoStruct)
        {
            var moveto = new GUIMoveTo();

            moveto.Up = movetoStruct.Exists("UP") ? movetoStruct.GetInt32("UP") : -1;
            moveto.Down = movetoStruct.Exists("DOWN") ? movetoStruct.GetInt32("DOWN") : -1;
            moveto.Left = movetoStruct.Exists("LEFT") ? movetoStruct.GetInt32("LEFT") : -1;
            moveto.Right = movetoStruct.Exists("RIGHT") ? movetoStruct.GetInt32("RIGHT") : -1;

            return moveto;
        }

        /// <summary>
        /// Loads selected state properties.
        /// </summary>
        private GUISelected LoadGUISelected(GFFStruct selectedStruct)
        {
            var selected = new GUISelected();

            selected.Corner = selectedStruct.GetResRef("CORNER");
            selected.Edge = selectedStruct.GetResRef("EDGE");
            selected.Fill = selectedStruct.GetResRef("FILL");
            selected.FillStyle = selectedStruct.Exists("FILLSTYLE") ? selectedStruct.GetInt32("FILLSTYLE") : -1;
            selected.Dimension = selectedStruct.Exists("DIMENSION") ? selectedStruct.GetInt32("DIMENSION") : 0;
            selected.InnerOffset = selectedStruct.Exists("INNEROFFSET") ? (int?)selectedStruct.GetInt32("INNEROFFSET") : null;
            selected.InnerOffsetY = selectedStruct.Exists("INNEROFFSETY") ? (int?)selectedStruct.GetInt32("INNEROFFSETY") : null;

            if (selectedStruct.Exists("COLOR"))
            {
                var vec = selectedStruct.GetVector3("COLOR");
                selected.Color = new Color(vec.X, vec.Y, vec.Z, 1.0f);
            }

            selected.Pulsing = selectedStruct.Exists("PULSING") ? (int?)selectedStruct.GetUInt8("PULSING") : null;

            return selected;
        }

        /// <summary>
        /// Loads highlight+selected state properties.
        /// </summary>
        private GUIHilightSelected LoadGUIHilightSelected(GFFStruct hilightSelectedStruct)
        {
            var hilightSelected = new GUIHilightSelected();

            hilightSelected.Corner = hilightSelectedStruct.GetResRef("CORNER");
            hilightSelected.Edge = hilightSelectedStruct.GetResRef("EDGE");
            hilightSelected.Fill = hilightSelectedStruct.GetResRef("FILL");
            hilightSelected.FillStyle = hilightSelectedStruct.Exists("FILLSTYLE") ? hilightSelectedStruct.GetInt32("FILLSTYLE") : -1;
            hilightSelected.Dimension = hilightSelectedStruct.Exists("DIMENSION") ? hilightSelectedStruct.GetInt32("DIMENSION") : 0;
            hilightSelected.InnerOffset = hilightSelectedStruct.Exists("INNEROFFSET") ? (int?)hilightSelectedStruct.GetInt32("INNEROFFSET") : null;
            hilightSelected.InnerOffsetY = hilightSelectedStruct.Exists("INNEROFFSETY") ? (int?)hilightSelectedStruct.GetInt32("INNEROFFSETY") : null;

            if (hilightSelectedStruct.Exists("COLOR"))
            {
                var vec = hilightSelectedStruct.GetVector3("COLOR");
                hilightSelected.Color = new Color(vec.X, vec.Y, vec.Z, 1.0f);
            }

            hilightSelected.Pulsing = hilightSelectedStruct.Exists("PULSING") ? (int?)hilightSelectedStruct.GetUInt8("PULSING") : null;

            return hilightSelected;
        }

        /// <summary>
        /// Loads control-specific properties that vary by control type.
        /// </summary>
        private void LoadControlSpecificProperties(GUIControl control, GFFStruct gffStruct)
        {
            switch (control.GuiType)
            {
                case GUIControlType.ListBox:
                    LoadListBoxProperties((GUIListBox)control, gffStruct);
                    break;
                case GUIControlType.ScrollBar:
                    LoadScrollBarProperties(control, gffStruct);
                    break;
                case GUIControlType.Progress:
                    LoadProgressBarProperties((GUIProgressBar)control, gffStruct);
                    break;
                case GUIControlType.Slider:
                    LoadSliderProperties((GUISlider)control, gffStruct);
                    break;
                case GUIControlType.CheckBox:
                    LoadCheckBoxProperties((GUICheckBox)control, gffStruct);
                    break;
                case GUIControlType.Button:
                    LoadButtonProperties((GUIButton)control, gffStruct);
                    break;
                case GUIControlType.Label:
                    LoadLabelProperties((GUILabel)control, gffStruct);
                    break;
                case GUIControlType.Panel:
                    LoadPanelProperties((GUIPanel)control, gffStruct);
                    break;
                case GUIControlType.ProtoItem:
                    LoadProtoItemProperties((GUIProtoItem)control, gffStruct);
                    break;
            }
        }

        private void LoadListBoxProperties(GUIListBox listBox, GFFStruct gffStruct)
        {
            // Load PROTOITEM template
            if (gffStruct.TryGetStruct("PROTOITEM", out var protoItemStruct))
            {
                listBox.ProtoItem = (GUIProtoItem)LoadControl(protoItemStruct, listBox);
            }

            // Load scrollbar
            if (gffStruct.TryGetStruct("SCROLLBAR", out var scrollbarStruct))
            {
                listBox.ScrollBar = LoadScrollbarControl(scrollbarStruct);
            }

            listBox.Padding = gffStruct.Exists("PADDING") ? gffStruct.GetInt32("PADDING") : 5;
            listBox.Looping = gffStruct.Exists("LOOPING") ? gffStruct.GetUInt8("LOOPING") != 0 : true;
            listBox.MaxValue = gffStruct.Exists("MAXVALUE") ? (int?)gffStruct.GetInt32("MAXVALUE") : null;
            listBox.LeftScrollbar = gffStruct.Exists("LEFTSCROLLBAR") ? (int?)gffStruct.GetInt32("LEFTSCROLLBAR") : null;
        }

        private void LoadScrollBarProperties(GUIControl control, GFFStruct gffStruct)
        {
            // For standalone scrollbar controls
            control.MaxValue = gffStruct.Exists("MAXVALUE") ? (int?)gffStruct.GetInt32("MAXVALUE") : null;
            control.CurrentValue = gffStruct.Exists("CURVALUE") ? (int?)gffStruct.GetInt32("CURVALUE") : null;
            control.DrawMode = gffStruct.Exists("DRAWMODE") ? (int?)gffStruct.GetUInt8("DRAWMODE") : null;

            // Load THUMB
            if (gffStruct.TryGetStruct("THUMB", out var thumbStruct))
            {
                control.Thumb = LoadScrollbarThumb(thumbStruct);
            }

            // Store DIR image if present
            if (gffStruct.TryGetStruct("DIR", out var dirStruct))
            {
                control.Properties["DIR_IMAGE"] = dirStruct.GetResRef("IMAGE");
            }
        }

        private GUIScrollbar LoadScrollbarControl(GFFStruct scrollbarStruct)
        {
            var scrollbar = new GUIScrollbar();

            scrollbar.MaxValue = scrollbarStruct.Exists("MAXVALUE") ? scrollbarStruct.GetInt32("MAXVALUE") : 0;
            scrollbar.VisibleValue = scrollbarStruct.Exists("VISIBLEVALUE") ? scrollbarStruct.GetInt32("VISIBLEVALUE") : 0;
            scrollbar.CurrentValue = scrollbarStruct.Exists("CURVALUE") ? (int?)scrollbarStruct.GetInt32("CURVALUE") : null;
            scrollbar.Horizontal = scrollbarStruct.Exists("HORIZONTAL") ? scrollbarStruct.GetUInt8("HORIZONTAL") != 0 : false;

            // Load DIR (direction arrow buttons)
            if (scrollbarStruct.TryGetStruct("DIR", out var dirStruct))
            {
                scrollbar.GuiDirection = new GUIScrollbarDir
                {
                    Image = dirStruct.GetResRef("IMAGE"),
                    Alignment = dirStruct.Exists("ALIGNMENT") ? dirStruct.GetInt32("ALIGNMENT") : 18
                };
            }

            // Load THUMB (draggable thumb)
            if (scrollbarStruct.TryGetStruct("THUMB", out var thumbStruct))
            {
                scrollbar.GuiThumb = LoadScrollbarThumb(thumbStruct);
            }

            return scrollbar;
        }

        private GUIScrollbarThumb LoadScrollbarThumb(GFFStruct thumbStruct)
        {
            var thumb = new GUIScrollbarThumb();

            thumb.Image = thumbStruct.GetResRef("IMAGE");
            thumb.Alignment = thumbStruct.Exists("ALIGNMENT") ? thumbStruct.GetInt32("ALIGNMENT") : 18;
            thumb.FlipStyle = thumbStruct.Exists("FLIPSTYLE") ? (int?)thumbStruct.GetInt32("FLIPSTYLE") : null;
            thumb.DrawStyle = thumbStruct.Exists("DRAWSTYLE") ? (int?)thumbStruct.GetInt32("DRAWSTYLE") : null;
            thumb.Rotate = thumbStruct.Exists("ROTATE") ? (float?)thumbStruct.GetSingle("ROTATE") : null;

            return thumb;
        }

        private void LoadProgressBarProperties(GUIProgressBar progressBar, GFFStruct gffStruct)
        {
            progressBar.MaxValue = gffStruct.Exists("MAXVALUE") ? gffStruct.GetInt32("MAXVALUE") : 100;
            progressBar.CurrentValue = gffStruct.Exists("CURVALUE") ? gffStruct.GetInt32("CURVALUE") : 0;
            progressBar.StartFromLeft = gffStruct.Exists("STARTFROMLEFT") ? gffStruct.GetInt32("STARTFROMLEFT") : 1;

            // Load PROGRESS struct - store in the base class Progress property which is of type GUIProgress
            if (gffStruct.TryGetStruct("PROGRESS", out var progressStruct))
            {
                // Access Progress through base class since GUIProgressBar.Progress is float?, not GUIProgress
                ((GUIControl)progressBar).Progress = LoadProgress(progressStruct);
            }
        }

        private GUIProgress LoadProgress(GFFStruct progressStruct)
        {
            var progress = new GUIProgress();

            progress.Corner = progressStruct.GetResRef("CORNER");
            progress.Edge = progressStruct.GetResRef("EDGE");
            progress.Fill = progressStruct.GetResRef("FILL");
            progress.FillStyle = progressStruct.Exists("FILLSTYLE") ? progressStruct.GetInt32("FILLSTYLE") : -1;
            progress.Dimension = progressStruct.Exists("DIMENSION") ? progressStruct.GetInt32("DIMENSION") : 0;
            progress.InnerOffset = progressStruct.Exists("INNEROFFSET") ? progressStruct.GetInt32("INNEROFFSET") : 0;
            progress.InnerOffsetY = progressStruct.Exists("INNEROFFSETY") ? (int?)progressStruct.GetInt32("INNEROFFSETY") : null;

            if (progressStruct.Exists("COLOR"))
            {
                var vec = progressStruct.GetVector3("COLOR");
                progress.Color = new Color(vec.X, vec.Y, vec.Z, 1.0f);
            }

            progress.Pulsing = progressStruct.Exists("PULSING") ? (int?)progressStruct.GetUInt8("PULSING") : null;

            return progress;
        }

        private void LoadSliderProperties(GUISlider slider, GFFStruct gffStruct)
        {
            slider.MaxValue = gffStruct.Exists("MAXVALUE") ? gffStruct.GetInt32("MAXVALUE") : 100;
            slider.Value = gffStruct.Exists("CURVALUE") ? gffStruct.GetInt32("CURVALUE") : 0;
            int direction = gffStruct.Exists("DIRECTION") ? gffStruct.GetInt32("DIRECTION") : 0;
            slider.Direction = direction == 0 ? "horizontal" : "vertical";

            // Load THUMB
            if (gffStruct.TryGetStruct("THUMB", out var thumbStruct))
            {
                slider.Properties["THUMB"] = LoadScrollbarThumb(thumbStruct);
            }
        }

        private void LoadCheckBoxProperties(GUICheckBox checkBox, GFFStruct gffStruct)
        {
            checkBox.IsSelected = gffStruct.Exists("ISSELECTED") ? (int?)gffStruct.GetUInt8("ISSELECTED") : null;
        }

        private void LoadButtonProperties(GUIButton button, GFFStruct gffStruct)
        {
            // Button-specific properties loaded from TEXT struct
            if (button.GuiText != null)
            {
                button.Text = button.GuiText.Text;
                button.TextColor = button.GuiText.Color;
            }

            button.Pulsing = gffStruct.Exists("PULSING") ? (int?)gffStruct.GetUInt8("PULSING") : null;
        }

        private void LoadLabelProperties(GUILabel label, GFFStruct gffStruct)
        {
            // Label-specific properties loaded from TEXT struct
            if (label.GuiText != null)
            {
                label.Text = label.GuiText.Text;
                label.TextColor = label.GuiText.Color;
                label.Alignment = label.GuiText.Alignment;
            }
        }

        private void LoadPanelProperties(GUIPanel panel, GFFStruct gffStruct)
        {
            // Panel uses Border for background
            if (gffStruct.Exists("ALPHA"))
            {
                panel.Alpha = gffStruct.GetSingle("ALPHA");
            }
        }

        private void LoadProtoItemProperties(GUIProtoItem protoItem, GFFStruct gffStruct)
        {
            protoItem.IsSelected = gffStruct.Exists("ISSELECTED") ? (int?)gffStruct.GetUInt8("ISSELECTED") : null;
            protoItem.Pulsing = gffStruct.Exists("PULSING") ? (int?)gffStruct.GetUInt8("PULSING") : null;
        }
    }
}