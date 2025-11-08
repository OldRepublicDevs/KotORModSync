# Wizard Integration in MainWindow

## Overview

The wizard interface from `InstallWizardDialog` has been integrated directly into `MainWindow` to provide a seamless installation experience for non-editor users.

## How It Works

### 1. WizardHostControl

A new `WizardHostControl` (UserControl) encapsulates all wizard logic:

- **Location**: `KOTORModSync.GUI/Controls/WizardHostControl.axaml[.cs]`
- **Purpose**: Hosts wizard pages and manages navigation
- **Events**:
  - `WizardCompleted` - fired when user finishes the wizard
  - `WizardCancelled` - fired when user cancels the wizard

### 2. MainWindow Integration

The wizard is embedded directly in `MainWindow.axaml`:

```xml
<Panel>
    <!-- Wizard Mode (shown when EditorMode is false and wizard is active) -->
    <controls:WizardHostControl
        x:Name="WizardHost"
        IsVisible="False" />

    <!-- Normal Mode (shown when not in wizard mode) -->
    <Grid x:Name="MainGrid" ... >
        <!-- Existing MainWindow content -->
    </Grid>
</Panel>
```

### 3. Activation Logic

The wizard activates automatically when:

1. `EditorMode == false`
2. An instruction file is loaded successfully
3. The loaded file contains components

**Code Location**: `MainWindow.axaml.cs` → `LoadInstructionFileAsync()`

```csharp
// If not in Editor Mode, activate (or refresh) wizard mode
if (!EditorMode && WizardMode)
{
    EnterWizardMode(forceRefresh: true);
}
```

### 4. Wizard Mode Management

Four key methods manage wizard state:

- **`EnterWizardMode(bool forceRefresh = false)`**: Shows or refreshes the wizard interface
- **`ExitWizardMode()`**: Hides wizard, shows main grid
- **`OnWizardCompleted()`**: Called when wizard finishes successfully
- **`OnWizardCancelled()`**: Called when user cancels wizard

### 5. Editor Mode Override

If user toggles `EditorMode` to `true` while in wizard mode, the wizard automatically exits and shows the full editor interface.

## Benefits

1. **No separate dialog** - MainWindow transforms into wizard interface
2. **Minimal MainWindow.axaml.cs code** - Only ~70 lines added
3. **Reusable logic** - All wizard logic in `WizardHostControl`
4. **Clean separation** - Wizard and editor interfaces don't interfere
5. **User-friendly** - Seamless transition between modes

## User Experience

### For End Users (EditorMode = false)

1. Load instruction file
2. MainWindow automatically becomes wizard interface
3. Follow wizard steps to install mods
4. Upon completion, wizard exits back to normal view

### For Developers (EditorMode = true)

1. Wizard never activates automatically
2. Full editor interface remains visible
3. Can toggle EditorMode at any time to switch modes

## Code Organization

```bash
KOTORModSync.GUI/
├── Controls/
│   ├── WizardHostControl.axaml          # Wizard UI layout
│   ├── WizardHostControl.axaml.cs       # Wizard logic (copied from InstallWizardDialog)
│   └── WIZARD_INTEGRATION.md            # This file
├── Dialogs/
│   ├── InstallWizardDialog.axaml[.cs]   # Original dialog (still usable if needed)
│   └── WizardPages/                     # Individual wizard page implementations
└── MainWindow.axaml[.cs]                # Main application window
    └── Added: ~70 lines for wizard mode management
```

## Future Enhancements

Potential improvements:

- Add wizard progress persistence (resume from last step)
- Allow manual wizard activation via menu option
- Add wizard reset/restart functionality
- Smooth transition animations between modes
