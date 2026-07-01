# Subscription Creation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add basic Azure Service Bus subscription creation from topic rows, with an options-based service contract ready for later advanced settings.

**Architecture:** Add `SubscriptionCreationOptions` and `CreateSubscriptionAsync` to the unified Service Bus operations interface. Keep create dialog state in `MainWindowViewModel`, because entity selection and topic subscription loading already live there. Add compact topic-row action plus modal overlay in `EntityTreeView.axaml`.

**Tech Stack:** .NET, Avalonia XAML, CommunityToolkit.Mvvm source generators, Azure.Messaging.ServiceBus, Azure.ResourceManager.ServiceBus, xUnit, FluentAssertions, NSubstitute.

---

## File Map

- Modify `BusLane/Services/ServiceBus/IServiceBusOperations.cs`: add `SubscriptionCreationOptions` record and service method.
- Modify `BusLane/Services/ServiceBus/ServiceBusOperations.cs`: add shared validation/mapping helper for connection-string creation options.
- Modify `BusLane/Services/ServiceBus/ConnectionStringOperations.cs`: implement `CreateSubscriptionAsync` using `ServiceBusAdministrationClient`.
- Modify `BusLane/Services/ServiceBus/AzureCredentialOperations.cs`: implement `CreateSubscriptionAsync` using ARM topic subscription collection.
- Modify `BusLane/ViewModels/MainWindowViewModel.cs`: add dialog state and commands.
- Modify `BusLane/Views/Controls/EntityTreeView.axaml`: add topic create button and dialog overlay.
- Modify `BusLane.Tests/Services/ServiceBus/ServiceBusOperationsTests.cs`: add option mapping/validation tests.
- Modify `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`: add create subscription command tests.
- Modify `BusLane.Tests/Views/EntityTreeViewTests.cs`: assert XAML exposes create command and dialog bindings.

---

### Task 1: Service Contract Tests

**Files:**
- Modify: `BusLane.Tests/Services/ServiceBus/ServiceBusOperationsTests.cs`
- Modify later: `BusLane/Services/ServiceBus/IServiceBusOperations.cs`
- Modify later: `BusLane/Services/ServiceBus/ServiceBusOperations.cs`

- [ ] **Step 1: Write failing tests**

Add these tests to `ServiceBusOperationsTests`:

```csharp
[Fact]
public void BuildCreateSubscriptionOptions_WithSessionOption_MapsTopicNameSubscriptionNameAndRequiresSession()
{
    // Arrange
    var options = new SubscriptionCreationOptions("processor", RequiresSession: true);

    // Act
    var sdkOptions = ServiceBusOperations.BuildCreateSubscriptionOptions("orders-topic", options);

    // Assert
    sdkOptions.TopicName.Should().Be("orders-topic");
    sdkOptions.SubscriptionName.Should().Be("processor");
    sdkOptions.RequiresSession.Should().BeTrue();
}

[Theory]
[InlineData("")]
[InlineData(" ")]
public void BuildCreateSubscriptionOptions_WithBlankTopicName_Throws(string topicName)
{
    // Arrange
    var options = new SubscriptionCreationOptions("processor");

    // Act
    var act = () => ServiceBusOperations.BuildCreateSubscriptionOptions(topicName, options);

    // Assert
    act.Should().Throw<ArgumentException>()
        .WithParameterName("topicName");
}

[Theory]
[InlineData("")]
[InlineData(" ")]
public void BuildCreateSubscriptionOptions_WithBlankSubscriptionName_Throws(string subscriptionName)
{
    // Arrange
    var options = new SubscriptionCreationOptions(subscriptionName);

    // Act
    var act = () => ServiceBusOperations.BuildCreateSubscriptionOptions("orders-topic", options);

    // Assert
    act.Should().Throw<ArgumentException>()
        .WithParameterName("Name");
}
```

