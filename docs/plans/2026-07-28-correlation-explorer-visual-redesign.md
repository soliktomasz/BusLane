# Correlation Explorer Visual Redesign Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Turn the Correlation Explorer into a BusLane-native, timeline-first investigation workspace without changing its correlation, comparison, replay, or persistence behavior.

**Architecture:** Keep the existing `CorrelationExplorerViewModel` and service flow intact. Replace the view's duplicated header and flat three-pane layout with an icon-led command bar, collapsible structured filters, a correlation rail, a visual event timeline, and a dense tabbed inspector. Add only narrowly scoped styles to the shared BusLane style sheet and protect the visual contract with focused XAML tests.

**Tech Stack:** .NET, C#, Avalonia XAML, Lucide Avalonia icons, xUnit, FluentAssertions

---

### Task 1: Lock the visual contract with failing tests

**Files:**
- Modify: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs:32-94`
- Test: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`

**Step 1: Write the failing visual-structure test**

Add this test after `CorrelationExplorer_ShowsGroupsTimelineDetailsAndHistory`:

```csharp
[Fact]
public void CorrelationExplorer_UsesTimelineFirstBusLaneWorkspace()
{
    // Arrange
    var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));

    // Assert
    xaml.Should().NotContain("Text=\"Correlation Explorer\"");
    xaml.Should().Contain("Classes=\"correlation-command-bar\"");
    xaml.Should().Contain("Kind=\"Search\"");
    xaml.Should().Contain("Kind=\"SlidersHorizontal\"");
    xaml.Should().Contain("Kind=\"RefreshCw\"");
    xaml.Should().Contain("Kind=\"Download\"");
    xaml.Should().Contain("Classes=\"correlation-list\"");
    xaml.Should().Contain("Classes=\"timeline-list\"");
    xaml.Should().Contain("Classes=\"timeline-node\"");
    xaml.Should().Contain("ColumnDefinitions=\"260,360,*\"");
}
```

Add this second test immediately after it:

```csharp
[Fact]
public void CorrelationExplorer_ProvidesInvestigationEmptyStates()
{
    // Arrange
    var xaml = File.ReadAllText(GetPath("Controls", "CorrelationExplorerView.axaml"));

    // Assert
    xaml.Should().Contain("No correlations found");
    xaml.Should().Contain("Select a correlation");
    xaml.Should().Contain("Select a message to inspect");
    xaml.Should().Contain("Choose message A and message B");
    xaml.Should().Contain("No replay activity yet");
    xaml.Should().Contain("Converter={StaticResource IntEqualsConverter}");
    xaml.Should().Contain("Converter={x:Static ObjectConverters.IsNull}");
}
```

Update the existing structured-filter layout assertions:

```csharp
xaml.Should().Contain("RowDefinitions=\"Auto,Auto,*\"");
xaml.Should().Contain("ColumnDefinitions=\"*,*,*,*\"");
```

Remove the old fixed-column assertion:

```csharp
xaml.Should().Contain("ColumnDefinitions=\"280,320,*\"");
```

**Step 2: Run the view tests to verify they fail**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Views.CorrelationExplorerViewTests"
```

Expected: FAIL in the new timeline-first and empty-state tests because the new classes, icons, dimensions, and empty-state copy do not exist yet.

**Step 3: Commit the failing tests**

```bash
rtk git add BusLane.Tests/Views/CorrelationExplorerViewTests.cs
rtk git commit -m "test: define correlation explorer visual workspace"
```

### Task 2: Add narrowly scoped explorer styles

**Files:**
- Modify: `BusLane/Styles/AppStyles.axaml:1987`
- Test: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`

**Step 1: Add the explorer workspace styles**

Append a dedicated section to `AppStyles.axaml`. Reuse the theme resources already defined in `App.axaml`; do not introduce hard-coded light-only colors.

