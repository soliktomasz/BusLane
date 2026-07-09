namespace BusLane.ViewModels.Core;

using System.Collections.ObjectModel;

using BusLane.Models;
using BusLane.Services.ServiceBus;

using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Coordinates entity-level queue, topic, subscription, and rule operations.
/// </summary>
public partial class EntityOperationsViewModel : ViewModelBase
{
    private readonly Func<IServiceBusOperations?> _getOperations;
    private readonly Func<NavigationState?> _getNavigation;
    private readonly Action<string> _setStatusMessage;
    private readonly ConfirmationDialogViewModel _confirmation;
    private readonly Action? _openSendMessage;

    [ObservableProperty] private bool _showSubscriptionRulesDialog;
    [ObservableProperty] private SubscriptionInfo? _rulesSubscription;
    [ObservableProperty] private bool _isLoadingSubscriptionRules;
    [ObservableProperty] private string _newRuleName = string.Empty;
    [ObservableProperty] private SubscriptionRuleFilterType _newRuleFilterType = SubscriptionRuleFilterType.Sql;
    [ObservableProperty] private string _newRuleSqlExpression = string.Empty;
    [ObservableProperty] private string _newRuleActionExpression = string.Empty;
    [ObservableProperty] private string? _subscriptionRuleValidationMessage;
    [ObservableProperty] private bool _showEntityDetailDialog;
    [ObservableProperty] private string _entityDetailTitle = string.Empty;
    [ObservableProperty] private bool _showEntityEditDialog;
    [ObservableProperty] private string _entityEditTitle = string.Empty;
    [ObservableProperty] private bool _isEditingQueue;
    [ObservableProperty] private bool _isEditingTopic;
    [ObservableProperty] private bool _isEditingSubscription;
    [ObservableProperty] private string _editLockDuration = string.Empty;
    [ObservableProperty] private string _editDefaultMessageTimeToLive = string.Empty;
    [ObservableProperty] private string _editMaxDeliveryCount = string.Empty;
    [ObservableProperty] private string _editAutoDeleteOnIdle = string.Empty;
    [ObservableProperty] private string _editForwardTo = string.Empty;
    [ObservableProperty] private string _editForwardDeadLetteredMessagesTo = string.Empty;
    [ObservableProperty] private string? _entityEditValidationMessage;
    [ObservableProperty] private bool _isUpdatingEntity;
    [ObservableProperty] private bool _showCreateQueueDialog;
    [ObservableProperty] private bool _showCreateTopicDialog;
    [ObservableProperty] private string _createEntityName = string.Empty;
    [ObservableProperty] private bool _createQueueRequiresSession;
    [ObservableProperty] private string? _createEntityValidationMessage;
    [ObservableProperty] private bool _isCreatingEntity;

    private QueueInfo? _editingQueue;
    private TopicInfo? _editingTopic;
    private SubscriptionInfo? _editingSubscription;

    public ObservableCollection<SubscriptionRuleInfo> SubscriptionRules { get; } = [];
    public ObservableCollection<EntityDetailRow> EntityDetailRows { get; } = [];

    public EntityOperationsViewModel(
        Func<IServiceBusOperations?> getOperations,
        Func<NavigationState?> getNavigation,
        Action<string> setStatusMessage,
        ConfirmationDialogViewModel confirmation,
        Action? openSendMessage = null)
    {
        _getOperations = getOperations;
        _getNavigation = getNavigation;
        _setStatusMessage = setStatusMessage;
        _confirmation = confirmation;
        _openSendMessage = openSendMessage;
    }

    [RelayCommand]
    private void OpenQueueDetails(QueueInfo? queue)
    {
        if (queue != null)
        {
            EntityDetailTitle = "Queue Details";
            EntityDetailRows.Clear();
            AddDetail("Name", queue.Name);
            AddDetail("Active Messages", queue.ActiveMessageCount);
            AddDetail("Dead Letters", queue.DeadLetterCount);
            AddDetail("Scheduled", queue.ScheduledCount);
            AddDetail("Size", queue.SizeInBytes);
            AddDetail("Requires Session", queue.RequiresSession);
            AddDetail("Default TTL", queue.DefaultMessageTtl);
            AddDetail("Lock Duration", queue.LockDuration);
            AddDetail("Accessed", queue.AccessedAt);
            ShowEntityDetailDialog = true;
        }
    }

