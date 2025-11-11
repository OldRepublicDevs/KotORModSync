// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KOTORModSync.Controls
{
    public partial class LandingPageView : UserControl
    {
        public event EventHandler LoadInstructionsRequested;
        public event EventHandler CreateInstructionsRequested;
        public event EventHandler OpenSponsorPageRequested;
        public LandingPageView()
        {
            InitializeComponent();
            LoadInstructionButton.Click += OnLoadInstructionButtonClick;
            CreateInstructionsButton.Click += OnCreateInstructionsButtonClick;
            SponsorButton.Click += OnSponsorButtonClick;
        }

        public void UpdateState(
            bool instructionFileLoaded,
            string instructionFileName,
            bool editorModeEnabled)
        {
            InstructionStatusText.Text = instructionFileLoaded
                ? string.IsNullOrWhiteSpace(instructionFileName)
                    ? "An instruction file is loaded."
                    : $"Loaded file: {instructionFileName}"
                : "No instruction file loaded yet.";

            EditorStatusText.Text = editorModeEnabled
                ? "Editor mode is enabled."
                : "Editor mode is currently off.";
        }

        private void OnLoadInstructionButtonClick(object sender, RoutedEventArgs e)
        {
            LoadInstructionsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnCreateInstructionsButtonClick(object sender, RoutedEventArgs e)
        {
            CreateInstructionsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnSponsorButtonClick(object sender, RoutedEventArgs e)
        {
            OpenSponsorPageRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}

