using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace AutoPartsShop.UI.ViewModels;

/// <summary>
/// Observable cart item model for the POS UI.
/// Unlike InvoiceItemDto, this class notifies the UI when Quantity, DiscountAmount, or LineTotal changes.
/// Discount is specified as a total discount amount for the entire line (not per unit, not percentage).
/// </summary>
public partial class CartItemModel : ObservableObject
{
    // Reference to parent POSViewModel for triggering totals recalculation
    private POSViewModel? _parentViewModel;

    // Guard flag to prevent circular updates between DiscountAmount and DiscountAmountText
    private bool _isUpdatingFromCode;

    [ObservableProperty]
    private int _sparePartId;

    [ObservableProperty]
    private string _partName = string.Empty;

    [ObservableProperty]
    private string _partNumber = string.Empty;

    [ObservableProperty]
    private string? _location;

    /// <summary>
    /// السيارة المتوافقة (قد تحتوي على عدة سيارات مفصولة بفاصلة منقوطة)
    /// </summary>
    [ObservableProperty]
    private string? _compatibleCar;

    /// <summary>
    /// موديل السيارة (قد يحتوي على عدة موديلات مفصولة بفاصلة منقوطة)
    /// </summary>
    [ObservableProperty]
    private string? _carModel;

    /// <summary>
    /// سنة الصنع
    /// </summary>
    [ObservableProperty]
    private string? _carYear;

    /// <summary>
    /// Indicates whether the Location field has a value (for UI visibility binding).
    /// </summary>
    public bool HasLocation => !string.IsNullOrWhiteSpace(_location);

    /// <summary>
    /// هل توجد سيارة متوافقة؟ (للتحكم في ظهور الملصق في الواجهة)
    /// </summary>
    public bool HasCompatibleCar => !string.IsNullOrWhiteSpace(_compatibleCar);

    /// <summary>
    /// هل يوجد موديل سيارة؟ (للتحكم في ظهور الملصق في الواجهة)
    /// </summary>
    public bool HasCarModel => !string.IsNullOrWhiteSpace(_carModel);

    /// <summary>
    /// هل توجد سنة صنع؟ (للتحكم في ظهور الملصق في الواجهة)
    /// </summary>
    public bool HasCarYear => !string.IsNullOrWhiteSpace(_carYear);

    /// <summary>
    /// نص مدمج للعرض: "السيارة: Toyota | الموديل: Camry | 2020"
    /// يُستخدم لعرض جميع بيانات التوافق في سطر واحد
    /// </summary>
    public string? CarCompatibilityDisplay
    {
        get
        {
            var parts = new List<string>();
            if (HasCompatibleCar)
                parts.Add($"السيارة: {_compatibleCar}");
            if (HasCarModel)
                parts.Add($"الموديل: {_carModel}");
            if (!string.IsNullOrWhiteSpace(_carYear))
                parts.Add(_carYear!);
            return parts.Count == 0 ? null : string.Join(" | ", parts);
        }
    }

    /// <summary>
    /// هل توجد أي معلومات توافق سيارة؟ (لإظهار/إخفاء السطر بالكامل)
    /// </summary>
    public bool HasCarInfo => HasCompatibleCar || HasCarModel || !string.IsNullOrWhiteSpace(_carYear);

    partial void OnLocationChanged(string? value)
    {
        OnPropertyChanged(nameof(HasLocation));
    }

    partial void OnCompatibleCarChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCompatibleCar));
        OnPropertyChanged(nameof(CarCompatibilityDisplay));
        OnPropertyChanged(nameof(HasCarInfo));
    }

    partial void OnCarModelChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCarModel));
        OnPropertyChanged(nameof(CarCompatibilityDisplay));
        OnPropertyChanged(nameof(HasCarInfo));
    }

    partial void OnCarYearChanged(string? value)
    {
        OnPropertyChanged(nameof(CarCompatibilityDisplay));
        OnPropertyChanged(nameof(HasCarInfo));
    }

    [ObservableProperty]
    private int _quantity;

    [ObservableProperty]
    private decimal _unitPrice;

    /// <summary>
    /// Total discount amount for the entire line (in currency, not percentage, not per unit).
    /// For example, if Quantity = 3, UnitPrice = 100, and DiscountAmount = 50, the customer pays 250 total.
    /// </summary>
    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private string _discountAmountText = "0";

    [ObservableProperty]
    private decimal _lineTotal;

    [ObservableProperty]
    private int _availableStock;

    /// <summary>
    /// Currency symbol for display (e.g., ر.ي, ر.س)
    /// </summary>
    [ObservableProperty]
    private string _currencySymbol = "ر.س";

    /// <summary>
    /// Sets the parent POSViewModel reference so this item can trigger recalculation.
    /// </summary>
    public void SetParent(POSViewModel parent)
    {
        _parentViewModel = parent;
    }

    partial void OnQuantityChanged(int value)
    {
        RecalculateLineTotal();
        _parentViewModel?.RecalculateTotals();
    }

    partial void OnUnitPriceChanged(decimal value)
    {
        RecalculateLineTotal();
        _parentViewModel?.RecalculateTotals();
    }

    partial void OnDiscountAmountChanged(decimal value)
    {
        if (_isUpdatingFromCode) return;
        _isUpdatingFromCode = true;
        try
        {
            DiscountAmountText = value.ToString(CultureInfo.InvariantCulture);
            RecalculateLineTotal();
            _parentViewModel?.RecalculateTotals();
        }
        finally
        {
            _isUpdatingFromCode = false;
        }
    }

    partial void OnDiscountAmountTextChanged(string value)
    {
        if (_isUpdatingFromCode) return;
        _isUpdatingFromCode = true;
        try
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result) && result >= 0)
            {
                // Ensure discount doesn't exceed total line price
                var maxDiscount = Quantity * UnitPrice;
                if (result > maxDiscount)
                    result = maxDiscount;
                DiscountAmount = result;
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                DiscountAmount = 0;
            }

            // Must recalculate here because OnDiscountAmountChanged may be skipped
            RecalculateLineTotal();
            _parentViewModel?.RecalculateTotals();
        }
        finally
        {
            _isUpdatingFromCode = false;
        }
    }

    private void RecalculateLineTotal()
    {
        // LineTotal = (Quantity × UnitPrice) - DiscountAmount (total for the line)
        LineTotal = (Quantity * UnitPrice) - DiscountAmount;
        if (LineTotal < 0) LineTotal = 0;
    }

    /// <summary>
    /// Converts the total discount amount for the line to a percentage for the InvoiceItemDto.
    /// The percentage is relative to the total line price (Quantity × UnitPrice).
    /// If total line price is 0, returns 0% discount.
    /// </summary>
    private decimal GetDiscountPercent()
    {
        var totalLinePrice = Quantity * UnitPrice;
        if (totalLinePrice == 0) return 0;
        return (DiscountAmount / totalLinePrice) * 100m;
    }

    /// <summary>
    /// Converts this cart item to an InvoiceItemDto for the service layer.
    /// The discount amount is converted to a percentage for storage.
    /// </summary>
    public Application.DTOs.Invoices.InvoiceItemDto ToInvoiceItemDto()
    {
        return new Application.DTOs.Invoices.InvoiceItemDto
        {
            SparePartId = SparePartId,
            PartName = PartName,
            Quantity = Quantity,
            UnitPrice = UnitPrice,
            DiscountPercent = GetDiscountPercent(),
            DiscountAmount = DiscountAmount,
            LineTotal = LineTotal
        };
    }
}
