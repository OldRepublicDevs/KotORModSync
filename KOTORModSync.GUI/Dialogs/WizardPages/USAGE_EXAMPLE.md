# Installation Wizard Usage Example

## Quick Start

### Using the Wizard in Your Code

```csharp
using KOTORModSync.Dialogs;

// In your MainWindow or any UI class:
public async Task LaunchInstallWizard()
{
    // Ensure MainConfig is initialized
    if (MainConfig.AllComponents == null || !MainConfig.AllComponents.Any())
    {
        await InformationDialog.ShowInformationDialogAsync(
            this,
            "Please load a mod build file first."
        );
        return;
    }

    // Launch the wizard
    await StartInstallWizardAsync();
}
```

### Button Click Integration

If you want to add a "Start Wizard" button to your UI:

```xml
<!-- In your XAML file: -->
<Button 
    Content="Start Installation Wizard" 
    Click="StartWizardButton_Click" />
```

```csharp
// In your code-behind:
private void StartWizardButton_Click(object sender, RoutedEventArgs e)
{
    _ = StartInstallWizardAsync();
}
```

## Toggling Between Old and New Flow

### Option 1: Keep Both (Recommended)

You can keep both installation flows and let users choose:

```xml
<StackPanel Orientation="Horizontal" Spacing="8">
    <Button Content="Classic Install" Click="StartInstall_Click" />
    <Button Content="Guided Wizard" Click="StartInstallWizard_Click" />
</StackPanel>
```

### Option 2: Replace Old Flow

Simply replace your existing install button's click handler:

```xml
<!-- Change this: -->
<Button Content="Start Installation" Click="StartInstall_Click" />

<!-- To this: -->
<Button Content="Start Installation" Click="StartInstallWizard_Click" />
```

### Option 3: Settings Toggle

Add a setting to let users choose their preferred installation method:

```csharp
// In AppSettings.cs:
public bool UseWizardInstall { get; set; } = true;

// In MainWindow:
private void Install_Click(object sender, RoutedEventArgs e)
{
    if (Settings.UseWizardInstall)
        _ = StartInstallWizardAsync();
    else
        _ = StartInstall_Click(sender, e);
}
```

## Customizing the Wizard

### Adding a Custom Page

1. Create a new class implementing `IWizardPage`:

```csharp
public class MyCustomPage : IWizardPage
{
    public string Title => "My Custom Step";
    public string Subtitle => "Additional configuration";
    public Control Content { get; }
    public bool CanNavigateBack => true;
    public bool CanNavigateForward => true;
    public bool CanCancel => true;

    public MyCustomPage()
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock 
        { 
            Text = "Custom content here" 
        });
        Content = panel;
    }

    public Task OnNavigatedToAsync(CancellationToken cancellationToken)
    {
        // Initialize page
        return Task.CompletedTask;
    }

    public Task OnNavigatingFromAsync(CancellationToken cancellationToken)
    {
        // Save state
        return Task.CompletedTask;
    }

    public Task<(bool isValid, string errorMessage)> ValidateAsync(CancellationToken cancellationToken)
    {
        // Validate user input
        return Task.FromResult((true, (string)null));
    }
}
```

2. Add it to the wizard in `InstallWizardDialog.cs`:

```csharp
private void InitializePages()
{
    _pages.Add(new WelcomePage());
    _pages.Add(new MyCustomPage()); // Your custom page
    _pages.Add(new SetupPage(_mainConfig));
    // ... rest of pages
}
```

### Removing Conditional Pages

If you don't want certain pages, just comment them out in `InitializePages()`:

```csharp
// Don't show BeforeContent page:
// if (!string.IsNullOrWhiteSpace(_mainConfig.preambleContent))
// {
//     _pages.Add(new BeforeContentPage(_mainConfig.preambleContent));
// }
```

### Changing Page Order

Simply rearrange the `_pages.Add()` calls in `InitializePages()`:

```csharp
// Move validation earlier:
_pages.Add(new WelcomePage());
_pages.Add(new ValidatePage(_allComponents, _mainConfig)); // Earlier
_pages.Add(new SetupPage(_mainConfig));
// ...
```

## Advanced Usage

### Accessing Wizard State

