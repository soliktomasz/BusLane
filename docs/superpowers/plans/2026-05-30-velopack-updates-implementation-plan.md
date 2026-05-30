# Velopack Updates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace BusLane's hand-rolled GitHub release updater with a Velopack-native update flow and updated UI states.

**Architecture:** Keep `IUpdateService` as the app-facing boundary, add a narrow Velopack adapter for testability, and let `UpdateService` translate Velopack behavior into BusLane status/state. The UI reads richer update state from `IUpdateService`, while GitHub Actions creates and uploads Velopack packages/feeds alongside existing secondary artifacts.

**Tech Stack:** .NET 10, Avalonia, CommunityToolkit.Mvvm, xUnit, FluentAssertions, NSubstitute, Velopack 0.0.1298, GitHub Actions.

---

## File Structure

- Modify `BusLane/BusLane.csproj`: add `Velopack` package reference and keep MinVer.
- Modify `BusLane/Program.cs`: invoke `VelopackApp.Build().Run()` at the start of `Main`.
- Modify `BusLane/Models/Update/UpdateStatus.cs`: replace old states with Velopack-native states.
- Modify `BusLane/Models/Update/ReleaseInfo.cs`: remove old asset dictionary dependency and add self-update metadata.
- Create `BusLane/Services/Update/VelopackUpdateInfo.cs`: adapter-neutral update payload.
- Create `BusLane/Services/Update/IVelopackUpdateManager.cs`: testable Velopack boundary.
- Create `BusLane/Services/Update/VelopackUpdateManager.cs`: production wrapper around `UpdateManager` and `GithubSource`.
- Modify `BusLane/Services/Update/IUpdateService.cs`: expose Velopack availability and status text.
- Modify `BusLane/Services/Update/UpdateService.cs`: replace old parser/downloader usage with adapter-driven Velopack logic.
- Delete `BusLane/Services/Update/UpdateCheckService.cs`: no longer used.
- Delete `BusLane/Services/Update/UpdateDownloadService.cs`: no longer used.
- Delete `BusLane/Models/Update/AssetInfo.cs`: no longer used by production updates.
- Modify `BusLane/ViewModels/UpdateNotificationViewModel.cs`: expose Velopack-native action text/visibility.
- Modify `BusLane/Views/Controls/UpdateNotificationView.axaml`: update notification copy/actions.
- Modify `BusLane/ViewModels/SettingsViewModel.cs`: expose update status and fallback link command.
- Modify `BusLane/Views/Dialogs/SettingsDialog.axaml`: update Settings update section.
- Modify `BusLane/Program.cs`: register `IVelopackUpdateManager`.
- Modify `.github/workflows/release.yml`: add Velopack package/feed generation and upload.
- Modify `BusLane.Tests/Services/Update/UpdateServiceTests.cs`: replace network/GitHub tests with fake-adapter tests.
- Delete `BusLane.Tests/Services/Update/UpdateCheckServiceTests.cs`: old parser tests.
- Delete `BusLane.Tests/Services/Update/UpdateDownloadServiceTests.cs`: old temp downloader tests.
- Create `BusLane.Tests/ViewModels/UpdateNotificationViewModelTests.cs`: verify notification state/action text.

## Task 1: Add Velopack Package and Startup Hook

**Files:**
- Modify: `BusLane/BusLane.csproj`
- Modify: `BusLane/Program.cs`

- [ ] **Step 1: Add package reference**

Add this package reference in `BusLane/BusLane.csproj` with the other runtime packages:

```xml
<PackageReference Include="Velopack" Version="0.0.1298" />
```

- [ ] **Step 2: Add startup hook**

Add `using Velopack;` after the namespace declaration in `BusLane/Program.cs`, then make the first line in `Main`:

```csharp
VelopackApp.Build().Run();
```

The start of `Main` becomes:

```csharp
[STAThread]
public static void Main(string[] args)
{
    VelopackApp.Build().Run();

    // Build configuration from appsettings files
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .Build();
```

- [ ] **Step 3: Build**

Run: `dotnet build`

Expected: build succeeds after package restore.

- [ ] **Step 4: Commit**

```bash
git add BusLane/BusLane.csproj BusLane/Program.cs
git commit -m "feat: initialize Velopack on startup"
```

## Task 2: Define Testable Velopack Update Boundary

