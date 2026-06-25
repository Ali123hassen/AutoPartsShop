using AutoPartsShop.Application.DTOs;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Enums;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoPartsShop.Application.Services;

/// <summary>
/// Manages application licensing with hardware binding, encryption, and periodic verification.
/// Supports two activation methods:
/// 1. License Key (copy-paste) - long key containing encrypted license data
/// 2. License File (.lic) - import a file with encrypted license data
/// 
/// Clock Rollback Detection:
/// - Saves LastKnownDate in license file AND registry on every validation
/// - On startup, compares current date with LastKnownDate
/// - If current date is earlier → clock was rolled back → block access
/// - Also uses file system timestamps as a third source of truth
/// </summary>
public class LicenseService : ILicenseService
{
    private readonly IHardwareFingerprintService _hardwareService;

    // AES encryption key (32 bytes for AES-256)
    // IMPORTANT: Change these keys in production! Generate new ones with:
    //   Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
    private static readonly byte[] EncryptionKey = Convert.FromBase64String("YXV0b1BhcnRzU2hvcF9MaWNLMzJfMjAyNl9TZWNyZXQ=");

    // HMAC key for integrity verification (32 bytes)
    private static readonly byte[] HmacKey = Convert.FromBase64String("YXV0b1BhcnRzU2hvcF9ITUFDXzMyXzIwMjZfVmVyaWZ5");

    // License file paths
    private static readonly string LicenseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AutoPartsShop");

    private static readonly string LicenseFilePath = Path.Combine(LicenseDirectory, "license.dat");
    private static readonly string TrialFilePath = Path.Combine(LicenseDirectory, "trial.dat");
    private static readonly string TimestampFilePath = Path.Combine(LicenseDirectory, ".ts"); // hidden timestamp file

    // Configuration
    private const int TrialDays = 14;
    private const int GracePeriodDays = 3;
    private const int VerificationIntervalDays = 7;

    // Allow small time differences (2 days) for timezone/DST adjustments, system migration, and DST transitions
    private const int ClockRollbackToleranceDays = 2;

    // NTP configuration
    private static readonly string[] NtpServers = {
        "time.windows.com",
        "time.nist.gov",
        "pool.ntp.org"
    };
    private const int NtpTimeoutMs = 1500; // 1.5 second timeout per server
    private const int MaxClockDriftHours = 2; // Max allowed drift between local and NTP time

    // Key prefix
    private const string KeyPrefix = "AP1-";

    public LicenseService(IHardwareFingerprintService hardwareService)
    {
        _hardwareService = hardwareService;
    }

