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
using Avalonia.Markup.Xaml;

using JetBrains.Annotations;

using KOTORModSync.Core;
using KOTORModSync.Core.Services;

namespace KOTORModSync.Dialogs
{
    public partial class DependencyResolutionErrorDialog : Window
    {
        private bool _mouseDownForWindowMoving;
        private PointerPoint _originalPoint;

        public List<DependencyError> Errors { get; set; } = new List<DependencyError>();
        public List<ModComponent> Components { get; set; } = new List<ModComponent>();
        public DependencyResolutionAction SelectedAction { get; set; } = DependencyResolutionAction.Cancel;

        public DependencyResolutionErrorDialog()
        {
            InitializeComponent();
            WireUpEvents();
            // Apply current theme
            ThemeManager.ApplyCurrentToWindow(this);
        }

        public DependencyResolutionErrorDialog(IReadOnlyList<DependencyError> errors, IReadOnlyList<ModComponent> components) : this()
        {
            Errors = errors?.ToList() ?? new List<DependencyError>();
            Components = components?.ToList() ?? new List<ModComponent>();
            LoadErrors();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void WireUpEvents()
        {
            PointerPressed += InputElement_OnPointerPressed;
            PointerMoved += InputElement_OnPointerMoved;
            PointerReleased += InputElement_OnPointerReleased;
            PointerExited += InputElement_OnPointerReleased;
        }

        private void LoadErrors()
        {
            ItemsRepeater errorsRepeater = this.FindControl<ItemsRepeater>("ErrorsRepeater");
            if (errorsRepeater != null)
            {
                errorsRepeater.ItemsSource = Errors;
            }
        }

        [UsedImplicitly]
        private async void AutoFix_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SelectedAction = DependencyResolutionAction.AutoFix;
                Close();
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex);
            }
        }

        [UsedImplicitly]
        private async void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SelectedAction = DependencyResolutionAction.ClearAll;
                Close();
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex);
            }
        }

        [UsedImplicitly]
        private async void LoadBestPossible_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SelectedAction = DependencyResolutionAction.IgnoreErrors;
                Close();
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex);
            }
        }

        [UsedImplicitly]
        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SelectedAction = DependencyResolutionAction.Cancel;
                Close();
            }
            catch (Exception ex)
            {
                await Logger.LogExceptionAsync(ex);
            }
        }

        private void InputElement_OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _mouseDownForWindowMoving = true;
                _originalPoint = e.GetCurrentPoint(this);
            }
        }

        private void InputElement_OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_mouseDownForWindowMoving)
            {
                return;
            }

            PointerPoint currentPoint = e.GetCurrentPoint(this);
            double deltaX = currentPoint.Position.X - _originalPoint.Position.X;
            double deltaY = currentPoint.Position.Y - _originalPoint.Position.Y;

            Position = new PixelPoint(
                (int)(Position.X + deltaX),
                (int)(Position.Y + deltaY)
            );
        }

        private void InputElement_OnPointerReleased(object sender, PointerEventArgs e)
        {
            _mouseDownForWindowMoving = false;
        }

        public static async Task<DependencyResolutionAction> ShowErrorDialogAsync(
            [NotNull] Window parentWindow,
            [NotNull] IReadOnlyList<DependencyError> errors,
            [NotNull] IReadOnlyList<ModComponent> components)
        {
            var dialog = new DependencyResolutionErrorDialog(errors, components);
            await dialog.ShowDialog(parentWindow);
            return dialog.SelectedAction;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0048:File name must match type name", Justification = "<Pending>")]
    public enum DependencyResolutionAction
    {
        Cancel,
        AutoFix,
        ClearAll,
        IgnoreErrors,
    }
}
