using AutoPartsShop.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AutoPartsShop.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<NotificationItem> _notifications = [];
    private int _nextId = 1;

    /// <summary>
    /// Event raised when a new toast notification should be shown in the UI.
    /// The UI layer subscribes to this event to display toasts.
    /// </summary>
    public event Action<NotificationItem>? ToastRequested;

    /// <summary>
    /// Event raised when the unread count changes.
    /// </summary>
    public event Action<int>? UnreadCountChanged;

    public NotificationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void ShowToast(string title, string message, NotificationType type = NotificationType.Info)
    {
        var item = new NotificationItem
        {
            Id = Interlocked.Increment(ref _nextId),
            Title = title,
            Message = message,
            Type = type,
            Timestamp = DateTime.UtcNow,
            IsRead = false
        };

        _notifications.Insert(0, item);
        if (_notifications.Count > 100)
            _notifications.RemoveAt(_notifications.Count - 1);

        // Raise events directly - UI subscribers handle thread marshaling themselves
        ToastRequested?.Invoke(item);
        UnreadCountChanged?.Invoke(GetUnreadCount());
    }

    public async Task AddNotificationAsync(string title, string message, NotificationType type)
    {
        var item = new NotificationItem
        {
            Id = Interlocked.Increment(ref _nextId),
            Title = title,
            Message = message,
            Type = type,
            Timestamp = DateTime.UtcNow,
            IsRead = false
        };

        _notifications.Insert(0, item);
        if (_notifications.Count > 100)
            _notifications.RemoveAt(_notifications.Count - 1);

        UnreadCountChanged?.Invoke(GetUnreadCount());

        await Task.CompletedTask;
    }

    public List<NotificationItem> GetUnreadNotifications()
    {
        return _notifications.Where(n => !n.IsRead).ToList();
    }

    public int GetUnreadCount()
    {
        return _notifications.Count(n => !n.IsRead);
    }

    public void MarkAllAsRead()
    {
        foreach (var n in _notifications)
            n.IsRead = true;

        UnreadCountChanged?.Invoke(0);
    }

    public async Task CheckLowStockAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();

            // Check if alerts are enabled
            var alertsEnabledStr = await settingService.GetAsync("LowStockAlertEnabled", "True");
            var alertsEnabled = alertsEnabledStr.Equals("True", StringComparison.OrdinalIgnoreCase);

            if (!alertsEnabled)
                return;

            // Get low stock items
            var lowStockItems = await sparePartService.GetLowStockAsync();

            // Get the threshold setting
            var thresholdStr = await settingService.GetAsync("LowStockThreshold", "5");
            int.TryParse(thresholdStr, out var globalThreshold);

            // Check each item against the global threshold too
            var criticalItems = lowStockItems
                .Where(item => item.CurrentStock <= (globalThreshold > 0 ? globalThreshold : item.MinStockLevel))
                .ToList();

            if (criticalItems.Count > 0)
            {
                var topItems = criticalItems.Take(5).ToList();
                var message = topItems.Count == 1
                    ? $"القطعة '{topItems[0].Name}' - المخزون: {topItems[0].CurrentStock}"
                    : $"{topItems.Count} قطع منخفضة المخزون (من أصل {criticalItems.Count})";

                ShowToast("تنبيه المخزون المنخفض", message, NotificationType.Warning);
            }
        }
        catch
        {
            // Silently fail - notifications are not critical
        }
    }
}