```xml
<!-- Correlation Explorer -->
<Style Selector="Border.correlation-command-bar">
    <Setter Property="Background" Value="{DynamicResource SurfaceSubtle}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource BorderDefault}"/>
    <Setter Property="BorderThickness" Value="0,0,0,1"/>
    <Setter Property="Padding" Value="16,12"/>
</Style>

<Style Selector="Border.correlation-filter-surface">
    <Setter Property="Background" Value="{DynamicResource LayerBackground}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource BorderDefault}"/>
    <Setter Property="BorderThickness" Value="0,0,0,1"/>
    <Setter Property="Padding" Value="16"/>
</Style>

<Style Selector="Border.correlation-pane">
    <Setter Property="Background" Value="{DynamicResource CardBackground}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource BorderDefault}"/>
    <Setter Property="BorderThickness" Value="0,0,1,0"/>
</Style>

<Style Selector="Border.correlation-pane.inspector">
    <Setter Property="BorderThickness" Value="0"/>
</Style>

<Style Selector="ListBox.correlation-list, ListBox.timeline-list">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Padding" Value="0"/>
</Style>

<Style Selector="ListBox.correlation-list ListBoxItem, ListBox.timeline-list ListBoxItem">
    <Setter Property="Padding" Value="0"/>
    <Setter Property="Margin" Value="0"/>
    <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
</Style>

<Style Selector="Border.correlation-group-row">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="3,0,0,0"/>
    <Setter Property="Padding" Value="12,10"/>
</Style>

<Style Selector="ListBox.correlation-list ListBoxItem:pointerover Border.correlation-group-row">
    <Setter Property="Background" Value="{DynamicResource HoverBackground}"/>
</Style>

<Style Selector="ListBox.correlation-list ListBoxItem:selected Border.correlation-group-row">
    <Setter Property="Background" Value="{DynamicResource SelectedBackground}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource SelectedBorder}"/>
</Style>

<Style Selector="Border.timeline-event">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="Padding" Value="12"/>
    <Setter Property="Margin" Value="0,4,10,4"/>
</Style>

<Style Selector="ListBox.timeline-list ListBoxItem:pointerover Border.timeline-event">
    <Setter Property="Background" Value="{DynamicResource HoverBackground}"/>
</Style>

<Style Selector="ListBox.timeline-list ListBoxItem:selected Border.timeline-event">
    <Setter Property="Background" Value="{DynamicResource SelectedBackground}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource SelectedBorder}"/>
</Style>

<Style Selector="Ellipse.timeline-node">
    <Setter Property="Width" Value="10"/>
    <Setter Property="Height" Value="10"/>
    <Setter Property="Fill" Value="{DynamicResource CardBackground}"/>
    <Setter Property="Stroke" Value="{DynamicResource AccentBrand}"/>
    <Setter Property="StrokeThickness" Value="2"/>
</Style>

<Style Selector="ListBox.timeline-list ListBoxItem:selected Ellipse.timeline-node">
    <Setter Property="Fill" Value="{DynamicResource AccentBrand}"/>
</Style>

<Style Selector="Border.correlation-inspector-header">
    <Setter Property="Background" Value="{DynamicResource SurfaceSubtle}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource BorderDefault}"/>
    <Setter Property="BorderThickness" Value="0,0,0,1"/>
    <Setter Property="Padding" Value="16,14"/>
</Style>
```

**Step 2: Run a build to validate selectors and resources**

Run:

```bash
rtk dotnet build
```

Expected: PASS with zero errors. The styles are not yet referenced, so the visual contract tests remain red.

**Step 3: Commit the style foundation**

```bash
rtk git add BusLane/Styles/AppStyles.axaml
rtk git commit -m "style: add correlation investigation surfaces"
```

### Task 3: Rebuild the command bar and filter surface

**Files:**
- Modify: `BusLane/Views/Controls/CorrelationExplorerView.axaml:1-98`
- Test: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`

**Step 1: Add the converter resource**

Add the converter namespace and local resource:

```xml
xmlns:converters="using:BusLane.Converters"
```

```xml
<UserControl.Resources>
    <converters:IntEqualsConverter x:Key="IntEqualsConverter"/>