**Files:**
- Create: `BusLane/Services/Update/VelopackUpdateInfo.cs`
- Create: `BusLane/Services/Update/IVelopackUpdateManager.cs`
- Create: `BusLane/Services/Update/VelopackUpdateManager.cs`
- Modify: `BusLane/Models/Update/ReleaseInfo.cs`
- Modify: `BusLane/Models/Update/UpdateStatus.cs`
- Modify: `BusLane/Services/Update/IUpdateService.cs`
- Modify: `BusLane/Program.cs`

- [ ] **Step 1: Update status enum**

Replace `BusLane/Models/Update/UpdateStatus.cs` with:

```csharp
namespace BusLane.Models.Update;

/// <summary>Represents the current state of the update process.</summary>
public enum UpdateStatus
{
    Idle,
    NotInstalled,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToRestart,
    Installing,
    Error
}
```

- [ ] **Step 2: Update release model**

Replace `BusLane/Models/Update/ReleaseInfo.cs` with:

```csharp
namespace BusLane.Models.Update;

/// <summary>Describes an available Velopack update.</summary>
public record ReleaseInfo
{
    public string Version { get; init; } = null!;
    public string ReleaseNotes { get; init; } = string.Empty;
    public DateTime? PublishedAt { get; init; }
    public bool CanSelfUpdate { get; init; } = true;
    public bool IsReadyToRestart { get; init; }
    public string ReleaseUrl { get; init; } = "https://github.com/soliktomasz/BusLane/releases";
}
```

- [ ] **Step 3: Create adapter DTO**

Create `BusLane/Services/Update/VelopackUpdateInfo.cs`:

```csharp
namespace BusLane.Services.Update;

/// <summary>Adapter-neutral details for a Velopack update.</summary>
public record VelopackUpdateInfo(
    string Version,
    string ReleaseNotes,
    DateTime? PublishedAt);
```

- [ ] **Step 4: Create adapter interface**

Create `BusLane/Services/Update/IVelopackUpdateManager.cs`:

```csharp
namespace BusLane.Services.Update;

/// <summary>Testable boundary around Velopack update APIs.</summary>
public interface IVelopackUpdateManager
{
    bool IsInstalled { get; }
    VelopackUpdateInfo? PendingUpdate { get; }
    Task<VelopackUpdateInfo?> CheckForUpdatesAsync(CancellationToken ct = default);
    Task DownloadUpdatesAsync(VelopackUpdateInfo update, Action<int> progress, CancellationToken ct = default);
    void ApplyUpdatesAndRestart(VelopackUpdateInfo update);
}
```

- [ ] **Step 5: Create production adapter**

Create `BusLane/Services/Update/VelopackUpdateManager.cs`:

```csharp
namespace BusLane.Services.Update;

using Velopack;
using Velopack.Sources;

/// <summary>Velopack-backed update manager for the BusLane GitHub release feed.</summary>
public class VelopackUpdateManager : IVelopackUpdateManager
{
    private const string RepositoryUrl = "https://github.com/soliktomasz/BusLane";
    private readonly UpdateManager _manager;
    private UpdateInfo? _availableUpdate;

    public VelopackUpdateManager()
    {
        _manager = new UpdateManager(
            new GithubSource(RepositoryUrl, accessToken: null, prerelease: false, downloader: null));
    }

    public bool IsInstalled => _manager.IsInstalled;

    public VelopackUpdateInfo? PendingUpdate => _manager.UpdatePendingRestart is { } asset
        ? new VelopackUpdateInfo(
            asset.Version?.ToString() ?? string.Empty,
            asset.NotesMarkdown ?? string.Empty,
            null)
        : null;

    public async Task<VelopackUpdateInfo?> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        _availableUpdate = await _manager.CheckForUpdatesAsync();
        return _availableUpdate?.TargetFullRelease is { } asset
            ? new VelopackUpdateInfo(
                asset.Version?.ToString() ?? string.Empty,
                asset.NotesMarkdown ?? string.Empty,
                null)
            : null;
    }

    public Task DownloadUpdatesAsync(VelopackUpdateInfo update, Action<int> progress, CancellationToken ct = default)
    {
        if (_availableUpdate == null)
            throw new InvalidOperationException("No Velopack update has been checked.");

        return _manager.DownloadUpdatesAsync(_availableUpdate, progress, ct);
    }

    public void ApplyUpdatesAndRestart(VelopackUpdateInfo update)
    {
        var pending = _manager.UpdatePendingRestart;
        if (pending == null)
            throw new InvalidOperationException("No Velopack update is ready to apply.");

        _manager.ApplyUpdatesAndRestart(pending);
    }
}
```

