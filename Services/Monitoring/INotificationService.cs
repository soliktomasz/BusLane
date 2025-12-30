namespace BusLane.Services.Monitoring;

using BusLane.Models;

public interface INotificationService
{
    /// <summary>
    /// Whether system notifications are enabled
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Show a system notification for an alert
    /// </summary>
    void ShowAlertNotification(AlertEvent alert);

    /// <summary>
    /// Show a generic notification
    /// </summary>
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Info);
}

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success
}