</UserControl.Resources>
```

**Step 2: Replace the duplicate header with the command bar**

Keep `RowDefinitions="Auto,Auto,*"` and replace the current inner title surface with:

```xml
<Border Grid.Row="0" Classes="correlation-command-bar">
    <Grid ColumnDefinitions="*,Auto" ColumnSpacing="12">
        <Border Classes="message-search-surface" MaxWidth="620">
            <Grid ColumnDefinitions="Auto,*,Auto">
                <LucideIcon Grid.Column="0"
                            Kind="Search"
                            Size="14"
                            Foreground="{DynamicResource MutedForeground}"
                            Margin="4,0,8,0"/>
                <TextBox Grid.Column="1"
                         Text="{Binding FilterText, Mode=TwoWay}"
                         PlaceholderText="Search message ID, body, correlation, session, or property"
                         Background="Transparent"
                         BorderThickness="0"
                         Padding="0,4"
                         FontSize="13"/>
                <Button Grid.Column="2"
                        Classes="subtle small"
                        Command="{Binding ClearFiltersCommand}"
                        IsVisible="{Binding FilterText, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
                        ToolTip.Tip="Clear search"
                        Padding="4">
                    <LucideIcon Kind="X" Size="12"/>
                </Button>
            </Grid>
        </Border>

        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="6">
            <Button Classes="secondary small"
                    Command="{Binding ToggleFiltersCommand}"
                    ToolTip.Tip="Show structured filters">
                <StackPanel Orientation="Horizontal" Spacing="6">
                    <LucideIcon Kind="SlidersHorizontal" Size="13"/>
                    <TextBlock Text="Filters"/>
                </StackPanel>
            </Button>
            <Button Classes="secondary small"
                    Command="{Binding RefreshCommand}"
                    ToolTip.Tip="Refresh correlation messages and replay history">
                <StackPanel Orientation="Horizontal" Spacing="6">
                    <LucideIcon Kind="RefreshCw" Size="13"/>
                    <TextBlock Text="Refresh"/>
                </StackPanel>
            </Button>
            <Button Classes="secondary small"
                    Command="{Binding ExportHistoryCommand}"
                    ToolTip.Tip="Export replay history">
                <StackPanel Orientation="Horizontal" Spacing="6">
                    <LucideIcon Kind="Download" Size="13"/>
                    <TextBlock Text="Export history"/>
                </StackPanel>
            </Button>
        </StackPanel>
    </Grid>
</Border>
```

**Step 3: Replace the filter `WrapPanel` with a structured grid**

Use `Classes="correlation-filter-surface"` and retain `IsVisible="{Binding ShowFilters}"`. Inside, use a four-column grid:

```xml
<Grid ColumnDefinitions="*,*,*,*"
      RowDefinitions="Auto,Auto,Auto"
      ColumnSpacing="12"
      RowSpacing="10">
```

Place fields as follows:

- Row 0: From, To, Namespace, Entity
- Row 1: Environment, Source, Correlation or session ID, a nested two-column Property key/value grid
- Row 2: validation message spanning the first two columns; Clear and Apply buttons in a right-aligned horizontal stack spanning the last two columns

Every label uses `Classes="field-label"`. Preserve these bindings exactly:

```xml
FilterFromText
FilterToText
FilterNamespace
FilterEntity
FilterEnvironmentOptions
FilterEnvironment
FilterSourceOptions
FilterSource
FilterIdentifier
FilterPropertyKey
FilterPropertyValue
FilterValidationMessage
ClearFiltersCommand
ApplyFiltersCommand
```

Use `{DynamicResource TextDanger}` instead of the Fluent-theme-specific critical brush for filter validation.

**Step 4: Run the focused view tests**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Views.CorrelationExplorerViewTests"
```

Expected: the icon and duplicate-header assertions pass; the timeline, dimensions, and empty-state assertions still fail.

**Step 5: Commit the command and filter surfaces**

```bash
rtk git add BusLane/Views/Controls/CorrelationExplorerView.axaml BusLane.Tests/Views/CorrelationExplorerViewTests.cs
rtk git commit -m "feat: align correlation explorer commands and filters"
```

### Task 4: Build the correlation rail and visual timeline

**Files:**
- Modify: `BusLane/Views/Controls/CorrelationExplorerView.axaml:100-166`
- Test: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`

**Step 1: Replace the flat workspace columns**

Use:

```xml
<Grid Grid.Row="2" ColumnDefinitions="260,360,*">
```

Each column uses `Classes="correlation-pane"`; add `inspector` to the right column. Do not use gaps between the panes because shared borders provide separation.

**Step 2: Implement the correlation rail**

Use a two-row grid with a compact 48-pixel section header. Bind the header count:

```xml
<TextBlock Text="{Binding Groups.Count, StringFormat='{}{0} groups'}"
           Classes="caption"/>
```

Set the list to:

```xml
<ListBox Grid.Row="1"
         Classes="correlation-list"
         ItemsSource="{Binding Groups}"
         SelectedItem="{Binding SelectedGroup}">
