// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

using JetBrains.Annotations;

namespace KOTORModSync.Dialogs
{
    public partial class OptionsDialog : Window
    {
        public static readonly AvaloniaProperty OptionsListProperty =
            AvaloniaProperty.Register<OptionsDialog, List<string>>(nameof(OptionsList));
        private bool _mouseDownForWindowMoving;
        private PointerPoint _originalPoint;

        public OptionsDialog()
        {
            InitializeComponent();
            OkButton.Click += OKButton_Click;

            PointerPressed += InputElement_OnPointerPressed;
            PointerMoved += InputElement_OnPointerMoved;
            PointerReleased += InputElement_OnPointerReleased;
            PointerExited += InputElement_OnPointerReleased;

            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);
        }

        [CanBeNull]
        public List<string> OptionsList
        {
            get => GetValue(OptionsListProperty) as List<string>;
            set => SetValue(OptionsListProperty, value);
        }

        private void OKButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e)
        {
            RadioButton selectedRadioButton = OptionStackPanel.Children.OfType<RadioButton>()
                .SingleOrDefault(rb => rb.IsChecked == true);

            if (selectedRadioButton != null)
            {
                string selectedOption = selectedRadioButton.Content?.ToString();
                OptionSelected?.Invoke(this, selectedOption);
            }

            Close();
        }

        private void CloseButton_Click([CanBeNull] object sender, [CanBeNull] RoutedEventArgs e) =>
            Close();

        public event EventHandler<string> OptionSelected;

        private void OnOpened([CanBeNull] object sender, [CanBeNull] EventArgs e)
        {
            if (OptionsList is null)
            {
                throw new NullReferenceException(nameof(OptionsList));
            }

            foreach (string option in OptionsList)
            {
                var radioButton = new RadioButton
                {
                    Content = option,
                    GroupName = "OptionsGroup",
                };
                OptionStackPanel.Children.Add(radioButton);
            }

            OptionStackPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            OptionStackPanel.Arrange(new Rect(OptionStackPanel.DesiredSize));

            Size actualSize = OptionStackPanel.Bounds.Size;

            const double horizontalPadding = 100;
            const double verticalPadding = 150;

            double contentWidth = actualSize.Width + 2 * horizontalPadding;
            double contentHeight = actualSize.Height + 2 * verticalPadding;

            Width = contentWidth;
            Height = contentHeight;

            InvalidateArrange();
            InvalidateMeasure();

            Screen screen = Screens.ScreenFromVisual(this);
            if (screen is null)
            {
                throw new NullReferenceException(nameof(screen));
            }

            double screenWidth = screen.Bounds.Width;
            double screenHeight = screen.Bounds.Height;
            double left = (screenWidth - contentWidth) / 2;
            double top = (screenHeight - contentHeight) / 2;
            Position = new PixelPoint((int)left, (int)top);
        }

        [ItemCanBeNull]
        public static async Task<string> ShowOptionsDialog(
            [CanBeNull] Window parentWindow,
            [CanBeNull] List<string> optionsList
        )
        {
            var tcs = new TaskCompletionSource<string>();

            await Dispatcher.UIThread.InvokeAsync(
                async () =>
                {
                    var optionsDialog = new OptionsDialog
                    {
                        OptionsList = optionsList,
                        Topmost = true,

                    };

                    optionsDialog.Closed += ClosedHandler;
                    optionsDialog.Opened += optionsDialog.OnOpened;

                    void ClosedHandler(object sender, EventArgs e)
                    {
                        optionsDialog.Closed -= ClosedHandler;
                        _ = tcs.TrySetResult(null);
                    }

                    optionsDialog.OptionSelected += (sender, option) => _ = tcs.TrySetResult(option);

                    if (!(parentWindow is null))
                    {
                        await optionsDialog.ShowDialog(parentWindow);
                    }
                }
            ).ConfigureAwait(true);

            return tcs is null ? throw new NullReferenceException(nameof(tcs)) : await tcs.Task;
        }

        private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_mouseDownForWindowMoving)
            {
                return;
            }

            PointerPoint currentPoint = e.GetCurrentPoint(this);
            Position = new PixelPoint(
                Position.X + (int)(currentPoint.Position.X - _originalPoint.Position.X),
                Position.Y + (int)(currentPoint.Position.Y - _originalPoint.Position.Y)
            );
        }

        private void InputElement_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (WindowState == WindowState.Maximized || WindowState == WindowState.FullScreen)
            {
                return;
            }

            if (ShouldIgnorePointerForWindowDrag(e))
            {
                return;
            }

            _mouseDownForWindowMoving = true;
            _originalPoint = e.GetCurrentPoint(this);
        }

        private void InputElement_OnPointerReleased(object sender, PointerEventArgs e) =>
            _mouseDownForWindowMoving = false;

        private bool ShouldIgnorePointerForWindowDrag(PointerEventArgs e)
        {

            if (!(e.Source is Visual source))
            {
                return false;
            }

            Visual current = source;
            while (current != null && current != this)
            {
                switch (current)
                {

                    case Button _:
                    case TextBox _:
                    case ComboBox _:
                    case ListBox _:
                    case MenuItem _:
                    case Menu _:
                    case Expander _:
                    case Slider _:
                    case TabControl _:
                    case TabItem _:
                    case ProgressBar _:
                    case ScrollViewer _:

                    case Control control when control.ContextMenu?.IsOpen == true:
                        return true;
                    case Control control when control.ContextFlyout?.IsOpen == true:
                        return true;
                    default:
                        current = current.GetVisualParent();
                        break;
                }
            }

            return false;
        }
    }
}