- [ ] **Step 6: Extend update service interface**

Add these properties to `BusLane/Services/Update/IUpdateService.cs`:

```csharp
/// <summary>Gets whether this app can self-update through Velopack.</summary>
bool CanSelfUpdate { get; }

/// <summary>Gets user-facing update status text.</summary>
string StatusMessage { get; }
```

Keep the existing methods for now.

- [ ] **Step 7: Register adapter**

In `Program.ConfigureServices`, replace:

```csharp
services.AddSingleton<UpdateDownloadService>();
services.AddSingleton<IUpdateService, UpdateService>();
```

with:

```csharp
services.AddSingleton<IVelopackUpdateManager, VelopackUpdateManager>();
services.AddSingleton<IUpdateService, UpdateService>();
```

- [ ] **Step 8: Build**

Run: `dotnet build`

Expected: build may fail only because `UpdateService` has not yet been moved to the new constructor/properties. If it fails for missing `UpdateDownloadService`, continue to Task 3 immediately.

## Task 3: Replace UpdateService Behavior with Velopack Adapter

**Files:**
- Modify: `BusLane.Tests/Services/Update/UpdateServiceTests.cs`
- Modify: `BusLane/Services/Update/UpdateService.cs`
- Delete: `BusLane/Services/Update/UpdateCheckService.cs`
- Delete: `BusLane/Services/Update/UpdateDownloadService.cs`
- Delete: `BusLane/Models/Update/AssetInfo.cs`
- Delete: `BusLane.Tests/Services/Update/UpdateCheckServiceTests.cs`
- Delete: `BusLane.Tests/Services/Update/UpdateDownloadServiceTests.cs`

- [ ] **Step 1: Write failing service tests**

Replace `BusLane.Tests/Services/Update/UpdateServiceTests.cs` with tests covering:

