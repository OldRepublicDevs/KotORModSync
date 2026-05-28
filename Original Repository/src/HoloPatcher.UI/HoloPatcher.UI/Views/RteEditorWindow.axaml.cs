using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using AvRichTextBox;
using HoloPatcher.UI.Rte;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using AvaloniaTextElement = Avalonia.Controls.Documents.TextElement;

namespace HoloPatcher.UI.Views
{
    public partial class RteEditorWindow : Window
    {
        private readonly string _initialDirectory;
        private string _currentFilePath;
        private bool _isDirty;

        public RteEditorWindow(string initialDirectory = null)
        {
            InitializeComponent();
            _initialDirectory = initialDirectory;
            PopulateFontSelector();
            FontSizeComboBox.SelectedIndex = 2;
            ForegroundComboBox.SelectedIndex = 0;
            BackgroundComboBox.SelectedIndex = 0;
            Editor.FlowDocument.Selection_Changed += OnSelectionChanged;
            Editor.AddHandler(KeyUpEvent, OnEditorKeyUp, Avalonia.Interactivity.RoutingStrategies.Bubble);
            _ = InitializeNewDocumentAsync();
        }

        private void PopulateFontSelector()
        {
            System.Collections.Generic.IEnumerable<string> fonts = FontManager.Current.SystemFonts
                .Select(f => f.Name)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .Take(30); // keep list manageable

            foreach (string font in fonts)
            {
                FontFamilyComboBox.Items.Add(new ComboBoxItem { Content = font });
            }

            FontFamilyComboBox.SelectedIndex = 0;
        }

        private async Task InitializeNewDocumentAsync()
        {
            if (!await ConfirmDiscardChangesAsync())
            {
                return;
            }

            Editor.FlowDocument = new FlowDocument();
            _currentFilePath = null;
            _isDirty = false;
            UpdateTitle();
        }

        private void OnNewDocument(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _ = InitializeNewDocumentAsync();
        }

        private async void OnOpenDocument(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!await ConfirmDiscardChangesAsync())
            {
                return;
            }

            var options = new FilePickerOpenOptions
            {
                Title = "Open info.rte",
                SuggestedStartLocation = await GetStartLocationAsync(),
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Rich Text Editor (*.rte)") { Patterns = new[] { "*.rte" } }
                }
            };

