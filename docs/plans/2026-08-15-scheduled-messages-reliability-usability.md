# Scheduled Messages Reliability and Usability Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Keep current scheduled-message records out of the legacy/stale path and make the Scheduled Messages panel clear, labeled, accessible, and visually exclusive with its empty state.

**Architecture:** Correct schema-version detection at the JSON store boundary while retaining the existing legacy migration. Restructure only `ScheduledMessagesView.axaml`, using existing BusLane styles and bindings; no new view-model state or theme tokens are needed.

**Tech Stack:** C# 13, .NET 10, Avalonia 12, CommunityToolkit.Mvvm, xUnit, FluentAssertions, NSubstitute

---

### Task 1: Reproduce and fix current-schema downgrading

**Files:**
- Modify: `BusLane.Tests/Services/ServiceBus/ScheduledMessageStoreTests.cs`
- Modify: `BusLane/Services/ServiceBus/ScheduledMessageStore.cs:167-195`

**Step 1: Write the failing current-schema regression test**

Add a test that persists a current record through the store and reads it back:

```csharp
[Fact]
public async Task LoadAsync_WithCurrentSchemaRecord_PreservesCurrentSchema()
{
    var path = Path.Combine(Path.GetTempPath(), $"buslane-scheduled-{Guid.NewGuid():N}.json");
    var encryption = Substitute.For<IEncryptionService>();
    encryption.Decrypt("encrypted").Returns("{}");
    var sut = new ScheduledMessageStore(encryption, TimeProvider.System, path);
    var current = new ScheduledMessageIndexEntry
    {
        RecordId = "record-1",
        EntityName = "orders",
        SequenceNumber = 42,
        ScheduledEnqueueTime = DateTimeOffset.UtcNow.AddHours(1),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        EncryptedPayload = "encrypted"
    };

    try
    {
        await sut.AddAsync(current);

        var loaded = (await sut.LoadAsync()).Single();
        loaded.SchemaVersion.Should().Be(ScheduledMessageIndexEntry.CurrentSchemaVersion);
        loaded.RecordId.Should().Be("record-1");
        loaded.IsLegacyLimited.Should().BeFalse();
    }
    finally
    {
        File.Delete(path);
    }
}
```

**Step 2: Run the focused test and verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessageStoreTests.LoadAsync_WithCurrentSchemaRecord_PreservesCurrentSchema"
```

Expected: FAIL because the reader checks for `SchemaVersion`, does not find persisted `schemaVersion`, and rewrites the record as schema 1.

**Step 3: Implement the minimal store fix**

Change the schema-presence check to the serializer’s camelCase property contract:

```csharp
if (!element.TryGetProperty(
        JsonNamingPolicy.CamelCase.ConvertName(nameof(ScheduledMessageIndexEntry.SchemaVersion)),
        out _))
{
    entry = entry with
    {
        SchemaVersion = 1,
        RecordId = $"{entry.EntityName}:{entry.SequenceNumber}",
        UpdatedAt = entry.UpdatedAt == default ? entry.CreatedAt : entry.UpdatedAt
    };
}
```

**Step 4: Run current and legacy schema tests and verify GREEN**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessageStoreTests.LoadAsync_WithCurrentSchemaRecord_PreservesCurrentSchema|FullyQualifiedName~ScheduledMessageStoreTests.LoadAsync_WithLegacyRecord_ReturnsLimitedEntry"
```

Expected: both tests PASS; current records remain schema 2 and schema-less records migrate to schema 1.

**Step 5: Commit the reliability fix**

```bash
rtk git add BusLane/Services/ServiceBus/ScheduledMessageStore.cs BusLane.Tests/Services/ServiceBus/ScheduledMessageStoreTests.cs
rtk git commit -m "fix: preserve scheduled message schema version"
```

### Task 2: Define the improved scheduled-messages view contract

**Files:**
- Modify: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`
- Test: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`

**Step 1: Extend the view contract test**

Require the XAML to contain the new usability and accessibility contracts:

```csharp
foreach (var value in new[]
         {
             "Filters", "Connection filter", "Entity filter", "Environment filter",
             "Status filter", "Time range filter", "FilteredEntries.Count",
             "Scheduled", "Message / status", "Actions",
             "No scheduled messages match these filters",
             "IsVisible=\"{Binding !IsEmpty}\""
         })
{
    xaml.Should().Contain(value);
}
```