```csharp
var wizard = new InstallWizardDialog(MainConfigInstance, MainConfig.AllComponents);
bool? result = await wizard.ShowDialog<bool?>(this);

if (wizard.InstallationCompleted)
{
    // Installation finished successfully
    Console.WriteLine("Installation completed!");
}
else if (wizard.InstallationCancelled)
{
    // User cancelled
    Console.WriteLine("Installation was cancelled");
}
```

### Pre-selecting Mods

Before launching the wizard, you can pre-select mods:

```csharp
// Select all mods by default
foreach (var mod in MainConfig.AllComponents)
{
    mod.IsSelected = true;
}

// Or select specific ones
var paragonMod = MainConfig.AllComponents.FirstOrDefault(m => m.Name == "Paragon");
if (paragonMod != null)
    paragonMod.IsSelected = true;

// Then launch wizard
await StartInstallWizardAsync();
```

### Custom Validation Logic

Extend the `ValidatePage` with custom checks:

```csharp
// In ValidatePage.cs, add to RunValidation():

// Custom check: disk space
var gameDir = new DirectoryInfo(_mainConfig.destinationPathFullName);
var drive = new DriveInfo(gameDir.Root.FullName);
if (drive.AvailableFreeSpace < 5_000_000_000) // 5GB
{
    AddResult("⚠️ Disk Space", 
        $"Low disk space: {drive.AvailableFreeSpace / 1_000_000_000}GB available", 
        false);
    warningCount++;
}
```

## Telemetry Integration

The wizard automatically logs telemetry events:

- `installation.wizard.started` - When wizard opens
- `installation.wizard.completed` - When wizard finishes
  - `success: true/false` - Whether installation succeeded
  - `cancelled: true/false` - Whether user cancelled

You can add custom events:

```csharp
// In your custom page:
_telemetryService?.RecordEvent("wizard.custom_page.action", new Dictionary<string, object>
{
    ["action_type"] = "button_clicked",
    ["value"] = someValue
});
```

## Troubleshooting

### Wizard doesn't open

Check that:
- `MainConfig.AllComponents` is populated
- `MainConfig.sourcePath` and `destinationPath` are set
- You're calling it from the UI thread

### Pages appear in wrong order

- Verify `InitializePages()` order
- Check conditional logic (some pages may be skipped)

### Installation doesn't start

- Ensure `InstallingPage.OnNavigatedToAsync()` is called
- Check that mods are selected (`IsSelected = true`)
- Verify `InstallationService` is accessible

### Widescreen pages don't appear

- Widescreen pages only appear if `WidescreenOnly` mods exist
- They're added dynamically in `AddWidescreenPages()`
- Check `MainConfig.widescreenWarningContent` is populated

## Best Practices

1. **Always validate before launching**: Check MainConfig and mod list
2. **Handle cancellation gracefully**: Use the cancellation token
3. **Log errors**: The wizard logs to the standard Logger
4. **Test conditional flows**: Test with/without widescreen mods, Aspyr content, etc.
5. **Keep pages focused**: Each page should have one clear purpose
6. **Validate thoroughly**: Use `ValidateAsync()` to prevent bad installations

## Complete Example

Here's a full integration example:

```csharp
// In MainWindow.axaml.cs
using KOTORModSync.Dialogs;

public partial class MainWindow : Window
{
    // ... existing code ...

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Pre-flight checks
            if (MainConfig.AllComponents == null || !MainConfig.AllComponents.Any())
            {
                await InformationDialog.ShowInformationDialogAsync(
                    this,
                    "No mods loaded. Please load a mod build file first."
                );
                return;
            }

            if (string.IsNullOrEmpty(MainConfig.sourcePathFullName))
            {
                await InformationDialog.ShowInformationDialogAsync(
                    this,
                    "Please configure your mod directory first."
                );
                return;
            }

            // Launch wizard (from MainWindowWizardExtensions.cs)
            await StartInstallWizardAsync();

            // Post-installation
            await UpdateStepProgress();
            await RefreshComponentListAsync();
        }
        catch (Exception ex)
        {
            await Logger.LogExceptionAsync(ex, "Error launching installation wizard");
            await InformationDialog.ShowInformationDialogAsync(
                this,
                $"Failed to start installation wizard: {ex.Message}"
            );
        }
    }
}
```

For more information, see `WIZARD_README.md`.

