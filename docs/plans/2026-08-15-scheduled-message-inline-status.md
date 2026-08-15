# Scheduled Message Inline Status Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Display each scheduled message ID and its status indicators on the same row.

**Architecture:** Keep the existing scheduled-message grid, row model, bindings, and nested status group. Change only the orientation and spacing of the message/status container, protected by a structural XAML regression test.

**Tech Stack:** C# 13, .NET 10, Avalonia 12, xUnit, FluentAssertions, System.Xml.Linq

---

### Task 1: Define the inline message-status contract

**Files:**
- Modify: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`
- Test: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`

**Step 1: Write the failing structural test**

Add this test to `ScheduledMessagesViewTests`:

```csharp
[Fact]
public void ScheduledMessagesView_DisplaysMessageStatusInline()
{
    var path = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "BusLane",
        "Views", "Controls", "ScheduledMessagesView.axaml"));
    var document = XDocument.Parse(File.ReadAllText(path));

    var messageId = document.Descendants()
        .Single(element => element.Name.LocalName == "TextBlock" &&
                           element.Attribute("Text")?.Value == "{Binding Entry.MessageId}");
    var messageStatusContainer = messageId.Parent;

    messageStatusContainer.Should().NotBeNull();
    messageStatusContainer!.Name.LocalName.Should().Be("StackPanel");
    messageStatusContainer.Attribute("Orientation")
        ?.Value
        .Should()
        .Be("Horizontal");
}
```

**Step 2: Run the focused test and verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessagesViewTests.ScheduledMessagesView_DisplaysMessageStatusInline"
```

Expected: FAIL because the message/status parent `StackPanel` has no `Orientation` attribute and therefore lays out vertically.

**Step 3: Commit the failing test**

```bash
rtk git add BusLane.Tests/Views/ScheduledMessagesViewTests.cs
rtk git commit -m "test: require inline scheduled message status"
```

### Task 2: Render message status inline

**Files:**
- Modify: `BusLane/Views/Controls/ScheduledMessagesView.axaml`
- Test: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`

**Step 1: Make the message/status container horizontal**

Change:

```xml
<StackPanel Grid.Column="3">
```

to:

```xml
<StackPanel Grid.Column="3" Orientation="Horizontal" Spacing="8">
```

Do not change the message/status bindings, nested status group, grid columns, compact action flyout, commands, or view model.

**Step 2: Run the focused view tests and verify GREEN**

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessagesViewTests"
```

Expected: all scheduled-message view tests pass.

**Step 3: Run the focused scheduled-message tests**

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessageStoreTests|FullyQualifiedName~ScheduledMessagesViewModelTests|FullyQualifiedName~ScheduledMessagesViewTests"
```

Expected: all focused scheduled-message tests pass.

**Step 4: Run the full test suite**

```bash
rtk dotnet test
```

Expected: all tests pass with zero failures.

**Step 5: Compile the Avalonia XAML**

```bash
rtk dotnet build
```

Expected: build succeeds with zero errors.

**Step 6: Commit the layout fix**

```bash
rtk git add BusLane/Views/Controls/ScheduledMessagesView.axaml
rtk git commit -m "fix: display scheduled message status inline"
```

### Task 3: Inspect final scope

**Files:**
- No changes expected

**Step 1: Inspect the branch diff and status**

```bash
rtk git diff codex/issue-160-scheduled-message-console...HEAD -- BusLane.Tests/Views/ScheduledMessagesViewTests.cs BusLane/Views/Controls/ScheduledMessagesView.axaml docs/plans/
rtk git status --short
```

Expected: only the approved scheduled-message view/test changes and design/plan documents differ; the worktree is clean.