Retain the existing command and lifecycle assertions.

**Step 2: Run the focused test and verify RED**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessagesViewTests"
```

Expected: FAIL because the current filter controls have placeholders only, the result content has no empty-state gate, and the list has no headings.

**Step 3: Commit the failing UI contract test**

```bash
rtk git add BusLane.Tests/Views/ScheduledMessagesViewTests.cs
rtk git commit -m "test: define scheduled messages usability contract"
```

### Task 3: Implement the scheduled-messages usability redesign

**Files:**
- Modify: `BusLane/Views/Controls/ScheduledMessagesView.axaml:5-112`
- Test: `BusLane.Tests/Views/ScheduledMessagesViewTests.cs`

**Step 1: Replace the command and filter areas**

Keep the root rows but replace rows 0 and 1 with two existing-token surfaces:

```xml
<Border Grid.Row="0" Classes="dialog-section-surface" Margin="16,12,16,8" Padding="12">
    <Grid ColumnDefinitions="*,Auto" ColumnSpacing="16">
        <StackPanel Grid.Column="0" Spacing="4">
            <TextBlock Text="Search" Classes="field-label"/>
            <TextBox Text="{Binding SearchText, Mode=TwoWay}"
                     PlaceholderText="Connection, entity, message, body, or property"
                     AutomationProperties.Name="Search scheduled messages"
                     TabIndex="0"/>
        </StackPanel>
        <StackPanel Grid.Column="1" Spacing="4" VerticalAlignment="Bottom">
            <TextBlock Text="View" Classes="field-label"/>
            <StackPanel Orientation="Horizontal" Spacing="6">
                <Button Classes="secondary small" Command="{Binding ShowListCommand}" Content="List" TabIndex="6"/>
                <Button Classes="secondary small" Command="{Binding ShowCalendarCommand}" Content="Calendar" TabIndex="7"/>
                <Button Classes="secondary small" Command="{Binding RefreshCommand}" TabIndex="8">
                    <StackPanel Orientation="Horizontal" Spacing="6">
                        <LucideIcon Kind="RefreshCw" Size="13"/>
                        <TextBlock Text="Refresh"/>
                    </StackPanel>
                </Button>
            </StackPanel>
        </StackPanel>
    </Grid>
</Border>

<Border Grid.Row="1" Classes="dialog-section-surface" Margin="16,0,16,12" Padding="12">
    <StackPanel Spacing="10">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <LucideIcon Kind="ListFilter" Size="14"/>
            <TextBlock Text="Filters" Classes="body-strong"/>
        </StackPanel>
        <WrapPanel Orientation="Horizontal">
            <StackPanel Width="170" Margin="0,0,8,8">
                <TextBlock Text="Connection" Classes="field-label"/>
                <ComboBox ItemsSource="{Binding ConnectionOptions}"
                          SelectedItem="{Binding SelectedConnection}"
                          AutomationProperties.Name="Connection filter" TabIndex="1"/>
            </StackPanel>
            <StackPanel Width="170" Margin="0,0,8,8">
                <TextBlock Text="Entity" Classes="field-label"/>
                <ComboBox ItemsSource="{Binding EntityOptions}"
                          SelectedItem="{Binding SelectedEntity}"
                          AutomationProperties.Name="Entity filter" TabIndex="2"/>
            </StackPanel>
            <StackPanel Width="170" Margin="0,0,8,8">
                <TextBlock Text="Environment" Classes="field-label"/>
                <ComboBox ItemsSource="{Binding EnvironmentOptions}"
                          SelectedItem="{Binding SelectedEnvironment}"
                          AutomationProperties.Name="Environment filter" TabIndex="3"/>
            </StackPanel>
            <StackPanel Width="170" Margin="0,0,8,8">
                <TextBlock Text="Status" Classes="field-label"/>
                <ComboBox ItemsSource="{Binding StatusOptions}"
                          SelectedItem="{Binding SelectedStatus}"
                          AutomationProperties.Name="Status filter" TabIndex="4"/>
            </StackPanel>
            <StackPanel Width="170" Margin="0,0,0,8">
                <TextBlock Text="Time range" Classes="field-label"/>
                <ComboBox ItemsSource="{Binding TimeRangeOptions}"
                          SelectedItem="{Binding SelectedTimeRange}"
                          AutomationProperties.Name="Time range filter" TabIndex="5"/>
            </StackPanel>
        </WrapPanel>
    </StackPanel>