```

Each item uses `Border Classes="correlation-group-row"` with:

- `DisplayId` as `body-strong`;
- `Messages.Count` in `badge-info`;
- `Correlation` in `badge-muted` when `UsesSessionFallback` is false;
- `Session` in `badge-warning` when `UsesSessionFallback` is true.

Overlay this empty state in the list row:

```xml
<Border Classes="empty-state"
        Margin="16"
        VerticalAlignment="Top"
        IsVisible="{Binding Groups.Count,
                            Converter={StaticResource IntEqualsConverter},
                            ConverterParameter=0}">
    <StackPanel Spacing="8" HorizontalAlignment="Center">
        <LucideIcon Kind="GitBranch" Size="24"
                    Foreground="{DynamicResource MutedForeground}"/>
        <TextBlock Text="No correlations found"
                   Classes="body-strong"
                   HorizontalAlignment="Center"/>
        <TextBlock Text="Load or stream messages with a correlation or session ID."
                   Classes="caption"
                   TextWrapping="Wrap"
                   TextAlignment="Center"/>
    </StackPanel>
</Border>
```

**Step 3: Implement the visual timeline**

Keep the new-message action in the timeline header. Set the list class to `timeline-list`.

Each item uses:

```xml
<Grid ColumnDefinitions="28,*">
    <Border Grid.Column="0"
            Width="1"
            Background="{DynamicResource BorderStrong}"
            HorizontalAlignment="Center"/>
    <Ellipse Grid.Column="0"
             Classes="timeline-node"
             VerticalAlignment="Top"
             Margin="0,18,0,0"/>
    <Border Grid.Column="1" Classes="timeline-event">
        <!-- timestamp, entity, badges, message ID, and A/B actions -->
    </Border>
</Grid>
```

Inside `timeline-event`, preserve bindings for:

```xml
EnqueuedTime
EntityName
EntityType
Source
Environment
MessageId
SetComparisonACommand
SetComparisonBCommand
```

Use `badge-info` for source and `badge-env` for environment. Use compact `subtle small` buttons labelled `A` and `B`, with tooltips `Use as comparison A` and `Use as comparison B`.

Add a timeline empty state that says `Select a correlation` and explains that its chronological messages will appear there. Its visibility uses `Timeline.Count` with `IntEqualsConverter`.

**Step 4: Run the focused tests**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Views.CorrelationExplorerViewTests"
```

Expected: all visual workspace assertions pass except inspector-specific empty-state assertions.

**Step 5: Commit the investigation navigation**

```bash
rtk git add BusLane/Views/Controls/CorrelationExplorerView.axaml
rtk git commit -m "feat: add correlation rail and event timeline"
```

### Task 5: Refine the inspector, comparison, and history states

**Files:**
- Modify: `BusLane/Views/Controls/CorrelationExplorerView.axaml:168-301`
- Test: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`

**Step 1: Build the inspector header**

Use `Classes="correlation-pane inspector"` and a three-row grid: header, status, tabs.

The header uses `Classes="correlation-inspector-header"`. Show:

- selected message ID using `body-strong`;
- namespace and entity context using captions;
- source and environment badges;
- icon-led `Compare previous` and `Replay message` buttons.

Preserve `CompareWithPreviousCommand` and `OpenReplayCommand`. Bind both button `IsEnabled` values through:

```xml
IsEnabled="{Binding SelectedMessage, Converter={x:Static ObjectConverters.IsNotNull}}"
```

Display `StatusMessage` in an `infobar` between the header and tabs when non-empty.

**Step 2: Add the no-selection state**

Keep the tab control bound exactly as today, but show it only when `SelectedMessage` is not null. In the same row, add:

```xml
<Border Classes="empty-state"
        Margin="24"
        VerticalAlignment="Center"
        IsVisible="{Binding SelectedMessage, Converter={x:Static ObjectConverters.IsNull}}">
    <StackPanel Spacing="10" HorizontalAlignment="Center">
        <LucideIcon Kind="MessageSquareText" Size="28"
                    Foreground="{DynamicResource MutedForeground}"/>
        <TextBlock Text="Select a message to inspect" Classes="body-strong"/>
        <TextBlock Text="Choose an event from the timeline to review its payload, metadata, and replay history."
                   Classes="caption"
                   TextWrapping="Wrap"
                   TextAlignment="Center"
                   MaxWidth="340"/>
    </StackPanel>