- [ ] **Step 2: Run tests to verify red**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~BusLane.Tests.Services.ServiceBus.ServiceBusOperationsTests"
```

Expected: compile fails because `SubscriptionCreationOptions` and `BuildCreateSubscriptionOptions` do not exist.

- [ ] **Step 3: Add minimal contract and helper**

In `IServiceBusOperations.cs`, add this record near `NamespaceInfo`:

```csharp
/// <summary>
/// Options used when creating a Service Bus subscription.
/// </summary>
public record SubscriptionCreationOptions(
    string Name,
    bool RequiresSession = false);
```

Add this method to `IServiceBusOperations` after `GetSubscriptionsAsync`:

```csharp
Task CreateSubscriptionAsync(
    string topicName,
    SubscriptionCreationOptions options,
    CancellationToken ct = default);
```

In `ServiceBusOperations.cs`, add `using Azure.Messaging.ServiceBus.Administration;` and this helper:

```csharp
public static CreateSubscriptionOptions BuildCreateSubscriptionOptions(
    string topicName,
    SubscriptionCreationOptions options)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
    ArgumentNullException.ThrowIfNull(options);
    ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);

    return new CreateSubscriptionOptions(topicName, options.Name)
    {
        RequiresSession = options.RequiresSession
    };
}
```

- [ ] **Step 4: Run tests to verify green for helper**

Run same command.

Expected: helper tests pass, compile may still fail because concrete classes have not implemented new interface method. If compile fails on missing implementations, continue Task 2.

---

### Task 2: Service Implementations

**Files:**
- Modify: `BusLane/Services/ServiceBus/ConnectionStringOperations.cs`
- Modify: `BusLane/Services/ServiceBus/AzureCredentialOperations.cs`

- [ ] **Step 1: Implement connection-string creation**

Add to `ConnectionStringOperations` near `GetSubscriptionsAsync`:

```csharp
public async Task CreateSubscriptionAsync(
    string topicName,
    SubscriptionCreationOptions options,
    CancellationToken ct = default)
{
    var sdkOptions = ServiceBusOperations.BuildCreateSubscriptionOptions(topicName, options);
    await AdminClient.CreateSubscriptionAsync(sdkOptions, ct);
}
```

- [ ] **Step 2: Implement Azure credential creation**

Add `using Azure;`, `using Azure.ResourceManager;`, and `using Azure.ResourceManager.ServiceBus.Models;` to `AzureCredentialOperations.cs` if needed by compiler.

Add to `AzureCredentialOperations` near `GetSubscriptionsAsync`:

```csharp
public async Task CreateSubscriptionAsync(
    string topicName,
    SubscriptionCreationOptions options,
    CancellationToken ct = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
    ArgumentNullException.ThrowIfNull(options);
    ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);

    var ns = _getNamespaceResource();
    var topic = await ns.GetServiceBusTopicAsync(topicName, ct);
    var data = new ServiceBusSubscriptionData
    {
        RequiresSession = options.RequiresSession
    };

    await topic.Value.GetServiceBusSubscriptions()
        .CreateOrUpdateAsync(WaitUntil.Completed, options.Name, data, ct);
}
```

- [ ] **Step 3: Run service tests**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~BusLane.Tests.Services.ServiceBus.ServiceBusOperationsTests"
```

Expected: PASS. If Azure ARM API signatures differ, adjust `CreateOrUpdateAsync` call to compiler-reported signature while keeping same behavior.

---

### Task 3: ViewModel Tests

