using AutoPartsShop.Application.DTOs.Stock;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace AutoPartsShop.UI.ViewModels;

public partial class BackupViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    // Setting keys - must match the keys used in AutoBackupScheduler
    private static class SettingKeys
    {
        public const string AutoBackupEnabled = "AutoBackupEnabled";
        public const string AutoBackupScheduleType = "AutoBackupScheduleType";
        public const string AutoBackupIntervalMinutes = "AutoBackupIntervalMinutes";
        public const string AutoBackupHour = "AutoBackupHour";
        public const string AutoBackupMinute = "AutoBackupMinute";
        public const string AutoBackupDayOfWeek = "AutoBackupDayOfWeek";
        public const string AutoBackupDayOfMonth = "AutoBackupDayOfMonth";
        public const string BackupDirectory = "BackupDirectory";
    }

    [ObservableProperty]
    private ObservableCollection<BackupHistoryDto> _backupHistory = [];

    [ObservableProperty]
    private string _backupDirectory = string.Empty;

    [ObservableProperty]
    private bool _isAutoBackupEnabled;

    [ObservableProperty]
    private bool _isCreatingBackup;

    [ObservableProperty]
    private bool _isRestoring;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    // ===== Schedule type (0=Interval, 1=Daily, 2=Weekly, 3=Monthly) =====
    [ObservableProperty]
    private int _scheduleTypeIndex = 0;  // bound to ComboBox.SelectedIndex

    public BackupScheduleType ScheduleType => (BackupScheduleType)ScheduleTypeIndex;

    // Convenience booleans for showing/hiding UI sections
    public bool IsIntervalMode => ScheduleTypeIndex == 0;
    public bool IsDailyMode    => ScheduleTypeIndex == 1;
    public bool IsWeeklyMode   => ScheduleTypeIndex == 2;
    public bool IsMonthlyMode  => ScheduleTypeIndex == 3;

    // ===== Interval mode =====
    [ObservableProperty]
    private int _intervalValue = 60;          // the raw number (e.g. 60)

    [ObservableProperty]
    private int _intervalUnitIndex = 1;       // 0=minutes, 1=hours, 2=days (default: hours)

    /// <summary>Display strings for the interval unit ComboBox.</summary>
    public List<string> IntervalUnits { get; } = new()
    {
        "دقائق",
        "ساعات",
        "أيام"
    };

    /// <summary>Helper: returns the configured interval in MINUTES (the value stored in DB).</summary>
    public int IntervalMinutes
    {
        get
        {
            return IntervalUnitIndex switch
            {
                0 => IntervalValue,                     // minutes
                1 => IntervalValue * 60,                 // hours
                2 => IntervalValue * 60 * 24,            // days
                _ => IntervalValue
            };
        }
    }

    // ===== Daily / Weekly / Monthly: time of day =====
    [ObservableProperty]
    private int _selectedHour = 2;             // 0..23, default 2 AM

    [ObservableProperty]
    private int _selectedMinute = 0;           // 0..59

    /// <summary>Hour choices for the ComboBox (0..23).</summary>
    public List<int> Hours { get; } = Enumerable.Range(0, 24).ToList();

    /// <summary>Minute choices for the ComboBox (0, 5, 10, ... 55).</summary>
    public List<int> Minutes { get; } = Enumerable.Range(0, 12).Select(i => i * 5).ToList();

    // ===== Weekly mode: day of week =====
    [ObservableProperty]
    private int _selectedDayOfWeekIndex = 0;   // 0=Sunday..6=Saturday

    public List<string> DaysOfWeek { get; } = new()
    {
        "الأحد",
        "الإثنين",
        "الثلاثاء",
        "الأربعاء",
        "الخميس",
        "الجمعة",
        "السبت"
    };

    // ===== Monthly mode: day of month =====
    [ObservableProperty]
    private int _selectedDayOfMonth = 1;       // 1..28

    public List<int> DaysOfMonth { get; } = Enumerable.Range(1, 28).ToList();

    // ===== Next backup preview (read-only, for UX) =====
    [ObservableProperty]
    private string _nextBackupDisplay = "—";

    public BackupViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = InitializeAsync();
    }

    partial void OnScheduleTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsIntervalMode));
        OnPropertyChanged(nameof(IsDailyMode));
        OnPropertyChanged(nameof(IsWeeklyMode));
        OnPropertyChanged(nameof(IsMonthlyMode));
        UpdateNextBackupDisplay();
    }

    partial void OnIntervalValueChanged(int value) => UpdateNextBackupDisplay();
    partial void OnIntervalUnitIndexChanged(int value) => UpdateNextBackupDisplay();
    partial void OnSelectedHourChanged(int value) => UpdateNextBackupDisplay();
    partial void OnSelectedMinuteChanged(int value) => UpdateNextBackupDisplay();
    partial void OnSelectedDayOfWeekIndexChanged(int value) => UpdateNextBackupDisplay();
    partial void OnSelectedDayOfMonthChanged(int value) => UpdateNextBackupDisplay();

    /// <summary>
    /// Updates the "next backup" preview shown under the schedule section.
    /// </summary>
    private void UpdateNextBackupDisplay()
    {
        try
        {
            DateTime now = DateTime.Now;
            DateTime next = ScheduleType switch
            {
                BackupScheduleType.Interval => now.AddMinutes(IntervalMinutes),
                BackupScheduleType.Daily    => ComputeNextDaily(now),
                BackupScheduleType.Weekly   => ComputeNextWeekly(now),
                BackupScheduleType.Monthly  => ComputeNextMonthly(now),
                _ => now
            };
            NextBackupDisplay = next.ToString("dddd، dd MMMM yyyy - HH:mm");
        }
        catch
        {
            NextBackupDisplay = "—";
        }
    }

    private DateTime ComputeNextDaily(DateTime now)
    {
        var todayAtTime = now.Date.AddHours(SelectedHour).AddMinutes(SelectedMinute);
        return todayAtTime <= now ? todayAtTime.AddDays(1) : todayAtTime;
    }

    private DateTime ComputeNextWeekly(DateTime now)
    {
        var targetDow = (DayOfWeek)SelectedDayOfWeekIndex;
        var candidate = now;
        for (int i = 0; i < 8; i++)
        {
            var dayAtTime = candidate.Date.AddHours(SelectedHour).AddMinutes(SelectedMinute);
            if (dayAtTime > now && candidate.DayOfWeek == targetDow)
                return dayAtTime;
            candidate = candidate.AddDays(1);
        }
        return now.AddDays(7);
    }

    private DateTime ComputeNextMonthly(DateTime now)
    {
        var monthCandidate = new DateTime(now.Year, now.Month, 1);
        for (int i = 0; i < 60; i++)
        {
            var daysInMonth = DateTime.DaysInMonth(monthCandidate.Year, monthCandidate.Month);
            var actualDay = Math.Min(SelectedDayOfMonth, daysInMonth);
            var dayAtTime = new DateTime(monthCandidate.Year, monthCandidate.Month, actualDay, SelectedHour, SelectedMinute, 0);
            if (dayAtTime > now)
                return dayAtTime;
            monthCandidate = monthCandidate.AddMonths(1);
        }
        return now.AddYears(1);
    }

    /// <summary>
    /// Loads backup settings from the database and backup history.
    /// </summary>
    private async Task InitializeAsync()
    {
        await LoadBackupSettingsAsync();
        await LoadBackupHistoryAsync();
        UpdateNextBackupDisplay();
    }

    /// <summary>
    /// Loads auto backup settings from the database so the UI reflects the current state.
    /// </summary>
    private async Task LoadBackupSettingsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
            var settings = await settingService.GetAllAsync();

            if (settings.TryGetValue(SettingKeys.AutoBackupEnabled, out var enabledStr))
            {
                IsAutoBackupEnabled = enabledStr.Equals("true", StringComparison.OrdinalIgnoreCase) || enabledStr == "1";
            }

            // Schedule type (default 0 = Interval)
            if (settings.TryGetValue(SettingKeys.AutoBackupScheduleType, out var typeStr)
                && int.TryParse(typeStr, out var typeInt)
                && typeInt >= 0 && typeInt <= 3)
            {
                ScheduleTypeIndex = typeInt;
            }

            // Interval minutes → break down into (value, unit) for the UI
            if (settings.TryGetValue(SettingKeys.AutoBackupIntervalMinutes, out var intervalStr)
                && int.TryParse(intervalStr, out var parsedMinutes) && parsedMinutes > 0)
            {
                SetIntervalFromMinutes(parsedMinutes);
            }
            else if (settings.TryGetValue("AutoBackupIntervalHours", out var hoursStr)
                && int.TryParse(hoursStr, out var parsedHours) && parsedHours > 0)
            {
                // Legacy hours-based setting
                IntervalValue = parsedHours;
                IntervalUnitIndex = 1;  // hours
            }

            // Hour / Minute
            if (settings.TryGetValue(SettingKeys.AutoBackupHour, out var hourStr)
                && int.TryParse(hourStr, out var parsedHour) && parsedHour >= 0 && parsedHour <= 23)
            {
                SelectedHour = parsedHour;
            }
            if (settings.TryGetValue(SettingKeys.AutoBackupMinute, out var minStr)
                && int.TryParse(minStr, out var parsedMin) && parsedMin >= 0 && parsedMin <= 59)
            {
                SelectedMinute = parsedMin;
            }

            // Day of week
            if (settings.TryGetValue(SettingKeys.AutoBackupDayOfWeek, out var dowStr)
                && int.TryParse(dowStr, out var parsedDow) && parsedDow >= 0 && parsedDow <= 6)
            {
                SelectedDayOfWeekIndex = parsedDow;
            }

            // Day of month
            if (settings.TryGetValue(SettingKeys.AutoBackupDayOfMonth, out var domStr)
                && int.TryParse(domStr, out var parsedDom) && parsedDom >= 1 && parsedDom <= 28)
            {
                SelectedDayOfMonth = parsedDom;
            }

            // Backup directory
            if (settings.TryGetValue(SettingKeys.BackupDirectory, out var dir))
            {
                BackupDirectory = dir;
            }
        }
        catch
        {
            // Use defaults if settings can't be loaded
        }
    }

    /// <summary>
    /// Converts an absolute minute count into (value, unit) for the UI.
    /// Picks the largest unit that divides evenly.
    /// </summary>
    private void SetIntervalFromMinutes(int totalMinutes)
    {
        if (totalMinutes >= 1440 && totalMinutes % 1440 == 0)
        {
            IntervalValue = totalMinutes / 1440;
            IntervalUnitIndex = 2;  // days
        }
        else if (totalMinutes >= 60 && totalMinutes % 60 == 0)
        {
            IntervalValue = totalMinutes / 60;
            IntervalUnitIndex = 1;  // hours
        }
        else
        {
            IntervalValue = totalMinutes;
            IntervalUnitIndex = 0;  // minutes
        }
    }

    /// <summary>
    /// Saves the current auto backup settings to the database.
    /// </summary>
    private async Task SaveAutoBackupSettingsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            var settings = new Dictionary<string, string>
            {
                [SettingKeys.AutoBackupEnabled] = IsAutoBackupEnabled.ToString(),
                [SettingKeys.AutoBackupScheduleType] = ((int)ScheduleType).ToString(),
                [SettingKeys.AutoBackupIntervalMinutes] = IntervalMinutes.ToString(),
                [SettingKeys.AutoBackupHour] = SelectedHour.ToString(),
                [SettingKeys.AutoBackupMinute] = SelectedMinute.ToString(),
                [SettingKeys.AutoBackupDayOfWeek] = SelectedDayOfWeekIndex.ToString(),
                [SettingKeys.AutoBackupDayOfMonth] = SelectedDayOfMonth.ToString(),
                [SettingKeys.BackupDirectory] = BackupDirectory ?? ""
            };

            await settingService.SaveSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save auto backup settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var dir = BackupDirectory;
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AutoPartsShop_Backups");
        }

        IsCreatingBackup = true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var backupPath = await backupService.CreateBackupAsync(dir);
            StatusMessage = $"تم إنشاء النسخة الاحتياطية بنجاح: {backupPath}";
            HasStatusMessage = true;

            await LoadBackupHistoryAsync();
            UpdateNextBackupDisplay();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في إنشاء النسخة الاحتياطية: {ex.Message}";
            HasStatusMessage = true;
        }
        finally
        {
            IsCreatingBackup = false;
        }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Backup Files (*.bak)|*.bak|All Files (*.*)|*.*",
            Title = "اختر ملف النسخة الاحتياطية"
        };

        if (dialog.ShowDialog() != true)
            return;

        var result = System.Windows.MessageBox.Show(
            "هل أنت متأكد من استعادة هذه النسخة الاحتياطية؟ سيتم استبدال البيانات الحالية.",
            "تأكيد الاستعادة",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        IsRestoring = true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            await backupService.RestoreBackupAsync(dialog.FileName);
            StatusMessage = "تمت استعادة النسخة الاحتياطية بنجاح. يرجى إعادة تشغيل التطبيق.";
            HasStatusMessage = true;

            await LoadBackupHistoryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في الاستعادة: {ex.Message}";
            HasStatusMessage = true;
        }
        finally
        {
            IsRestoring = false;
        }
    }

    /// <summary>
    /// Toggles auto backup on/off AND applies the new schedule.
    /// Saves settings to the database, then starts/restarts the scheduler.
    /// </summary>
    [RelayCommand]
    private async Task SaveBackupSettingsAsync()
    {
        try
        {
            // Validate inputs based on schedule type
            if (IsIntervalMode && IntervalValue <= 0)
            {
                StatusMessage = "قيمة الفترة يجب أن تكون أكبر من صفر";
                HasStatusMessage = true;
                return;
            }

            if (IsIntervalMode && IntervalMinutes < 5)
            {
                StatusMessage = "أقل فترة مسموح بها هي 5 دقائق";
                HasStatusMessage = true;
                return;
            }

            // Save settings to DB first
            await SaveAutoBackupSettingsAsync();

            using var scope = _scopeFactory.CreateScope();
            var autoBackupScheduler = scope.ServiceProvider.GetRequiredService<IAutoBackupScheduler>();

            if (IsAutoBackupEnabled)
            {
                // Restart the scheduler so it picks up the new settings from DB
                await autoBackupScheduler.RestartAsync();

                var description = ScheduleType switch
                {
                    BackupScheduleType.Interval => $"كل {IntervalValue} {IntervalUnits[IntervalUnitIndex]}",
                    BackupScheduleType.Daily    => $"يومياً الساعة {SelectedHour:D2}:{SelectedMinute:D2}",
                    BackupScheduleType.Weekly   => $"أسبوعياً يوم {DaysOfWeek[SelectedDayOfWeekIndex]} الساعة {SelectedHour:D2}:{SelectedMinute:D2}",
                    BackupScheduleType.Monthly  => $"شهرياً يوم {SelectedDayOfMonth} الساعة {SelectedHour:D2}:{SelectedMinute:D2}",
                    _ => ""
                };
                StatusMessage = $"تم حفظ إعدادات النسخ الاحتياطي التلقائي: {description}";
                HasStatusMessage = true;
            }
            else
            {
                await autoBackupScheduler.StopAsync();
                StatusMessage = "تم إيقاف النسخ الاحتياطي التلقائي";
                HasStatusMessage = true;
            }

            UpdateNextBackupDisplay();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            HasStatusMessage = true;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadBackupSettingsAsync();
        await LoadBackupHistoryAsync();
        UpdateNextBackupDisplay();
    }

    [RelayCommand]
    private void BrowseBackupDirectory()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "اختر مجلد النسخ الاحتياطي",
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            BackupDirectory = dialog.SelectedPath;
        }
    }

    private async Task LoadBackupHistoryAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var history = await backupService.GetBackupHistoryAsync();
            BackupHistory = new ObservableCollection<BackupHistoryDto>(history);
        }
        catch
        {
            BackupHistory = [];
        }
    }
}
