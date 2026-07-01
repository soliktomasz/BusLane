# Subscription Creation Design

## Goal

Add a focused way to create Azure Service Bus subscriptions from an existing topic in the entity browser.

Initial UI stays basic: subscription name and whether sessions are required. Service and view model boundaries use an options object so advanced subscription settings can be added later without changing the command shape.

## User Experience

Users create subscriptions from the topic row in the entity browser.

- Each topic row gets a compact create-subscription icon button beside the subscription count.
- The command opens a modal overlay titled `Create subscription`.
- The form shows the selected topic name as read-only context.
- The form includes subscription name and a `Requires session` checkbox.
- `Create` validates non-empty subscription name, calls the current connection operations, reloads that topic's subscriptions, expands the topic, selects the new subscription, and updates status.
- `Cancel` closes the form and clears draft state.

## Service Contract

Add `CreateSubscriptionAsync(string topicName, SubscriptionCreationOptions options, CancellationToken ct = default)` to `IServiceBusOperations`.

`SubscriptionCreationOptions` starts with:

- `Name`
- `RequiresSession`

This keeps advanced creation extensible. Later fields such as lock duration, TTL, max delivery count, forwarding targets, duplicate detection, or SQL filters can be added to the options record and surfaced in an advanced form section.

## Implementations

Connection-string mode uses `ServiceBusAdministrationClient.CreateSubscriptionAsync` with `CreateSubscriptionOptions`.

Azure-account mode uses the existing ARM namespace resource, gets the topic resource, and creates a subscription through its subscription collection using `ServiceBusSubscriptionData`.

Both implementations validate required names, pass cancellation tokens, and avoid logging secrets.

## View Model Boundaries

Keep creation state in `MainWindowViewModel` for now, matching existing dialog and entity coordination patterns.

Add observable state:

- `ShowCreateSubscriptionDialog`
- `CreateSubscriptionTopic`
- `NewSubscriptionName`
- `NewSubscriptionRequiresSession`
- `IsCreatingSubscription`

Add commands:

- `OpenCreateSubscriptionDialog(TopicInfo topic)`
- `CloseCreateSubscriptionDialog()`
- `CreateSubscriptionAsync()`

Creation uses `ActiveTab?.Operations ?? _operations` and `CurrentNavigation`, matching existing topic/subscription selection commands.

## Refresh Behavior

After successful creation:

- Reload `CreateSubscriptionTopic.Subscriptions`.
- Set `SubscriptionsLoaded = true`.
- Set `IsExpanded = true`.
- Update `CurrentNavigation.TopicSubscriptions` when the created topic is selected or current navigation is using that topic.
- Select the newly created `SubscriptionInfo`.
- Load messages for the selected subscription via existing selection flow.

If creation fails, keep the dialog open and show `StatusMessage = "Unable to create subscription: ..."` so the user can correct input or permissions.

## Tests

Add focused tests for:

- Empty subscription name validation.
- Options passed to `IServiceBusOperations.CreateSubscriptionAsync`.
- Successful creation reloads subscriptions and selects the new subscription.
- Service implementation maps `RequiresSession` into Azure SDK creation options/data.

Build verification: `dotnet build`.