```csharp
namespace BusLane.Tests.Services.Update;

using BusLane.Models.Update;
using BusLane.Services.Abstractions;
using BusLane.Services.Infrastructure;
using BusLane.Services.Update;
using FluentAssertions;
using NSubstitute;

public class UpdateServiceTests
{
    private readonly IVersionService _versionService = Substitute.For<IVersionService>();
    private readonly IPreferencesService _preferencesService = Substitute.For<IPreferencesService>();
    private readonly FakeVelopackUpdateManager _manager = new();

    public UpdateServiceTests()
    {
        _versionService.Version.Returns("0.9.0");
        _versionService.InformationalVersion.Returns("0.9.0");
        _preferencesService.AutoCheckForUpdates.Returns(true);
        _preferencesService.SkippedUpdateVersion.Returns((string?)null);
        _preferencesService.UpdateRemindLaterDate.Returns((DateTime?)null);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNotVelopackInstalled_ShowsNotInstalledForManualCheck()
    {
        var sut = CreateSut();
        _manager.IsInstalled = false;

        await sut.CheckForUpdatesAsync(manualCheck: true);

        sut.Status.Should().Be(UpdateStatus.NotInstalled);
        sut.CanSelfUpdate.Should().BeFalse();
        sut.StatusMessage.Should().Contain("GitHub Releases");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNoUpdateFound_ShowsUpToDateForManualCheck()
    {
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = null;

        await sut.CheckForUpdatesAsync(manualCheck: true);

        sut.Status.Should().Be(UpdateStatus.UpToDate);
        sut.AvailableRelease.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenUpdateFound_ShowsUpdateAvailable()
    {
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);

        await sut.CheckForUpdatesAsync(manualCheck: true);

        sut.Status.Should().Be(UpdateStatus.UpdateAvailable);
        sut.AvailableRelease.Should().NotBeNull();
        sut.AvailableRelease!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task DownloadUpdateAsync_WhenUpdateAvailable_ReportsProgressAndReadiesRestart()
    {
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);
        var progress = new List<double>();
        sut.DownloadProgressChanged += (_, value) => progress.Add(value);

        await sut.CheckForUpdatesAsync(manualCheck: true);
        await sut.DownloadUpdateAsync();

        sut.Status.Should().Be(UpdateStatus.ReadyToRestart);
        sut.AvailableRelease!.IsReadyToRestart.Should().BeTrue();
        progress.Should().Contain(42);
        progress.Should().Contain(100);
    }

    [Fact]
    public async Task InstallUpdateAsync_WhenReadyToRestart_DelegatesToVelopack()
    {
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);

        await sut.CheckForUpdatesAsync(manualCheck: true);
        await sut.DownloadUpdateAsync();
        await sut.InstallUpdateAsync();

        _manager.AppliedUpdate.Should().NotBeNull();
        _manager.AppliedUpdate!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenSkippedVersionAndAutomaticCheck_DoesNotNotify()
    {
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);
        _preferencesService.SkippedUpdateVersion.Returns("1.0.0");

        await sut.CheckForUpdatesAsync(manualCheck: false);

        sut.Status.Should().Be(UpdateStatus.Idle);
        sut.AvailableRelease.Should().BeNull();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ManualCheck_IgnoresRemindLater()
    {
        var sut = CreateSut();
        _manager.IsInstalled = true;
        _manager.UpdateToReturn = new VelopackUpdateInfo("1.0.0", "Release notes", null);
        _preferencesService.UpdateRemindLaterDate.Returns(DateTime.UtcNow.AddDays(1));

        await sut.CheckForUpdatesAsync(manualCheck: true);

        sut.Status.Should().Be(UpdateStatus.UpdateAvailable);
    }

    [Fact]
    public void SkipVersion_SetsStatusToIdleAndSavesPreference()
    {
        var sut = CreateSut();

        sut.SkipVersion("1.0.0");

        sut.Status.Should().Be(UpdateStatus.Idle);
        _preferencesService.SkippedUpdateVersion.Should().Be("1.0.0");
        _preferencesService.Received().Save();
    }

    [Fact]
    public void RemindLater_SetsStatusToIdleAndSavesDate()
    {
        var sut = CreateSut();

        sut.RemindLater(TimeSpan.FromDays(3));

        sut.Status.Should().Be(UpdateStatus.Idle);
        _preferencesService.UpdateRemindLaterDate.Should().NotBeNull();
        _preferencesService.Received().Save();
    }

    private UpdateService CreateSut() => new(_versionService, _preferencesService, _manager);

    private sealed class FakeVelopackUpdateManager : IVelopackUpdateManager
    {
        public bool IsInstalled { get; set; } = true;
        public VelopackUpdateInfo? PendingUpdate { get; set; }
        public VelopackUpdateInfo? UpdateToReturn { get; set; }
        public VelopackUpdateInfo? AppliedUpdate { get; private set; }

        public Task<VelopackUpdateInfo?> CheckForUpdatesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(UpdateToReturn);
        }

        public Task DownloadUpdatesAsync(VelopackUpdateInfo update, Action<int> progress, CancellationToken ct = default)
        {
            progress(42);
            progress(100);
            PendingUpdate = update;
            return Task.CompletedTask;
        }

        public void ApplyUpdatesAndRestart(VelopackUpdateInfo update)
        {
            AppliedUpdate = update;
        }
    }
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.Update.UpdateServiceTests"`

Expected: fails because `UpdateService` has no `IVelopackUpdateManager` constructor and statuses/properties are missing.

- [ ] **Step 3: Implement Velopack-backed service**

Update `BusLane/Services/Update/UpdateService.cs` so its constructor is:

```csharp
public UpdateService(
    IVersionService versionService,
    IPreferencesService preferencesService,
    IVelopackUpdateManager updateManager)
```

Store `_updateManager`, remove `_downloadService` and `_platform`, initialize:

```csharp
CanSelfUpdate = _updateManager.IsInstalled;
StatusMessage = CanSelfUpdate
    ? "Updates are managed by Velopack."
    : "Self-update is unavailable for this build. Download updates from GitHub Releases.";
```

Implement these properties:

```csharp
public bool CanSelfUpdate { get; private set; }
public string StatusMessage { get; private set; } = string.Empty;
```

In `CheckForUpdatesAsync`:

```csharp
if (!_updateManager.IsInstalled)
{
    CanSelfUpdate = false;
    StatusMessage = "Self-update is unavailable for this build. Download updates from GitHub Releases.";
    Status = manualCheck ? UpdateStatus.NotInstalled : UpdateStatus.Idle;
    return;
}

var pending = _updateManager.PendingUpdate;
if (pending != null)
{
    _availableRelease = ToReleaseInfo(pending, readyToRestart: true);
    StatusMessage = $"BusLane {pending.Version} is ready to install.";
    Status = UpdateStatus.ReadyToRestart;
    return;
}

Status = UpdateStatus.Checking;
var update = await _updateManager.CheckForUpdatesAsync();
if (update == null)
{
    StatusMessage = manualCheck ? "BusLane is up to date." : string.Empty;
    Status = manualCheck ? UpdateStatus.UpToDate : UpdateStatus.Idle;
    return;
}

if (!manualCheck && LoadPreferences().SkippedVersion == update.Version)
{
    Status = UpdateStatus.Idle;
    return;
}

_availableRelease = ToReleaseInfo(update, readyToRestart: false);
StatusMessage = $"BusLane {update.Version} is available.";
Status = UpdateStatus.UpdateAvailable;
```

In `DownloadUpdateAsync`, require `_availableRelease`, call `_updateManager.DownloadUpdatesAsync`, forward progress, then set `ReadyToRestart`.

In `InstallUpdateAsync`, require `_availableRelease`, set `Installing`, call `_updateManager.ApplyUpdatesAndRestart`, and preserve error handling.

Add helper:

```csharp
private static ReleaseInfo ToReleaseInfo(VelopackUpdateInfo update, bool readyToRestart) => new()
{
    Version = update.Version,
    ReleaseNotes = update.ReleaseNotes,
    PublishedAt = update.PublishedAt,
    CanSelfUpdate = true,
    IsReadyToRestart = readyToRestart
};
```

- [ ] **Step 4: Delete old update infrastructure**

Delete:

```text
BusLane/Services/Update/UpdateCheckService.cs
BusLane/Services/Update/UpdateDownloadService.cs
BusLane/Models/Update/AssetInfo.cs
BusLane.Tests/Services/Update/UpdateCheckServiceTests.cs
BusLane.Tests/Services/Update/UpdateDownloadServiceTests.cs
```

- [ ] **Step 5: Run service tests to verify GREEN**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.Update.UpdateServiceTests"`

Expected: all `UpdateServiceTests` pass.

- [ ] **Step 6: Commit**

```bash
git add BusLane/Models/Update BusLane/Services/Update BusLane.Tests/Services/Update BusLane/Program.cs
git commit -m "feat: replace updater service with Velopack"
```

## Task 4: Redesign Update Notification and Settings State

**Files:**
- Create: `BusLane.Tests/ViewModels/UpdateNotificationViewModelTests.cs`
- Modify: `BusLane/ViewModels/UpdateNotificationViewModel.cs`
- Modify: `BusLane/Views/Controls/UpdateNotificationView.axaml`
- Modify: `BusLane/ViewModels/SettingsViewModel.cs`
- Modify: `BusLane/Views/Dialogs/SettingsDialog.axaml`

- [ ] **Step 1: Write failing notification tests**

Create `BusLane.Tests/ViewModels/UpdateNotificationViewModelTests.cs`:

```csharp
namespace BusLane.Tests.ViewModels;

using BusLane.Models.Update;
using BusLane.Services.Update;
using BusLane.ViewModels;
using FluentAssertions;

public class UpdateNotificationViewModelTests
{
    [Fact]
    public void StatusChanged_WhenUpdateAvailable_ShowsDownloadAction()
    {
        var service = new FakeUpdateService
        {
            Status = UpdateStatus.UpdateAvailable,
            AvailableRelease = new ReleaseInfo { Version = "1.0.0", ReleaseNotes = "Notes" },
            StatusMessage = "BusLane 1.0.0 is available."
        };
        var sut = new UpdateNotificationViewModel(service);

        service.RaiseStatusChanged(UpdateStatus.UpdateAvailable);

        sut.IsVisible.Should().BeTrue();
        sut.PrimaryActionText.Should().Be("Download");
        sut.VersionText.Should().Be("BusLane 1.0.0 is available");
    }

    [Fact]
    public void StatusChanged_WhenReadyToRestart_ShowsRestartAction()
    {
        var service = new FakeUpdateService
        {
            Status = UpdateStatus.ReadyToRestart,
            AvailableRelease = new ReleaseInfo { Version = "1.0.0", IsReadyToRestart = true },
            StatusMessage = "BusLane 1.0.0 is ready to install."
        };
        var sut = new UpdateNotificationViewModel(service);

        service.RaiseStatusChanged(UpdateStatus.ReadyToRestart);

        sut.IsVisible.Should().BeTrue();
        sut.PrimaryActionText.Should().Be("Restart");
        sut.IsReadyToRestart.Should().BeTrue();
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public UpdateStatus Status { get; set; }
        public ReleaseInfo? AvailableRelease { get; set; }
        public double DownloadProgress { get; set; }
        public string? ErrorMessage { get; set; }
        public bool CanSelfUpdate { get; set; } = true;
        public string StatusMessage { get; set; } = string.Empty;
        public event EventHandler<UpdateStatus>? StatusChanged;
        public event EventHandler<double>? DownloadProgressChanged;
        public Task CheckForUpdatesAsync(bool manualCheck = false) => Task.CompletedTask;
        public Task DownloadUpdateAsync() => Task.CompletedTask;
        public Task InstallUpdateAsync() => Task.CompletedTask;
        public void SkipVersion(string version) { }
        public void RemindLater(TimeSpan delay) { }
        public void DismissNotification() { }
        public void RaiseStatusChanged(UpdateStatus status) => StatusChanged?.Invoke(this, status);
    }
}
```