</Border>
```

**Step 3: Structure the existing tabs**

Keep all current bindings and commands.

- Payload: retain the read-only `code-editor`.
- Metadata: replace interpolated sentences with `property-row` label/value grids for correlation ID, session ID, entity, content type, and sequence.
- Application properties: wrap each item in `property-row`.
- Compare: wrap current content in a root grid. Show the comparison content only when `Comparison` is non-null. Show `Choose message A and message B` when it is null. Keep all existing comparison bindings.
- Replay history: keep the list and add `No replay activity yet` when `ReplayHistory.Count == 0`.

Comparison and history empty-state visibility must use `ObjectConverters.IsNull` and `IntEqualsConverter`, respectively.

**Step 4: Run focused view and ViewModel tests**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Views.CorrelationExplorerViewTests|FullyQualifiedName~BusLane.Tests.ViewModels.CorrelationExplorerViewModelTests"
```

Expected: PASS.

**Step 5: Run a build**

Run:

```bash
rtk dotnet build
```

Expected: PASS with zero errors.

**Step 6: Commit the inspector**

```bash
rtk git add BusLane/Views/Controls/CorrelationExplorerView.axaml BusLane.Tests/Views/CorrelationExplorerViewTests.cs
rtk git commit -m "feat: refine correlation message inspector"
```

### Task 6: Visual QA and regression verification

**Files:**
- Verify: `BusLane/Views/Controls/CorrelationExplorerView.axaml`
- Verify: `BusLane/Styles/AppStyles.axaml`
- Verify: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`

**Step 1: Run the explorer-focused test set**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~CorrelationExplorer|FullyQualifiedName~CorrelationMessageComparison|FullyQualifiedName~CorrelationMessageFilter"
```

Expected: PASS.

**Step 2: Run the full regression suite**

Run:

```bash
rtk dotnet test
```

Expected: PASS with zero failing tests.

**Step 3: Run the application for visual inspection**

Run:

```bash
rtk dotnet run --project BusLane/BusLane.csproj
```

Verify:

- there is only one Correlation Explorer page header;
- the command bar visually matches Messages and Live Stream;
- filters form a balanced four-column grid at normal desktop width;
- group selection and timeline selection are obvious in light and dark themes;
- the timeline reads chronologically and its A/B actions do not dominate;
- the inspector remains usable near 1100 pixels wide and expands cleanly at 1600 pixels;
- empty states appear without overlapping lists;
- the replay dialog still opens and closes above the explorer.

**Step 4: Fix only visual defects found during QA**

Keep fixes scoped to the three files listed above. Do not change the ViewModel or services unless visual QA reveals a real binding defect that cannot be solved in XAML.

**Step 5: Re-run focused tests and build after QA changes**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.Views.CorrelationExplorerViewTests"
rtk dotnet build
```

Expected: PASS.

**Step 6: Commit QA adjustments if any**

```bash
rtk git add BusLane/Views/Controls/CorrelationExplorerView.axaml BusLane/Styles/AppStyles.axaml BusLane.Tests/Views/CorrelationExplorerViewTests.cs
rtk git commit -m "fix: polish correlation explorer workspace"
```

### Task 7: Final evidence review

**Files:**
- Review: `docs/plans/2026-07-28-correlation-explorer-visual-redesign-design.md`
- Review: `BusLane/Views/Controls/CorrelationExplorerView.axaml`
- Review: `BusLane/Styles/AppStyles.axaml`
- Review: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`

**Step 1: Invoke the verification skill**

Use `@superpowers:verification-before-completion`.

**Step 2: Inspect the final diff**

Run:

```bash
rtk git diff HEAD~4 -- BusLane/Views/Controls/CorrelationExplorerView.axaml BusLane/Styles/AppStyles.axaml BusLane.Tests/Views/CorrelationExplorerViewTests.cs docs/plans/2026-07-28-correlation-explorer-visual-redesign-design.md
```

Expected: every changed production line supports the approved visual redesign; no service or unrelated UI files changed.

**Step 3: Confirm the worktree is clean**

Run:

```bash
rtk git status --short
```

Expected: no output.

**Step 4: Record the final verification evidence**

Report the focused-test count, full-suite test count, build result, and visual QA result. Do not claim completion without fresh command output.
