# Utility Navigation Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the six-row expanded utility dock with three pinned destinations and a collapsible More tools group.

**Architecture:** Keep the existing navigation commands and collapsed rail unchanged. Add one transient observable property to `MainWindowViewModel`, bind it to an Avalonia `ToggleButton`, and use the same property to reveal a secondary-tools container above the pinned Dashboard, Alerts, and Settings rows.

**Tech Stack:** .NET, C#, Avalonia XAML, CommunityToolkit.Mvvm, xUnit, FluentAssertions

---

### Task 1: Add transient expansion state

**Files:**
- Modify: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs:130-160`
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs:220-265`

**Step 1: Write the failing state test**

Add a focused test near the other basic UI-state tests:

```csharp
[Fact]
public void MoreTools_OnCreation_IsCollapsed()
{
    // Arrange
    var preferences = new TestPreferencesService();

    // Act
    using var sut = CreateSut(preferences);

    // Assert
    sut.IsMoreToolsExpanded.Should().BeFalse();
}
```

**Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.ViewModels.MainWindowViewModelTests.MoreTools_OnCreation_IsCollapsed"
```

Expected: FAIL to compile because `MainWindowViewModel` does not expose `IsMoreToolsExpanded`.

**Step 3: Add the minimal view-model state**

Add the field beside the other transient UI booleans in `MainWindowViewModel`:

```csharp
[ObservableProperty] private bool _isMoreToolsExpanded;
```

Do not persist this property and do not add a command; two-way toggle binding is sufficient.

**Step 4: Run the focused test**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.ViewModels.MainWindowViewModelTests.MoreTools_OnCreation_IsCollapsed"
```

Expected: PASS.

**Step 5: Commit**

```bash
rtk git add BusLane/ViewModels/MainWindowViewModel.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs
rtk git commit -m "feat: add utility disclosure state"
```

### Task 2: Restructure the expanded utility dock

**Files:**
- Modify: `BusLane.Tests/Views/NavigationSidebarTests.cs:150-175`
- Modify: `BusLane/Views/Controls/NavigationSidebar.axaml:382-454`
- Modify: `BusLane/Styles/AppStyles.axaml:1838-1855`

**Step 1: Write failing structure tests**

Add helpers that extract the marked secondary-tools region, then add these assertions:

```csharp
[Fact]
public void NavigationSidebar_CollapsesSecondaryUtilitiesBehindMoreTools()
{
    // Arrange
    var xaml = File.ReadAllText(GetSidebarPath());

    // Act
    var secondaryTools = ExtractRegion(
        xaml,
        "<!-- More Tools Panel -->",
        "<!-- /More Tools Panel -->");

    // Assert
    xaml.Should().Contain("IsChecked=\"{Binding IsMoreToolsExpanded, Mode=TwoWay}\"");
    xaml.Should().Contain("AutomationProperties.Name=\"More tools\"");
    secondaryTools.Should().Contain("IsVisible=\"{Binding IsMoreToolsExpanded}\"");
    secondaryTools.Should().Contain("OpenLiveStreamCommand");
    secondaryTools.Should().Contain("OpenCorrelationExplorerCommand");
    secondaryTools.Should().Contain("OpenScheduledMessagesCommand");
}

[Fact]
public void NavigationSidebar_KeepsFrequentUtilitiesOutsideMoreTools()
{
    // Arrange
    var xaml = File.ReadAllText(GetSidebarPath());

    // Act
    var secondaryTools = ExtractRegion(
        xaml,
        "<!-- More Tools Panel -->",
        "<!-- /More Tools Panel -->");

    // Assert
    secondaryTools.Should().NotContain("OpenChartsCommand");
    secondaryTools.Should().NotContain("OpenAlertsCommand");
    secondaryTools.Should().NotContain("OpenSettingsCommand");
}
```

Add the test helper:

```csharp
private static string ExtractRegion(string text, string startMarker, string endMarker)
{
    var start = text.IndexOf(startMarker, StringComparison.Ordinal);
    var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);

    start.Should().BeGreaterThanOrEqualTo(0);
    end.Should().BeGreaterThan(start);

    return text[start..end];
}
```

**Step 2: Run the sidebar tests to verify they fail**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Views.NavigationSidebarTests"
```

Expected: FAIL because the More tools toggle, markers, and visibility binding do not exist.

**Step 3: Add the secondary-tools disclosure group**

In the existing `sidebar-utility-dock`, replace the Utilities heading and the first three permanent buttons with:

```xml
<!-- More Tools Panel -->
<Border Classes="sidebar-more-tools-panel"
        IsVisible="{Binding IsMoreToolsExpanded}">
    <StackPanel Spacing="2">
        <!-- Existing Live Stream, Correlation Explorer, and Scheduled Messages buttons. -->
    </StackPanel>
