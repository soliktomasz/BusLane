# Scheduled Message Compact Actions Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Prevent scheduled-message row content from overlapping at smaller window widths by replacing four inline row actions with one compact action flyout.

**Architecture:** Keep the existing scheduled-message grid, commands, and row model. Change only the row action presentation in `ScheduledMessagesView.axaml`, reusing BusLane's established right-aligned `Button.Flyout` pattern so no view-model or service changes are required.

**Tech Stack:** C# 13, .NET 10, Avalonia 12, xUnit, FluentAssertions, System.Xml.Linq

---

### Task 1: Define the compact row-action contract

**Files:**
- Modify: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`
- Test: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`

**Step 1: Write the failing structural test**

Add `using System.Xml.Linq;` and this test:

```csharp
[Fact]
public void ScheduledMessagesView_CollapsesRowActionsIntoFlyout()
{
    var path = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "BusLane",
        "Views", "Controls", "ScheduledMessagesView.axaml"));
    var document = XDocument.Parse(File.ReadAllText(path));

    var actionButton = document.Descendants()
        .Single(element => element.Name.LocalName == "Button" &&
                           element.Attribute("ToolTip.Tip")?.Value == "More scheduled message actions");

    actionButton.Descendants()
        .Single(element => element.Name.LocalName == "Flyout")
        .Attribute("Placement")
        ?.Value
        .Should()
        .Be("BottomEdgeAlignedRight");

    actionButton.Descendants()
        .Where(element => element.Name.LocalName == "Button")
        .Select(element => element.Attribute("Command")?.Value)
        .Should()
        .Contain([
            "{Binding $parent[UserControl].DataContext.CloneCommand}",
            "{Binding $parent[UserControl].DataContext.BeginCancelCommand}",
            "{Binding $parent[UserControl].DataContext.BeginRescheduleCommand}",
            "{Binding $parent[UserControl].DataContext.ResolveCommand}"
        ]);

    document.Descendants()
        .Where(element => element.Name.LocalName == "Button")
        .Select(element => element.Attribute("Content")?.Value)
        .Should()
        .NotContain(["Clone", "Cancel", "Reschedule", "Resolve"]);
}
```

**Step 2: Run the focused test and verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessagesViewTests.ScheduledMessagesView_CollapsesRowActionsIntoFlyout"
```

Expected: FAIL because the current row has four inline buttons and no button with the `More scheduled message actions` tooltip.

**Step 3: Commit the failing contract test**

```bash
rtk git add BusLane.Tests/Views/ScheduledMessagesViewTests.cs
rtk git commit -m "test: define compact scheduled message actions"
```

### Task 2: Replace inline actions with the compact flyout

**Files:**
- Modify: `BusLane/Views/Controls/ScheduledMessagesView.axaml`
- Test: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`

**Step 1: Replace the row action `StackPanel`**

Replace the four-button `StackPanel` in grid column 4 with:

```xml
<Button Grid.Column="4"
        Classes="toolbar"
        ToolTip.Tip="More scheduled message actions"
        AutomationProperties.Name="Scheduled message actions"
        HorizontalAlignment="Right"
        VerticalAlignment="Center">
    <Button.Flyout>
        <Flyout Placement="BottomEdgeAlignedRight">
            <StackPanel Spacing="4" MinWidth="160">
                <Button Classes="subtle small"
                        Command="{Binding $parent[UserControl].DataContext.CloneCommand}"
                        CommandParameter="{Binding}"
                        HorizontalAlignment="Stretch"
                        HorizontalContentAlignment="Left">
                    <TextBlock Text="Clone"/>
                </Button>
                <Button Classes="subtle small"
                        Command="{Binding $parent[UserControl].DataContext.BeginCancelCommand}"
                        CommandParameter="{Binding}"
                        HorizontalAlignment="Stretch"
                        HorizontalContentAlignment="Left">
                    <TextBlock Text="Cancel"/>
                </Button>
                <Button Classes="subtle small"
                        Command="{Binding $parent[UserControl].DataContext.BeginRescheduleCommand}"
                        CommandParameter="{Binding}"
                        HorizontalAlignment="Stretch"
                        HorizontalContentAlignment="Left">
                    <TextBlock Text="Reschedule"/>
                </Button>
                <Button Classes="subtle small"
                        Command="{Binding $parent[UserControl].DataContext.ResolveCommand}"
                        CommandParameter="{Binding}"
                        HorizontalAlignment="Stretch"
                        HorizontalContentAlignment="Left">
                    <TextBlock Text="Resolve"/>
                </Button>
            </StackPanel>
        </Flyout>
    </Button.Flyout>
    <LucideIcon Kind="Ellipsis" Size="13"/>
</Button>
```

Do not change the grid columns, message/status content, commands, confirmation overlay, or view model.

**Step 2: Run the focused view tests and verify GREEN**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessagesViewTests"
```

Expected: all scheduled-messages view tests PASS.

**Step 3: Compile the Avalonia XAML**

Run:

```bash
rtk dotnet build
```

Expected: build succeeds with zero errors.

**Step 4: Commit the view fix**

```bash
rtk git add BusLane/Views/Controls/ScheduledMessagesView.axaml
rtk git commit -m "fix: compact scheduled message row actions"
```

### Task 3: Verify the complete change

**Files:**
- No changes expected

**Step 1: Run the focused scheduled-message tests**

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessageStoreTests|FullyQualifiedName~ScheduledMessagesViewModelTests|FullyQualifiedName~ScheduledMessagesViewTests"
```

Expected: all focused scheduled-message tests pass.

**Step 2: Run the full test suite**

```bash
rtk dotnet test
```

Expected: all tests pass with zero failures.

**Step 3: Run a fresh full build**

```bash
rtk dotnet build
```

Expected: build succeeds with zero errors.

**Step 4: Inspect scope and status**

```bash
rtk git diff codex/issue-160-scheduled-message-console...HEAD -- BusLane.Tests/Views/ScheduledMessagesViewTests.cs BusLane/Views/Controls/ScheduledMessagesView.axaml docs/plans/
rtk git status --short
```

Expected: only the approved view test, scheduled-messages XAML, and design/plan documents differ; `.reasonix/` and `reasonix.toml` remain untouched in the primary worktree.