    [RelayCommand]
    private void OpenTopicDetails(TopicInfo? topic)
    {
        if (topic != null)
        {
            EntityDetailTitle = "Topic Details";
            EntityDetailRows.Clear();
            AddDetail("Name", topic.Name);
            AddDetail("Subscriptions", topic.SubscriptionCount);
            AddDetail("Size", topic.SizeInBytes);
            AddDetail("Default TTL", topic.DefaultMessageTtl);
            AddDetail("Accessed", topic.AccessedAt);
            ShowEntityDetailDialog = true;
        }
    }

    [RelayCommand]
    private void OpenSubscriptionDetails(SubscriptionInfo? subscription)
    {
        if (subscription != null)
        {
            EntityDetailTitle = "Subscription Details";
            EntityDetailRows.Clear();
            AddDetail("Name", subscription.Name);
            AddDetail("Topic", subscription.TopicName);
            AddDetail("Active Messages", subscription.ActiveMessageCount);
            AddDetail("Dead Letters", subscription.DeadLetterCount);
            AddDetail("Requires Session", subscription.RequiresSession);
            AddDetail("Lock Duration", subscription.LockDuration);
            AddDetail("Max Delivery Count", subscription.MaxDeliveryCount);
            AddDetail("Default TTL", subscription.DefaultMessageTimeToLive);
            AddDetail("Auto Delete on Idle", subscription.AutoDeleteOnIdle);
            AddDetail("Forward To", subscription.ForwardTo);
            AddDetail("Forward DLQ To", subscription.ForwardDeadLetteredMessagesTo);
            AddDetail("Accessed", subscription.AccessedAt);
            ShowEntityDetailDialog = true;
        }
    }

    [RelayCommand]
    private void OpenQueueEdit(QueueInfo? queue)
    {
        if (queue != null)
        {
            ResetEditState();
            _editingQueue = queue;
            IsEditingQueue = true;
            EntityEditTitle = $"Edit Queue: {queue.Name}";
            EditLockDuration = FormatEditableTimeSpan(queue.LockDuration);
            EditDefaultMessageTimeToLive = FormatEditableTimeSpan(queue.DefaultMessageTtl);
            ShowEntityEditDialog = true;
        }
    }

    [RelayCommand]
    private void OpenTopicEdit(TopicInfo? topic)
    {
        if (topic != null)
        {
            ResetEditState();
            _editingTopic = topic;
            IsEditingTopic = true;
            EntityEditTitle = $"Edit Topic: {topic.Name}";
            EditDefaultMessageTimeToLive = FormatEditableTimeSpan(topic.DefaultMessageTtl);
            ShowEntityEditDialog = true;
        }
    }

    [RelayCommand]
    private void OpenSubscriptionEdit(SubscriptionInfo? subscription)
    {
        if (subscription != null)
        {
            ResetEditState();
            _editingSubscription = subscription;
            IsEditingSubscription = true;
            EntityEditTitle = $"Edit Subscription: {subscription.Name}";
            EditLockDuration = FormatEditableTimeSpan(subscription.LockDuration);
            EditDefaultMessageTimeToLive = FormatEditableTimeSpan(subscription.DefaultMessageTimeToLive);
            EditMaxDeliveryCount = subscription.MaxDeliveryCount?.ToString() ?? string.Empty;
            EditAutoDeleteOnIdle = FormatEditableTimeSpan(subscription.AutoDeleteOnIdle);
            EditForwardTo = subscription.ForwardTo ?? string.Empty;
            EditForwardDeadLetteredMessagesTo = subscription.ForwardDeadLetteredMessagesTo ?? string.Empty;
            ShowEntityEditDialog = true;
        }
    }

