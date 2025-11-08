# KOTORModSync Installation Wizard

## Overview

This directory contains the **Inno Setup-style Installation Wizard** for KOTORModSync. This wizard provides a modern, guided installation experience with 15 distinct pages that walk users through the entire mod installation process.

## Architecture

### Main Components

1. **InstallWizardDialog.axaml/.cs** - Main wizard window with navigation controls
2. **WizardPages/** - Individual page implementations (15 pages total)
3. **MainWindowWizardExtensions.cs** - Integration with existing MainWindow

### Wizard Flow

The wizard follows this 15-step process:

1. **Welcome** - Introduction and overview
2. **BeforeContent** (conditional) - Preamble content from MainConfig
3. **Setup** - Configure directories and load instruction file
4. **AspyrNotice** (conditional) - Aspyr-specific warnings for KOTOR2
5. **ModSelection** - Select mods to install with categorized list
6. **DownloadsExplain** - Kick off background downloads
7. **Validate** - Validate installation environment and dependencies
8. **InstallStart** - Final review before installation
9. **Installing** - Progress tracking with metrics (replaces ProgressWindow)
10. **BaseInstallComplete** - Results and statistics
11. **WidescreenNotice** (conditional) - Widescreen mod information
12. **WidescreenModSelection** (conditional) - Select widescreen mods
13. **WidescreenInstalling** (conditional) - Install widescreen mods
14. **WidescreenComplete** (conditional) - Widescreen results
15. **Finished** - Thank you and next steps

### Conditional Pages

Some pages only appear under certain conditions:

- **BeforeContent**: Only if `MainConfig.preambleContent` is not empty
- **AspyrNotice**: Only for KOTOR2/TSL with `aspyrExclusiveWarningContent`
- **Widescreen pages (11-14)**: Only if widescreen mods are detected after base install

## Usage

### Using the Wizard

To use the new wizard instead of the old installation flow:

```csharp
// In MainWindow or any UI code:
await StartInstallWizardAsync();

// Or from a button click event:
StartInstallWizard_Click(sender, e);
```

### Reverting to Old Flow

The wizard is completely **non-invasive**. The original `StartInstall_Click` method remains unchanged. To revert:

1. Delete `MainWindowWizardExtensions.cs`
2. Delete `Dialogs/InstallWizardDialog.axaml` and `.cs`
3. Delete `Dialogs/WizardPages/` directory
4. Use the original `StartInstall_Click` button event

## Page Interface

All pages implement `IWizardPage`:

```csharp
public interface IWizardPage
{
    string Title { get; }
    string Subtitle { get; }
    Control Content { get; }
    bool CanNavigateBack { get; }
    bool CanNavigateForward { get; }
    bool CanCancel { get; }
    
    Task OnNavigatedToAsync(CancellationToken cancellationToken);
    Task OnNavigatingFromAsync(CancellationToken cancellationToken);
    Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken);
}
```

## Features

### Progress Tracking (Installing Page)

The `InstallingPage` replaces the old `ProgressWindow` with integrated tracking:

- **Main progress bar** - Overall installation progress
- **Current mod progress** - Per-mod progress indicator
- **Statistics**:
  - Elapsed time
  - Remaining time (est.)
  - Installation rate (mods/min)
  - Warnings count
  - Errors count
- **Live updates** - Real-time status updates during installation
- **Cancellation support** - Users can stop installation gracefully

### Validation

The `ValidatePage` performs comprehensive checks:

- Environment validation (directories, permissions)
- Dependency checking
- Install order verification
- Circular dependency detection
- Blocks navigation on critical errors
- Allows proceeding with warnings only

### Navigation

- **Back/Next buttons** - Navigate between pages
- **Progress indicator** - Shows current step (e.g., "Step 5 of 15")
- **Progress bar** - Visual progress through wizard
- **Conditional navigation** - Pages can control if Back/Next are enabled
- **Validation gates** - Pages can block navigation until requirements met

## Future Enhancements

### Planned Features

1. **Mod Image Slideshow** (TODO in InstallingPage)
   - Show mod screenshots during installation
   - Rotate through images while installing
   - Add to placeholder area in InstallingPage line ~200

2. **Checkpoint Animations** (TODO)
   - Visual feedback when checkpoints are created
   - Progress indicators for checkpoint system
   - Rollback UI integration

3. **Download Progress Integration**
   - Show live download status in DownloadsExplainPage
   - "Show Downloads" button throughout wizard
   - Background download management

4. **Enhanced Statistics**
   - Actual checkpoint count in BaseInstallCompletePage
   - Installation time breakdown
   - Storage space used/freed
   - Export installation report

## File Structure

```
Dialogs/
├── InstallWizardDialog.axaml          # Main wizard UI
├── InstallWizardDialog.axaml.cs       # Wizard logic & navigation
├── WIZARD_README.md                   # This file
└── WizardPages/
    ├── SetupPage.cs                   # LoadInstructionPage (Step 1), ModDirectoryPage (Step 4), GameDirectoryPage (Step 5)
    ├── WelcomePage.cs                 # Step 2
    ├── BeforeContentPage.cs           # Step 3 (conditional)
    ├── AspyrNoticePage.cs             # Step 6 (conditional)
    ├── ModSelectionPage.cs            # Step 7
    ├── DownloadsExplainPage.cs        # Step 8
    ├── ValidatePage.cs                # Step 9
    ├── InstallStartPage.cs            # Step 10
    ├── InstallingPage.cs              # Step 11 (core install logic)
    ├── BaseInstallCompletePage.cs     # Step 12
    ├── WidescreenNoticePage.cs        # Step 13 (conditional)
    ├── WidescreenModSelectionPage.cs  # Step 14 (conditional)
    ├── WidescreenInstallingPage.cs    # Step 15 (conditional)
    ├── WidescreenCompletePage.cs      # Step 16 (conditional)
    └── FinishedPage.cs                # Final step

MainWindowWizardExtensions.cs          # Integration (can be removed safely)
```

## Testing

### Manual Testing Checklist

- [ ] Welcome page displays correctly
- [ ] Navigation (Back/Next) works on all pages
- [ ] Conditional pages appear only when appropriate
- [ ] Mod selection checkboxes work
- [ ] Validation catches errors
- [ ] Installation progress updates in real-time
- [ ] Statistics (time, rate, warnings) display correctly
- [ ] Widescreen flow only appears when needed
- [ ] Cancellation works at all stages
- [ ] Finish button closes wizard properly

### Integration Testing

- [ ] Wizard integrates with existing MainConfig
- [ ] Installation actually installs mods correctly
- [ ] Checkpoints are created properly
- [ ] Old installation flow still works
- [ ] No conflicts between wizard and existing code

## Troubleshooting

### Common Issues

**Issue**: Pages don't navigate
- Check `CanNavigateForward` property
- Verify `ValidateAsync()` returns true
- Check for exceptions in `OnNavigatedToAsync`

**Issue**: Installation doesn't start
- Verify `InstallingPage.OnNavigatedToAsync` is called
- Check `InstallationService.InstallSingleComponentAsync` is available
- Verify mods are actually selected

**Issue**: Conditional pages not appearing
- Check MainConfig properties (preambleContent, aspyrExclusiveWarningContent, etc.)
- Verify `MainConfig.TargetGame` is set correctly for Aspyr notice
- Check widescreen mod detection logic

## Contributing

When adding new pages:

1. Create new class implementing `IWizardPage`
2. Add to `InitializePages()` in InstallWizardDialog.cs
3. Set appropriate `CanNavigateBack`/`CanNavigateForward`/`CanCancel` values
4. Implement validation logic in `ValidateAsync()`
5. Add page content in constructor
6. Handle page activation in `OnNavigatedToAsync()`
7. Update this README

## License

Copyright 2021-2025 KOTORModSync
Licensed under the Business Source License 1.1 (BSL 1.1).
See LICENSE.txt file in the project root for full license information.

