namespace AutoPartsShop.Application.Interfaces;

/// <summary>
/// Manages in-app notifications such as low stock alerts, backup status, etc.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Event raised when a new toast notification should be shown in the UI.
    /// </summary>
    event Action<NotificationItem>? ToastRequested;

    /// <summary>
    /// Event raised when the unread count changes.
    /// </summary>
    event Action<int>? UnreadCountChanged;

    /// <summary>
    /// Shows a toast notification to the user.
    /// </summary>
    void ShowToast(string title, string message, NotificationType type = NotificationType.Info);

    /// <summary>
    /// Adds a notification to the notification history.
    /// </summary>
    Task AddNotificationAsync(string title, string message, NotificationType type);

    /// <summary>
    /// Gets all unread notifications.
    /// </summary>
    List<NotificationItem> GetUnreadNotifications();

    /// <summary>
    /// Gets the count of unread notifications.
    /// </summary>
    int GetUnreadCount();

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    void MarkAllAsRead();

    /// <summary>
    /// Checks for low stock items and raises notifications if enabled.
    /// </summary>
    Task CheckLowStockAsync();
}

/// <summary>
/// Notification severity type.
/// </summary>
public enum NotificationType
{
    Info,
    Warning,
    Error,
    Success
}

/// <summary>
/// A single notification item.
/// </summary>
public class NotificationItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