    /// <inheritdoc />
    public async Task<LicenseValidationResult> ValidateLicenseAsync()
    {
        try
        {
            var currentFingerprint = await _hardwareService.GetFingerprintAsync();
            var today = DateTime.Now.Date;

            // ===== تنظيف بيانات الكشف عند تغير العتاد (حاسوب جديد) =====
            await CleanupClockRollbackDataIfHardwareChangedAsync(currentFingerprint);

            // ===== كشف التلاعب بالساعة =====
            var clockCheck = await CheckClockRollbackAsync(today);
            if (clockCheck != null)
                return clockCheck;

            // Check for valid license file first
            if (File.Exists(LicenseFilePath))
            {
                var license = await ReadLicenseFileAsync(LicenseFilePath);
                if (license != null)
                {
                    var result = ValidateExistingLicense(license, currentFingerprint, today);

                    // Update LastKnownDate and save updated license on successful validation
                    if (result.IsValid)
                    {
                        await UpdateLastKnownDateAsync(today);
                        // حفظ الترخيص المحدث (بما فيه DaysRemaining الجديد) على القرص
                        await SaveLicenseFileAsync(license, LicenseFilePath);
                    }

                    return result;
                }
            }

            // Check for existing trial
            if (File.Exists(TrialFilePath))
            {
                var trial = await ReadTrialFileAsync();
                if (trial != null)
                {
                    // Verify hardware fingerprint for trial
                    if (!string.Equals(trial.HardwareFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
                    {
                        return LicenseValidationResult.NoLicense();
                    }

                    // Check clock rollback for trial too
                    var trialClockCheck = CheckClockRollbackForLicense(trial, today);
                    if (trialClockCheck != null)
                        return trialClockCheck;

                    var daysRemaining = (int)(trial.ExpirationDate.Date - today).TotalDays;
                    if (daysRemaining <= 0)
                    {
                        return LicenseValidationResult.TrialExpired();
                    }

                    trial.Status = LicenseStatus.TrialActive;
                    trial.DaysRemaining = daysRemaining;

                    // Update LastKnownDate and save updated trial
                    await UpdateLastKnownDateAsync(today);
                    await SaveTrialFileAsync(trial);

                    return LicenseValidationResult.Valid(trial);
                }
            }

            // No license and no trial - show activation window
            return LicenseValidationResult.NoLicense();
        }
        catch (Exception ex)
        {
            return LicenseValidationResult.Invalid($"Validation error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<LicenseValidationResult> ActivateLicenseAsync(string licenseKey)
    {
        try
        {
            var currentFingerprint = await _hardwareService.GetFingerprintAsync();
            var today = DateTime.Now.Date;
            var payload = ParseLicenseKey(licenseKey);

            if (payload == null)
            {
                return LicenseValidationResult.Invalid("صيغة مفتاح الترخيص غير صالحة");
            }

            // Verify hardware fingerprint matches
            if (!string.Equals(payload.HardwareFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return LicenseValidationResult.HardwareMismatch();
            }

            // Check expiration
            if (payload.ExpirationDate.Date < today)
            {
                return LicenseValidationResult.Expired((int)(today - payload.ExpirationDate.Date).TotalDays);
            }

            // License is valid - save it
            var licenseInfo = new LicenseInfo
            {
                Status = LicenseStatus.Active,
                LicenseType = payload.LicenseType,
                CustomerName = payload.CustomerName,
                LicenseKey = licenseKey,
                HardwareFingerprint = currentFingerprint,
                ActivationDate = DateTime.Now,
                ExpirationDate = payload.ExpirationDate,
                LastVerificationDate = DateTime.Now,
                IsTrial = false,
                TrialStartDate = null,
                DaysRemaining = (int)(payload.ExpirationDate.Date - today).TotalDays,
                MaxUsers = payload.MaxUsers,
                LastKnownDate = today
            };

            EnsureLicenseDirectory();
            await SaveLicenseFileAsync(licenseInfo, LicenseFilePath);
            await UpdateLastKnownDateAsync(today);

            return LicenseValidationResult.Valid(licenseInfo);
        }
        catch (Exception ex)
        {
            return LicenseValidationResult.Invalid($"Activation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Activates a license from a .lic file.
    /// </summary>
    public async Task<LicenseValidationResult> ActivateLicenseFromFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return LicenseValidationResult.Invalid("ملف الترخيص غير موجود");
            }

            var currentFingerprint = await _hardwareService.GetFingerprintAsync();
            var today = DateTime.Now.Date;

            // Read and decrypt the license file
            var license = await ReadLicenseFileAsync(filePath);
            if (license == null)
            {
                return LicenseValidationResult.Invalid("ملف الترخيص تالف أو غير صالح");
            }

            // Verify hardware fingerprint
            if (!string.Equals(license.HardwareFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return LicenseValidationResult.HardwareMismatch();
            }

            // Check expiration
            if (license.ExpirationDate.Date < today)
            {
                return LicenseValidationResult.Expired(
                    (int)(today - license.ExpirationDate.Date).TotalDays);
            }

            // Save as active license
            license.Status = LicenseStatus.Active;
            license.LastVerificationDate = DateTime.Now;
            license.LastKnownDate = today;
            license.DaysRemaining = (int)(license.ExpirationDate.Date - today).TotalDays;

            EnsureLicenseDirectory();
            await SaveLicenseFileAsync(license, LicenseFilePath);
            await UpdateLastKnownDateAsync(today);

            return LicenseValidationResult.Valid(license);
        }
        catch (Exception ex)
        {
            return LicenseValidationResult.Invalid($"File activation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a license file (.lic) for a client.
    /// </summary>
    public async Task<string> GenerateLicenseFileAsync(string hardwareFingerprint, DateTime expirationDate,
        string customerName, LicenseType licenseType, int maxUsers, string outputPath)
    {
        var licenseInfo = new LicenseInfo
        {
            Status = LicenseStatus.Active,
            LicenseType = licenseType,
            CustomerName = customerName,
            LicenseKey = GenerateLicenseKey(hardwareFingerprint, expirationDate, customerName, licenseType, maxUsers),
            HardwareFingerprint = hardwareFingerprint,
            ActivationDate = DateTime.Now,
            ExpirationDate = expirationDate,
            LastVerificationDate = DateTime.Now,
            IsTrial = false,
            TrialStartDate = null,
            DaysRemaining = (int)(expirationDate.Date - DateTime.Now.Date).TotalDays,
            MaxUsers = maxUsers,
            LastKnownDate = DateTime.Now.Date
        };

        await SaveLicenseFileAsync(licenseInfo, outputPath);
        return outputPath;
    }

    /// <inheritdoc />
    public async Task<LicenseInfo?> GetCurrentLicenseAsync()
    {
        if (!File.Exists(LicenseFilePath)) return null;
        var license = await ReadLicenseFileAsync(LicenseFilePath);

        // إعادة حساب الأيام المتبقية بناءً على التاريخ الحالي
        // لأن القيمة المحفوظة في الملف قد تكون قديمة
        if (license != null)
        {
            var today = DateTime.Now.Date;
            license.DaysRemaining = (int)(license.ExpirationDate.Date - today).TotalDays;
        }

        return license;
    }

    /// <inheritdoc />
    public async Task<string> GetHardwareFingerprintAsync()
    {
        return await _hardwareService.GetFingerprintAsync();
    }

    /// <inheritdoc />
    public async Task DeactivateLicenseAsync()
    {
        await Task.Run(() =>
        {
            if (File.Exists(LicenseFilePath))
            {
                File.Delete(LicenseFilePath);
            }
            if (File.Exists(TrialFilePath))
            {
                File.Delete(TrialFilePath);
            }
            if (File.Exists(TimestampFilePath))
            {
                File.Delete(TimestampFilePath);
            }

            // Clean encrypted timestamp file
            try
            {
                var encTsPath = Path.Combine(LicenseDirectory, ".ets");
                if (File.Exists(encTsPath))
                    File.Delete(encTsPath);
            }
            catch { }

            // Clean tick count record
            try
            {
                var tickFilePath = Path.Combine(LicenseDirectory, ".tk");
                if (File.Exists(tickFilePath))
                    File.Delete(tickFilePath);
            }
            catch { }

            // Clean registry
            try
            {
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(
                    @"SOFTWARE\AutoPartsShop\License", false);
            }
            catch { }
        });
    }

    /// <inheritdoc />
    public async Task<bool> CheckPeriodicVerificationAsync()
    {
        var license = await GetCurrentLicenseAsync();
        if (license == null) return false;

        if (license.IsTrial) return false; // No periodic check for trial

        var today = DateTime.Now.Date;

        // ===== كشف التلاعب بالساعة =====
        var clockCheck = await CheckClockRollbackAsync(today);
        if (clockCheck != null)
            return true; // Force re-verification

        var daysSinceLastCheck = (today - license.LastVerificationDate.Date).TotalDays;

        if (daysSinceLastCheck >= VerificationIntervalDays)
        {
            // Re-validate the license
            var result = await ValidateLicenseAsync();

            // Update last verification date
            license.LastVerificationDate = DateTime.Now;
            license.LastKnownDate = today;
            await SaveLicenseFileAsync(license, LicenseFilePath);
            await UpdateLastKnownDateAsync(today);

            return true;
        }

        // Check for system clock rollback (also checked in ValidateLicenseAsync)
        if (today < license.LastVerificationDate.Date.AddDays(-ClockRollbackToleranceDays))
        {
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<LicenseValidationResult> StartTrialAsync()
    {
        var currentFingerprint = await _hardwareService.GetFingerprintAsync();
        var today = DateTime.Now.Date;

        // Check if trial already exists
        if (File.Exists(TrialFilePath))
        {
            return await HandleTrialAsync(currentFingerprint);
        }

        // Start new trial
        var trialLicense = new LicenseInfo
        {
            Status = LicenseStatus.TrialActive,
            LicenseType = LicenseType.Trial,
            CustomerName = "مستخدم تجريبي",
            LicenseKey = string.Empty,
            HardwareFingerprint = currentFingerprint,
            ActivationDate = DateTime.Now,
            ExpirationDate = DateTime.Now.AddDays(TrialDays),
            LastVerificationDate = DateTime.Now,
            IsTrial = true,
            TrialStartDate = DateTime.Now,
            DaysRemaining = TrialDays,
            MaxUsers = 1,
            LastKnownDate = today
        };

        EnsureLicenseDirectory();
        await SaveTrialFileAsync(trialLicense);
        await UpdateLastKnownDateAsync(today);

        return LicenseValidationResult.Valid(trialLicense);
    }

    /// <inheritdoc />
    public string GenerateLicenseKey(string hardwareFingerprint, DateTime expirationDate,
        string customerName, LicenseType licenseType, int maxUsers = 1)
    {
        // Create license data payload
        var payload = new LicensePayload
        {
            HardwareFingerprint = hardwareFingerprint,
            ExpirationDate = expirationDate,
            CustomerName = customerName,
            LicenseType = licenseType,
            MaxUsers = maxUsers,
            GeneratedDate = DateTime.Now
        };

        // Serialize and encrypt the payload
        var json = JsonSerializer.Serialize(payload);
        var encryptedData = EncryptAes(json);

        // Compute HMAC for integrity
        var hmac = ComputeHmac(encryptedData);

        // Combine: [4 bytes data length][encrypted data][32 bytes HMAC]
        var combined = CombineEncryptedDataWithHmac(encryptedData, hmac);

        // Encode as Base64Url for compact representation
        var base64 = Base64UrlEncode(combined);

        // Format with prefix and dashes for readability
        return FormatLicenseKey(base64);
    }

    #region Clock Rollback Detection

    /// <summary>
    /// يتحقق من التلاعب بساعة النظام باستخدام مصادر متعددة:
    /// 1. NTP - مصدر وقت خارجي من الإنترنت (الأقوى)
    /// 2. Environment.TickCount64 - عداد رتيب لا يرجع للخلف أبداً
    /// 3. ملف Timestamp مشفر ومخفي
    /// 4. الريجستري
    /// 5. تاريخ تعديل الملفات (File System)
    /// 
    /// هذا الإصدار يعمل بشكل غير متزامن (async) لمنع تجميد واجهة المستخدم.
    /// </summary>

    /// <summary>
    /// ينظف بيانات كشف التلاعب بالساعة عند اكتشاف تغير بصمة العتاد (حاسوب جديد).
    /// عند نقل النظام لحاسوب آخر، الملفات المحفوظة تحمل تواريخ من الحاسوب القديم
    /// مما يسبب إيجابية كاذبة في كشف التلاعب. هذه الدالة تمسح البيانات القديمة
    /// وتسمح للنظام بالبدء بشكل نظيف على الحاسوب الجديد.
    /// </summary>
    private async Task CleanupClockRollbackDataIfHardwareChangedAsync(string currentFingerprint)
    {
        try
        {
            var storedFingerprint = ReadStoredHardwareFingerprint();

            // إذا لم تكن هناك بصمة محفوظة (أول مرة) ← نحفظ البصمة الحالية
            if (string.IsNullOrEmpty(storedFingerprint))
            {
                SaveStoredHardwareFingerprint(currentFingerprint);
                return;
            }

            // إذا البصمة هي نفسها ← لا شيء يتغير
            if (string.Equals(storedFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
                return;

            // البصمة تغيرت! ← حاسوب جديد ← ننظف بيانات الكشف القديمة
            System.Diagnostics.Debug.WriteLine("[LicenseService] Hardware fingerprint changed - cleaning up clock rollback data for new machine.");

            // حذف ملف Timestamp البسيط
            try { if (File.Exists(TimestampFilePath)) File.Delete(TimestampFilePath); } catch { }

            // حذف ملف Timestamp المشفر
            try { var encTsPath = Path.Combine(LicenseDirectory, ".ets"); if (File.Exists(encTsPath)) File.Delete(encTsPath); } catch { }

            // حذف ملف العداد الرتيب
            try { var tickPath = Path.Combine(LicenseDirectory, ".tick"); if (File.Exists(tickPath)) File.Delete(tickPath); } catch { }

            // حذف بيانات الريجستري
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\AutoPartsShop\License", true);
                key?.DeleteValue("LastKnownDate", false);
                key?.DeleteValue("NtpLastKnownDate", false);
            }
            catch { }

            // تحديث بصمة العتاد إلى الجديدة
            SaveStoredHardwareFingerprint(currentFingerprint);

            // تحديث التاريخ المعروف بالتاريخ الحالي
            await UpdateLastKnownDateAsync(DateTime.Now.Date);
        }
        catch { }
    }

    /// <summary>
    /// يقرأ بصمة العتاد المحفوظة من ملف
    /// </summary>
    private string? ReadStoredHardwareFingerprint()
    {
        try
        {
            var fpPath = Path.Combine(LicenseDirectory, ".hfp");
            if (File.Exists(fpPath))
                return File.ReadAllText(fpPath).Trim();
        }
        catch { }
        return null;
    }

    /// <summary>
    /// يحفظ بصمة العتاد في ملف مخفي
    /// </summary>
    private void SaveStoredHardwareFingerprint(string fingerprint)
    {
        try
        {
            EnsureLicenseDirectory();
            var fpPath = Path.Combine(LicenseDirectory, ".hfp");
            File.WriteAllText(fpPath, fingerprint);
            if (File.Exists(fpPath))
                File.SetAttributes(fpPath, FileAttributes.Hidden | FileAttributes.System);
        }
        catch { }
    }

    private async Task<LicenseValidationResult?> CheckClockRollbackAsync(DateTime today)
    {
        // ===== المصدر 0: التحقق من NTP (مصدر خارجي لا يمكن التلاعب به) =====
        var ntpResult = await CheckNtpTimeAsync(today);
        if (ntpResult != null)
            return ntpResult;

        // ===== المصدر 1: Environment.TickCount64 (عداد رتيب لا يرجع للخلف) =====
        var tickCheck = CheckMonotonicTickCount();
        if (tickCheck != null)
            return tickCheck;

        // ===== المصدر 2: ملف Timestamp المشفر والمخفي (يحتوي أيضاً على NTP verified date) =====
        var lastKnownFromTimestampFile = ReadEncryptedTimestampFile();

        // ===== المصدر 2b: التحقق من تاريخ NTP المحفوظ في الملف المشفر =====
        // FIX #1: تجاهل التواريخ المستقبلية - كانت الساعة خاطئة في الماضي وتم إصلاحها (ليس تلاعب)
        var ntpDateFromFile = ReadNtpVerifiedDateFromEncryptedFile();
        if (ntpDateFromFile.HasValue && ntpDateFromFile.Value.Date <= today.AddDays(ClockRollbackToleranceDays))
        {
            if (today < ntpDateFromFile.Value.Date.AddDays(-ClockRollbackToleranceDays))
            {
                LogDiagnostic("ClockRollbackDetected: NTP date from encrypted file is ahead of today");
                return LicenseValidationResult.ClockRollbackDetected();
            }
        }

        // ===== المصدر 3: ملف Timestamp بسيط (للتوافق مع الإصدارات السابقة) =====
        var lastKnownFromLegacyFile = ReadLastKnownDateFromTimestampFile();

        // ===== المصدر 4: الريجستري =====
        var lastKnownFromRegistry = ReadLastKnownDateFromRegistry();

        // ===== المصدر 4b: تاريخ NTP المحفوظ في الريجستري =====
        // FIX #1: تجاهل التواريخ المستقبلية
        var ntpDateFromRegistry = ReadNtpDateFromRegistry();
        if (ntpDateFromRegistry.HasValue && ntpDateFromRegistry.Value.Date <= today.AddDays(ClockRollbackToleranceDays))
        {
            if (today < ntpDateFromRegistry.Value.Date.AddDays(-ClockRollbackToleranceDays))
            {
                LogDiagnostic("ClockRollbackDetected: NTP date from registry is ahead of today");
                return LicenseValidationResult.ClockRollbackDetected();
            }
        }

        // ===== المصدر 5: تاريخ تعديل ملف الترخيص (File System) =====
        // FIX #1 & #3: تجاهل تواريخ الملفات المستقبلية (كانت ساعة النظام متقدمة في الماضي)
        DateTime? lastKnownFromFileSystem = null;
        if (File.Exists(LicenseFilePath))
        {
            try
            {
                var fileDate = File.GetLastWriteTime(LicenseFilePath).Date;
                if (fileDate <= today.AddDays(ClockRollbackToleranceDays))
                    lastKnownFromFileSystem = fileDate;
            }
            catch { }
        }
        if (File.Exists(TrialFilePath))
        {
            try
            {
                var trialWriteTime = File.GetLastWriteTime(TrialFilePath).Date;
                if (trialWriteTime <= today.AddDays(ClockRollbackToleranceDays))
                {
                    if (lastKnownFromFileSystem == null || trialWriteTime > lastKnownFromFileSystem)
                        lastKnownFromFileSystem = trialWriteTime;
                }
            }
            catch { }
        }

        // اختيار أحدث تاريخ من جميع المصادر
        var allDates = new[] { lastKnownFromTimestampFile, lastKnownFromLegacyFile, lastKnownFromRegistry, lastKnownFromFileSystem }
            .Where(d => d.HasValue && d.Value != default)
            .Select(d => d!.Value)
            .ToList();

        // إذا لم يوجد تاريخ سابق (أول مرة)، لا مشكلة
        if (allDates.Count == 0)
            return null;

        var lastKnownDate = allDates.Max();

        // FIX #1: إذا كان آخر تاريخ معروف في المستقبل البعيد (ساعة خاطئة سابقاً)، تجاهله ولا تعتبره تلاعب
        // الساعة كانت متقدمة في الماضي وتم إصلاحها - هذا ليس تلاعب، بل إصلاح
        if (lastKnownDate > today.AddDays(ClockRollbackToleranceDays))
        {
            LogDiagnostic($"Ignored future-dated lastKnownDate={lastKnownDate:yyyy-MM-dd}, today={today:yyyy-MM-dd} (past clock error corrected)");
            return null;
        }

        // إذا التاريخ الحالي أقدم من آخر تاريخ معروف (مع سماح للتوقيت الصيفي)
        if (today < lastKnownDate.AddDays(-ClockRollbackToleranceDays))
        {
            LogDiagnostic($"ClockRollbackDetected: today={today:yyyy-MM-dd} < lastKnownDate={lastKnownDate:yyyy-MM-dd} - tolerance");
            return LicenseValidationResult.ClockRollbackDetected();
        }

        return null;
    }

    /// <summary>
    /// يتحقق من وقت NTP ويقارنه بالوقت المحلي (بشكل غير متزامن).
    /// إذا كان الفرق كبير جداً، فهذا يعني أن الساعة تم التلاعب بها.
    /// </summary>
    private async Task<LicenseValidationResult?> CheckNtpTimeAsync(DateTime localToday)
    {
        var ntpTime = await GetNtpTimeAsync();
        if (ntpTime == null)
            return null; // No internet - can't verify, fall through to other checks

        var ntpToday = ntpTime.Value.Date;

        // If NTP date is significantly ahead of local date (more than tolerance), clock was rolled back
        if (localToday < ntpToday.AddDays(-ClockRollbackToleranceDays))
        {
            return LicenseValidationResult.ClockRollbackDetected();
        }

        return null;
    }

    /// <summary>
    /// يحصل على الوقت الحالي من خوادم NTP (بشكل غير متزامن).
    /// يعمل في خلفية لمنع تجميد واجهة المستخدم.
    /// </summary>
    private async Task<DateTime?> GetNtpTimeAsync()
    {
        return await Task.Run(() =>
        {
            foreach (var server in NtpServers)
            {
                try
                {
                    var time = QueryNtpServer(server);
                    if (time.HasValue)
                        return time.Value;
                }
                catch
                {
                    // Try next server
                }
            }
            return (DateTime?)null;
        });
    }

    /// <summary>
    /// يستعلم عن خادم NTP واحد ويحصل على الوقت الحالي.
    /// </summary>
    private DateTime? QueryNtpServer(string server)
    {
        try
        {
            var ntpData = new byte[48];
            ntpData[0] = 0x1B; // LI = 0, VN = 3, Mode = 3 (client)

            var addresses = Dns.GetHostAddresses(server);
            if (addresses.Length == 0) return null;

            var endPoint = new IPEndPoint(addresses[0], 123);

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.ReceiveTimeout = NtpTimeoutMs;
            socket.SendTimeout = NtpTimeoutMs;

            socket.Connect(endPoint);
            socket.Send(ntpData);

            // Wait for response with timeout
            var receiveResult = socket.BeginReceive(ntpData, 0, ntpData.Length, SocketFlags.None, null, null);
            var waitHandle = receiveResult.AsyncWaitHandle;

            if (!waitHandle.WaitOne(NtpTimeoutMs, true))
            {
                socket.Close();
                return null;
            }

            socket.EndReceive(receiveResult);

            // Transmitted timestamp starts at byte 40
            // First 4 bytes: seconds since 1900-01-01
            // Next 4 bytes: fraction of second
            ulong intPart = ((ulong)ntpData[40] << 24) | ((ulong)ntpData[41] << 16) | ((ulong)ntpData[42] << 8) | ntpData[43];
            ulong fractPart = ((ulong)ntpData[44] << 24) | ((ulong)ntpData[45] << 16) | ((ulong)ntpData[46] << 8) | ntpData[47];

            // Convert to milliseconds
            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);

            // NTP epoch is January 1, 1900
            var ntpEpoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var networkTime = ntpEpoch.AddMilliseconds((long)milliseconds);

            // Convert to local time
            return networkTime.ToLocalTime();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// يتحقق من العداد الرتيب (Environment.TickCount64).
    /// هذا العداد يبدأ من 0 عند تشغيل النظام ويزداد دائماً.
    /// إذا كان العداد المحفوظ أكبر من العداد الحالي، فهذا يعني أن الجهاز تم إعادة تشغيله
    /// بتاريخ أقدم (تم رجوع الساعة قبل الإقلاع).
    /// </summary>
    private LicenseValidationResult? CheckMonotonicTickCount()
    {
        try
        {
            var tickRecord = ReadTickRecord();
            if (tickRecord == null)
                return null; // No previous record

            var currentTick = Environment.TickCount64;

            // TickCount64 resets on reboot. If we have a record from the same boot session,
            // current tick should be >= recorded tick. If it's less, something is wrong.
            // But if the machine rebooted, currentTick will be small which is fine.
            // The real protection is: if recorded tick is very large and current is very small,
            // it means machine rebooted. If the date also went backwards, it's tampering.

            // Check: If recorded boot session shows a later date, and current boot shows an earlier date,
            // that's tampering. We detect this by:
            // 1. Same boot session (TickCount64 keeps increasing) - tick should never go backwards
            // 2. Different boot session - check if the recorded date is in the future relative to now

            // For same boot session: TickCount64 should always increase
            // Since TickCount64 resets on reboot, if currentTick < recordedTick AND
            // the recorded date is recent (within reasonable uptime), this suggests date rollback
            // without a reboot

            // FIX #2: معالجة إعادة التشغيل بشكل صحيح
            // TickCount64 يصفّر عند كل إعادة تشغيل - إذا currentTick صغير من القيمة المحفوظة
            // فهذا عاديًا تعني reboot عادي وليس تلاعب
            // الحالة الوحيدة التي تعني تلاعب حقيقي هي:
            //   - نفس جلسة التشغيل (currentTick كبير من عتبة زمنية معقولة)
            //   - والتاريخ رجع للخلف
            // العتبة الزمنية: 30 ثانية بعد الإقلاع (تجنب الملث الخاطئ بعد reboot)
            const long rebootThresholdMs = 30_000; // 30 ثانية
            if (tickRecord.RecordedDate.Date > DateTime.Now.Date.AddDays(-ClockRollbackToleranceDays))
            {
                // تسجيل حديث من المهم مراعاته
                // إذا currentTick صغير من tickRecord.TickCount64 ولكن currentTick أكبر من rebootThresholdMs
                // فهذا تعني أننا في جلسة تشغيل جديدة (لم يحدث إلا reboot) - هذا عادي
                if (currentTick < tickRecord.TickCount64 && currentTick > rebootThresholdMs)
                {
                    // نفس جلسة التشغيل، والعداد رجع للخلف - مستحيل دون تلاعب
                    LogDiagnostic($"ClockRollbackDetected: TickCount64 went backwards in same boot session. current={currentTick}, recorded={tickRecord.TickCount64}");
                    return LicenseValidationResult.ClockRollbackDetected();
                }
                // إذا currentTick صغير من rebootThresholdMs فالجهاز تم إعادة تشغيله - عادي، تجاهل
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// سجل العداد الرتيب - يحفظ قيمة TickCount64 والتاريخ معاً
    /// </summary>
    private class TickCountRecord
    {
        public long TickCount64 { get; set; }
        public DateTime RecordedDate { get; set; }
        public string? HardwareFingerprint { get; set; }
    }

    /// <summary>
    /// يقرأ سجل العداد الرتيب من ملف مشفر
    /// </summary>
    private TickCountRecord? ReadTickRecord()
    {
        try
        {
            var tickFilePath = Path.Combine(LicenseDirectory, ".tk");
            if (!File.Exists(tickFilePath))
                return null;

            var encrypted = File.ReadAllBytes(tickFilePath);
            var json = DecryptAes(encrypted);
            return JsonSerializer.Deserialize<TickCountRecord>(json);
        }
        catch { return null; }
    }

    /// <summary>
    /// يحفظ سجل العداد الرتيب في ملف مشفر ومخفي
    /// </summary>
    private void SaveTickRecord()
    {
        try
        {
            EnsureLicenseDirectory();
            // استخدام Task.Run لتجنب deadlock في WPF SynchronizationContext
            var currentFingerprint = Task.Run(() => _hardwareService.GetFingerprintAsync()).GetAwaiter().GetResult();
            var record = new TickCountRecord
            {
                TickCount64 = Environment.TickCount64,
                RecordedDate = DateTime.Now,
                HardwareFingerprint = currentFingerprint
            };

            var json = JsonSerializer.Serialize(record);
            var encrypted = EncryptAes(json);

            var tickFilePath = Path.Combine(LicenseDirectory, ".tk");
            File.WriteAllBytes(tickFilePath, encrypted);

            // إخفاء الملف
            if (File.Exists(tickFilePath))
            {
                File.SetAttributes(tickFilePath, FileAttributes.Hidden | FileAttributes.System);
            }
        }
        catch { }
    }

    /// <summary>
    /// يقرأ آخر تاريخ معروف من ملف Timestamp المشفر (النسخة المحسنة)
    /// الملف الآن مشفر ويحتوي أيضاً على البصمة لمنع النسخ من جهاز آخر
    /// </summary>
    private DateTime? ReadEncryptedTimestampFile()
    {
        try
        {
            var encTsPath = Path.Combine(LicenseDirectory, ".ets");
            if (!File.Exists(encTsPath))
                return null;

            var encrypted = File.ReadAllBytes(encTsPath);
            var json = DecryptAes(encrypted);
            var record = JsonSerializer.Deserialize<EncryptedTimestampRecord>(json);

            if (record == null) return null;

            // التحقق من البصمة - إذا لم تتطابق، تم نسخ الملف من جهاز آخر
            // استخدام Task.Run لتجنب deadlock في WPF SynchronizationContext
            var currentFingerprint = Task.Run(() => _hardwareService.GetFingerprintAsync()).GetAwaiter().GetResult();
            if (!string.Equals(record.HardwareFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
                return null; // ملف من جهاز آخر - تجاهله

            return record.LastKnownDate.Date;
        }
        catch { return null; }
    }

    /// <summary>
    /// يحفظ آخر تاريخ معروف في ملف مشفر مع البصمة وNTP
    /// (بشكل غير متزامن لمنع تجميد واجهة المستخدم)
    /// </summary>
    private async Task SaveEncryptedTimestampFileAsync(DateTime today)
    {
        try
        {
            EnsureLicenseDirectory();

            var currentFingerprint = await _hardwareService.GetFingerprintAsync();
            var ntpTime = await GetNtpTimeAsync();
            var record = new EncryptedTimestampRecord
            {
                LastKnownDate = today,
                HardwareFingerprint = currentFingerprint,
                RecordedAt = DateTime.Now,
                NtpVerifiedDate = ntpTime
            };

            var json = JsonSerializer.Serialize(record);
            var encrypted = EncryptAes(json);

            var encTsPath = Path.Combine(LicenseDirectory, ".ets");
            await File.WriteAllBytesAsync(encTsPath, encrypted);

            // إخفاء الملف
            if (File.Exists(encTsPath))
            {
                File.SetAttributes(encTsPath, FileAttributes.Hidden | FileAttributes.System);
            }
        }
        catch { }
    }

    /// <summary>
    /// سجل التاريخ المشفر - يحتوي على البصمة لمنع النسخ من جهاز آخر
    /// ويحتوي على وقت NTP كمرجع خارجي
    /// </summary>
    private class EncryptedTimestampRecord
    {
        public DateTime LastKnownDate { get; set; }
        public string HardwareFingerprint { get; set; } = string.Empty;
        public DateTime RecordedAt { get; set; }
        public DateTime? NtpVerifiedDate { get; set; }
    }

    /// <summary>
    /// يقرأ تاريخ NTP المحفوظ في الملف المشفر.
    /// هذا التاريخ تم التحقق منه من الإنترنت سابقاً ولا يمكن التلاعب به محلياً.
    /// </summary>
    private DateTime? ReadNtpVerifiedDateFromEncryptedFile()
    {
        try
        {
            var encTsPath = Path.Combine(LicenseDirectory, ".ets");
            if (!File.Exists(encTsPath))
                return null;

            var encrypted = File.ReadAllBytes(encTsPath);
            var json = DecryptAes(encrypted);
            var record = JsonSerializer.Deserialize<EncryptedTimestampRecord>(json);

            if (record == null) return null;

            // التحقق من البصمة - استخدام Task.Run لتجنب deadlock
            var currentFingerprint = Task.Run(() => _hardwareService.GetFingerprintAsync()).GetAwaiter().GetResult();
            if (!string.Equals(record.HardwareFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
                return null;

            // إرجاع تاريخ NTP المحفوظ (إن وُجد)
            return record.NtpVerifiedDate?.Date;
        }
        catch { return null; }
    }

    /// <summary>
    /// يقرأ تاريخ NTP المحفوظ في الريجستري.
    /// </summary>
    private DateTime? ReadNtpDateFromRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\AutoPartsShop\License", false);
            var value = key?.GetValue("NtpLastKnownDate")?.ToString();
            if (value != null && DateTime.TryParse(value, out var date))
                return date.Date;
        }
        catch { }
        return null;
    }

    /// <summary>
    /// يتحقق من التلاعب بالساعة بالنسبة لترخيص معين (يستخدم LastKnownDate المحفوظ في الترخيص)
    /// </summary>
    private LicenseValidationResult? CheckClockRollbackForLicense(LicenseInfo license, DateTime today)
    {
        // FIX #4: تجاهل التواريخ المستقبلية في جميع الفحوصات
        // إذا كان التاريخ المحفوظ في المستقبل، فالساعة كانت خاطئة في الماضي وتم إصلاحها - ليس تلاعب

        // التحقق من LastKnownDate المحفوظ في ملف الترخيص
        if (license.LastKnownDate.HasValue)
        {
            var lkd = license.LastKnownDate.Value.Date;
            // إذا التاريخ المحفوظ في المستقبل - تجاهل (ساعة خاطئة سابقاً)
            if (lkd <= today.AddDays(ClockRollbackToleranceDays) && today < lkd.AddDays(-ClockRollbackToleranceDays))
            {
                LogDiagnostic($"ClockRollbackDetected (license.LastKnownDate): today={today:yyyy-MM-dd}, lkd={lkd:yyyy-MM-dd}");
                return LicenseValidationResult.ClockRollbackDetected();
            }
        }

        // التحقق من LastVerificationDate
        {
            var lvd = license.LastVerificationDate.Date;
            // إذا تاريخ التحقق في المستقبل - تجاهل
            if (lvd <= today.AddDays(ClockRollbackToleranceDays) && today < lvd.AddDays(-ClockRollbackToleranceDays))
            {
                LogDiagnostic($"ClockRollbackDetected (license.LastVerificationDate): today={today:yyyy-MM-dd}, lvd={lvd:yyyy-MM-dd}");
                return LicenseValidationResult.ClockRollbackDetected();
            }
        }

        // التحقق من تاريخ بدء التجربة
        if (license.TrialStartDate.HasValue)
        {
            var tsd = license.TrialStartDate.Value.Date;
            // إذا تاريخ بدء التجربة في المستقبل - تجاهل
            if (tsd <= today.AddDays(ClockRollbackToleranceDays) && today < tsd.AddDays(-ClockRollbackToleranceDays))
            {
                LogDiagnostic($"ClockRollbackDetected (license.TrialStartDate): today={today:yyyy-MM-dd}, tsd={tsd:yyyy-MM-dd}");
                return LicenseValidationResult.ClockRollbackDetected();
            }
        }

        return null;
    }

    /// <summary>
    /// يحفظ آخر تاريخ معروف في جميع المصادر:
    /// 1. ملف Timestamp بسيط (للتوافق)
    /// 2. ملف Timestamp مشفر مع البصمة وNTP
    /// 3. ملف العداد الرتيب
    /// 4. الريجستري
    /// 
    /// يعمل بشكل غير متزامن لمنع تجميد واجهة المستخدم.
    /// </summary>
    private async Task UpdateLastKnownDateAsync(DateTime today)
    {
        // حفظ في ملف Timestamp بسيط (للتوافق مع الإصدارات السابقة)
        try
        {
            EnsureLicenseDirectory();
            await File.WriteAllTextAsync(TimestampFilePath, today.ToString("O"));
            // إخفاء الملف
            if (File.Exists(TimestampFilePath))
            {
                File.SetAttributes(TimestampFilePath, FileAttributes.Hidden | FileAttributes.System);
            }
        }
        catch { }

        // حفظ في ملف Timestamp مشفر مع البصمة وNTP (في خلفية)
        try
        {
            await SaveEncryptedTimestampFileAsync(today);
        }
        catch { }

        // حفظ سجل العداد الرتيب
        try
        {
            SaveTickRecord();
        }
        catch { }

        // حفظ في الريجستري + NTP verified date (في خلفية)
        try
        {
            await Task.Run(async () =>
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                    @"SOFTWARE\AutoPartsShop\License");
                key?.SetValue("LastKnownDate", today.ToString("O"));
                // حفظ NTP verified date في الريجستري أيضاً
                var ntpTime = await GetNtpTimeAsync();
                if (ntpTime.HasValue)
                {
                    key?.SetValue("NtpLastKnownDate", ntpTime.Value.ToString("O"));
                }
            });
        }
        catch { }
    }

    /// <summary>
    /// يقرأ آخر تاريخ معروف من ملف Timestamp
    /// </summary>
    private DateTime? ReadLastKnownDateFromTimestampFile()
    {
        try
        {
            if (File.Exists(TimestampFilePath))
            {
                var content = File.ReadAllText(TimestampFilePath);
                if (DateTime.TryParse(content, out var date))
                    return date.Date;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// يقرأ آخر تاريخ معروف من الريجستري
    /// </summary>
    private DateTime? ReadLastKnownDateFromRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\AutoPartsShop\License", false);
            var value = key?.GetValue("LastKnownDate")?.ToString();
            if (value != null && DateTime.TryParse(value, out var date))
                return date.Date;
        }
        catch { }
        return null;
    }

    #endregion

    #region Private Validation Methods

    private LicenseValidationResult ValidateExistingLicense(LicenseInfo license, string currentFingerprint, DateTime today)
    {
        // Check hardware fingerprint
        if (!string.Equals(license.HardwareFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return LicenseValidationResult.HardwareMismatch();
        }

        // Check clock rollback
        var clockCheck = CheckClockRollbackForLicense(license, today);
        if (clockCheck != null)
            return clockCheck;

        // Check expiration
        var daysRemaining = (int)(license.ExpirationDate.Date - today).TotalDays;

        if (license.IsTrial)
        {
            if (daysRemaining <= 0)
            {
                return LicenseValidationResult.TrialExpired();
            }

            license.Status = LicenseStatus.TrialActive;
            license.DaysRemaining = daysRemaining;
            license.LastKnownDate = today;
            return LicenseValidationResult.Valid(license);
        }

        // Full license checks
        if (daysRemaining <= 0)
        {
            // Check grace period
            var graceDaysRemaining = daysRemaining + GracePeriodDays;
            if (graceDaysRemaining > 0)
            {
                return LicenseValidationResult.GracePeriod(graceDaysRemaining);
            }
            return LicenseValidationResult.Expired(-daysRemaining);
        }

        // License is valid
        license.Status = LicenseStatus.Active;
        license.DaysRemaining = daysRemaining;
        license.LastKnownDate = today;
        return LicenseValidationResult.Valid(license);
    }

    private async Task<LicenseValidationResult> HandleTrialAsync(string currentFingerprint)
    {
        if (File.Exists(TrialFilePath))
        {
            var trial = await ReadTrialFileAsync();
            if (trial != null)
            {
                // Verify hardware fingerprint for trial too
                if (!string.Equals(trial.HardwareFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(TrialFilePath);
                    return await StartTrialAsync();
                }

                var today = DateTime.Now.Date;

                // Check clock rollback for trial
                var clockCheck = CheckClockRollbackForLicense(trial, today);
                if (clockCheck != null)
                    return clockCheck;

                var daysRemaining = (int)(trial.ExpirationDate.Date - today).TotalDays;
                if (daysRemaining <= 0)
                {
                    trial.Status = LicenseStatus.TrialExpired;
                    trial.DaysRemaining = 0;
                    return LicenseValidationResult.TrialExpired();
                }

                trial.Status = LicenseStatus.TrialActive;
                trial.DaysRemaining = daysRemaining;
                return LicenseValidationResult.Valid(trial);
            }
        }

        return await StartTrialAsync();
    }

    #endregion

    #region License Key Parsing

    private LicensePayload? ParseLicenseKey(string licenseKey)
    {
        try
        {
            // Remove prefix and dashes
            var cleanKey = licenseKey.Replace(KeyPrefix, "").Replace("-", "");

            // Decode from Base64Url
            var combined = Base64UrlDecode(cleanKey);

            // Split encrypted data and HMAC
            var (encryptedData, hmac) = SplitEncryptedDataAndHmac(combined);

            // Verify HMAC
            var computedHmac = ComputeHmac(encryptedData);
            if (!CryptographicOperations.FixedTimeEquals(hmac, computedHmac))
            {
                return null; // Integrity check failed - tampered key
            }

            // Decrypt
            var json = DecryptAes(encryptedData);

            // Deserialize
            return JsonSerializer.Deserialize<LicensePayload>(json);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region File Storage (AES Encrypted)

    private async Task SaveLicenseFileAsync(LicenseInfo license, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(license);
        var encrypted = EncryptAes(json);
        await File.WriteAllBytesAsync(filePath, encrypted);
    }

    private async Task<LicenseInfo?> ReadLicenseFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        try
        {
            var encrypted = await File.ReadAllBytesAsync(filePath);
            var json = DecryptAes(encrypted);
            return JsonSerializer.Deserialize<LicenseInfo>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveTrialFileAsync(LicenseInfo trial)
    {
        EnsureLicenseDirectory();
        var json = JsonSerializer.Serialize(trial);
        var encrypted = EncryptAes(json);
        await File.WriteAllBytesAsync(TrialFilePath, encrypted);

        // Also store trial start in registry as backup (anti-tampering)
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                @"SOFTWARE\AutoPartsShop\License");
            key?.SetValue("TrialStart", trial.TrialStartDate?.ToString("O") ?? DateTime.Now.ToString("O"));
            key?.SetValue("TrialFingerprint", trial.HardwareFingerprint);
        }
        catch
        {
            // Registry write might fail without admin rights - that's OK
        }
    }

    private async Task<LicenseInfo?> ReadTrialFileAsync()
    {
        if (!File.Exists(TrialFilePath)) return null;

        try
        {
            var encrypted = await File.ReadAllBytesAsync(TrialFilePath);
            var json = DecryptAes(encrypted);
            var trial = JsonSerializer.Deserialize<LicenseInfo>(json);

            // Cross-check with registry (anti-tampering)
            if (trial != null)
            {
                try
                {
                    using var regKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\AutoPartsShop\License", false);
                    if (regKey != null)
                    {
                        var regTrialStart = regKey.GetValue("TrialStart")?.ToString();
                        if (regTrialStart != null && DateTime.TryParse(regTrialStart, out var regDate))
                        {
                            // If registry shows an earlier date, use that (anti-tampering)
                            if (trial.TrialStartDate.HasValue && regDate < trial.TrialStartDate.Value)
                            {
                                trial.TrialStartDate = regDate;
                                trial.ExpirationDate = regDate.AddDays(TrialDays);
                            }
                        }
                    }
                }
                catch { }
            }

            return trial;
        }
        catch
        {
            return null;
        }
    }

    private void EnsureLicenseDirectory()
    {
        if (!Directory.Exists(LicenseDirectory))
            Directory.CreateDirectory(LicenseDirectory);
    }

    #endregion

    #region AES Encryption / Decryption

    private byte[] EncryptAes(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

        return result;
    }

    private string DecryptAes(byte[] cipherData)
    {
        using var aes = Aes.Create();
        aes.Key = EncryptionKey;

        // Extract IV from beginning
        var iv = new byte[aes.BlockSize / 8];
        var encrypted = new byte[cipherData.Length - iv.Length];
        Buffer.BlockCopy(cipherData, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(cipherData, iv.Length, encrypted, 0, encrypted.Length);

        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);

        return Encoding.UTF8.GetString(decrypted);
    }

    #endregion

    #region HMAC Integrity

    private byte[] ComputeHmac(byte[] data)
    {
        using var hmac = new HMACSHA256(HmacKey);
        return hmac.ComputeHash(data);
    }

    private byte[] CombineEncryptedDataWithHmac(byte[] encryptedData, byte[] hmac)
    {
        // Format: [4 bytes data length][encrypted data][32 bytes HMAC]
        var result = new byte[4 + encryptedData.Length + 32];
        Buffer.BlockCopy(BitConverter.GetBytes(encryptedData.Length), 0, result, 0, 4);
        Buffer.BlockCopy(encryptedData, 0, result, 4, encryptedData.Length);
        Buffer.BlockCopy(hmac, 0, result, 4 + encryptedData.Length, 32);
        return result;
    }

    private (byte[] encryptedData, byte[] hmac) SplitEncryptedDataAndHmac(byte[] combined)
    {
        if (combined.Length < 36)
            throw new FormatException("Invalid license data");

        var dataLength = BitConverter.ToInt32(combined, 0);
        if (dataLength < 0 || dataLength > combined.Length - 36)
            throw new FormatException("Invalid license data length");

        var encryptedData = new byte[dataLength];
        var hmac = new byte[32];
        Buffer.BlockCopy(combined, 4, encryptedData, 0, dataLength);
        Buffer.BlockCopy(combined, 4 + dataLength, hmac, 0, 32);
        return (encryptedData, hmac);
    }

    #endregion

    #region Base64Url Encoding

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string base64Url)
    {
        var base64 = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        // Pad with '=' to make length a multiple of 4
        var padding = (4 - base64.Length % 4) % 4;
        base64 += new string('=', padding);

        return Convert.FromBase64String(base64);
    }

    private static string FormatLicenseKey(string base64Url)
    {
        // Format: AP1-XXXX-XXXX-XXXX-... (groups of 4 chars for readability)
        var sb = new StringBuilder();
        sb.Append(KeyPrefix);

        for (var i = 0; i < base64Url.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
                sb.Append('-');
            sb.Append(base64Url[i]);
        }

        return sb.ToString();
    }

    #endregion

    #region License Payload

    private class LicensePayload
    {
        public string HardwareFingerprint { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public LicenseType LicenseType { get; set; } = LicenseType.Standard;
        public int MaxUsers { get; set; } = 1;
        public DateTime GeneratedDate { get; set; }
    }

    #endregion

    /// <summary>
    /// FIX #5: تسجيل تشخيصي لمعرفة مصدر إنذار التلاعب الكاذب
    /// يكتب في: %ProgramData%\AutoPartsShop\license_diag.log
    /// </summary>
    private void LogDiagnostic(string message)
    {
        try
        {
            EnsureLicenseDirectory();
            var logPath = Path.Combine(LicenseDirectory, "license_diag.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, line);
        }
        catch
        {
            // تجاهل أخطاء التسجيل
        }
    }

}
