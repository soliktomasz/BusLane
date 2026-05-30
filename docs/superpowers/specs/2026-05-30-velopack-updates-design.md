# Velopack Updates Design

## Goal

Replace BusLane's hand-rolled GitHub release update infrastructure with a Velopack-native update system that works with the BusLane GitHub repository and provides a clearer in-app update experience.

## Current State

BusLane currently checks `https://api.github.com/repos/soliktomasz/BusLane/releases/latest`, parses release JSON, matches assets by runtime string, downloads matching files to a temp folder, and opens the GitHub release page for manual installation. This avoids running downloaded binaries directly, but it also means updates are not applied by the app.

The release workflow already builds platform artifacts and publishes them to GitHub Releases. MinVer derives app versions from `v*` tags, and release docs describe tag-driven releases.

## Chosen Approach

Use Velopack as the update runtime and redesign the update surface around Velopack states.

Velopack will own the installable self-update channel. Existing Linux package artifacts such as `.deb`, `.rpm`, `.flatpak`, and `.snap` can continue to be published as secondary downloads, but the in-app updater will target Velopack-supported release packages and feeds.

## Application Startup

`Program.Main` will call `VelopackApp.Build().Run()` before Sentry, logging, dependency injection, or Avalonia startup. This lets Velopack handle install/update hooks without creating the full UI.

## Update Service Architecture

Keep `IUpdateService` as the app-facing update boundary so `MainWindowViewModel`, `SettingsViewModel`, and the notification control remain loosely coupled to update mechanics.

Introduce a small adapter around Velopack APIs so tests can exercise update behavior without requiring a real Velopack install:

- Create an update manager from `GithubSource("https://github.com/soliktomasz/BusLane", accessToken: null, prerelease: false, downloader: null)`.
- Report whether the app is installed through Velopack.
- Check for updates.
- Download updates with progress callbacks.
- Apply the staged update and restart.
- Detect pending updates that need restart.

`UpdateService` will translate adapter results into BusLane update state, preserve existing skip/remind-later preferences, and keep automatic check behavior. The old GitHub API parser and temp-file downloader will be removed from production update flow.

## Update States

The UI will expose Velopack-specific states:

- Not installed through Velopack: self-update is unavailable for this build.
- Checking: looking for updates.
- Up to date: manual check completed and no update was found.
- Update available: an update can be downloaded.
- Downloading: update package is downloading/preparing.
- Ready to restart: an update has been staged and can be applied.
- Error: manual update check, download, or apply failed.

Automatic checks will stay quiet when no update is available or when the app is not Velopack-installed.

## Update UI

Redesign the Settings update section and notification text around the new states:

- The update setting still controls startup/periodic automatic checks.
- Manual check shows clear status after completion.
- When Velopack is unavailable, show a disabled self-update action and a GitHub Releases fallback link.
- When an update is available, the primary action downloads it.
- When an update is staged, the primary action restarts to update.
- Progress will continue to use the existing progress event.

The notification control will avoid saying users must manually download updates when Velopack can apply them.

## GitHub Release Packaging

The release workflow will add Velopack packaging and upload steps:

- Install or invoke the same `vpk` version as the app's Velopack package.
- Download the latest existing GitHub Velopack release assets for each channel before packing, so delta packages and release feed files can be generated.
- Pack each supported runtime with a stable app id, title, version, runtime, icon, package directory, and main executable.
- Upload Velopack output to the BusLane GitHub release with `--merge` and `--publish`.
- Preserve existing secondary artifacts for users who prefer platform package managers or manual downloads.

Use stable channel names per runtime so Velopack does not mix incompatible packages: `win-x64`, `osx-x64`, `osx-arm64`, and `linux-x64`.

## Testing

Add or update tests around:

- Velopack-unavailable builds do not advertise automatic install.
- Manual check reports up-to-date when no update exists.
- Available updates transition to download-ready state.
- Download progress updates the service.
- Downloaded updates transition to ready-to-restart state.
- Skip and remind-later preferences still suppress automatic notifications.
- Manual checks ignore remind-later.

Remove tests that only validate the old GitHub JSON parser and temp-file downloader once those production types are removed.

## Non-Goals

- Do not add private repository token support.
- Do not sign Windows or macOS packages as part of this change.
- Do not remove existing Linux package artifacts.
- Do not migrate release versioning away from MinVer.