**Files:**
- Modify: `BusLane.Tests/ViewModels/MainWindowViewModelTests.cs`
- Modify later: `BusLane/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 1: Write failing validation test**

Add test before helper methods:

```csharp
[Fact]
public async Task CreateSubscriptionAsync_WithBlankName_ShowsValidationAndDoesNotCallOperations()
{
    // Arrange
    var preferences = new TestPreferencesService();
    var operations = Substitute.For<IConnectionStringOperations>();
    var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
    operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
    operations.GetTopicInfoAsync("orders-topic", Arg.Any<CancellationToken>())
        .Returns(new TopicInfo("orders-topic", 1024, 0, null, TimeSpan.FromDays(14)));
    operations.GetSubscriptionsAsync("orders-topic", Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<IEnumerable<SubscriptionInfo>>([]));

    using var sut = CreateSut(preferences, operationsFactory: operationsFactory);
    var tab = CreateConnectedTopicTab("tab-1", preferences, operationsFactory, operations, "orders-topic");
    sut.ConnectionTabs.Add(tab);
    sut.ActiveTab = tab;
    var topic = tab.Navigation.Topics.Single();

    // Act
    sut.OpenCreateSubscriptionDialogCommand.Execute(topic);
    sut.NewSubscriptionName = " ";
    await sut.CreateSubscriptionCommand.ExecuteAsync(null);

    // Assert
    sut.ShowCreateSubscriptionDialog.Should().BeTrue();
    sut.StatusMessage.Should().Be("Subscription name is required");
    await operations.DidNotReceive().CreateSubscriptionAsync(
        Arg.Any<string>(),
        Arg.Any<SubscriptionCreationOptions>(),
        Arg.Any<CancellationToken>());
}
```

- [ ] **Step 2: Write failing success test**

Add:

```csharp
[Fact]
public async Task CreateSubscriptionAsync_WithValidName_CreatesReloadsAndSelectsSubscription()
{
    // Arrange
    var preferences = new TestPreferencesService();
    var operations = Substitute.For<IConnectionStringOperations>();
    var operationsFactory = Substitute.For<IServiceBusOperationsFactory>();
    var initialSubscriptions = new[]
    {
        new SubscriptionInfo("existing", "orders-topic", 0, 0, 0, null, false)
    };
    var refreshedSubscriptions = new[]
    {
        initialSubscriptions[0],
        new SubscriptionInfo("processor", "orders-topic", 0, 0, 0, null, true)
    };

    operationsFactory.CreateFromConnectionString(Arg.Any<string>()).Returns(operations);
    operations.GetTopicInfoAsync("orders-topic", Arg.Any<CancellationToken>())
        .Returns(new TopicInfo("orders-topic", 1024, 1, null, TimeSpan.FromDays(14)));
    operations.GetSubscriptionsAsync("orders-topic", Arg.Any<CancellationToken>())
        .Returns(
            Task.FromResult<IEnumerable<SubscriptionInfo>>(initialSubscriptions),
            Task.FromResult<IEnumerable<SubscriptionInfo>>(refreshedSubscriptions));
    operations.PeekMessagesAsync(
            "orders-topic",
            "processor",
            Arg.Any<int>(),
            null,
            false,
            true,
            null,
            Arg.Any<CancellationToken>())
        .Returns(Task.FromResult<IEnumerable<MessageInfo>>([]));

    using var sut = CreateSut(preferences, operationsFactory: operationsFactory);
    var tab = CreateConnectedTopicTab("tab-1", preferences, operationsFactory, operations, "orders-topic");
    sut.ConnectionTabs.Add(tab);
    sut.ActiveTab = tab;
    var topic = tab.Navigation.Topics.Single();

    // Act
    sut.OpenCreateSubscriptionDialogCommand.Execute(topic);
    sut.NewSubscriptionName = "processor";
    sut.NewSubscriptionRequiresSession = true;
    await sut.CreateSubscriptionCommand.ExecuteAsync(null);

    // Assert
    await operations.Received(1).CreateSubscriptionAsync(
        "orders-topic",
        Arg.Is<SubscriptionCreationOptions>(options =>
            options.Name == "processor" &&
            options.RequiresSession),
        Arg.Any<CancellationToken>());
    sut.ShowCreateSubscriptionDialog.Should().BeFalse();
    topic.IsExpanded.Should().BeTrue();
    topic.Subscriptions.Should().ContainSingle(subscription => subscription.Name == "processor");
    tab.Navigation.SelectedSubscription.Should().NotBeNull();
    tab.Navigation.SelectedSubscription!.Name.Should().Be("processor");
    sut.StatusMessage.Should().Be("Subscription 'processor' created");
}
```

- [ ] **Step 3: Add test helper**

Add near `CreateConnectedQueueTab`:

```csharp
private static ConnectionTabViewModel CreateConnectedTopicTab(
    string tabId,
    TestPreferencesService preferences,
    IServiceBusOperationsFactory operationsFactory,
    IServiceBusOperations operations,
    string topicName)
{
    var tab = CreateTab(tabId, preferences);
    var connection = SavedConnection.Create(
        "Orders Topic",
        "Endpoint=sb://orders.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
        ConnectionType.Topic,
        entityName: topicName);

    tab.ConnectWithConnectionStringAsync(connection, operationsFactory).GetAwaiter().GetResult();
    return tab;
}
```

- [ ] **Step 4: Run tests to verify red**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~BusLane.Tests.ViewModels.MainWindowViewModelTests"
```