- [ ] **Step 2: Run tests to verify RED**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.ViewModels.UpdateNotificationViewModelTests"`

Expected: fails because `PrimaryActionText` and `IsReadyToRestart` do not exist.

- [ ] **Step 3: Update notification viewmodel**

In `UpdateNotificationViewModel`, replace `IsDownloaded` with:

```csharp
[ObservableProperty]
private bool _isReadyToRestart;

[ObservableProperty]
private string _primaryActionText = "Download";
```

Update `UpdateFromServiceState`:

```csharp
IsVisible = status is UpdateStatus.UpdateAvailable
                  or UpdateStatus.Downloading
                  or UpdateStatus.ReadyToRestart
                  or UpdateStatus.Error;

IsDownloading = status == UpdateStatus.Downloading;
IsReadyToRestart = status == UpdateStatus.ReadyToRestart;
PrimaryActionText = IsReadyToRestart ? "Restart" : "Download";
ErrorMessage = _updateService.ErrorMessage;
```

Update `InstallNowAsync`:

```csharp
if (_updateService.Status == UpdateStatus.UpdateAvailable)
{
    await _updateService.DownloadUpdateAsync();
    return;
}

await _updateService.InstallUpdateAsync();
```

- [ ] **Step 4: Update notification XAML**

In `UpdateNotificationView.axaml`, replace the two Install/Restart buttons with one button:

```xml
<Button Classes="primary small"
        Command="{Binding InstallNowCommand}"
        IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNullOrEmpty}}">
    <TextBlock Text="{Binding PrimaryActionText}"/>
</Button>
```

Keep Retry, Later, and Dismiss.

- [ ] **Step 5: Update SettingsViewModel status**

Add properties:

```csharp
[ObservableProperty] private string _updateStatusMessage = string.Empty;
[ObservableProperty] private bool _canSelfUpdate = true;
```

After loading settings, initialize:

```csharp
CanSelfUpdate = _updateService?.CanSelfUpdate ?? false;
UpdateStatusMessage = _updateService?.StatusMessage ?? "Update service is unavailable.";
```

In `CheckForUpdatesNowAsync`, after the check finishes, update those properties again.

- [ ] **Step 6: Update SettingsDialog update section**

In the Updates section, add a caption bound to `SettingsViewModel.UpdateStatusMessage` under the manual check row and disable the Check button when self-update is not available:

```xml
IsEnabled="{Binding SettingsViewModel.CanSelfUpdate}"
```

Keep the current version row.

- [ ] **Step 7: Run viewmodel tests**

Run: `dotnet test --filter "FullyQualifiedName~BusLane.Tests.ViewModels.UpdateNotificationViewModelTests"`

Expected: tests pass.

- [ ] **Step 8: Commit**

```bash
git add BusLane/ViewModels/UpdateNotificationViewModel.cs BusLane/Views/Controls/UpdateNotificationView.axaml BusLane/ViewModels/SettingsViewModel.cs BusLane/Views/Dialogs/SettingsDialog.axaml BusLane.Tests/ViewModels/UpdateNotificationViewModelTests.cs
git commit -m "feat: update Velopack update UI states"
```

## Task 5: Add Velopack GitHub Release Packaging

