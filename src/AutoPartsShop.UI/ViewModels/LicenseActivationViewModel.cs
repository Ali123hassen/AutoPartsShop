using System.Windows;
using AutoPartsShop.Application.DTOs;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Application.Services;
using AutoPartsShop.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AutoPartsShop.UI.ViewModels;

public partial class LicenseActivationViewModel : ObservableObject
{
    private readonly ILicenseService _licenseService;

    [ObservableProperty]
    private string _licenseKey = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isActivating;

    [ObservableProperty]
    private string _hardwareFingerprint = string.Empty;

    [ObservableProperty]
    private LicenseInfo? _currentLicense;

    [ObservableProperty]
    private bool _hasLicense;

    [ObservableProperty]
    private string _licenseStatusText = string.Empty;

    [ObservableProperty]
    private string _expirationText = string.Empty;

    [ObservableProperty]
    private string _daysRemainingText = string.Empty;

    [ObservableProperty]
    private string _customerNameText = string.Empty;

    [ObservableProperty]
    private string _licenseTypeText = string.Empty;

    [ObservableProperty]
    private bool _isTrial;

    [ObservableProperty]
    private bool _canContinue;

    public event Action? RequestClose;
    public event Action? LicenseActivated;

    public LicenseActivationViewModel(ILicenseService licenseService)
    {
        _licenseService = licenseService;
        _ = LoadLicenseInfoAsync();
    }

    private async Task LoadLicenseInfoAsync()
    {
        try
        {
            var fingerprint = await _licenseService.GetHardwareFingerprintAsync();
            HardwareFingerprint = fingerprint;

            // تحقق من حالة الترخيص (بما فيها كشف التلاعب بالساعة)
            var validationResult = await _licenseService.ValidateLicenseAsync();
            var license = await _licenseService.GetCurrentLicenseAsync();

            // إذا تم كشف تلاعب بالساعة، اعرض رسالة واضحة
            if (validationResult.Status == LicenseStatus.ClockTampered)
            {
                HasLicense = false;
                IsTrial = true;
                LicenseStatusText = "⚠ تم كشف تلاعب بتاريخ النظام!";
                StatusMessage = "تم رجوع تاريخ النظام. يرجى استعادة التاريخ الصحيح لإعادة تفعيل البرنامج.";
                IsSuccess = false;
                HasStatusMessage = true;
                CanContinue = false;
                return;
            }

            if (license != null)
            {
                CurrentLicense = license;
                HasLicense = true;
                IsTrial = license.IsTrial;
                CustomerNameText = license.CustomerName;
                LicenseTypeText = GetLicenseTypeArabic(license.LicenseType);
                ExpirationText = license.ExpirationDate.ToString("yyyy/MM/dd");
                DaysRemainingText = license.DaysRemaining > 0
                    ? $"{license.DaysRemaining} يوم متبقي"
                    : "منتهي الصلاحية";

                if (license.IsTrial)
                {
                    LicenseStatusText = $"فترة تجربة ({license.DaysRemaining} يوم متبقي)";
                    CanContinue = license.DaysRemaining > 0;
                }
                else
                {
                    LicenseStatusText = license.DaysRemaining > 0 ? "مرخص وفعال" : "منتهي الصلاحية";
                    CanContinue = license.DaysRemaining > 0;
                }
            }
            else
            {
                HasLicense = false;
                IsTrial = true;
                LicenseStatusText = "غير مفعّل";
                CanContinue = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل معلومات الترخيص: {ex.Message}";
            IsSuccess = false;
            HasStatusMessage = true;
        }
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        if (string.IsNullOrWhiteSpace(LicenseKey))
        {
            StatusMessage = "يرجى إدخال مفتاح الترخيص";
            IsSuccess = false;
            HasStatusMessage = true;
            return;
        }

        IsActivating = true;
        HasStatusMessage = false;

        try
        {
            var result = await _licenseService.ActivateLicenseAsync(LicenseKey.Trim());

            if (result.IsValid)
            {
                StatusMessage = "تم تفعيل الترخيص بنجاح!";
                IsSuccess = true;
                HasStatusMessage = true;
                LicenseKey = string.Empty;

                await LoadLicenseInfoAsync();
                LicenseActivated?.Invoke();
            }
            else
            {
                StatusMessage = result.MessageAr;
                IsSuccess = false;
                HasStatusMessage = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في التفعيل: {ex.Message}";
            IsSuccess = false;
            HasStatusMessage = true;
        }
        finally
        {
            IsActivating = false;
        }
    }

    [RelayCommand]
    private async Task ImportLicenseFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "استيراد ملف الترخيص",
            Filter = "License Files (*.lic)|*.lic|All Files (*.*)|*.*",
            DefaultExt = ".lic"
        };

        if (dialog.ShowDialog() == true)
        {
            IsActivating = true;
            HasStatusMessage = false;

            try
            {
                // Use the extended method from LicenseService
                if (_licenseService is LicenseService fullService)
                {
                    var result = await fullService.ActivateLicenseFromFileAsync(dialog.FileName);

                    if (result.IsValid)
                    {
                        StatusMessage = "تم استيراد ملف الترخيص بنجاح!";
                        IsSuccess = true;
                        HasStatusMessage = true;

                        await LoadLicenseInfoAsync();
                        LicenseActivated?.Invoke();
                    }
                    else
                    {
                        StatusMessage = result.MessageAr;
                        IsSuccess = false;
                        HasStatusMessage = true;
                    }
                }
                else
                {
                    StatusMessage = "خدمة الترخيص غير مدعومة";
                    IsSuccess = false;
                    HasStatusMessage = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في استيراد الملف: {ex.Message}";
                IsSuccess = false;
                HasStatusMessage = true;
            }
            finally
            {
                IsActivating = false;
            }
        }
    }

    [RelayCommand]
    private async Task StartTrialAsync()
    {
        IsActivating = true;
        try
        {
            var result = await _licenseService.StartTrialAsync();
            if (result.IsValid)
            {
                await LoadLicenseInfoAsync();
                LicenseActivated?.Invoke();
            }
            else
            {
                StatusMessage = result.MessageAr;
                IsSuccess = false;
                HasStatusMessage = true;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            IsSuccess = false;
            HasStatusMessage = true;
        }
        finally
        {
            IsActivating = false;
        }
    }

    [RelayCommand]
    private void CopyFingerprint()
    {
        if (!string.IsNullOrEmpty(HardwareFingerprint))
        {
            System.Windows.Clipboard.SetText(HardwareFingerprint);
            StatusMessage = "تم نسخ بصمة الجهاز!";
            IsSuccess = true;
            HasStatusMessage = true;
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync()
    {
        var confirm = System.Windows.MessageBox.Show(
            "هل أنت متأكد من إلغاء الترخيص؟ سيحتاج النظام إلى مفتاح جديد للعمل.",
            "تأكيد إلغاء الترخيص",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            await _licenseService.DeactivateLicenseAsync();
            await LoadLicenseInfoAsync();
        }
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }

    private static string GetLicenseTypeArabic(LicenseType type)
    {
        return type switch
        {
            LicenseType.Trial => "تجريبي",
            LicenseType.Standard => "قياسي",
            LicenseType.Professional => "احترافي",
            LicenseType.Enterprise => "مؤسسي",
            _ => "غير معروف"
        };
    }
}