Expected: compile fails because dialog properties and commands do not exist.

---

### Task 4: ViewModel Implementation

**Files:**
- Modify: `BusLane/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 1: Add observable properties**

Add near other UI state properties:

```csharp
[ObservableProperty] private bool _showCreateSubscriptionDialog;
[ObservableProperty] private TopicInfo? _createSubscriptionTopic;
[ObservableProperty] private string _newSubscriptionName = "";
[ObservableProperty] private bool _newSubscriptionRequiresSession;
[ObservableProperty] private bool _isCreatingSubscription;
```

- [ ] **Step 2: Add open and close commands**

Add in `#region Entity Selection` or `#region UI Commands`:

```csharp
[RelayCommand]
private void OpenCreateSubscriptionDialog(TopicInfo topic)
{
    CreateSubscriptionTopic = topic;
    NewSubscriptionName = string.Empty;
    NewSubscriptionRequiresSession = false;
    ShowCreateSubscriptionDialog = true;
}

[RelayCommand]
private void CloseCreateSubscriptionDialog()
{
    ShowCreateSubscriptionDialog = false;
    CreateSubscriptionTopic = null;
    NewSubscriptionName = string.Empty;
    NewSubscriptionRequiresSession = false;
}
```

- [ ] **Step 3: Add create command**

Add:

```csharp
[RelayCommand]
private async Task CreateSubscriptionAsync()
{
    var topic = CreateSubscriptionTopic;
    var operations = ActiveTab?.Operations ?? _operations;

    if (topic == null || operations == null)
    {
        StatusMessage = "Select a topic before creating a subscription";
        return;
    }

    var subscriptionName = NewSubscriptionName.Trim();
    if (string.IsNullOrWhiteSpace(subscriptionName))
    {
        StatusMessage = "Subscription name is required";
        return;
    }

    IsCreatingSubscription = true;
    StatusMessage = $"Creating subscription '{subscriptionName}'...";

    try
    {
        var options = new SubscriptionCreationOptions(subscriptionName, NewSubscriptionRequiresSession);
        await operations.CreateSubscriptionAsync(topic.Name, options);
        await ReloadTopicSubscriptionsAsync(topic, operations);

        var createdSubscription = topic.Subscriptions.FirstOrDefault(subscription =>
            string.Equals(subscription.Name, subscriptionName, StringComparison.OrdinalIgnoreCase));

        if (createdSubscription != null)
        {
            topic.IsExpanded = true;
            await SelectSubscriptionAsync(createdSubscription);
        }

        StatusMessage = $"Subscription '{subscriptionName}' created";
        CloseCreateSubscriptionDialog();
    }
    catch (Exception ex)
    {
        StatusMessage = $"Unable to create subscription: {ex.Message}";
    }
    finally
    {
        IsCreatingSubscription = false;
    }
}
```

- [ ] **Step 4: Add reload helper and reuse in load command**

Add helper near `LoadTopicSubscriptionsAsync`:

```csharp
private async Task ReloadTopicSubscriptionsAsync(TopicInfo topic, IServiceBusOperations operations)
{
    var subs = (await operations.GetSubscriptionsAsync(topic.Name)).ToList();

    topic.Subscriptions.Clear();
    foreach (var sub in subs)
        topic.Subscriptions.Add(sub);
    topic.SubscriptionsLoaded = true;

    if (CurrentNavigation.SelectedTopic == topic ||
        string.Equals(CurrentNavigation.CurrentEntityName, topic.Name, StringComparison.OrdinalIgnoreCase))
    {
        CurrentNavigation.TopicSubscriptions.Clear();
        foreach (var sub in subs)
            CurrentNavigation.TopicSubscriptions.Add(sub);
    }
}
```

Replace body of `LoadTopicSubscriptionsAsync` after `topic.IsLoadingSubscriptions = true;` with:

```csharp
try
{
    await ReloadTopicSubscriptionsAsync(topic, operations);
}
catch (Exception ex)
{
    StatusMessage = $"Error loading subscriptions: {ex.Message}";
}
finally
{
    topic.IsLoadingSubscriptions = false;
}
```

- [ ] **Step 5: Run viewmodel tests**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~BusLane.Tests.ViewModels.MainWindowViewModelTests"
```

Expected: PASS for new tests and existing viewmodel tests.

---

### Task 5: XAML UI and View Tests

**Files:**
- Modify: `BusLane.Tests/Views/EntityTreeViewTests.cs`
- Modify: `BusLane/Views/Controls/EntityTreeView.axaml`

- [ ] **Step 1: Write failing XAML test**

Add:

```csharp
[Fact]
public void EntityTreeView_ExposesCreateSubscriptionActionAndDialog()
{
    // Arrange
    var xaml = File.ReadAllText(GetConnectionTreePath());

    // Assert
    xaml.Should().Contain("OpenCreateSubscriptionDialogCommand");
    xaml.Should().Contain("ShowCreateSubscriptionDialog");
    xaml.Should().Contain("CreateSubscriptionCommand");
    xaml.Should().Contain("NewSubscriptionRequiresSession");
}
```

- [ ] **Step 2: Run view test to verify red**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~BusLane.Tests.Views.EntityTreeViewTests"
```

Expected: FAIL because XAML does not contain create subscription bindings.

- [ ] **Step 3: Add topic row create button**

In `EntityTreeView.axaml`, change topic header inner grid from `ColumnDefinitions="Auto,*,Auto"` to:

```xml
<Grid ColumnDefinitions="Auto,*,Auto,Auto" ColumnSpacing="8">
```

Keep existing icon/name/count, then add after count badge:

```xml
<Button Grid.Column="3"
        Classes="icon-button"
        Command="{Binding $parent[Window].DataContext.OpenCreateSubscriptionDialogCommand}"
        CommandParameter="{Binding}"
        ToolTip.Tip="Create subscription"
        VerticalAlignment="Center">
    <LucideIcon Kind="Plus" Size="12"/>
</Button>
```

- [ ] **Step 4: Add dialog overlay**

Wrap existing root `Border Classes="pane-card entity-pane"` in a root `Grid`, and add this sibling overlay after the border:

```xml
<Border IsVisible="{Binding ShowCreateSubscriptionDialog}"
        Background="#99000000"
        ZIndex="20">
    <Border Classes="dialog-card"
            Width="360"
            HorizontalAlignment="Center"
            VerticalAlignment="Center"
            Padding="20">
        <StackPanel Spacing="14">
            <Grid ColumnDefinitions="*,Auto">
                <TextBlock Grid.Column="0"
                           Text="Create subscription"
                           Classes="title-small"/>
                <Button Grid.Column="1"
                        Classes="icon-button"
                        Command="{Binding CloseCreateSubscriptionDialogCommand}">
                    <LucideIcon Kind="X" Size="14"/>
                </Button>
            </Grid>

            <StackPanel Spacing="6">
                <TextBlock Text="Topic" Classes="caption"/>
                <TextBox Text="{Binding CreateSubscriptionTopic.Name}"
                         IsReadOnly="True"/>
            </StackPanel>

            <StackPanel Spacing="6">
                <TextBlock Text="Subscription name" Classes="caption"/>
                <TextBox Text="{Binding NewSubscriptionName, Mode=TwoWay}"
                         Watermark="processor"/>
            </StackPanel>

            <CheckBox Content="Requires session"
                      IsChecked="{Binding NewSubscriptionRequiresSession, Mode=TwoWay}"/>

            <Grid ColumnDefinitions="*,Auto,Auto" ColumnSpacing="8">
                <ProgressBar Grid.Column="0"
                             IsIndeterminate="True"
                             IsVisible="{Binding IsCreatingSubscription}"
                             Height="2"
                             VerticalAlignment="Center"/>
                <Button Grid.Column="1"
                        Content="Cancel"
                        Command="{Binding CloseCreateSubscriptionDialogCommand}"/>
                <Button Grid.Column="2"
                        Content="Create"
                        Classes="primary"
                        Command="{Binding CreateSubscriptionCommand}"
                        IsEnabled="{Binding IsCreatingSubscription, Converter={x:Static BoolConverters.Not}}"/>
            </Grid>
        </StackPanel>
    </Border>
</Border>
```

If `Watermark` is not accepted by Avalonia version, use `PlaceholderText="processor"` to match existing text boxes.

- [ ] **Step 5: Run view tests**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~BusLane.Tests.Views.EntityTreeViewTests"
```

Expected: PASS.

---

### Task 6: Full Verification

**Files:**
- All files touched above.

- [ ] **Step 1: Run targeted tests**

Run:

```bash
dotnet test BusLane.Tests/BusLane.Tests.csproj --filter "FullyQualifiedName~BusLane.Tests.Services.ServiceBus.ServiceBusOperationsTests|FullyQualifiedName~BusLane.Tests.ViewModels.MainWindowViewModelTests|FullyQualifiedName~BusLane.Tests.Views.EntityTreeViewTests"
```

Expected: PASS.

- [ ] **Step 2: Run build**

Run:

```bash
dotnet build
```

Expected: build succeeds with 0 errors.

- [ ] **Step 3: Inspect diff**

Run:

```bash
git diff -- BusLane/Services/ServiceBus/IServiceBusOperations.cs BusLane/Services/ServiceBus/ServiceBusOperations.cs BusLane/Services/ServiceBus/ConnectionStringOperations.cs BusLane/Services/ServiceBus/AzureCredentialOperations.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane/Views/Controls/EntityTreeView.axaml BusLane.Tests/Services/ServiceBus/ServiceBusOperationsTests.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs BusLane.Tests/Views/EntityTreeViewTests.cs
```

Expected: diff only contains subscription creation contract, commands, UI, and tests.

- [ ] **Step 4: Commit implementation**

Run:

```bash
git add BusLane/Services/ServiceBus/IServiceBusOperations.cs BusLane/Services/ServiceBus/ServiceBusOperations.cs BusLane/Services/ServiceBus/ConnectionStringOperations.cs BusLane/Services/ServiceBus/AzureCredentialOperations.cs BusLane/ViewModels/MainWindowViewModel.cs BusLane/Views/Controls/EntityTreeView.axaml BusLane.Tests/Services/ServiceBus/ServiceBusOperationsTests.cs BusLane.Tests/ViewModels/MainWindowViewModelTests.cs BusLane.Tests/Views/EntityTreeViewTests.cs
git commit -m "feat: add subscription creation"
```

Expected: commit succeeds.

---

## Self-Review

- Spec coverage: service options object, connection-string creation, Azure credential creation, basic dialog, topic-row action, refresh/select behavior, and tests are covered.
- Placeholder scan: no placeholder tasks remain; Azure ARM call has explicit code plus compiler-adjustment note because SDK minor signatures can vary.
- Type consistency: `SubscriptionCreationOptions`, `CreateSubscriptionAsync`, `OpenCreateSubscriptionDialogCommand`, and dialog property names match across tasks.
