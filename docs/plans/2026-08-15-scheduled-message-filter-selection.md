# Scheduled Message Filter Selection Fix Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Prevent the scheduled-message filter ComboBoxes from recursively rebuilding their item sources while retaining valid selections across refreshes.

**Architecture:** Keep the existing computed option properties and XAML bindings. Split filtered-result notifications from filter-option notifications, and normalize connection/entity selections only after refreshed entries produce their new option sets.

**Tech Stack:** C# 13, .NET 10, Avalonia, CommunityToolkit.Mvvm, xUnit, FluentAssertions, NSubstitute

---

### Task 1: Reproduce the notification feedback loop

**Files:**
- Modify: `BusLane.Tests/ViewModels/ScheduledMessagesViewModelTests.cs`
- Test: `BusLane.Tests/ViewModels/ScheduledMessagesViewModelTests.cs`

**Step 1: Write the failing test**

Add a test that subscribes to `PropertyChanged`, changes `SelectedConnection` and `SelectedEntity`, and asserts that neither `ConnectionOptions` nor `EntityOptions` was notified:

```csharp
[Fact]
public void SelectedFilters_Changed_DoNotNotifyOptionSources()
{
    var service = Substitute.For<IScheduledMessageManagementService>();
    var sut = new ScheduledMessagesViewModel(service, (_, _) => Task.CompletedTask, TimeProvider.System);
    var changedProperties = new List<string?>();
    sut.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

    sut.SelectedConnection = "Development";
    sut.SelectedEntity = "orders";

    changedProperties.Should().NotContain(nameof(sut.ConnectionOptions));
    changedProperties.Should().NotContain(nameof(sut.EntityOptions));
    changedProperties.Should().Contain(nameof(sut.FilteredEntries));
}
```

**Step 2: Run the focused test and verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessagesViewModelTests.SelectedFilters_Changed_DoNotNotifyOptionSources"
```

Expected: FAIL because `NotifyProjectionChanged()` currently raises both option-source properties.

**Step 3: Commit the regression test**

```bash
rtk git add BusLane.Tests/ViewModels/ScheduledMessagesViewModelTests.cs
rtk git commit -m "test: reproduce scheduled filter selection loop"
```

### Task 2: Separate projection and option notifications

**Files:**
- Modify: `BusLane/ViewModels/ScheduledMessagesViewModel.cs`
- Test: `BusLane.Tests/ViewModels/ScheduledMessagesViewModelTests.cs`

**Step 1: Implement the minimal notification split**

Keep `NotifyProjectionChanged()` responsible only for filtered results:

```csharp
private void NotifyProjectionChanged()
{
    OnPropertyChanged(nameof(FilteredEntries));
    OnPropertyChanged(nameof(CalendarDays));
    OnPropertyChanged(nameof(IsEmpty));
}

private void NotifyFilterOptionsChanged()
{
    OnPropertyChanged(nameof(ConnectionOptions));
    OnPropertyChanged(nameof(EntityOptions));
}
```

Call `NotifyFilterOptionsChanged()` in `RefreshAsync()` after entries are loaded, before the filtered projection notification. Do not call it from selection-change hooks.

**Step 2: Run the focused test and verify GREEN**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessagesViewModelTests.SelectedFilters_Changed_DoNotNotifyOptionSources"
```

Expected: PASS.

### Task 3: Preserve or reset selections after refresh

**Files:**
- Modify: `BusLane.Tests/ViewModels/ScheduledMessagesViewModelTests.cs`
- Modify: `BusLane/ViewModels/ScheduledMessagesViewModel.cs`

**Step 1: Write failing refresh-selection tests**

Add tests that refresh twice with controlled entries:

- A selected connection/entity still present after the second refresh remains selected.
- A selected connection/entity absent after the second refresh resets to `"All"`.

**Step 2: Run the two tests and verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessagesViewModelTests.RefreshAsync_"
```

Expected: the missing-option reset test fails because refresh currently leaves stale selections in place.

**Step 3: Implement minimal selection normalization**

After loading entries, retain selections found in the corresponding option list and reset missing values:

```csharp
if (!ConnectionOptions.Contains(SelectedConnection, StringComparer.OrdinalIgnoreCase))
{
    SelectedConnection = "All";
}
if (!EntityOptions.Contains(SelectedEntity, StringComparer.OrdinalIgnoreCase))
{
    SelectedEntity = "All";
}
NotifyFilterOptionsChanged();
NotifyProjectionChanged();
```

**Step 4: Run the scheduled-message view-model tests**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~BusLane.Tests.ViewModels.ScheduledMessagesViewModelTests"
```

Expected: PASS.

**Step 5: Commit the fix**

```bash
rtk git add BusLane/ViewModels/ScheduledMessagesViewModel.cs BusLane.Tests/ViewModels/ScheduledMessagesViewModelTests.cs
rtk git commit -m "fix: prevent scheduled filter selection recursion"
```

### Task 4: Verify the repository

**Files:**
- No changes expected

**Step 1: Run all tests**

```bash
rtk dotnet test
```

Expected: all tests pass with no failures.

**Step 2: Build the application**

```bash
rtk dotnet build
```

Expected: build succeeds with no errors.

**Step 3: Inspect the final diff and status**

```bash
rtk git diff HEAD~2 -- BusLane/ViewModels/ScheduledMessagesViewModel.cs BusLane.Tests/ViewModels/ScheduledMessagesViewModelTests.cs docs/plans/
rtk git status --short
```

Expected: only the planned fix, tests, and plan documents are tracked; the pre-existing `.reasonix/` and `reasonix.toml` remain untouched.
