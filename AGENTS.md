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

## Code Refactoring Best Practices

### Property & Binding Refactoring
- When removing duplicate properties, remove **ALL** occurrences from parent ViewModel
- Update XAML bindings to use the new location (e.g., `{Binding Confirmation.ShowConfirmDialog}`)
- Ensure all code references (commands, event handlers, keyboard shortcuts) use new property path
- Run `dotnet build` after property removals to catch all compilation errors
- Update namespace declarations in XAML files when moving classes between namespaces

### Thread Safety Refactoring
- When adding locks, verify the add/update factories are also protected
- `ConcurrentDictionary.AddOrUpdate` guarantees atomic key insertion but NOT thread-safe value access
- Wrap list operations in dedicated thread-safe class with its own lock
- Ensure each instance of the wrapper has its own lock object (not shared)

### MVVM Component Extraction
- When extracting properties to a child ViewModel, check all parent references
- Update XAML `DataType` and `DataContext` bindings
- Update command methods to delegate to child component
- Don't create duplicate command methods - delegate existing ones instead

### General Refactoring Guidelines
- Always build after each major change to catch errors immediately
- Test the UI flow after extracting/renaming components
- Check keyboard shortcuts and event handlers reference the new property paths
- Verify all dialogs, popups, and overlays reference the correct properties

# Behavioral Guidelines (CLAUDE.md)

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
