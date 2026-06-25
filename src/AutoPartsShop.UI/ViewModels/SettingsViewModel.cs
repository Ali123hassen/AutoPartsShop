using AutoPartsShop.Application.DTOs;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.UI.Services;
using AutoPartsShop.UI.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace AutoPartsShop.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SettingsViewModel> _logger;

    #region User Settings Properties

    [ObservableProperty]
    private string _shopName = string.Empty;

    [ObservableProperty]
    private string _shopAddress = string.Empty;

    [ObservableProperty]
    private string _shopPhone = string.Empty;

    [ObservableProperty]
    private string _taxRate = "15";

    [ObservableProperty]
    private string _currency = "ر.س";

    [ObservableProperty]
    private string _profitMargin = "0";

    /// <summary>
    /// مسار صورة اللوقو - يُخزّن في مجلد التطبيق ويُحفظ مساره في الإعدادات
    /// </summary>
    [ObservableProperty]
    private string _shopLogoPath = string.Empty;

    /// <summary>
    /// هل يوجد لوقو محفوظ؟ (لإظهار/إخفاء المعاينة وزر الحذف)
    /// </summary>
    [ObservableProperty]
    private bool _hasShopLogo;

    #endregion

    #region Application Settings Properties

    [ObservableProperty]
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private bool _autoBackupEnabled;

    // ===== NEW: Schedule type and timing properties =====

    /// <summary>0=Interval, 1=Daily, 2=Weekly, 3=Monthly (bound to ComboBox.SelectedIndex).</summary>
    [ObservableProperty]
    private int _autoBackupScheduleTypeIndex = 0;

    public BackupScheduleType AutoBackupScheduleType => (BackupScheduleType)AutoBackupScheduleTypeIndex;

    public bool IsIntervalMode => AutoBackupScheduleTypeIndex == 0;
    public bool IsDailyMode => AutoBackupScheduleTypeIndex == 1;
    public bool IsWeeklyMode => AutoBackupScheduleTypeIndex == 2;
    public bool IsMonthlyMode => AutoBackupScheduleTypeIndex == 3;

    /// <summary>Raw interval value (e.g. 60).</summary>
    [ObservableProperty]
    private string _autoBackupIntervalValue = "1";

    /// <summary>0=minutes, 1=hours, 2=days.</summary>
    [ObservableProperty]
    private int _autoBackupIntervalUnitIndex = 1;

    public List<string> IntervalUnits { get; } = new() { "دقائق", "ساعات", "أيام" };

    /// <summary>Helper: returns the configured interval in MINUTES (the value stored in DB).</summary>
    public int AutoBackupIntervalMinutes
    {
        get
        {
            if (int.TryParse(AutoBackupIntervalValue, out var val) && val > 0)
            {
                return AutoBackupIntervalUnitIndex switch
                {
                    0 => val,                  // minutes
                    1 => val * 60,              // hours
                    2 => val * 60 * 24,         // days
                    _ => val
                };
            }
            return 60;  // safe default
        }
    }

    /// <summary>Hour of day (0-23) for Daily/Weekly/Monthly modes.</summary>
    [ObservableProperty]
    private int _autoBackupHour = 2;

    /// <summary>Minute (0-59) for Daily/Weekly/Monthly modes.</summary>
    [ObservableProperty]
    private int _autoBackupMinute = 0;

    public List<int> HoursList { get; } = Enumerable.Range(0, 24).ToList();
    public List<int> MinutesList { get; } = Enumerable.Range(0, 12).Select(i => i * 5).ToList();

    /// <summary>Day of week index (0=Sunday..6=Saturday) for Weekly mode.</summary>
    [ObservableProperty]
    private int _autoBackupDayOfWeekIndex = 0;

    public List<string> DaysOfWeekList { get; } = new()
    {
        "الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"
    };

    /// <summary>Day of month (1-28) for Monthly mode.</summary>
    [ObservableProperty]
    private int _autoBackupDayOfMonth = 1;

    public List<int> DaysOfMonthList { get; } = Enumerable.Range(1, 28).ToList();

    // ===== Next backup preview =====
    [ObservableProperty]
    private string _nextBackupDisplay = "—";

    [ObservableProperty]
    private string _backupDirectory = string.Empty;

    [ObservableProperty]
    private bool _lowStockAlertEnabled = true;

    [ObservableProperty]
    private string _lowStockThreshold = "5";

    #endregion

    #region UI State

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private bool _isCreatingBackup;

    [ObservableProperty]
    private bool _isRestoring;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isSuccessMessage;

    [ObservableProperty]
    private bool _isLoading;

    #region License Properties

    [ObservableProperty]
    private string _licenseStatusText = "جاري التحميل...";

    [ObservableProperty]
    private string _licenseCustomerName = string.Empty;

    [ObservableProperty]
    private string _licenseTypeText = string.Empty;

    [ObservableProperty]
    private string _licenseExpirationDate = string.Empty;

    [ObservableProperty]
    private string _licenseDaysRemaining = string.Empty;

    [ObservableProperty]
    private string _licenseHardwareId = string.Empty;

    [ObservableProperty]
    private bool _hasActiveLicense;

    [ObservableProperty]
    private bool _isLicenseTrial;

    [ObservableProperty]
    private bool _isLicenseExpiringSoon;

    #endregion

    #endregion

    // Setting keys constants
    private static class SettingKeys
    {
        public const string ShopName = "ShopName";
        public const string ShopAddress = "ShopAddress";
        public const string ShopPhone = "ShopPhone";
        public const string TaxRate = "TaxRate";
        public const string Currency = "Currency";
        public const string ProfitMargin = "ProfitMargin";
        public const string ShopLogoPath = "ShopLogoPath";
        public const string NotificationsEnabled = "NotificationsEnabled";
        public const string AutoBackupEnabled = "AutoBackupEnabled";
        public const string AutoBackupIntervalMinutes = "AutoBackupIntervalMinutes";
        public const string AutoBackupScheduleType = "AutoBackupScheduleType";
        public const string AutoBackupHour = "AutoBackupHour";
        public const string AutoBackupMinute = "AutoBackupMinute";
        public const string AutoBackupDayOfWeek = "AutoBackupDayOfWeek";
        public const string AutoBackupDayOfMonth = "AutoBackupDayOfMonth";
        public const string BackupDirectory = "BackupDirectory";
        public const string LowStockAlertEnabled = "LowStockAlertEnabled";
        public const string LowStockThreshold = "LowStockThreshold";
    }

    public SettingsViewModel(IServiceScopeFactory scopeFactory, ILogger<SettingsViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        // Settings are loaded when the page fires the Loaded event
    }

    // ===== Property change handlers to update the "next backup" preview =====
    partial void OnAutoBackupScheduleTypeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsIntervalMode));
        OnPropertyChanged(nameof(IsDailyMode));
        OnPropertyChanged(nameof(IsWeeklyMode));
        OnPropertyChanged(nameof(IsMonthlyMode));
        UpdateNextBackupDisplay();
    }

    partial void OnAutoBackupIntervalValueChanged(string value) => UpdateNextBackupDisplay();
    partial void OnAutoBackupIntervalUnitIndexChanged(int value) => UpdateNextBackupDisplay();
    partial void OnAutoBackupHourChanged(int value) => UpdateNextBackupDisplay();
    partial void OnAutoBackupMinuteChanged(int value) => UpdateNextBackupDisplay();
    partial void OnAutoBackupDayOfWeekIndexChanged(int value) => UpdateNextBackupDisplay();
    partial void OnAutoBackupDayOfMonthChanged(int value) => UpdateNextBackupDisplay();

    /// <summary>
    /// Updates the "next backup" preview shown under the schedule section.
    /// </summary>
    private void UpdateNextBackupDisplay()
    {
        try
        {
            DateTime now = DateTime.Now;
            DateTime next = AutoBackupScheduleType switch
            {
                BackupScheduleType.Interval => now.AddMinutes(AutoBackupIntervalMinutes),
                BackupScheduleType.Daily => ComputeNextDaily(now),
                BackupScheduleType.Weekly => ComputeNextWeekly(now),
                BackupScheduleType.Monthly => ComputeNextMonthly(now),
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
        var todayAtTime = now.Date.AddHours(AutoBackupHour).AddMinutes(AutoBackupMinute);
        return todayAtTime <= now ? todayAtTime.AddDays(1) : todayAtTime;
    }

    private DateTime ComputeNextWeekly(DateTime now)
    {
        var targetDow = (DayOfWeek)AutoBackupDayOfWeekIndex;
        var candidate = now;
        for (int i = 0; i < 8; i++)
        {
            var dayAtTime = candidate.Date.AddHours(AutoBackupHour).AddMinutes(AutoBackupMinute);
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
            var actualDay = Math.Min(AutoBackupDayOfMonth, daysInMonth);
            var dayAtTime = new DateTime(monthCandidate.Year, monthCandidate.Month, actualDay, AutoBackupHour, AutoBackupMinute, 0);
            if (dayAtTime > now)
                return dayAtTime;
            monthCandidate = monthCandidate.AddMonths(1);
        }
        return now.AddYears(1);
    }

    #region Load Settings

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            var settings = await settingService.GetAllAsync();

            _logger.LogInformation("Loaded {Count} settings from database", settings.Count);

            ShopName = GetValue(settings, SettingKeys.ShopName, "");
            ShopAddress = GetValue(settings, SettingKeys.ShopAddress, "");
            ShopPhone = GetValue(settings, SettingKeys.ShopPhone, "");
            TaxRate = GetValue(settings, SettingKeys.TaxRate, "15");
            Currency = GetValue(settings, SettingKeys.Currency, "ر.س");
            ProfitMargin = GetValue(settings, SettingKeys.ProfitMargin, "0");
            ShopLogoPath = GetValue(settings, SettingKeys.ShopLogoPath, "");
            HasShopLogo = !string.IsNullOrWhiteSpace(ShopLogoPath) && System.IO.File.Exists(ShopLogoPath);
            NotificationsEnabled = GetBoolValue(settings, SettingKeys.NotificationsEnabled, true);
            AutoBackupEnabled = GetBoolValue(settings, SettingKeys.AutoBackupEnabled, false);

            // ===== NEW: Load schedule type and timing settings =====
            if (settings.TryGetValue(SettingKeys.AutoBackupScheduleType, out var typeStr)
                && int.TryParse(typeStr, out var typeInt) && typeInt >= 0 && typeInt <= 3)
            {
                AutoBackupScheduleTypeIndex = typeInt;
            }

            // Load interval minutes and break it down into (value, unit)
            var intervalStr = GetValue(settings, SettingKeys.AutoBackupIntervalMinutes, "60");
            if (int.TryParse(intervalStr, out var totalMinutes) && totalMinutes > 0)
            {
                SetIntervalFromMinutes(totalMinutes);
            }
            else
            {
                AutoBackupIntervalValue = "1";
                AutoBackupIntervalUnitIndex = 1;  // hours
            }

            // Hour and Minute
            if (settings.TryGetValue(SettingKeys.AutoBackupHour, out var hourStr)
                && int.TryParse(hourStr, out var parsedHour) && parsedHour >= 0 && parsedHour <= 23)
            {
                AutoBackupHour = parsedHour;
            }
            if (settings.TryGetValue(SettingKeys.AutoBackupMinute, out var minStr)
                && int.TryParse(minStr, out var parsedMin) && parsedMin >= 0 && parsedMin <= 59)
            {
                AutoBackupMinute = parsedMin;
            }

            // Day of week
            if (settings.TryGetValue(SettingKeys.AutoBackupDayOfWeek, out var dowStr)
                && int.TryParse(dowStr, out var parsedDow) && parsedDow >= 0 && parsedDow <= 6)
            {
                AutoBackupDayOfWeekIndex = parsedDow;
            }

            // Day of month
            if (settings.TryGetValue(SettingKeys.AutoBackupDayOfMonth, out var domStr)
                && int.TryParse(domStr, out var parsedDom) && parsedDom >= 1 && parsedDom <= 28)
            {
                AutoBackupDayOfMonth = parsedDom;
            }

            BackupDirectory = GetValue(settings, SettingKeys.BackupDirectory, "");
            LowStockAlertEnabled = GetBoolValue(settings, SettingKeys.LowStockAlertEnabled, true);
            LowStockThreshold = GetValue(settings, SettingKeys.LowStockThreshold, "5");

            _logger.LogInformation("Settings applied: ShopName={ShopName}, TaxRate={TaxRate}, Currency={Currency}",
                ShopName, TaxRate, Currency);

            // Update the next-backup preview
            UpdateNextBackupDisplay();

            // Load license info
            await LoadLicenseInfoAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings");
            ShowStatus($"خطأ في تحميل الإعدادات: {ex.Message}", false);
        }
        finally
        {
            IsLoading = false;
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
            AutoBackupIntervalValue = (totalMinutes / 1440).ToString();
            AutoBackupIntervalUnitIndex = 2;  // days
        }
        else if (totalMinutes >= 60 && totalMinutes % 60 == 0)
        {
            AutoBackupIntervalValue = (totalMinutes / 60).ToString();
            AutoBackupIntervalUnitIndex = 1;  // hours
        }
        else
        {
            AutoBackupIntervalValue = totalMinutes.ToString();
            AutoBackupIntervalUnitIndex = 0;  // minutes
        }
    }

    [RelayCommand]
    private async Task LoadLicenseInfoAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var licenseService = scope.ServiceProvider.GetRequiredService<ILicenseService>();

            var license = await licenseService.GetCurrentLicenseAsync();

            if (license != null)
            {
                HasActiveLicense = true;
                IsLicenseTrial = license.IsTrial;
                LicenseCustomerName = license.CustomerName;
                LicenseTypeText = GetLicenseTypeArabic(license.LicenseType);
                LicenseExpirationDate = license.ExpirationDate.ToString("yyyy/MM/dd");
                LicenseDaysRemaining = license.DaysRemaining > 0
                    ? $"{license.DaysRemaining} يوم"
                    : "منتهي الصلاحية";
                IsLicenseExpiringSoon = license.DaysRemaining > 0 && license.DaysRemaining <= 7;

                if (license.IsTrial)
                {
                    LicenseStatusText = $"فترة تجربة ({license.DaysRemaining} يوم متبقي)";
                }
                else if (license.DaysRemaining > 0)
                {
                    LicenseStatusText = "مرخص وفعال";
                }
                else
                {
                    LicenseStatusText = "منتهي الصلاحية";
                    HasActiveLicense = false;
                }
            }
            else
            {
                HasActiveLicense = false;
                IsLicenseTrial = true;
                LicenseStatusText = "غير مفعّل";
            }

            LicenseHardwareId = await licenseService.GetHardwareFingerprintAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading license info");
        }
    }

    [RelayCommand]
    private async Task ShowLicenseActivationAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var activationView = scope.ServiceProvider.GetRequiredService<LicenseActivationView>();
            var activationVm = scope.ServiceProvider.GetRequiredService<LicenseActivationViewModel>();
            activationView.DataContext = activationVm;

            activationVm.LicenseActivated += async () =>
            {
                await LoadLicenseInfoAsync();
            };

            activationView.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing license activation");
        }
    }

    [RelayCommand]
    private void CopyHardwareId()
    {
        if (!string.IsNullOrEmpty(LicenseHardwareId))
        {
            System.Windows.Clipboard.SetText(LicenseHardwareId);
            ShowStatus("تم نسخ بصمة الجهاز!", true);
        }
    }

    private static string GetLicenseTypeArabic(LicenseType type) => type switch
    {
        LicenseType.Trial => "تجريبي",
        LicenseType.Standard => "قياسي",
        LicenseType.Professional => "احترافي",
        LicenseType.Enterprise => "مؤسسي",
        _ => "غير معروف"
    };

    private static string GetValue(Dictionary<string, string> settings, string key, string defaultValue)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : defaultValue;
    }

    private static bool GetBoolValue(Dictionary<string, string> settings, string key, bool defaultValue)
    {
        if (settings.TryGetValue(key, out var value))
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }
        return defaultValue;
    }

    #endregion

    #region Save Settings

    [RelayCommand]
    private async Task SaveUserSettingsAsync()
    {
        IsSaving = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            var settings = new Dictionary<string, string>
            {
                [SettingKeys.ShopName] = ShopName ?? "",
                [SettingKeys.ShopAddress] = ShopAddress ?? "",
                [SettingKeys.ShopPhone] = ShopPhone ?? "",
                [SettingKeys.TaxRate] = TaxRate ?? "15",
                [SettingKeys.Currency] = Currency ?? "ر.س",
                [SettingKeys.ProfitMargin] = ProfitMargin ?? "0",
                [SettingKeys.ShopLogoPath] = ShopLogoPath ?? ""
            };

            await settingService.SaveSettingsAsync(settings);

            // تحديث اسم المحل واللوقو في الشريط الجانبي وعنوان النافذة فوراً
            try
            {
                foreach (var window in System.Windows.Application.Current.Windows)
                {
                    if (window is Views.MainWindow mainWindow && mainWindow.DataContext is MainViewModel mainVm)
                    {
                        await mainVm.RefreshShopNameAsync();
                        break;
                    }
                }
            }
            catch { /* لا توقف العملية إذا فشل التحديث */ }

            ShowStatus("تم حفظ إعدادات النظام بنجاح", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user settings");
            ShowStatus($"خطأ في حفظ الإعدادات: {ex.Message}", false);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task SaveAppSettingsAsync()
    {
        // Validate interval input
        if (IsIntervalMode)
        {
            if (!int.TryParse(AutoBackupIntervalValue, out var val) || val <= 0)
            {
                ShowStatus("قيمة الفترة يجب أن تكون رقماً أكبر من صفر", false);
                return;
            }
            if (AutoBackupIntervalMinutes < 5)
            {
                ShowStatus("أقل فترة مسموح بها هي 5 دقائق", false);
                return;
            }
        }

        IsSaving = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            var settings = new Dictionary<string, string>
            {
                [SettingKeys.NotificationsEnabled] = NotificationsEnabled.ToString(),
                [SettingKeys.AutoBackupEnabled] = AutoBackupEnabled.ToString(),
                [SettingKeys.AutoBackupScheduleType] = AutoBackupScheduleTypeIndex.ToString(),
                [SettingKeys.AutoBackupIntervalMinutes] = AutoBackupIntervalMinutes.ToString(),
                [SettingKeys.AutoBackupHour] = AutoBackupHour.ToString(),
                [SettingKeys.AutoBackupMinute] = AutoBackupMinute.ToString(),
                [SettingKeys.AutoBackupDayOfWeek] = AutoBackupDayOfWeekIndex.ToString(),
                [SettingKeys.AutoBackupDayOfMonth] = AutoBackupDayOfMonth.ToString(),
                [SettingKeys.BackupDirectory] = BackupDirectory ?? "",
                [SettingKeys.LowStockAlertEnabled] = LowStockAlertEnabled.ToString(),
                [SettingKeys.LowStockThreshold] = LowStockThreshold ?? "5"
            };

            await settingService.SaveSettingsAsync(settings);

            // If auto backup enabled, restart the scheduler with new settings
            var autoBackupScheduler = scope.ServiceProvider.GetRequiredService<IAutoBackupScheduler>();
            await autoBackupScheduler.RestartAsync();

            // Build a human-readable description for the success message
            var description = AutoBackupScheduleType switch
            {
                BackupScheduleType.Interval => $"كل {AutoBackupIntervalValue} {IntervalUnits[AutoBackupIntervalUnitIndex]}",
                BackupScheduleType.Daily => $"يومياً الساعة {AutoBackupHour:D2}:{AutoBackupMinute:D2}",
                BackupScheduleType.Weekly => $"أسبوعياً يوم {DaysOfWeekList[AutoBackupDayOfWeekIndex]} الساعة {AutoBackupHour:D2}:{AutoBackupMinute:D2}",
                BackupScheduleType.Monthly => $"شهرياً يوم {AutoBackupDayOfMonth} الساعة {AutoBackupHour:D2}:{AutoBackupMinute:D2}",
                _ => ""
            };
            ShowStatus($"تم حفظ إعدادات النسخ الاحتياطي: {description}", true);

            UpdateNextBackupDisplay();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving app settings");
            ShowStatus($"خطأ في حفظ الإعدادات: {ex.Message}", false);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        var result = System.Windows.MessageBox.Show(
            "هل أنت متأكد من إعادة تعيين الإعدادات إلى القيم الافتراضية؟",
            "تأكيد إعادة التعيين",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            // Reset to defaults
            ShopName = string.Empty;
            ShopAddress = string.Empty;
            ShopPhone = string.Empty;
            TaxRate = "15";
            Currency = "ر.س";
            ProfitMargin = "0";
            ShopLogoPath = string.Empty;
            HasShopLogo = false;
            NotificationsEnabled = true;
            AutoBackupEnabled = false;
            AutoBackupScheduleTypeIndex = 0;
            AutoBackupIntervalValue = "1";
            AutoBackupIntervalUnitIndex = 1;
            AutoBackupHour = 2;
            AutoBackupMinute = 0;
            AutoBackupDayOfWeekIndex = 0;
            AutoBackupDayOfMonth = 1;
            BackupDirectory = string.Empty;
            LowStockAlertEnabled = true;
            LowStockThreshold = "5";

            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            var settings = new Dictionary<string, string>
            {
                [SettingKeys.ShopName] = "",
                [SettingKeys.ShopAddress] = "",
                [SettingKeys.ShopPhone] = "",
                [SettingKeys.TaxRate] = "15",
                [SettingKeys.Currency] = "ر.س",
                [SettingKeys.ProfitMargin] = "0",
                [SettingKeys.ShopLogoPath] = "",
                [SettingKeys.NotificationsEnabled] = "True",
                [SettingKeys.AutoBackupEnabled] = "False",
                [SettingKeys.AutoBackupScheduleType] = "0",
                [SettingKeys.AutoBackupIntervalMinutes] = "60",
                [SettingKeys.AutoBackupHour] = "2",
                [SettingKeys.AutoBackupMinute] = "0",
                [SettingKeys.AutoBackupDayOfWeek] = "0",
                [SettingKeys.AutoBackupDayOfMonth] = "1",
                [SettingKeys.BackupDirectory] = "",
                [SettingKeys.LowStockAlertEnabled] = "True",
                [SettingKeys.LowStockThreshold] = "5"
            };

            await settingService.SaveSettingsAsync(settings);
            ShowStatus("تم إعادة تعيين الإعدادات بنجاح", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting settings");
            ShowStatus($"خطأ في إعادة التعيين: {ex.Message}", false);
        }
    }

    #endregion

    #region Backup Commands

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        IsCreatingBackup = true;
        try
        {
            var dir = BackupDirectory;
            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AutoPartsShop_Backups");
            }

            using var scope = _scopeFactory.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var backupPath = await backupService.CreateBackupAsync(dir);
            ShowStatus($"تم إنشاء النسخة الاحتياطية: {backupPath}", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating backup");
            ShowStatus($"خطأ في إنشاء النسخة الاحتياطية: {ex.Message}", false);
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
            ShowStatus("تمت استعادة النسخة الاحتياطية بنجاح. يرجى إعادة تشغيل التطبيق.", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring backup");
            ShowStatus($"خطأ في الاستعادة: {ex.Message}", false);
        }
        finally
        {
            IsRestoring = false;
        }
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
            var selectedPath = dialog.SelectedPath;

            // Warn the user if the path contains non-ASCII characters
            // which may cause issues with SQL Server backup operations
            if (selectedPath.Any(c => c > 127))
            {
                var result = System.Windows.MessageBox.Show(
                    "المسار المختار يحتوي على أحرف غير إنجليزية. قد يسبب ذلك مشاكل في عملية النسخ الاحتياطي.\n\n" +
                    "هل تريد اختيار مسار آخر يحتوي على أحرف إنجليزية فقط؟",
                    "تحذير: مسار غير مدعوم",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                    return; // Let the user browse again
            }

            BackupDirectory = selectedPath;
        }
    }

    /// <summary>
    /// يفتح نافذة اختيار صورة اللوقو ويحفظها في مجلد التطبيق
    /// </summary>
    [RelayCommand]
    private void BrowseShopLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختر صورة اللوقو",
            Filter = "ملفات الصور|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif|PNG|*.png|JPEG|*.jpg;*.jpeg|BMP|*.bmp|ICO|*.ico|الكل|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var sourceFile = dialog.FileName;

            // إنشاء مجلد اللوقو في مجلد بيانات التطبيق
            var appDataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AutoPartsShop");

            if (!System.IO.Directory.Exists(appDataDir))
                System.IO.Directory.CreateDirectory(appDataDir);

            // نسخ الصورة باسم ثابت (استبدال أي لوقو سابق)
            var destFile = System.IO.Path.Combine(appDataDir, "shop_logo" + System.IO.Path.GetExtension(sourceFile));

            // تحرير مرجع الصورة القديمة أولاً لتجنب قفل الملف
            ShopLogoPath = string.Empty;
            HasShopLogo = false;

            // انتظار قصير لتحرير الملف من الذاكرة
            System.Threading.Thread.Sleep(100);

            // حذف ملف اللوقو القديم إن وُجد
            var oldLogoFiles = System.IO.Directory.GetFiles(appDataDir, "shop_logo*");
            foreach (var oldFile in oldLogoFiles)
            {
                try { System.IO.File.Delete(oldFile); } catch { }
            }

            System.IO.File.Copy(sourceFile, destFile, overwrite: true);

            ShopLogoPath = destFile;
            HasShopLogo = true;

            ShowStatus("تم تحميل صورة اللوقو بنجاح. اضغط حفظ الإعدادات لتطبيق التغيير.", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading shop logo");
            ShowStatus($"خطأ في تحميل اللوقو: {ex.Message}", false);
        }
    }

    /// <summary>
    /// يحذف صورة اللوقو المحفوظة
    /// </summary>
    [RelayCommand]
    private void RemoveShopLogo()
    {
        var result = System.Windows.MessageBox.Show(
            "هل تريد إزالة صورة اللوقو؟",
            "تأكيد الإزالة",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            // تحرير مرجع الصورة أولاً لتجنب قفل الملف
            var oldPath = ShopLogoPath;
            ShopLogoPath = string.Empty;
            HasShopLogo = false;

            // انتظار قصير لتحرير الملف من الذاكرة
            System.Threading.Thread.Sleep(100);

            if (!string.IsNullOrWhiteSpace(oldPath) && System.IO.File.Exists(oldPath))
            {
                try { System.IO.File.Delete(oldPath); } catch { /* تجاهل إذا فشل الحذف */ }
            }

            ShowStatus("تم إزالة اللوقو. اضغط حفظ الإعدادات لتطبيق التغيير.", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing shop logo");
            ShowStatus($"خطأ في إزالة اللوقو: {ex.Message}", false);
        }
    }

    #endregion

    #region Database Reset

    /// <summary>
    /// Shows a preview dialog of what will be deleted/kept, then asks for confirmation
    /// (including typing "RESET" to confirm), then resets the database.
    /// </summary>
    [RelayCommand]
    private async Task ResetDatabaseAsync()
    {
        try
        {
            // ===== Step 1: Load the preview so the user can see exactly what will be deleted =====
            using var previewScope = _scopeFactory.CreateScope();
            var resetService = previewScope.ServiceProvider.GetRequiredService<IDatabaseResetService>();
            var preview = await resetService.GetResetPreviewAsync();

            // ===== Step 2: Show a detailed confirmation dialog with the preview =====
            var previewText = new System.Text.StringBuilder();
            previewText.AppendLine("⚠️ تحذير: سيتم مسح جميع البيانات التالية نهائياً:");
            previewText.AppendLine();
            previewText.AppendLine("🗑️ سيتم مسح:");
            foreach (var t in preview.TablesToClear)
            {
                previewText.AppendLine($"   • {t.DisplayName}: {t.RowCount:N0} سجل");
            }
            previewText.AppendLine();
            previewText.AppendLine("✅ سيتم الاحتفاظ بـ:");
            foreach (var t in preview.TablesToKeep)
            {
                previewText.AppendLine($"   • {t.DisplayName}: {t.RowCount:N0} سجل");
            }
            previewText.AppendLine();
            previewText.AppendLine($"📊 إجمالي السجلات التي سيتم حذفها: {preview.TotalRowsToBeDeleted:N0}");
            previewText.AppendLine();
            previewText.AppendLine("هل تريد المتابعة؟");

            var firstConfirm = System.Windows.MessageBox.Show(
                previewText.ToString(),
                "تأكيد إعادة ضبط قاعدة البيانات",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (firstConfirm != System.Windows.MessageBoxResult.Yes)
                return;

            // ===== Step 3: Ask whether to create a backup first (recommended) =====
            var backupConfirm = System.Windows.MessageBox.Show(
                "هل تريد إنشاء نسخة احتياطية قبل المسح؟\n\n" +
                "✅ مُوصى به بشدة — يمكنك استعادة البيانات لو ندمت.\n" +
                "❌ لا — احذف مباشرة بدون نسخة احتياطية.",
                "نسخة احتياطية قبل المسح؟",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question,
                System.Windows.MessageBoxResult.Yes);

            if (backupConfirm == System.Windows.MessageBoxResult.Cancel)
                return;

            bool createBackup = backupConfirm == System.Windows.MessageBoxResult.Yes;
            string? backupDir = createBackup ? BackupDirectory : null;

            // If backup requested but no directory set, use default
            if (createBackup && string.IsNullOrWhiteSpace(backupDir))
            {
                backupDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "AutoPartsShop_Backups");
            }

            // ===== Step 4: Final confirmation — user must type "RESET" =====
            // This is the strongest safeguard against accidental resets.
            // We build the dialog in code to avoid adding a new XAML file.
            //
            // NOTE: This project uses BOTH WPF and Windows Forms (UseWPF + UseWindowsForms),
            // so we MUST use fully-qualified type names here to avoid ambiguity between
            // System.Windows.Controls.* (WPF) and System.Windows.Forms.* (WinForms).
            var inputDialog = new System.Windows.Window
            {
                Title = "تأكيد نهائي - اكتب RESET",
                Width = 400,
                Height = 220,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                FlowDirection = System.Windows.FlowDirection.RightToLeft
            };

            var inputPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new System.Windows.Thickness(20)
            };
            inputPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "للتأكيد النهائي، اكتب كلمة RESET في الحقل أدناه:",
                FontWeight = System.Windows.FontWeights.SemiBold,
                Margin = new System.Windows.Thickness(0, 0, 0, 10),
                TextWrapping = System.Windows.TextWrapping.Wrap
            });

            var inputBox = new System.Windows.Controls.TextBox
            {
                FontSize = 16,
                Padding = new System.Windows.Thickness(8, 6, 8, 6),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                FlowDirection = System.Windows.FlowDirection.LeftToRight
            };
            inputPanel.Children.Add(inputBox);

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 16, 0, 0)
            };

            var confirmBtn = new System.Windows.Controls.Button
            {
                Content = "تأكيد المسح",
                Padding = new System.Windows.Thickness(20, 6, 20, 6),
                Margin = new System.Windows.Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            var cancelBtn = new System.Windows.Controls.Button
            {
                Content = "إلغاء",
                Padding = new System.Windows.Thickness(20, 6, 20, 6),
                IsCancel = true
            };
            btnPanel.Children.Add(confirmBtn);
            btnPanel.Children.Add(cancelBtn);
            inputPanel.Children.Add(btnPanel);

            inputDialog.Content = inputPanel;

            // Wire up the confirm button
            string typedText = "";
            confirmBtn.Click += (s, e) =>
            {
                typedText = inputBox.Text?.Trim() ?? "";
                inputDialog.DialogResult = true;
                inputDialog.Close();
            };

            // Also accept Enter key in the textbox
            inputBox.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    typedText = inputBox.Text?.Trim() ?? "";
                    inputDialog.DialogResult = true;
                    inputDialog.Close();
                }
            };

            var dialogResult = inputDialog.ShowDialog();

            if (dialogResult != true)
                return;

            if (!string.Equals(typedText, "RESET", StringComparison.OrdinalIgnoreCase))
            {
                System.Windows.MessageBox.Show(
                    "النص الذي أدخلته غير صحيح. تم إلغاء العملية.",
                    "إلغاء",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            // ===== Step 5: Execute the reset =====
            IsSaving = true;
            ShowStatus("جاري إعادة ضبط قاعدة البيانات... قد يستغرق ذلك بعض الوقت.", false);

            using var execScope = _scopeFactory.CreateScope();
            var execResetService = execScope.ServiceProvider.GetRequiredService<IDatabaseResetService>();
            var resetResult = await execResetService.ResetDatabaseAsync(createBackup, backupDir);

            if (resetResult.IsSuccess)
            {
                var successMsg = $"تم مسح قاعدة البيانات بنجاح!\n\n" +
                                 $"📊 الإحصائيات:\n" +
                                 $"   • السجلات المحذوفة: {resetResult.TotalRowsDeleted:N0}\n" +
                                 $"   • الجداول الممسوحة: {resetResult.ClearedTables.Count}\n" +
                                 $"   • الوقت المستغرق: {resetResult.Duration.TotalSeconds:F1} ثانية";

                if (!string.IsNullOrEmpty(resetResult.BackupPath))
                {
                    successMsg += $"\n\n💾 النسخة الاحتياطية: {resetResult.BackupPath}";
                }

                successMsg += "\n\n⚠️ يُنصح بإعادة تشغيل البرنامج للتأكد من تحديث كل البيانات.";

                System.Windows.MessageBox.Show(
                    successMsg,
                    "تمت إعادة الضبط بنجاح",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                ShowStatus($"تم مسح {resetResult.TotalRowsDeleted:N0} سجل بنجاح", true);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"فشل مسح قاعدة البيانات:\n\n{resetResult.ErrorMessage}",
                    "خطأ في إعادة الضبط",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);

                ShowStatus($"خطأ: {resetResult.ErrorMessage}", false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database reset");
            System.Windows.MessageBox.Show(
                $"حدث خطأ غير متوقع:\n\n{ex.Message}",
                "خطأ",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            ShowStatus($"خطأ: {ex.Message}", false);
        }
        finally
        {
            IsSaving = false;
        }
    }

    #endregion

    #region Helper Methods

    private void ShowStatus(string message, bool isSuccess)
    {
        StatusMessage = message;
        HasStatusMessage = true;
        IsSuccessMessage = isSuccess;

        if (isSuccess)
        {
            _ = Task.Delay(5000).ContinueWith(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (StatusMessage == message)
                    {
                        StatusMessage = string.Empty;
                        HasStatusMessage = false;
                    }
                });
            });
        }
    }

    #endregion


}
