namespace BusLane.Services.Monitoring;

using System.Net.Http.Json;
using BusLane.Models;

public class WebhookNotificationChannel : INotificationChannel
{
    private readonly HttpClient _httpClient;
    private readonly Func<DateTimeOffset> _nowProvider;

    public WebhookNotificationChannel(HttpClient? httpClient = null, Func<DateTimeOffset>? nowProvider = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public AlertDeliveryChannelType ChannelType => AlertDeliveryChannelType.Webhook;

    public async Task<AlertHistoryEntry> SendAsync(
        AlertEvent alert,
        AlertDeliveryTarget target,
        CancellationToken ct = default)
    {
        var payload = new
        {
            rule = alert.Rule.Name,
            severity = alert.Rule.Severity.ToString(),
            entity = alert.EntityName,
            entityType = alert.EntityType,
            currentValue = alert.CurrentValue,
            threshold = alert.Rule.Threshold,
            triggeredAt = alert.TriggeredAt
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(target.Target, payload, ct);
            response.EnsureSuccessStatusCode();

            return new AlertHistoryEntry(
                Guid.NewGuid().ToString(),
                alert.Rule.Id,
                alert.Rule.Name,
                alert.EntityName,
                alert.EntityType,
                alert.CurrentValue,
                _nowProvider(),
                AlertHistoryStatus.Delivered,
                Details: $"Webhook delivered to {target.Target}");
        }
        catch (Exception ex)
        {
            return new AlertHistoryEntry(
                Guid.NewGuid().ToString(),
                alert.Rule.Id,
                alert.Rule.Name,
                alert.EntityName,
                alert.EntityType,
                alert.CurrentValue,
                _nowProvider(),
                AlertHistoryStatus.DeliveryFailed,
                Details: ex.Message);
        }
    }
}
