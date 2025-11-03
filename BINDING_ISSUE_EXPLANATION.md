# Avalonia Binding Issue: Property Changes Not Updating UI Immediately

## The Problem

When you modify properties like `Description` in the EditorTab, the SummaryTab doesn't update immediately when you switch to it, even though:

- `ModComponent` properly implements `INotifyPropertyChanged`
- Properties fire `PropertyChanged` events correctly
- Both tabs bind to the same `CurrentComponent` property

## Root Cause Analysis

### How Avalonia Binding Works

Avalonia's binding system follows these principles:

1. **Direct Property Binding**: When you bind `{Binding Description}`, Avalonia:
   - Resolves the binding path against the current `DataContext`
   - Subscribes to `PropertyChanged` events on the DataContext object
   - Updates the UI when the DataContext's properties change

2. **DataContext Chain**: Bindings like `{Binding CurrentComponent}` resolve by:
   - Starting from the control's `DataContext`
   - Following the property path (e.g., `this.CurrentComponent`)
   - Setting the resolved object as the new DataContext for child elements

3. **Property Change Notification**: Avalonia listens for `INotifyPropertyChanged.PropertyChanged` events on:
   - The **direct DataContext object** (the object bound to `DataContext="{Binding CurrentComponent}"`)
   - NOT on intermediate properties in the binding chain

### The Architecture Issue

Your code structure:

- `EditorTab` and `SummaryTab` set `DataContext = this` (the UserControl itself)
- They bind `DataContext="{Binding CurrentComponent}"` in XAML
- `CurrentComponent` returns `MainConfig.CurrentComponent` (a ModComponent instance)
- `ModComponent` implements `INotifyPropertyChanged` correctly

**The Problem**: When `ModComponent.Description` changes:

1. `ModComponent.PropertyChanged` fires ✅
2. But the binding system's subscription might not be active if:
   - The DataContext wasn't properly established initially
   - The binding system didn't subscribe to PropertyChanged on the DataContext object
   - There's a timing issue where the subscription happens after the tab is created

### Why This Happens

Avalonia's binding engine subscribes to `PropertyChanged` events when:

1. The binding is **first established** (when the control is loaded/visible)
2. The DataContext is **set or changed**

However, if a tab isn't visible when `CurrentComponent` is set, the bindings may not be fully initialized. When you switch tabs:

- The SummaryTab's bindings are created fresh
- They read the current value (stale)
- But they should subscribe to PropertyChanged going forward

The issue is that bindings should automatically subscribe, but there might be a disconnect because:

- The `CurrentComponent` property getter returns `MainConfig.CurrentComponent` directly
- This creates a binding that reads a static value, not an observable

## The Solution

You need to ensure that when `ModComponent` properties change, the binding system is notified. There are two approaches:

### Solution 1: Subscribe to ModComponent.PropertyChanged in the Tab Controls

Subscribe to PropertyChanged events on the ModComponent when CurrentComponent changes:

```csharp
// In EditorTab and SummaryTab
private ModComponent _subscribedComponent;

private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
{
    if (e.Property == CurrentComponentProperty)
    {
        // Unsubscribe from old component
        if (_subscribedComponent != null)
        {
            _subscribedComponent.PropertyChanged -= OnCurrentComponentPropertyChanged;
        }
        
        // Subscribe to new component
        _subscribedComponent = CurrentComponent;
        if (_subscribedComponent != null)
        {
            _subscribedComponent.PropertyChanged += OnCurrentComponentPropertyChanged;
        }
        
        // Force DataContext update
        this.GetObservable(DataContextProperty).Subscribe(_ => { });
    }
}

private void OnCurrentComponentPropertyChanged(object sender, PropertyChangedEventArgs e)
{
    // Trigger binding refresh by notifying that CurrentComponent property changed
    // (even though the object reference is the same, properties within it changed)
    RaisePropertyChanged(CurrentComponentProperty);
}
```

### Solution 2: Use Observable Pattern (Recommended)

Instead of binding to a static getter, create an observable that emits changes:

```csharp
// In EditorTab and SummaryTab
private IDisposable _currentComponentSubscription;

private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
{
    if (e.Property == CurrentComponentProperty)
    {
        _currentComponentSubscription?.Dispose();
        
        if (CurrentComponent != null)
        {
            // Subscribe to all property changes on the component
            _currentComponentSubscription = CurrentComponent
                .GetType()
                .GetEvent(nameof(INotifyPropertyChanged.PropertyChanged))
                // Actually, better approach:
                Observable.FromEvent<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    h => CurrentComponent.PropertyChanged += h,
                    h => CurrentComponent.PropertyChanged -= h)
                .Subscribe(_ => 
                {
                    // Force DataContext to refresh by raising CurrentComponent property changed
                    Dispatcher.UIThread.Post(() => 
                        RaisePropertyChanged(CurrentComponentProperty));
                });
        }
    }
}
```

### Solution 3: Direct DataContext Binding (Simplest)

The cleanest solution is to ensure the DataContext is set directly to the ModComponent and stays synchronized:

```csharp
// In EditorTab and SummaryTab OnPropertyChanged
private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
{
    if (e.Property == CurrentComponentProperty)
    {
        // Find the StackPanel/ScrollViewer that has DataContext="{Binding CurrentComponent}"
        // and force its DataContext to update
        var stackPanel = this.FindControl<StackPanel>("GuiEditGrid"); // or appropriate name
        if (stackPanel != null)
        {
            stackPanel.DataContext = CurrentComponent;
        }
        
        // Also ensure child controls refresh
        this.InvalidateVisual();
    }
}
```

Actually, the BEST solution is to handle PropertyChanged events on the ModComponent directly:

```csharp
private ModComponent _trackedComponent;

private void OnPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
{
    if (e.Property == CurrentComponentProperty)
    {
        // Unsubscribe from previous component
        if (_trackedComponent != null)
        {
            _trackedComponent.PropertyChanged -= OnModComponentPropertyChanged;
        }
        
        // Track and subscribe to new component
        _trackedComponent = CurrentComponent;
        if (_trackedComponent != null)
        {
            _trackedComponent.PropertyChanged += OnModComponentPropertyChanged;
        }
    }
}

private void OnModComponentPropertyChanged(object sender, PropertyChangedEventArgs e)
{
    // When any property on ModComponent changes, notify that CurrentComponent changed
    // This causes bindings to re-evaluate even though the object reference is the same
    Dispatcher.UIThread.Post(() =>
    {
        RaisePropertyChanged(CurrentComponentProperty);
    }, DispatcherPriority.DataBind);
}
```

## Key Insight

The fundamental issue is that Avalonia bindings to nested properties (like `Description` when bound via `DataContext="{Binding CurrentComponent}"`) DO subscribe to `PropertyChanged` on the DataContext object. However, your `CurrentComponent` property getter returns `MainConfig.CurrentComponent` directly, which means:

1. The binding resolves to the ModComponent instance ✅
2. The binding should subscribe to ModComponent.PropertyChanged ✅
3. BUT: If the tab wasn't visible when CurrentComponent was set, the bindings might not be fully initialized
4. AND: When you switch tabs, the bindings read the current value but may not have been subscribed yet

The fix ensures that property changes on ModComponent trigger a refresh of the CurrentComponent property binding, causing all dependent bindings to re-evaluate.
