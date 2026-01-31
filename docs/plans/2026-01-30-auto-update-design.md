# Auto-Update Feature Design

**Date:** 2026-01-30
**Branch:** auto-update
**Status:** Design Complete

---

## Overview

In-app update notifications and installation for BusLane, checking GitHub Releases and prompting users to install new versions with a non-intrusive banner UI.

---

## Architecture

### Components

1. **UpdateCheckService** - Checks GitHub Releases API for new versions
   - Runs on startup and periodically (default: every 24 hours)
   - Parses release metadata for current platform
   - Respects user skip/remind preferences

2. **UpdateDownloadService** - Manages package downloads
   - Resumable downloads with progress tracking
   - Checksum validation
   - Caches in temp directory

3. **UpdateInstallService** - Platform-specific installation
   - Windows: MSI/MSIX installer execution
   - macOS: .app bundle replacement
   - Linux: AppImage or archive extraction

4. **UpdateNotificationViewModel/View** - Banner UI
   - Non-intrusive top banner
   - States: Available, Downloading, Ready, Error

### Data Flow

```
App startup → Check GitHub API → Compare versions
    ↓
If newer and not skipped → Show banner
    ↓
User clicks Install → Download → Verify → Install → Restart
```

---

## Data Models

```csharp
public record ReleaseInfo
{
    public string Version { get; init; } = null!;
    public string ReleaseNotes { get; init; } = null!;
    public DateTime PublishedAt { get; init; }
    public Dictionary<string, AssetInfo> Assets { get; init; } = new();
    public bool IsPrerelease { get; init; }
}

public record AssetInfo
{
    public string DownloadUrl { get; init; } = null!;
    public string FileName { get; init; } = null!;
    public long Size { get; init; }
    public string Checksum { get; init; } = null!;
}

public record UpdatePreferences
{
    public DateTime? LastCheckTime { get; init; }
    public string? SkippedVersion { get; init; }
    public DateTime? RemindLaterDate { get; init; }
    public bool AutoCheckEnabled { get; init; } = true;
}
```

---

## GitHub API Integration

**Endpoint:** `GET https://api.github.com/repos/soliktomasz/BusLane/releases/latest`

**Asset Mapping:**
| Platform | Asset Pattern |
|----------|---------------|
| Windows x64 | `BusLane-{version}-win-x64.msi` |
| macOS ARM64 | `BusLane-{version}-osx-arm64.dmg` |
| macOS x64 | `BusLane-{version}-osx-x64.dmg` |
| Linux x64 | `BusLane-{version}-linux-x64.AppImage` |

**Version Comparison:** Semantic versioning against `IVersionService.CurrentVersion`

---

## UI/UX Design

### Notification Banner

- **Position:** Fixed top of main window, slides down
- **Height:** 60px, Fluent2 styling
- **Content:** Version info, release notes snippet, action buttons

### Banner States

| State | Content | Actions |
|-------|---------|---------|
| Available | "BusLane 0.10.0 is available" | Install Now, Later, Skip |
| Downloading | Progress bar + percentage | Cancel |
| Ready | "Ready to install" | Restart to Update, Later |
| Error | Brief error message | Retry, Dismiss |

### User Actions

- **Install Now:** Download → Install → Prompt restart
- **Later:** Close banner, remind in 3 days
- **Skip:** Close banner, skip this version

### Settings Integration

New "Updates" section in Settings dialog:
- Toggle: "Automatically check for updates" (default: on)
- Display: Last check time, current version
- Button: "Check Now" (manual trigger)

---

## Error Handling

| Scenario | Behavior |
|----------|----------|
| GitHub API unavailable | Silent retry with exponential backoff |
| No assets for platform | Log warning, don't show update |
| Download interrupted | Resume if possible, else restart |
| Checksum mismatch | Delete, retry once, then error |
| Installation fails | Show error, preserve file for manual install |
| Insufficient permissions | Prompt for elevation |

---

## Testing Strategy

### Unit Tests
- `UpdateCheckServiceTests`: Mock GitHub API, version comparison
- `UpdateDownloadServiceTests`: Mock HTTP, progress, resume logic
- `UpdatePreferencesTests`: Skip/remind persistence

### Integration Tests
- Full flow with local mock GitHub server
- Platform asset selection verification

### Manual Tests
- Actual update from previous version per platform
- Code signing/notarization preservation (macOS)

---

## Security

- HTTPS for all API calls and downloads
- Checksum validation before installation
- Verify package signatures where available

---

## Implementation Notes

- Use existing `IPreferencesService` for update preferences storage
- Use existing `IVersionService` for current version
- Integrate with existing Fluent2 design system
- Follow established service/ViewModel patterns in codebase