    [RelayCommand]
    private void CloseEntityDetailDialog()
    {
        ShowEntityDetailDialog = false;
        EntityDetailRows.Clear();
    }

    [RelayCommand]
    private void CloseEntityEditDialog()
    {
        ShowEntityEditDialog = false;
        ResetEditState();
    }

    [RelayCommand]
    private void OpenCreateQueueDialog()
    {
        ResetCreateState();
        ShowCreateTopicDialog = false;
        ShowCreateQueueDialog = true;
    }

    [RelayCommand]
    private void OpenCreateTopicDialog()
    {
        ResetCreateState();
        ShowCreateQueueDialog = false;
        ShowCreateTopicDialog = true;
    }

    [RelayCommand]
    private void CloseCreateEntityDialog()
    {
        ShowCreateQueueDialog = false;
        ShowCreateTopicDialog = false;
        ResetCreateState();
    }

    [RelayCommand]
    private async Task CreateQueueAsync()
    {
        var operations = _getOperations();
        var navigation = _getNavigation();
        if (operations == null || navigation == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        CreateEntityValidationMessage = null;
        if (string.IsNullOrWhiteSpace(CreateEntityName))
        {
            CreateEntityValidationMessage = "Queue name is required";
            return;
        }

        IsCreatingEntity = true;
        try
        {
            var name = CreateEntityName.Trim();
            await operations.CreateQueueAsync(new QueueCreationOptions(name, CreateQueueRequiresSession));
            var queue = await operations.GetQueueInfoAsync(name) ??
                new QueueInfo(name, 0, 0, 0, 0, 0, null, CreateQueueRequiresSession, TimeSpan.FromDays(14), TimeSpan.FromMinutes(1));
            navigation.Queues.Add(queue);
            navigation.SelectedQueue = queue;
            navigation.SelectedEntity = queue;
            _setStatusMessage($"Queue '{name}' created");
            ShowCreateQueueDialog = false;
            ResetCreateState();
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to create queue: {ex.Message}");
        }
        finally
        {
            IsCreatingEntity = false;
        }
    }

    [RelayCommand]
    private async Task CreateTopicAsync()
    {
        var operations = _getOperations();
        var navigation = _getNavigation();
        if (operations == null || navigation == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        CreateEntityValidationMessage = null;
        if (string.IsNullOrWhiteSpace(CreateEntityName))
        {
            CreateEntityValidationMessage = "Topic name is required";
            return;
        }

        IsCreatingEntity = true;
        try
        {
            var name = CreateEntityName.Trim();
            await operations.CreateTopicAsync(new TopicCreationOptions(name));
            var topic = await operations.GetTopicInfoAsync(name) ??
                new TopicInfo(name, 0, 0, null, TimeSpan.FromDays(14));
            navigation.Topics.Add(topic);
            navigation.SelectedTopic = topic;
            navigation.SelectedEntity = topic;
            _setStatusMessage($"Topic '{name}' created");
            ShowCreateTopicDialog = false;
            ResetCreateState();
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to create topic: {ex.Message}");
        }
        finally
        {
            IsCreatingEntity = false;
        }
    }

    [RelayCommand]
    private async Task SaveEntityEditAsync()
    {
        var operations = _getOperations();
        if (operations == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        EntityEditValidationMessage = null;
        IsUpdatingEntity = true;
        try
        {
            if (_editingQueue != null)
            {
                await SaveQueueEditAsync(operations, _editingQueue);
            }
            else if (_editingTopic != null)
            {
                await SaveTopicEditAsync(operations, _editingTopic);
            }
            else if (_editingSubscription != null)
            {
                await SaveSubscriptionEditAsync(operations, _editingSubscription);
            }
        }
        finally
        {
            IsUpdatingEntity = false;
        }
    }

    [RelayCommand]
    private async Task CopyQueueNameAsync(QueueInfo? queue)
    {
        if (queue != null)
        {
            await CopyTextAsync(queue.Name, "Queue name copied");
        }
    }

    [RelayCommand]
    private async Task CopyTopicNameAsync(TopicInfo? topic)
    {
        if (topic != null)
        {
            await CopyTextAsync(topic.Name, "Topic name copied");
        }
    }

    [RelayCommand]
    private async Task CopySubscriptionPathAsync(SubscriptionInfo? subscription)
    {
        if (subscription != null)
        {
            await CopyTextAsync($"{subscription.TopicName}/{subscription.Name}", "Subscription path copied");
        }
    }

    [RelayCommand]
    private void SendQueueMessage(QueueInfo? queue)
    {
        var navigation = _getNavigation();
        if (queue == null || navigation == null)
        {
            return;
        }

        navigation.SelectedQueue = queue;
        navigation.SelectedTopic = null;
        navigation.SelectedSubscription = null;
        navigation.SelectedEntity = queue;
        _openSendMessage?.Invoke();
    }

    [RelayCommand]
    private void SendTopicMessage(TopicInfo? topic)
    {
        var navigation = _getNavigation();
        if (topic == null || navigation == null)
        {
            return;
        }

        navigation.SelectedTopic = topic;
        navigation.SelectedQueue = null;
        navigation.SelectedSubscription = null;
        navigation.SelectedEntity = topic;
        _openSendMessage?.Invoke();
    }

    [RelayCommand]
    private void DeleteQueueRequest(QueueInfo? queue)
    {
        if (queue == null)
        {
            return;
        }

        _confirmation.ShowConfirmation(
            "Delete queue",
            $"Are you sure you want to delete queue '{queue.Name}'? This action cannot be undone.",
            "Delete",
            () => DeleteQueueAsync(queue));
    }

    [RelayCommand]
    private void DeleteTopicRequest(TopicInfo? topic)
    {
        if (topic == null)
        {
            return;
        }

        _confirmation.ShowConfirmation(
            "Delete topic",
            $"Are you sure you want to delete topic '{topic.Name}'? This action cannot be undone.",
            "Delete",
            () => DeleteTopicAsync(topic));
    }

    [RelayCommand]
    private void DeleteSubscriptionRequest(SubscriptionInfo? subscription)
    {
        if (subscription == null)
        {
            return;
        }

        _confirmation.ShowConfirmation(
            "Delete subscription",
            $"Are you sure you want to delete subscription '{subscription.Name}' from topic '{subscription.TopicName}'? This action cannot be undone.",
            "Delete",
            () => DeleteSubscriptionAsync(subscription));
    }

    [RelayCommand]
    private void PurgeQueueActive(QueueInfo? queue) => RequestPurgeQueue(queue, deadLetter: false);

    [RelayCommand]
    private void PurgeQueueDeadLetters(QueueInfo? queue) => RequestPurgeQueue(queue, deadLetter: true);

    [RelayCommand]
    private void PurgeSubscriptionActive(SubscriptionInfo? subscription) => RequestPurgeSubscription(subscription, deadLetter: false);

    [RelayCommand]
    private void PurgeSubscriptionDeadLetters(SubscriptionInfo? subscription) => RequestPurgeSubscription(subscription, deadLetter: true);

    [RelayCommand]
    private async Task RefreshQueueAsync(QueueInfo? queue)
    {
        if (queue == null)
        {
            return;
        }

        var operations = _getOperations();
        var navigation = _getNavigation();
        if (operations == null || navigation == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        try
        {
            var refreshed = await operations.GetQueueInfoAsync(queue.Name);
            if (refreshed == null)
            {
                _setStatusMessage($"Queue '{queue.Name}' was not found");
                return;
            }

            ReplaceQueue(navigation, queue, refreshed);
            _setStatusMessage($"Queue '{queue.Name}' refreshed");
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to refresh queue: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshTopicAsync(TopicInfo? topic)
    {
        if (topic == null)
        {
            return;
        }

        var operations = _getOperations();
        var navigation = _getNavigation();
        if (operations == null || navigation == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        try
        {
            await ReloadTopicSubscriptionsAsync(topic, operations, navigation);
            _setStatusMessage($"Topic '{topic.Name}' subscriptions refreshed");
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to refresh topic: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshSubscriptionAsync(SubscriptionInfo? subscription)
    {
        if (subscription == null)
        {
            return;
        }

        var topic = _getNavigation()?.Topics.FirstOrDefault(t =>
            string.Equals(t.Name, subscription.TopicName, StringComparison.OrdinalIgnoreCase));
        if (topic != null)
        {
            await RefreshTopicAsync(topic);
        }
    }

    [RelayCommand]
    private async Task OpenSubscriptionRulesDialogAsync(SubscriptionInfo? subscription)
    {
        if (subscription == null)
        {
            return;
        }

        RulesSubscription = subscription;
        NewRuleName = string.Empty;
        NewRuleFilterType = SubscriptionRuleFilterType.Sql;
        NewRuleSqlExpression = string.Empty;
        NewRuleActionExpression = string.Empty;
        SubscriptionRuleValidationMessage = null;
        ShowSubscriptionRulesDialog = true;
        await LoadSubscriptionRulesAsync();
    }

    [RelayCommand]
    private void CloseSubscriptionRulesDialog()
    {
        ShowSubscriptionRulesDialog = false;
        RulesSubscription = null;
        SubscriptionRules.Clear();
        SubscriptionRuleValidationMessage = null;
    }

    [RelayCommand]
    private async Task LoadSubscriptionRulesAsync()
    {
        var operations = _getOperations();
        var subscription = RulesSubscription;
        if (operations == null || subscription == null)
        {
            return;
        }

        IsLoadingSubscriptionRules = true;
        try
        {
            SubscriptionRules.Clear();
            var rules = await operations.GetSubscriptionRulesAsync(subscription.TopicName, subscription.Name);
            foreach (var rule in rules)
            {
                SubscriptionRules.Add(rule);
            }
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to load subscription rules: {ex.Message}");
        }
        finally
        {
            IsLoadingSubscriptionRules = false;
        }
    }

    [RelayCommand]
    private async Task CreateSubscriptionRuleAsync()
    {
        var operations = _getOperations();
        var subscription = RulesSubscription;
        if (operations == null || subscription == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        var ruleName = NewRuleName.Trim();
        if (string.IsNullOrWhiteSpace(ruleName))
        {
            SubscriptionRuleValidationMessage = "Rule name is required";
            return;
        }

        if (NewRuleFilterType == SubscriptionRuleFilterType.Sql && string.IsNullOrWhiteSpace(NewRuleSqlExpression))
        {
            SubscriptionRuleValidationMessage = "SQL expression is required";
            return;
        }

        SubscriptionRuleValidationMessage = null;
        try
        {
            var options = new SubscriptionRuleCreationOptions(
                ruleName,
                NewRuleSqlExpression.Trim(),
                NewRuleFilterType,
                string.IsNullOrWhiteSpace(NewRuleActionExpression) ? null : NewRuleActionExpression.Trim());
            await operations.CreateSubscriptionRuleAsync(subscription.TopicName, subscription.Name, options);
            _setStatusMessage($"Rule '{ruleName}' created");
            NewRuleName = string.Empty;
            NewRuleSqlExpression = string.Empty;
            NewRuleActionExpression = string.Empty;
            await LoadSubscriptionRulesAsync();
        }
        catch (Exception ex)
        {
            SubscriptionRuleValidationMessage = $"Unable to create rule: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DeleteSubscriptionRuleRequest(SubscriptionRuleInfo? rule)
    {
        var subscription = RulesSubscription;
        if (rule == null || subscription == null)
        {
            return;
        }

        _confirmation.ShowConfirmation(
            "Delete rule",
            $"Are you sure you want to delete rule '{rule.Name}' from subscription '{subscription.Name}'?",
            "Delete",
            () => DeleteSubscriptionRuleAsync(rule));
    }

    private void RequestPurgeQueue(QueueInfo? queue, bool deadLetter)
    {
        if (queue == null)
        {
            return;
        }

        var scope = deadLetter ? "dead-letter messages" : "active messages";
        _confirmation.ShowConfirmation(
            "Purge queue messages",
            $"Are you sure you want to purge {scope} from queue '{queue.Name}'?",
            "Purge",
            () => PurgeAsync(queue.Name, null, deadLetter));
    }

    private void RequestPurgeSubscription(SubscriptionInfo? subscription, bool deadLetter)
    {
        if (subscription == null)
        {
            return;
        }

        var scope = deadLetter ? "dead-letter messages" : "active messages";
        _confirmation.ShowConfirmation(
            "Purge subscription messages",
            $"Are you sure you want to purge {scope} from subscription '{subscription.TopicName}/{subscription.Name}'?",
            "Purge",
            () => PurgeAsync(subscription.TopicName, subscription.Name, deadLetter));
    }

    private async Task DeleteQueueAsync(QueueInfo queue)
    {
        var operations = _getOperations();
        var navigation = _getNavigation();
        if (operations == null || navigation == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        try
        {
            await operations.DeleteQueueAsync(queue.Name);
            navigation.Queues.Remove(queue);
            if (navigation.SelectedQueue == queue)
            {
                navigation.SelectedQueue = null;
                navigation.SelectedEntity = null;
            }

            _setStatusMessage($"Queue '{queue.Name}' deleted");
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to delete queue: {ex.Message}");
        }
    }

    private async Task DeleteTopicAsync(TopicInfo topic)
    {
        var operations = _getOperations();
        var navigation = _getNavigation();
        if (operations == null || navigation == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        try
        {
            await operations.DeleteTopicAsync(topic.Name);
            navigation.Topics.Remove(topic);
            foreach (var subscription in navigation.TopicSubscriptions.Where(s => s.TopicName == topic.Name).ToList())
            {
                navigation.TopicSubscriptions.Remove(subscription);
            }

            if (navigation.SelectedTopic == topic)
            {
                navigation.SelectedTopic = null;
                navigation.SelectedEntity = null;
            }

            _setStatusMessage($"Topic '{topic.Name}' deleted");
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to delete topic: {ex.Message}");
        }
    }

    private async Task DeleteSubscriptionAsync(SubscriptionInfo subscription)
    {
        var operations = _getOperations();
        var navigation = _getNavigation();
        if (operations == null || navigation == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        try
        {
            await operations.DeleteSubscriptionAsync(subscription.TopicName, subscription.Name);
            var topic = navigation.Topics.FirstOrDefault(t =>
                string.Equals(t.Name, subscription.TopicName, StringComparison.OrdinalIgnoreCase));
            if (topic != null)
            {
                await ReloadTopicSubscriptionsAsync(topic, operations, navigation);
            }
            else
            {
                navigation.TopicSubscriptions.Remove(subscription);
            }

            if (navigation.SelectedSubscription == subscription)
            {
                navigation.SelectedSubscription = null;
                navigation.SelectedEntity = null;
            }

            _setStatusMessage($"Subscription '{subscription.Name}' deleted");
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to delete subscription: {ex.Message}");
        }
    }

    private async Task PurgeAsync(string entityName, string? subscriptionName, bool deadLetter)
    {
        var operations = _getOperations();
        if (operations == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        try
        {
            await operations.PurgeMessagesAsync(entityName, subscriptionName, deadLetter);
            _setStatusMessage("Messages purged");
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to purge messages: {ex.Message}");
        }
    }

    private async Task DeleteSubscriptionRuleAsync(SubscriptionRuleInfo rule)
    {
        var operations = _getOperations();
        var subscription = RulesSubscription;
        if (operations == null || subscription == null)
        {
            SetNoActiveConnectionStatus();
            return;
        }

        try
        {
            await operations.DeleteSubscriptionRuleAsync(subscription.TopicName, subscription.Name, rule.Name);
            _setStatusMessage($"Rule '{rule.Name}' deleted");
            await LoadSubscriptionRulesAsync();
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to delete rule: {ex.Message}");
        }
    }

    private static void SelectQueue(QueueInfo queue)
    {
        _ = queue;
    }

    private static void SelectTopic(TopicInfo topic)
    {
        _ = topic;
    }

    private static void SelectSubscription(SubscriptionInfo subscription)
    {
        _ = subscription;
    }

    private static void ReplaceQueue(NavigationState navigation, QueueInfo oldQueue, QueueInfo newQueue)
    {
        var index = navigation.Queues.IndexOf(oldQueue);
        if (index >= 0)
        {
            navigation.Queues[index] = newQueue;
        }

        if (navigation.SelectedQueue == oldQueue)
        {
            navigation.SelectedQueue = newQueue;
            navigation.SelectedEntity = newQueue;
        }
    }

    private static async Task ReloadTopicSubscriptionsAsync(
        TopicInfo topic,
        IServiceBusOperations operations,
        NavigationState navigation)
    {
        var subscriptions = (await operations.GetSubscriptionsAsync(topic.Name)).ToList();

        topic.Subscriptions.Clear();
        foreach (var subscription in subscriptions)
        {
            topic.Subscriptions.Add(subscription);
        }

        topic.SubscriptionCount = subscriptions.Count;
        topic.SubscriptionsLoaded = true;

        if (navigation.SelectedTopic == topic ||
            string.Equals(navigation.CurrentEntityName, topic.Name, StringComparison.OrdinalIgnoreCase))
        {
            navigation.TopicSubscriptions.Clear();
            foreach (var subscription in subscriptions)
            {
                navigation.TopicSubscriptions.Add(subscription);
            }
        }
    }

    private async Task CopyTextAsync(string text, string successMessage)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard != null)
        {
            await desktop.MainWindow.Clipboard.SetTextAsync(text);
            _setStatusMessage(successMessage);
            return;
        }

        _setStatusMessage("Clipboard is not available");
    }

    private async Task SaveQueueEditAsync(IServiceBusOperations operations, QueueInfo queue)
    {
        var lockDuration = ParseTimeSpan(EditLockDuration, "Lock duration", required: false);
        if (EntityEditValidationMessage != null)
        {
            return;
        }

        var ttl = ParseTimeSpan(EditDefaultMessageTimeToLive, "Default message TTL", required: false);
        if (EntityEditValidationMessage != null)
        {
            return;
        }

        try
        {
            await operations.UpdateQueueAsync(queue.Name, new QueueUpdateOptions(lockDuration, ttl));
            _setStatusMessage($"Queue '{queue.Name}' updated");
            CloseEntityEditDialog();
            await RefreshQueueAsync(queue);
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to update queue: {ex.Message}");
        }
    }

    private async Task SaveTopicEditAsync(IServiceBusOperations operations, TopicInfo topic)
    {
        var ttl = ParseTimeSpan(EditDefaultMessageTimeToLive, "Default message TTL", required: false);
        if (EntityEditValidationMessage != null)
        {
            return;
        }

        try
        {
            await operations.UpdateTopicAsync(topic.Name, new TopicUpdateOptions(ttl));
            await RefreshTopicInfoAsync(operations, topic);
            _setStatusMessage($"Topic '{topic.Name}' updated");
            CloseEntityEditDialog();
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to update topic: {ex.Message}");
        }
    }

    private async Task SaveSubscriptionEditAsync(IServiceBusOperations operations, SubscriptionInfo subscription)
    {
        var lockDuration = ParseTimeSpan(EditLockDuration, "Lock duration", required: false);
        if (EntityEditValidationMessage != null)
        {
            return;
        }

        var ttl = ParseTimeSpan(EditDefaultMessageTimeToLive, "Default message TTL", required: false);
        if (EntityEditValidationMessage != null)
        {
            return;
        }

        var autoDelete = ParseTimeSpan(EditAutoDeleteOnIdle, "Auto delete on idle", required: false);
        if (EntityEditValidationMessage != null)
        {
            return;
        }

        int? maxDeliveryCount = null;
        if (!string.IsNullOrWhiteSpace(EditMaxDeliveryCount))
        {
            if (!int.TryParse(EditMaxDeliveryCount, out var parsedMaxDeliveryCount) || parsedMaxDeliveryCount <= 0)
            {
                EntityEditValidationMessage = "Max delivery count must be a positive number";
                return;
            }

            maxDeliveryCount = parsedMaxDeliveryCount;
        }

        var options = new SubscriptionUpdateOptions(
            lockDuration,
            maxDeliveryCount,
            ttl,
            autoDelete,
            EditForwardTo,
            EditForwardDeadLetteredMessagesTo);
        try
        {
            await operations.UpdateSubscriptionAsync(subscription.TopicName, subscription.Name, options);
            _setStatusMessage($"Subscription '{subscription.Name}' updated");
            CloseEntityEditDialog();
            await RefreshSubscriptionAsync(subscription);
        }
        catch (Exception ex)
        {
            _setStatusMessage($"Unable to update subscription: {ex.Message}");
        }
    }

    private async Task RefreshTopicInfoAsync(IServiceBusOperations operations, TopicInfo topic)
    {
        var navigation = _getNavigation();
        if (navigation == null)
        {
            return;
        }

        var refreshed = await operations.GetTopicInfoAsync(topic.Name);
        if (refreshed == null)
        {
            return;
        }

        foreach (var subscription in topic.Subscriptions)
        {
            refreshed.Subscriptions.Add(subscription);
        }

        refreshed.SubscriptionsLoaded = topic.SubscriptionsLoaded;
        refreshed.IsExpanded = topic.IsExpanded;

        var index = navigation.Topics.IndexOf(topic);
        if (index >= 0)
        {
            navigation.Topics[index] = refreshed;
        }

        if (navigation.SelectedTopic == topic || navigation.SelectedEntity == topic)
        {
            navigation.SelectedTopic = refreshed;
            navigation.SelectedEntity = refreshed;
        }
    }

    private TimeSpan? ParseTimeSpan(string value, string label, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                EntityEditValidationMessage = $"{label} is required";
            }

            return null;
        }

        if (TimeSpan.TryParse(value, out var parsed))
        {
            return parsed;
        }

        EntityEditValidationMessage = $"{label} must be a valid time span";
        return null;
    }

    private void ResetEditState()
    {
        _editingQueue = null;
        _editingTopic = null;
        _editingSubscription = null;
        IsEditingQueue = false;
        IsEditingTopic = false;
        IsEditingSubscription = false;
        EditLockDuration = string.Empty;
        EditDefaultMessageTimeToLive = string.Empty;
        EditMaxDeliveryCount = string.Empty;
        EditAutoDeleteOnIdle = string.Empty;
        EditForwardTo = string.Empty;
        EditForwardDeadLetteredMessagesTo = string.Empty;
        EntityEditValidationMessage = null;
    }

    private void ResetCreateState()
    {
        CreateEntityName = string.Empty;
        CreateQueueRequiresSession = false;
        CreateEntityValidationMessage = null;
    }

    private void AddDetail(string label, object? value)
    {
        EntityDetailRows.Add(new EntityDetailRow(label, FormatValue(value)));
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "N/A",
            DateTimeOffset dateTime => dateTime.ToString("u"),
            TimeSpan timeSpan => timeSpan.ToString(),
            _ => value.ToString() ?? "N/A"
        };
    }

    private static string FormatEditableTimeSpan(TimeSpan? value) => value?.ToString() ?? string.Empty;

    private void SetNoActiveConnectionStatus() => _setStatusMessage("No active connection");
}

public record EntityDetailRow(string Label, string Value);