</Border>
```

**Step 2: Gate result content and add result/list headings**

Inside row 2, wrap list/calendar content in a grid that is hidden for the empty projection:

```xml
<Grid IsVisible="{Binding !IsEmpty}" RowDefinitions="Auto,*">
    <Grid Grid.Row="0" ColumnDefinitions="*,Auto" Margin="16,0,16,8">
        <TextBlock Text="Scheduled messages" Classes="body-strong"/>
        <TextBlock Grid.Column="1"
                   Text="{Binding FilteredEntries.Count, StringFormat='{}{0} results'}"
                   Classes="caption"/>
    </Grid>

    <ScrollViewer Grid.Row="1" IsVisible="{Binding !IsCalendarMode}">
        <StackPanel>
            <Border Background="{DynamicResource SurfaceSubtle}"
                    BorderBrush="{DynamicResource BorderDefault}"
                    BorderThickness="0,0,0,1" Margin="16,0,16,8" Padding="12,8">
                <Grid ColumnDefinitions="150,160,160,*,Auto" ColumnSpacing="10">
                    <TextBlock Text="Scheduled" Classes="field-label"/>
                    <TextBlock Grid.Column="1" Text="Connection" Classes="field-label"/>
                    <TextBlock Grid.Column="2" Text="Entity" Classes="field-label"/>
                    <TextBlock Grid.Column="3" Text="Message / status" Classes="field-label"/>
                    <TextBlock Grid.Column="4" Text="Actions" Classes="field-label"/>
                </Grid>
            </Border>
            <!-- Existing ItemsControl and row template remain here unchanged. -->
        </StackPanel>
    </ScrollViewer>

    <!-- Existing calendar grid remains here with Grid.Row="1". -->
</Grid>
```

Change the empty state to:

```xml
<Border Classes="empty-state" Margin="24" IsVisible="{Binding IsEmpty}"
        HorizontalAlignment="Stretch" VerticalAlignment="Center">
    <StackPanel HorizontalAlignment="Center" Spacing="8" MaxWidth="420">
        <LucideIcon Kind="SearchX" Size="28" HorizontalAlignment="Center"/>
        <TextBlock Text="No scheduled messages match these filters"
                   Classes="body-strong" HorizontalAlignment="Center"/>
        <TextBlock Text="Adjust the filters above or refresh to check for newly scheduled messages."
                   Classes="caption" TextAlignment="Center" TextWrapping="Wrap"/>
    </StackPanel>
</Border>
```

Keep confirmation, status, and error layers outside the result-content visibility gate.

**Step 3: Run the focused view test and compile the XAML**

Run:

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessagesViewTests"
rtk dotnet build
```

Expected: the view contract test passes and Avalonia XAML compilation succeeds with no errors.

**Step 4: Commit the UI implementation**

```bash
rtk git add BusLane/Views/Controls/ScheduledMessagesView.axaml
rtk git commit -m "feat: improve scheduled messages usability"
```

### Task 4: Verify the complete change

**Files:**
- No changes expected

**Step 1: Run focused scheduled-message tests**

```bash
rtk dotnet test --filter "FullyQualifiedName~ScheduledMessageStoreTests|FullyQualifiedName~ScheduledMessagesViewModelTests|FullyQualifiedName~ScheduledMessagesViewTests"
```

Expected: all focused store, view-model, and view tests pass.

**Step 2: Run the full test suite**

```bash
rtk dotnet test
```

Expected: all tests pass with zero failures.

**Step 3: Run a fresh full build**

```bash
rtk dotnet build
```

Expected: build succeeds with zero errors.

**Step 4: Inspect scope and status**

```bash
rtk git diff codex/issue-160-scheduled-message-console...HEAD -- BusLane/Services/ServiceBus/ScheduledMessageStore.cs BusLane.Tests/Services/ServiceBus/ScheduledMessageStoreTests.cs BusLane/Views/Controls/ScheduledMessagesView.axaml BusLane.Tests/Views/ScheduledMessagesViewTests.cs docs/plans/
rtk git status --short
```

Expected: only the approved store, tests, view, and design/plan documents are tracked; `.reasonix/` and `reasonix.toml` remain untouched.
