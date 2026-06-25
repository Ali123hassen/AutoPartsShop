using AutoPartsShop.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using ThreadingTimer = System.Threading.Timer;

namespace AutoPartsShop.UI.Services;

/// <summary>
/// Backup schedule type.
/// </summary>
public enum BackupScheduleType
{
    /// <summary>Every N minutes/hours (interval-based, the legacy behaviour).</summary>
    Interval = 0,

    /// <summary>Every day at a specific time (HH:mm).</summary>
    Daily = 1,

    /// <summary>Every week on a specific day-of-week and time.</summary>
    /// <remarks>DayOfWeek: 0=Sunday, 1=Monday, ..., 6=Saturday</remarks>
    Weekly = 2,

    /// <summary>Every month on a specific day (1..31) and time.</summary>
    Monthly = 3
}

/// <summary>
/// Background service that automatically creates database backups on a schedule.
/// Supports 4 schedule types: Interval, Daily, Weekly, Monthly.
/// Uses the database's BackupHistory table to remember the last successful backup,
/// so it survives app restarts (no more "wait 24h from app start" bug).
/// </summary>
public class AutoBackupScheduler : IAutoBackupScheduler, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoBackupScheduler> _logger;
    private ThreadingTimer? _timer;
    private bool _isRunning;
    private DateTime? _lastBackupTime;
    private bool _isExecutingBackup;

    // Schedule settings (loaded from DB)
    private BackupScheduleType _scheduleType = BackupScheduleType.Interval;
    private int _intervalMinutes = 60;          // For Interval mode
    private int _hour = 2;                       // For Daily/Weekly/Monthly (default 2 AM)
    private int _minute = 0;                     // For Daily/Weekly/Monthly
    private DayOfWeek _dayOfWeek = DayOfWeek.Sunday;  // For Weekly
    private int _dayOfMonth = 1;                 // For Monthly (1..31)
    private string _backupDirectory = "";

    public bool IsRunning => _isRunning;
    public DateTime? LastBackupTime => _lastBackupTime;

    public event EventHandler<AutoBackupResult>? BackupCompleted;

    public AutoBackupScheduler(
        IServiceProvider serviceProvider,
        ILogger<AutoBackupScheduler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts the scheduler by reading settings from the database.
    /// Used at application startup.
    /// </summary>
    public async Task StartAsync()
    {
        try
        {
            var settings = await ReadBackupSettingsAsync();

            if (!settings.IsEnabled)
            {
                _logger.LogInformation("Auto backup is disabled in settings, scheduler will not start");
                return;
            }

            await StartCoreAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start auto backup scheduler");
        }
    }

    /// <summary>
    /// Legacy overload for interval-only mode. Kept for backwards compatibility.
    /// New code should use StartAsync(BackupScheduleSettings) instead.
    /// </summary>
    public async Task StartAsync(int intervalMinutes, string backupDirectory)
    {
        try
        {
            var settings = new BackupScheduleSettings
            {
                IsEnabled = true,
                ScheduleType = BackupScheduleType.Interval,
                IntervalMinutes = intervalMinutes,
                BackupDirectory = backupDirectory
            };
            await StartCoreAsync(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start auto backup scheduler with explicit settings");
        }
    }

    private async Task StartCoreAsync(BackupScheduleSettings settings)
    {
        // Stop any existing timer first
        _timer?.Dispose();
        _timer = null;

        // Apply settings
        _scheduleType = settings.ScheduleType;
        _intervalMinutes = Math.Max(settings.IntervalMinutes, 5);
        _hour = Math.Clamp(settings.Hour, 0, 23);
        _minute = Math.Clamp(settings.Minute, 0, 59);
        _dayOfWeek = settings.DayOfWeek;
        _dayOfMonth = Math.Clamp(settings.DayOfMonth, 1, 28);  // Cap at 28 to avoid missing Feb/30/31
        _backupDirectory = settings.BackupDirectory ?? "";

        // ===== KEY FIX: load the last successful backup time from the DB, not from in-memory state =====
        // This means if the user closed the app for 12 hours and the schedule was "every 6 hours",
        // on next startup the scheduler will see that the last backup was 12 hours ago and run immediately.
        _lastBackupTime = await GetLastSuccessfulBackupTimeAsync();
        if (_lastBackupTime.HasValue)
        {
            _logger.LogInformation("Loaded last successful backup time from DB: {LastBackup}", _lastBackupTime.Value);

            // ===== Check if a backup was missed while the app was closed =====
            var nextDue = CalculateNextDueTime(_lastBackupTime.Value);
            if (nextDue <= DateTime.UtcNow)
            {
                _logger.LogWarning("Missed backup detected! Last backup was {LastBackup}, scheduled for {Due}. Running immediately.",
                    _lastBackupTime.Value, nextDue);
                // Fire-and-forget; the timer will pick it up
                _ = Task.Run(async () => await CheckAndExecuteBackupAsync());
            }
        }

        // Check every minute if a backup is due
        var checkInterval = TimeSpan.FromMinutes(1);
        _timer = new ThreadingTimer(async _ => await CheckAndExecuteBackupAsync(), null, checkInterval, checkInterval);

        _isRunning = true;

        _logger.LogInformation(
            "Auto backup scheduler started. Type: {Type}, Interval: {Interval}min, Hour: {Hour}:{Minute:D2}, DayOfWeek: {Dow}, DayOfMonth: {Dom}, Directory: {Directory}",
            _scheduleType, _intervalMinutes, _hour, _minute, _dayOfWeek, _dayOfMonth,
            string.IsNullOrEmpty(_backupDirectory) ? "(default)" : _backupDirectory);

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        try
        {
            _timer?.Dispose();
            _timer = null;
            _isRunning = false;
            _logger.LogInformation("Auto backup scheduler stopped");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping auto backup scheduler");
        }
    }

    public async Task RestartAsync()
    {
        _logger.LogInformation("Restarting auto backup scheduler with new settings");
        await StopAsync();
        await StartAsync();
    }

    private async Task CheckAndExecuteBackupAsync()
    {
        // Prevent concurrent backup operations
        if (_isExecutingBackup)
            return;

        // Check if a backup is due
        if (_lastBackupTime.HasValue)
        {
            var nextDue = CalculateNextDueTime(_lastBackupTime.Value);
            if (DateTime.UtcNow < nextDue)
                return; // Not time yet
        }

        _isExecutingBackup = true;

        try
        {
            // Re-read settings in case they changed
            var settings = await ReadBackupSettingsAsync();

            if (!settings.IsEnabled)
            {
                _logger.LogInformation("Auto backup was disabled in settings, stopping scheduler");
                await StopAsync();
                return;
            }

            // Apply any changed settings
            _scheduleType = settings.ScheduleType;
            _intervalMinutes = Math.Max(settings.IntervalMinutes, 5);
            _hour = Math.Clamp(settings.Hour, 0, 23);
            _minute = Math.Clamp(settings.Minute, 0, 59);
            _dayOfWeek = settings.DayOfWeek;
            _dayOfMonth = Math.Clamp(settings.DayOfMonth, 1, 28);

            var backupDir = !string.IsNullOrWhiteSpace(settings.BackupDirectory)
                ? settings.BackupDirectory
                : _backupDirectory;

            if (string.IsNullOrWhiteSpace(backupDir))
            {
                backupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AutoPartsShop_Backups");
            }

            // Create the backup
            using var scope = _serviceProvider.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var backupPath = await backupService.CreateBackupAsync(backupDir);

            // Update the last backup time (in memory and read back from DB next time)
            _lastBackupTime = DateTime.UtcNow;

            _logger.LogInformation("Auto backup completed successfully: {Path}", backupPath);

            BackupCompleted?.Invoke(this, new AutoBackupResult
            {
                IsSuccess = true,
                BackupPath = backupPath,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto backup failed");

            BackupCompleted?.Invoke(this, new AutoBackupResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
        finally
        {
            _isExecutingBackup = false;
        }
    }

    /// <summary>
    /// Calculates the next due time based on the schedule type and the last backup time.
    /// This is the heart of the scheduler — it knows when the next backup should happen.
    /// </summary>
    private DateTime CalculateNextDueTime(DateTime lastBackup)
    {
        switch (_scheduleType)
        {
            case BackupScheduleType.Interval:
                return lastBackup.AddMinutes(_intervalMinutes);

            case BackupScheduleType.Daily:
                // Next occurrence of HH:mm after lastBackup.
                // If lastBackup was today before HH:mm, due is today HH:mm.
                // Otherwise, due is tomorrow HH:mm.
                var todayDue = lastBackup.Date.AddHours(_hour).AddMinutes(_minute);
                if (todayDue <= lastBackup)
                    return todayDue.AddDays(1);
                return todayDue;

            case BackupScheduleType.Weekly:
                // Find the next occurrence of (DayOfWeek, HH:mm) strictly after lastBackup.
                var candidate = lastBackup;
                // Move to the next matching day-of-week at HH:mm
                for (int i = 0; i < 8; i++)
                {
                    var dayAtTime = candidate.Date.AddHours(_hour).AddMinutes(_minute);
                    if (dayAtTime > lastBackup && candidate.DayOfWeek == _dayOfWeek)
                        return dayAtTime;
                    candidate = candidate.AddDays(1);
                }
                // Fallback: next week same day
                return lastBackup.AddDays(7);

            case BackupScheduleType.Monthly:
                // Find the next occurrence of day-of-month (_dayOfMonth) at HH:mm after lastBackup.
                // If the day doesn't exist in a month (e.g., 31 in February), we use the last day of that month.
                var monthCandidate = new DateTime(lastBackup.Year, lastBackup.Month, 1);
                for (int i = 0; i < 60; i++)  // search up to 5 years
                {
                    var daysInMonth = DateTime.DaysInMonth(monthCandidate.Year, monthCandidate.Month);
                    var actualDay = Math.Min(_dayOfMonth, daysInMonth);
                    var dayAtTime = new DateTime(monthCandidate.Year, monthCandidate.Month, actualDay, _hour, _minute, 0);
                    if (dayAtTime > lastBackup)
                        return dayAtTime;
                    monthCandidate = monthCandidate.AddMonths(1);
                }
                return lastBackup.AddYears(1);

            default:
                return lastBackup.AddMinutes(_intervalMinutes);
        }
    }

    /// <summary>
    /// Reads the last successful backup time from the BackupHistory table.
    /// This survives app restarts — the scheduler doesn't restart its counter
    /// every time the app starts.
    /// </summary>
    private async Task<DateTime?> GetLastSuccessfulBackupTimeAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var history = await backupService.GetBackupHistoryAsync();
            var lastSuccess = history.FirstOrDefault(h => h.IsSuccessful);
            return lastSuccess?.BackupDate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read last backup time from history, assuming no previous backup");
            return null;
        }
    }

    private async Task<BackupScheduleSettings> ReadBackupSettingsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
            var settings = await settingService.GetAllAsync();

            var isEnabled = settings.TryGetValue("AutoBackupEnabled", out var enabledStr)
                && (enabledStr.Equals("true", StringComparison.OrdinalIgnoreCase) || enabledStr == "1");

            // Schedule type (default: Interval for backwards compat)
            var scheduleType = BackupScheduleType.Interval;
            if (settings.TryGetValue("AutoBackupScheduleType", out var typeStr)
                && int.TryParse(typeStr, out var typeInt)
                && Enum.IsDefined(typeof(BackupScheduleType), typeInt))
            {
                scheduleType = (BackupScheduleType)typeInt;
            }

            // Interval minutes (used for Interval mode, and as a fallback)
            var intervalMinutes = 60;
            if (settings.TryGetValue("AutoBackupIntervalMinutes", out var intervalStr)
                && int.TryParse(intervalStr, out var parsedMinutes) && parsedMinutes > 0)
            {
                intervalMinutes = parsedMinutes;
            }
            else if (settings.TryGetValue("AutoBackupIntervalHours", out var hoursStr)
                && int.TryParse(hoursStr, out var parsedHours) && parsedHours > 0)
            {
                // Support legacy hours-based setting
                intervalMinutes = parsedHours * 60;
            }

            // Hour and Minute (for Daily/Weekly/Monthly)
            var hour = 2;
            if (settings.TryGetValue("AutoBackupHour", out var hourStr)
                && int.TryParse(hourStr, out var parsedHour) && parsedHour >= 0 && parsedHour <= 23)
            {
                hour = parsedHour;
            }

            var minute = 0;
            if (settings.TryGetValue("AutoBackupMinute", out var minStr)
                && int.TryParse(minStr, out var parsedMin) && parsedMin >= 0 && parsedMin <= 59)
            {
                minute = parsedMin;
            }

            // Day of week (0=Sunday .. 6=Saturday)
            var dayOfWeek = DayOfWeek.Sunday;
            if (settings.TryGetValue("AutoBackupDayOfWeek", out var dowStr)
                && int.TryParse(dowStr, out var parsedDow) && parsedDow >= 0 && parsedDow <= 6)
            {
                dayOfWeek = (DayOfWeek)parsedDow;
            }

            // Day of month (1..28 — capped to avoid missing Feb)
            var dayOfMonth = 1;
            if (settings.TryGetValue("AutoBackupDayOfMonth", out var domStr)
                && int.TryParse(domStr, out var parsedDom) && parsedDom >= 1 && parsedDom <= 28)
            {
                dayOfMonth = parsedDom;
            }

            var backupDirectory = settings.TryGetValue("BackupDirectory", out var dir) ? dir : "";

            return new BackupScheduleSettings
            {
                IsEnabled = isEnabled,
                ScheduleType = scheduleType,
                IntervalMinutes = intervalMinutes,
                Hour = hour,
                Minute = minute,
                DayOfWeek = dayOfWeek,
                DayOfMonth = dayOfMonth,
                BackupDirectory = backupDirectory
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read backup settings");
            return new BackupScheduleSettings
            {
                IsEnabled = false,
                ScheduleType = BackupScheduleType.Interval,
                IntervalMinutes = 60,
                Hour = 2,
                Minute = 0,
                DayOfWeek = DayOfWeek.Sunday,
                DayOfMonth = 1,
                BackupDirectory = ""
            };
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}

/// <summary>
/// Strongly-typed backup schedule settings.
/// </summary>
public class BackupScheduleSettings
{
    public bool IsEnabled { get; set; }
    public BackupScheduleType ScheduleType { get; set; } = BackupScheduleType.Interval;
    public int IntervalMinutes { get; set; } = 60;
    public int Hour { get; set; } = 2;
    public int Minute { get; set; } = 0;
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Sunday;
    public int DayOfMonth { get; set; } = 1;
    public string BackupDirectory { get; set; } = "";
}