**Files:**
- Modify: `.github/workflows/release.yml`
- Modify: `RELEASE.md`

- [ ] **Step 1: Update workflow permissions**

Ensure the workflow has:

```yaml
permissions:
  contents: write
```

- [ ] **Step 2: Add Velopack package steps per runtime job**

After each `dotnet publish` step, add Velopack steps that use the runtime channel:

```yaml
- name: Install Velopack CLI
  run: dotnet tool install --global vpk --version 0.0.1298

- name: Download previous Velopack release assets
  run: vpk download github --repoUrl https://github.com/${{ github.repository }} --channel ${{ matrix.runtime }} --token ${{ secrets.GITHUB_TOKEN }}
  continue-on-error: true

- name: Create Velopack package
  run: vpk pack --packId BusLane --packTitle BusLane --packAuthors BusLane --packVersion ${{ steps.version.outputs.version }} --packDir ./publish --mainExe BusLane --runtime ${{ matrix.runtime }} --channel ${{ matrix.runtime }} --icon BusLane/Assets/icon.png
```

For Windows, use `--mainExe BusLane.exe`, `--runtime win-x64`, and `--channel win-x64`.

For Linux, use `--mainExe BusLane`, `--runtime linux-x64`, and `--channel linux-x64`.

- [ ] **Step 3: Upload Velopack assets in create-release**

After downloading artifacts, install the Velopack CLI and upload each runtime's `Releases` directory with an explicit channel derived from the artifact folder name:

```yaml
- name: Install Velopack CLI
  run: dotnet tool install --global vpk --version 0.0.1298

- name: Upload Velopack packages
  run: |
    for releases_dir in $(find artifacts -type d -name Releases); do
      artifact_dir="$(basename "$(dirname "$releases_dir")")"
      case "$artifact_dir" in
        *win-x64*) channel="win-x64" ;;
        *linux-x64*) channel="linux-x64" ;;
        *osx-x64*) channel="osx-x64" ;;
        *osx-arm64*) channel="osx-arm64" ;;
        *) echo "Unknown Velopack artifact channel for $artifact_dir"; exit 1 ;;
      esac

      vpk upload github --outputDir "$releases_dir" --repoUrl https://github.com/${{ github.repository }} --channel "$channel" --token ${{ secrets.GITHUB_TOKEN }} --merge --publish --tag ${{ github.event_name == 'workflow_dispatch' && format('v{0}', github.event.inputs.version) || github.ref_name }}
    done
```

- [ ] **Step 4: Include Velopack output in artifacts**

Update each `actions/upload-artifact` path to include:

```yaml
Releases/**
```

- [ ] **Step 5: Update release docs**

In `RELEASE.md`, add that GitHub Releases now include Velopack release feeds and packages used by the in-app updater. State that `.deb`, `.rpm`, `.flatpak`, and `.snap` remain secondary downloads.

- [ ] **Step 6: Validate workflow YAML**

Run: `dotnet build`

Expected: project build still succeeds. Workflow upload is verified by GitHub Actions on the next release tag.

- [ ] **Step 7: Commit**

```bash
git add .github/workflows/release.yml RELEASE.md
git commit -m "ci: publish Velopack packages to GitHub releases"
```

## Task 6: Full Verification and Cleanup

**Files:**
- Review all changed files

- [ ] **Step 1: Search for old updater references**

Run:

```bash
rg -n "UpdateCheckService|UpdateDownloadService|AssetInfo|Downloaded|GitHub API|manual install|release page" BusLane BusLane.Tests
```

Expected: no stale production references. UI text can mention GitHub Releases only as fallback for non-Velopack installs.

- [ ] **Step 2: Run update tests**

Run:

```bash
dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.Update|FullyQualifiedName~BusLane.Tests.ViewModels.UpdateNotificationViewModelTests"
```

Expected: pass.

- [ ] **Step 3: Run full test suite**

Run:

```bash
dotnet test
```

Expected: pass.

- [ ] **Step 4: Run build**

Run:

```bash
dotnet build
```

Expected: pass.

- [ ] **Step 5: Review diff**

Run:

```bash
git status --short
git diff --stat
```

Expected: only Velopack update files, tests, docs, and release workflow are changed.

- [ ] **Step 6: Commit final fixes found during verification**

Commit any fixes made during Task 6:

```bash
git add .
git commit -m "chore: verify Velopack update migration"
```
