# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BusLane is a cross-platform Azure Service Bus management tool built with Avalonia UI and .NET 10. It supports two connection modes:
1. **Azure Account Mode** - Uses Azure Identity with credential-based authentication
2. **Connection String Mode** - Direct connection using Service Bus connection strings

## Build and Test Commands

### Build
```bash
dotnet build
```

### Run the application
```bash
dotnet run --project BusLane/BusLane.csproj
```

### Run tests
```bash
dotnet test
```

### Run a single test class
```bash
dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.Infrastructure.EncryptionServiceTests"
```

### Publish for specific platforms
```bash
# macOS (Intel)
dotnet publish BusLane/BusLane.csproj -c Release -r osx-x64 --self-contained

# macOS (Apple Silicon)
dotnet publish BusLane/BusLane.csproj -c Release -r osx-arm64 --self-contained

# Windows
dotnet publish BusLane/BusLane.csproj -c Release -r win-x64 --self-contained

# Linux
dotnet publish BusLane/BusLane.csproj -c Release -r linux-x64 --self-contained
```

## Architecture

### MVVM Pattern with Component Composition

BusLane follows MVVM using CommunityToolkit.Mvvm source generators. The main view model (`MainWindowViewModel`) is a **slim coordinator** that composes specialized components rather than handling all logic directly.

Key composed components in `MainWindowViewModel`:
- **NavigationState** - Tracks current selections (subscriptions, namespaces, queues, topics)
- **MessageOperationsViewModel** - Handles message loading, filtering, selection, and bulk operations
- **ConnectionViewModel** - Manages Azure/connection string authentication and connection lifecycle
- **FeaturePanelsViewModel** - Coordinates live streaming, charts, and alerts features

This composition pattern keeps `MainWindowViewModel` focused on coordination while delegating specialized work to focused components.

### Service Bus Operations Architecture

The codebase is **mid-refactor** to unify Service Bus operations. Understanding this is critical:

#### New Unified Pattern (Use This)
- **`IServiceBusOperations`** - Unified interface for all Service Bus operations
- **`IServiceBusOperationsFactory`** - Factory that creates operations instances
- Two implementations:
  - `ConnectionStringOperations` - For connection string mode
  - `AzureCredentialOperations` - For Azure account mode

The factory pattern allows the same operations interface to work with different authentication methods.

#### Legacy Pattern (Being Removed)
- `IServiceBusService` and `IConnectionStringService` are legacy services
- Still used by `MessageOperationsViewModel` and `SendMessageViewModel`
- **TODO comments** in `Program.cs:51` indicate these should be removed after migration
- When adding new features, use `IServiceBusOperations`, not the legacy services

### Dependency Injection

All services are registered in `Program.cs:ConfigureServices()`. The main service categories:

1. **Infrastructure** - `IVersionService`, `IEncryptionService`, `IPreferencesService`, `IDialogService`
2. **Authentication** - `IAzureAuthService` (manages Azure credentials and ArmClient)
3. **Service Bus** - `IServiceBusOperationsFactory`, `IAzureResourceService` (lists subscriptions/namespaces)
4. **Storage** - `IConnectionStorageService` (persists saved connection strings with AES-256 encryption)
5. **Monitoring** - `ILiveStreamService`, `IMetricsService`, `IAlertService`, `INotificationService`

ViewModels are instantiated via DI. `MainWindowViewModel` is a singleton, while feature ViewModels are transient.

### Connection Flow

#### Azure Account Mode
1. User clicks "Sign in with Azure" → `IAzureAuthService.LoginAsync()`
2. After login → `IAzureResourceService.GetAzureSubscriptionsAsync()` loads subscriptions
3. Select subscription → `IAzureResourceService.GetNamespacesAsync()` loads namespaces
4. Select namespace → Factory creates `IAzureCredentialOperations` instance
5. Operations instance used to load queues/topics and perform message operations

#### Connection String Mode
1. User opens connection library → `IConnectionStorageService.LoadConnectionsAsync()`
2. Select saved connection → Factory creates `IConnectionStringOperations` instance
3. Operations instance used to load entities and perform message operations

Both modes converge on the same `IServiceBusOperations` interface after authentication.

### Security

Connection strings are encrypted using AES-256-CBC with machine-specific keys via `IEncryptionService`. Encrypted data is stored in:
- Windows: `%APPDATA%/BusLane/connections.json`
- macOS/Linux: `~/.config/BusLane/connections.json`

Azure tokens are stored in the system credential store (Keychain on macOS, Credential Manager on Windows).

### Monitoring Features

Live Stream, Charts, and Alerts are composed in `FeaturePanelsViewModel`:
- **Live Stream** (`ILiveStreamService`) - Real-time message streaming using System.Reactive
- **Metrics** (`IMetricsService`) - Records message counts over time for charting
- **Alerts** (`IAlertService`) - Evaluates user-defined rules and triggers notifications
- **Notifications** (`INotificationService`) - System desktop notifications

These features coordinate with the main navigation state to track selected entities.

### Versioning

The project uses MinVer for automatic versioning from Git tags:
- Tag format: `v{major}.{minor}.{patch}` (e.g., `v0.7.1`)
- `IVersionService` provides `DisplayVersion` for UI
- Version displayed in main window footer

## Key Patterns

### CommunityToolkit.Mvvm
- Use `[ObservableProperty]` for bindable properties
- Use `[RelayCommand]` for command methods
- Use `[NotifyPropertyChangedFor]` to trigger dependent property updates

### Reactive Extensions
Live streaming uses `System.Reactive` (Rx) for message streams. See `LiveStreamService` for patterns.

### Error Handling
Most async operations use try-catch with status messages displayed via `StatusMessage` property. User-facing errors should be clear and actionable.

## Test Framework

Tests use:
- **xUnit** for test framework
- **FluentAssertions** for assertions
- **NSubstitute** for mocking
- **coverlet** for code coverage

Test structure mirrors source structure: `BusLane.Tests/Services/Infrastructure/EncryptionServiceTests.cs` tests `BusLane/Services/Infrastructure/EncryptionService.cs`.

## Important Notes

- Avalonia uses compiled bindings by default (`AvaloniaUseCompiledBindingsByDefault: true`)
- When modifying ViewModels, ensure `OnPropertyChanged` is called for computed properties
- The auto-refresh timer in `MainWindowViewModel` periodically loads messages and evaluates alerts
- Session-enabled queues/subscriptions require special handling (see `requiresSession` parameter)
- Dead letter queues are accessed via `showDeadLetter` flag, not as separate entities
