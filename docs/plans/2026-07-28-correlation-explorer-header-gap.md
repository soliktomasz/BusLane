# Correlation Explorer Header Gap Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove the correlation explorer's unnecessary dark header gap and restore the standard search input surface.

**Architecture:** Keep the existing two-row panel and shared styles. Change only the correlation explorer header instance and its search `TextBox`, with XAML contract tests protecting both decisions.

**Tech Stack:** Avalonia XAML, C#, xUnit, FluentAssertions

---

### Task 1: Correct the header and search surfaces

**Files:**
- Modify: `BusLane/Views/MainWindow.axaml:101-127`
- Modify: `BusLane/Views/Controls/CorrelationExplorerView.axaml:14-35`
- Test: `BusLane.Tests/Views/CorrelationExplorerViewTests.cs`

**Step 1: Write the failing test**

Add a focused XAML contract test that extracts the correlation explorer panel from
`MainWindow.axaml` and asserts that its `page-header-surface` has no bottom margin.
In the same test, load `CorrelationExplorerView.axaml`, locate the search text box,
and assert that it does not contain `Background="Transparent"`.

**Step 2: Run the test to verify it fails**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~CorrelationExplorerViewTests.CorrelationWorkspace_UsesContinuousHeaderAndStandardSearchSurface"
```

Expected: FAIL because the header contains `Margin="0,0,0,16"` and the search text
box contains `Background="Transparent"`.

**Step 3: Write the minimal implementation**

Remove `Margin="0,0,0,16"` from the correlation explorer's
`page-header-surface` in `MainWindow.axaml`. Remove `Background="Transparent"`
from the search `TextBox` in `CorrelationExplorerView.axaml`; do not alter the
shared `message-search-surface` style or other panels.

**Step 4: Run focused verification**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~CorrelationExplorerViewTests"
```

Expected: PASS.

**Step 5: Run full verification**

Run:

```bash
dotnet test
dotnet build
```

Expected: all tests pass and the build completes with zero errors.

**Step 6: Commit**

```bash
git add BusLane/Views/MainWindow.axaml \
  BusLane/Views/Controls/CorrelationExplorerView.axaml \
  BusLane.Tests/Views/CorrelationExplorerViewTests.cs \
  docs/plans/2026-07-28-correlation-explorer-header-gap.md
git commit -m "fix: remove correlation explorer header gap"
```