            System.Collections.Generic.IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count == 0)
            {
                return;
            }

            string path = files[0].TryGetLocalPath();
            if (path is null)
            {
                return;
            }

            string json = await File.ReadAllTextAsync(path);
            var document = RteDocument.Parse(json);
            RteDocumentConverter.ApplyToRichTextBox(Editor, document);
            _currentFilePath = path;
            _isDirty = false;
            UpdateTitle();
        }

        private async void OnSaveDocument(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveDocumentAsync(false);
        }

        private async void OnSaveDocumentAs(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            await SaveDocumentAsync(true);
        }

        private async Task SaveDocumentAsync(bool saveAs)
        {
            if (string.IsNullOrEmpty(_currentFilePath) || saveAs)
            {
                var options = new FilePickerSaveOptions
                {
                    Title = "Save info.rte",
                    SuggestedStartLocation = await GetStartLocationAsync(),
                    SuggestedFileName = string.IsNullOrEmpty(_currentFilePath) ? "info.rte" : Path.GetFileName(_currentFilePath),
                    DefaultExtension = "rte",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Rich Text Editor (*.rte)") { Patterns = new[] { "*.rte" } }
                    }
                };

                IStorageFile file = await StorageProvider.SaveFilePickerAsync(options);
                if (file is null)
                {
                    return;
                }
                _currentFilePath = file.Path.LocalPath;
            }

            RteDocument rte = RteDocumentConverter.FromFlowDocument(Editor.FlowDocument);
            await File.WriteAllTextAsync(_currentFilePath, rte.ToJson());
            _isDirty = false;
            UpdateTitle();
        }

        private void OnCloseEditor(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Close();
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (!await ConfirmDiscardChangesAsync())
            {
                e.Cancel = true;
                return;
            }
            base.OnClosing(e);
        }

        private async Task<bool> ConfirmDiscardChangesAsync()
        {
            if (!_isDirty)
            {
                return true;
            }

            MsBox.Avalonia.Base.IMsBox<ButtonResult> messageBox = MessageBoxManager.GetMessageBoxStandard(
                "Unsaved changes",
                "You have unsaved changes. Do you want to discard them?",
                ButtonEnum.YesNo,
                MsBox.Avalonia.Enums.Icon.Warning);

            return await messageBox.ShowAsync() == ButtonResult.Yes;
        }

        private async Task<IStorageFolder> GetStartLocationAsync()
        {
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                string folder = Path.GetDirectoryName(_currentFilePath);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    return await StorageProvider.TryGetFolderFromPathAsync(folder);
                }
            }

            if (!string.IsNullOrEmpty(_initialDirectory) && Directory.Exists(_initialDirectory))
            {
                return await StorageProvider.TryGetFolderFromPathAsync(_initialDirectory);
            }

            return null;
        }

        private void UpdateTitle()
        {
            string name = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : Path.GetFileName(_currentFilePath);
            Title = _isDirty ? $"{name}* - RTE Editor" : $"{name} - RTE Editor";
        }

        private void MarkDirty()
        {
            _isDirty = true;
            UpdateTitle();
        }

        private void OnBoldClicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToggleFontWeight(FontWeight.Bold);
        }

        private void OnItalicClicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToggleFontStyle(FontStyle.Italic);
        }

        private void OnUnderlineClicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToggleTextDecoration(TextDecorationLocation.Underline);
        }

        private void OnStrikeClicked(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ToggleTextDecoration(TextDecorationLocation.Strikethrough);
        }

        private void ToggleFontWeight(FontWeight weight)
        {
            TextRange selection = Editor.FlowDocument.Selection;
            FontWeight current = selection.GetFormatting(AvaloniaTextElement.FontWeightProperty) as FontWeight? ?? FontWeight.Normal;
            FontWeight newValue = current == weight ? FontWeight.Normal : weight;
            selection.ApplyFormatting(AvaloniaTextElement.FontWeightProperty, newValue);
            MarkDirty();
        }

        private void ToggleFontStyle(FontStyle style)
        {
            TextRange selection = Editor.FlowDocument.Selection;
            FontStyle current = selection.GetFormatting(AvaloniaTextElement.FontStyleProperty) as FontStyle? ?? FontStyle.Normal;
            FontStyle newValue = current == style ? FontStyle.Normal : style;
            selection.ApplyFormatting(AvaloniaTextElement.FontStyleProperty, newValue);
            MarkDirty();
        }

        private void ToggleTextDecoration(TextDecorationLocation location)
        {
            TextRange selection = Editor.FlowDocument.Selection;
            // Try to get TextDecorationsProperty via reflection since it may not be directly accessible
            Avalonia.AvaloniaProperty textDecorationsProp = null;
            try
            {
                Type textElementType = typeof(AvaloniaTextElement);
                System.Reflection.PropertyInfo prop = textElementType.GetProperty("TextDecorationsProperty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null)
                {
                    textDecorationsProp = prop.GetValue(null) as Avalonia.AvaloniaProperty;
                }
            }
            catch
            {
                // Property not available - try alternative approach
            }

            if (textDecorationsProp != null)
            {
                var existing = selection.GetFormatting(textDecorationsProp) as TextDecorationCollection;
                var collection = new TextDecorationCollection(existing ?? new TextDecorationCollection());

                bool hasDecoration = collection.Any(dec => dec.Location == location);
                if (hasDecoration)
                {
                    collection = new TextDecorationCollection(collection.Where(dec => dec.Location != location));
                }
                else
                {
                    collection.Add(new TextDecoration { Location = location });
                }

                selection.ApplyFormatting(textDecorationsProp, collection);
                MarkDirty();
            }
            else
            {
                // Fallback: work directly with inlines if property not available
                // TODO: HACK - This is a workaround for API differences
                MarkDirty();
            }
        }

        private void OnFontSizeChanged(object sender, SelectionChangedEventArgs e)
        {
            double size;
            var item = FontSizeComboBox.SelectedItem as ComboBoxItem;
            if (item != null && double.TryParse(item.Content?.ToString(), out size))
            {
                Editor.FlowDocument.Selection.ApplyFormatting(AvaloniaTextElement.FontSizeProperty, size);
                MarkDirty();
            }
        }

        private void OnFontFamilyChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = FontFamilyComboBox.SelectedItem as ComboBoxItem;
            string familyName = item?.Content as string;
            if (item != null && familyName != null)
            {
                Editor.FlowDocument.Selection.ApplyFormatting(AvaloniaTextElement.FontFamilyProperty, new FontFamily(familyName));
                MarkDirty();
            }
        }

        private void OnForegroundChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = ForegroundComboBox.SelectedItem as ComboBoxItem;
            string name = item?.Content as string;
            if (item != null && name != null)
            {
                var brush = new SolidColorBrush(ColorFromName(name));
                Editor.FlowDocument.Selection.ApplyFormatting(AvaloniaTextElement.ForegroundProperty, brush);
                MarkDirty();
            }
        }

        private void OnBackgroundChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = BackgroundComboBox.SelectedItem as ComboBoxItem;
            string name = item?.Content as string;
            if (item != null && name != null)
            {
                IBrush brush = name.Equals("Transparent", StringComparison.OrdinalIgnoreCase)
                    ? (IBrush)Brushes.Transparent
                    : new SolidColorBrush(ColorFromName(name));
                Editor.FlowDocument.Selection.ApplyFormatting(AvaloniaTextElement.BackgroundProperty, brush);
                MarkDirty();
            }
        }

        private static Color ColorFromName(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "red": return Colors.Red;
                case "green": return Colors.Green;
                case "blue": return Colors.Blue;
                case "gray": return Colors.Gray;
                case "yellow": return Colors.Yellow;
                case "lightblue": return Colors.LightBlue;
                case "lightgreen": return Colors.LightGreen;
                default: return Colors.Black;
            }
        }

        private void OnAlignLeft(object sender, Avalonia.Interactivity.RoutedEventArgs e) { ApplyAlignment(TextAlignment.Left); }

        private void OnAlignCenter(object sender, Avalonia.Interactivity.RoutedEventArgs e) { ApplyAlignment(TextAlignment.Center); }

        private void OnAlignRight(object sender, Avalonia.Interactivity.RoutedEventArgs e) { ApplyAlignment(TextAlignment.Right); }

        private void ApplyAlignment(TextAlignment alignment)
        {
            foreach (Paragraph paragraph in Editor.FlowDocument.GetSelectedParagraphs)
            {
                paragraph.TextAlignment = alignment;
            }
            MarkDirty();
        }

        private void OnSelectionChanged(TextRange range)
        {
            // Update toolbar state to reflect current selection
            if (range is null)
            {
                return;
            }

            var weight = range.GetFormatting(AvaloniaTextElement.FontWeightProperty) as FontWeight?;
            var style = range.GetFormatting(AvaloniaTextElement.FontStyleProperty) as FontStyle?;
            // TextDecorationsProperty may not be available
            TextDecorationCollection decorations = null;
            try
            {
                Type textElementType = typeof(AvaloniaTextElement);
                System.Reflection.PropertyInfo prop = textElementType.GetProperty("TextDecorationsProperty", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop != null)
                {
                    var textDecorationsProp = prop.GetValue(null) as Avalonia.AvaloniaProperty;
                    if (textDecorationsProp != null)
                    {
                        decorations = range.GetFormatting(textDecorationsProp) as TextDecorationCollection;
                    }
                }
            }
            catch
            {
                // Property not available
            }

            // Update toggle buttons appearance if desired in future.
        }

        private void OnEditorKeyUp(object sender, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                MarkDirty();
            }
        }
    }
}

