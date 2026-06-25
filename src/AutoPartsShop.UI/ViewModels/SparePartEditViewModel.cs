using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;

namespace AutoPartsShop.UI.ViewModels;

public partial class SparePartEditViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _partNumber = string.Empty;

    [ObservableProperty]
    private string _barcode = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string? _nameAr;

    [ObservableProperty]
    private int _categoryId;

    [ObservableProperty]
    private string? _manufacturer;

    [ObservableProperty]
    private decimal _salePrice;

    [ObservableProperty]
    private decimal? _minSalePrice;

    [ObservableProperty]
    private int _minStockLevel = 5;

    [ObservableProperty]
    private int? _maxStockLevel;

    [ObservableProperty]
    private string? _location;

    [ObservableProperty]
    private string _unit = "قطعة";

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private string? _barcodeType;

    [ObservableProperty]
    private string? _barcodeValue;

    // ===== قوائم السيارات والموديلات المتوافقة (متعددة) =====
    // تُخزَّن في قاعدة البيانات كسلسلة مفصولة بفاصلة منقوطة (;) للتوافق مع الحقول الموجودة

    /// <summary>
    /// قائمة السيارات المتوافقة (متعددة)
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _compatibleCars = [];

    /// <summary>
    /// قائمة موديلات السيارات المتوافقة (متعددة)
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _carModels = [];

    /// <summary>
    /// نص الإدخال الحالي للسيارة الجديدة
    /// </summary>
    [ObservableProperty]
    private string _newCompatibleCar = string.Empty;

    /// <summary>
    /// نص الإدخال الحالي للموديل الجديد
    /// </summary>
    [ObservableProperty]
    private string _newCarModel = string.Empty;

    /// <summary>
    /// السيارة المحددة حالياً في القائمة (للحذف)
    /// </summary>
    [ObservableProperty]
    private string? _selectedCompatibleCar;

    /// <summary>
    /// الموديل المحدد حالياً في القائمة (للحذف)
    /// </summary>
    [ObservableProperty]
    private string? _selectedCarModel;

    [ObservableProperty]
    private string? _carYear;

    [ObservableProperty]
    private string? _countryOfOrigin;

    [ObservableProperty]
    private decimal? _weight;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private ObservableCollection<CategoryDto> _categories = [];

    [ObservableProperty]
    private CategoryDto? _selectedCategory;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isSaving;

    public SparePartEditViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task LoadCategoriesAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();
            var categories = await sparePartService.GetCategoriesAsync();
            Categories = new ObservableCollection<CategoryDto>(categories);

            // اختيار أول تصنيف كافتراضي عند الإضافة
            if (!IsEditMode && Categories.Count > 0 && SelectedCategory == null)
            {
                SelectedCategory = Categories[0];
                CategoryId = Categories[0].Id;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"خطأ في تحميل التصنيفات: {ex.Message}";
            HasError = true;
        }
    }

    public void LoadForEdit(SparePartDto sparePart)
    {
        IsEditMode = true;
        Id = sparePart.Id;
        PartNumber = sparePart.PartNumber;
        Barcode = sparePart.Barcode;
        Name = sparePart.Name;
        NameAr = sparePart.NameAr;
        CategoryId = sparePart.CategoryId;
        Manufacturer = sparePart.Manufacturer;
        SalePrice = sparePart.SalePrice;
        MinSalePrice = sparePart.MinSalePrice;
        MinStockLevel = sparePart.MinStockLevel;
        MaxStockLevel = sparePart.MaxStockLevel;
        Location = sparePart.Location;
        Unit = sparePart.Unit;
        Notes = sparePart.Notes;
        IsActive = sparePart.IsActive;
        BarcodeType = sparePart.BarcodeType;
        BarcodeValue = sparePart.BarcodeValue;
        CarYear = sparePart.CarYear;
        CountryOfOrigin = sparePart.CountryOfOrigin;
        Weight = sparePart.Weight;

        // تحويل السلاسل المفصولة بفواصل إلى قوائم
        CompatibleCars = new ObservableCollection<string>(SplitValues(sparePart.CompatibleCar));
        CarModels = new ObservableCollection<string>(SplitValues(sparePart.CarModel));

        SelectedCategory = Categories.FirstOrDefault(c => c.Id == sparePart.CategoryId);
    }

    /// <summary>
    /// يقسم سلسلة مفصولة بفواصل (, أو ;) إلى قائمة عناصر نظيفة
    /// </summary>
    private static List<string> SplitValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        return value
            .Split(new[] { ';', '،', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
    }

    /// <summary>
    /// يدمج قائمة عناصر في سلسلة مفصولة بفاصلة منقوطة (;) لتخزينها في قاعدة البيانات
    /// </summary>
    private static string? JoinValues(IEnumerable<string> values)
    {
        var list = values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().ToList();
        if (list == null || list.Count == 0)
            return null;
        return string.Join("; ", list);
    }

    /// <summary>
    /// نسخة مدمجة من السلسلة للحقول المفردة (للتوافق مع قاعدة البيانات)
    /// </summary>
    public string? CompatibleCarJoined => JoinValues(CompatibleCars);
    public string? CarModelJoined => JoinValues(CarModels);

    // ===== Commands للسيارات =====

    [RelayCommand]
    private void AddCompatibleCar()
    {
        var value = (NewCompatibleCar ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return;

        // منع التكرار
        if (!CompatibleCars.Any(c => string.Equals(c, value, StringComparison.OrdinalIgnoreCase)))
        {
            CompatibleCars.Add(value);
        }

        NewCompatibleCar = string.Empty;
    }

    [RelayCommand]
    private void RemoveCompatibleCar(string? car)
    {
        if (car != null && CompatibleCars.Contains(car))
        {
            CompatibleCars.Remove(car);
        }
    }

    // ===== Commands للموديلات =====

    [RelayCommand]
    private void AddCarModel()
    {
        var value = (NewCarModel ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!CarModels.Any(m => string.Equals(m, value, StringComparison.OrdinalIgnoreCase)))
        {
            CarModels.Add(value);
        }

        NewCarModel = string.Empty;
    }

    [RelayCommand]
    private void RemoveCarModel(string? model)
    {
        if (model != null && CarModels.Contains(model))
        {
            CarModels.Remove(model);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!Validate())
            return;

        IsSaving = true;
        HasError = false;

        try
        {
            // استخدام معرّف التصنيف المختار
            var effectiveCategoryId = SelectedCategory?.Id ?? CategoryId;

            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

            // دمج القوائم في سلاسل لل حفظ في قاعدة البيانات
            var compatibleCarStr = CompatibleCarJoined;
            var carModelStr = CarModelJoined;

            if (IsEditMode)
            {
                var updateDto = new UpdateSparePartDto
                {
                    Id = Id,
                    PartNumber = PartNumber,
                    Barcode = Barcode,
                    Name = Name,
                    NameAr = NameAr,
                    CategoryId = effectiveCategoryId,
                    Manufacturer = Manufacturer,
                    SalePrice = SalePrice,
                    MinSalePrice = MinSalePrice,
                    MinStockLevel = MinStockLevel,
                    MaxStockLevel = MaxStockLevel,
                    Location = Location,
                    Unit = Unit,
                    Notes = Notes,
                    IsActive = IsActive,
                    BarcodeType = BarcodeType,
                    BarcodeValue = BarcodeValue,
                    CompatibleCar = compatibleCarStr,
                    CarModel = carModelStr,
                    CarYear = CarYear,
                    CountryOfOrigin = CountryOfOrigin,
                    Weight = Weight
                };

                await sparePartService.UpdateAsync(updateDto);
                await auditService.LogAsync("Update", "SparePart", Id, null, $"Updated: {Name}");
            }
            else
            {
                var createDto = new CreateSparePartDto
                {
                    PartNumber = PartNumber,
                    Barcode = Barcode,
                    Name = Name,
                    NameAr = NameAr,
                    CategoryId = effectiveCategoryId,
                    Manufacturer = Manufacturer,
                    Location = Location,
                    Unit = Unit,
                    Notes = Notes,
                    BarcodeType = BarcodeType,
                    BarcodeValue = BarcodeValue,
                    CompatibleCar = compatibleCarStr,
                    CarModel = carModelStr,
                    CarYear = CarYear,
                    CountryOfOrigin = CountryOfOrigin,
                    Weight = Weight
                };

                var result = await sparePartService.CreateAsync(createDto);
                await auditService.LogAsync("Create", "SparePart", result.Id, null, $"Created: {Name}");
            }

            OnSaveCompleted();
        }
        catch (Exception ex)
        {
            // عرض رسالة الخطأ مع Inner Exception إن وُجد
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            ErrorMessage = $"خطأ في الحفظ: {innerMsg}";
            HasError = true;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        OnCancelRequested();
    }

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(PartNumber))
        {
            ErrorMessage = "رقم القطعة مطلوب";
            HasError = true;
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "اسم القطعة مطلوب";
            HasError = true;
            return false;
        }

        // التحقق من اختيار التصنيف
        if (SelectedCategory == null && CategoryId <= 0)
        {
            ErrorMessage = "يجب اختيار التصنيف";
            HasError = true;
            return false;
        }

        // Prices and stock are managed through Purchase Invoices, not here

        HasError = false;
        return true;
    }

    [RelayCommand]
    private void GoToStock()
    {
        OnNavigateToStockRequested();
    }

    public event Action? SaveCompleted;
    public event Action? CancelRequested;
    public event Action? NavigateToStockRequested;

    protected virtual void OnSaveCompleted() => SaveCompleted?.Invoke();
    protected virtual void OnCancelRequested() => CancelRequested?.Invoke();
    protected virtual void OnNavigateToStockRequested() => NavigateToStockRequested?.Invoke();
}