</Border>
<!-- /More Tools Panel -->

<ToggleButton Classes="subtle sidebar-more-tools-toggle"
              IsChecked="{Binding IsMoreToolsExpanded, Mode=TwoWay}"
              AutomationProperties.Name="More tools"
              HorizontalAlignment="Stretch">
    <Grid ColumnDefinitions="Auto,*,Auto" ColumnSpacing="10">
        <LucideIcon Grid.Column="0" Kind="LayoutGrid" Size="16"/>
        <TextBlock Grid.Column="1" Text="More tools" VerticalAlignment="Center"/>
        <LucideIcon Grid.Column="2"
                    Kind="ChevronDown"
                    Size="16"
                    Classes.expanded="{Binding IsMoreToolsExpanded}"/>
    </Grid>
</ToggleButton>

<Border Classes="sidebar-utility-divider"/>
```

Keep the existing Dashboard, Alerts, and Settings buttons immediately after the divider. Preserve their commands, icons, labels, and the Alerts count badge. Keep the existing collapsed-rail buttons untouched.

If Avalonia does not support binding a style class directly for chevron rotation, use two mutually exclusive icons instead:

```xml
<LucideIcon Grid.Column="2" Kind="ChevronDown" Size="16"
            IsVisible="{Binding !IsMoreToolsExpanded}"/>
<LucideIcon Grid.Column="2" Kind="ChevronUp" Size="16"
            IsVisible="{Binding IsMoreToolsExpanded}"/>
```

**Step 4: Add compact styles**

Extend the sidebar styles with the existing theme resources:

```xml
<Style Selector="Border.sidebar-more-tools-panel">
    <Setter Property="Background" Value="{DynamicResource SurfaceSubtle}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource BorderDefault}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="Padding" Value="4"/>
    <Setter Property="Margin" Value="0,0,0,4"/>
</Style>

<Style Selector="ToggleButton.sidebar-more-tools-toggle">
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="Padding" Value="12,10"/>
    <Setter Property="MinHeight" Value="44"/>
</Style>

<Style Selector="Border.sidebar-utility-divider">
    <Setter Property="Height" Value="1"/>
    <Setter Property="Background" Value="{DynamicResource BorderDefault}"/>
    <Setter Property="Margin" Value="0,4"/>
</Style>

<Style Selector="Button.sidebar-utility-button">
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="Padding" Value="12,8"/>
    <Setter Property="MinHeight" Value="44"/>
</Style>
```

Use an existing subtle surface resource that is already defined in `App.axaml`; do not introduce a one-off color.

**Step 5: Run the sidebar tests**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Views.NavigationSidebarTests"
```

Expected: PASS, including the existing scheduled-messages count assertion.

**Step 6: Run related navigation tests**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Views.NavigationSidebarTests|FullyQualifiedName~BusLane.Tests.Views.CorrelationExplorerViewTests"
```

Expected: PASS. The expanded and collapsed shortcuts remain present.

**Step 7: Commit**

```bash
rtk git add BusLane/Views/Controls/NavigationSidebar.axaml BusLane/Styles/AppStyles.axaml BusLane.Tests/Views/NavigationSidebarTests.cs
rtk git commit -m "feat: prioritize frequent sidebar utilities"
```

### Task 3: Verify behavior and visual fit

**Files:**
- Verify only; modify the Task 2 files only if verification reveals a defect

**Step 1: Build the application**

Run:

```bash
rtk dotnet build
```

Expected: PASS with no compiler or Avalonia XAML errors.

**Step 2: Run the full test suite**

Run:

```bash
rtk dotnet test
```

Expected: PASS.

**Step 3: Inspect the expanded sidebar manually**

Run:

```bash
rtk dotnet run --project BusLane/BusLane.csproj
```

Verify:

- More tools starts collapsed.
- Dashboard, Alerts, and Settings are always visible in the expanded sidebar.
- The alert badge remains aligned and readable with zero and non-zero counts.
- More tools expands and collapses with mouse and keyboard.
- Focus order matches visual order and focus indicators remain visible.
- The secondary panel does not overlap workspace content at the minimum supported window height.
- The collapsed rail still exposes all existing utility shortcuts.
- Light and dark themes retain readable text, borders, and interaction states.

**Step 4: Check the final diff**

Run:

```bash
rtk git diff --check
rtk git status --short
```

Expected: no whitespace errors and no unrelated files changed.

**Step 5: Commit verification fixes, if any**

If manual verification required a small adjustment:

```bash
rtk git add BusLane/Views/Controls/NavigationSidebar.axaml BusLane/Styles/AppStyles.axaml BusLane.Tests/Views/NavigationSidebarTests.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs
rtk git commit -m "fix: polish utility disclosure layout"
```
