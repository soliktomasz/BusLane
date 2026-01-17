# BusLane Agent Guidelines

## Build & Test Commands

### Build
```bash
dotnet build
```

### Run Application
```bash
dotnet run --project BusLane/BusLane.csproj
```

### Run Tests
```bash
# All tests
dotnet test

# Single test class
dotnet test --filter "FullyQualifiedName~BusLane.Tests.Services.Infrastructure.EncryptionServiceTests"

# Specific test
dotnet test --filter "FullyQualifiedName~~Encrypt_WithValidText_ReturnsEncryptedStringWithPrefix"
```

### Publish
```bash
dotnet publish BusLane/BusLane.csproj -c Release -r osx-arm64 --self-contained
```

## Code Style Guidelines

### Imports & Namespaces
- File-scoped namespaces: `namespace BusLane.Services;`
- Place `using` directives after the namespace declaration
- Order: System/standard imports → BusLane imports → third-party imports (Avalonia, CommunityToolkit, Serilog, etc.)

### Formatting
- 4-space indentation
- No explicit line length limit, but keep lines readable
- Place private fields before public properties
- Use XML doc comments for public classes, methods, and key regions
- Group related members with `#region` blocks

### Types & Nullability
- `Nullable` enabled project-wide - always handle nullable reference types
- Use `CancellationToken ct = default` as last parameter in async methods
- Records with primary constructors for immutable data models: `public record MessageInfo(string Id, string Body);`
- Interfaces prefixed with `I`: `IServiceBusOperations`
- Async methods must end with `Async` suffix
- Use nullable string overloads (`string?`) for optional string parameters

### Naming Conventions
- **Classes/Records**: PascalCase (`MainWindowViewModel`, `EncryptionService`)
- **Interfaces**: `I` prefix + PascalCase (`IServiceBusOperations`)
- **Methods**: PascalCase (`GetQueuesAsync`, `LoginAsync`)
- **Private fields**: `_camelCase` prefix (`_auth`, `_operations`)
- **Public properties**: PascalCase (`StatusMessage`, `IsActiveTab`)
- **Constants**: PascalCase (`MaxPreviewLength`)
- **Local variables**: camelCase (`queueCount`, `isValid`)
- **Async methods**: Must end with `Async` (`LoadMessagesAsync`, not `LoadMessages`)

### MVVM Pattern
- Use `CommunityToolkit.Mvvm` source generators:
  - `[ObservableProperty]` for bindable properties (generates `PropertyName`)
  - `[RelayCommand]` for command methods
  - `[NotifyPropertyChangedFor]` to trigger dependent property updates
- ViewModels inherit from `ViewModelBase` in `ViewModels/Core/`
- Main `MainWindowViewModel` is a coordinator - compose specialized components (NavigationState, MessageOperationsViewModel, ConnectionViewModel, FeaturePanelsViewModel)
- Always call `OnPropertyChanged()` for computed properties

### Dependency Injection
- Register services in `Program.cs:ConfigureServices()`
- Use `Microsoft.Extensions.DependencyInjection`
- Singleton for services/stateful ViewModels (`MainWindowViewModel`)
- Transient for feature ViewModels (`LoginViewModel`, `NamespaceViewModel`)
- Factory pattern for operations: `IServiceBusOperationsFactory` creates `IServiceBusOperations` instances

### Error Handling
- Wrap async operations in try-catch blocks
- Set `StatusMessage` property for user-facing errors
- Use `ArgumentException.ThrowIfNullOrWhiteSpace()` for validation
- Catch `Exception` for general error handling in UI contexts
- Return `null` or default values in service layer for expected failures (e.g., `GetQueueInfoAsync`)

### Async/Await
- Always `await` async calls (no `ConfigureAwait(false)` - Avalonia requires context)
- Use `Avalonia.Threading.Dispatcher.UIThread.Post()` or `InvokeAsync()` for UI updates from background threads
- Disposables should be awaited or used with `await using` pattern

### Testing
- **Framework**: xUnit, **Assertions**: FluentAssertions, **Mocking**: NSubstitute
- Test naming: `MethodName_StateUnderTest_ExpectedResult`
- Arrange-Act-Assert structure with comments
- Use `_sut` for System Under Test
- Use `[Theory]` with `[InlineData]` for parameterized tests
- Mirror source structure: `BusLane.Tests/Services/Infrastructure/` tests `BusLane/Services/Infrastructure/`

### Security
- Never log or expose connection strings or Azure tokens
- Use `IEncryptionService` for sensitive data storage
- Azure tokens stored in system credential store (Keychain/Credential Manager)
- Do not commit `.env` or credential files

### Avalonia-Specific
- Compiled bindings enabled by default
- All ViewModels must be public for compiled bindings
- Use `OnPropertyChanged()` for computed properties
- Views in `Views/`, ViewModels in `ViewModels/`
- Dialogs in `Views/Dialogs/`

### Service Bus Architecture (Important!)
- **Use `IServiceBusOperations`** - the unified interface for all Service Bus operations
- **Legacy services** (`IServiceBusService`, `IConnectionStringService`) are being phased out
- Factory pattern: `IServiceBusOperationsFactory.CreateFromConnectionString()` / `.CreateFromAzureCredential()`
- Dead letter queues accessed via `showDeadLetter` flag, not as separate entities
- Session-enabled queues require `requiresSession` parameter

### Reactive Extensions
- Use `System.Reactive` for live streaming
- Properly dispose `IDisposable` subscriptions to prevent memory leaks

### Logging
- Use Serilog: `Log.Debug()`, `Log.Information()`, `Log.Warning()`, `Log.Error()`
- Structured logging with message templates: `Log.Information("Resent {Count} messages to {Entity}", count, entityName);`
