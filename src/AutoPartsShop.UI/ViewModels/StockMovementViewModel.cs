using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.DTOs.Stock;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace AutoPartsShop.UI.ViewModels;

public partial class StockMovementViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    // === بيانات الحركات ===
    [ObservableProperty]
    private ObservableCollection<StockMovementDto> _movements = [];

    // === تنبيهات المخزون المنخفض ===
    [ObservableProperty]
    private ObservableCollection<SparePartDto> _lowStockItems = [];

    // === قائمة قطع الغيار ===
    [ObservableProperty]
    private ObservableCollection<SparePartDto> _spareParts = [];

    // === القطعة المختارة ===
    [ObservableProperty]
    private SparePartDto? _selectedSparePart;

    // === كمية التعديل ===
    [ObservableProperty]
    private int _addQuantity;

    // === ملاحظات ===
    [ObservableProperty]
    private string _addNotes = string.Empty;

    // === نوع الحركة ===
    [ObservableProperty]
    private MovementType _movementType = MovementType.In;

    // === حالة التحميل ===
    [ObservableProperty]
    private bool _isLoading;

    // === حالة حفظ التعديل ===
    [ObservableProperty]
    private bool _isAddingStock;

    // === رسالة الحالة ===
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    // === التاب النشط ===
    [ObservableProperty]
    private int _selectedTabIndex;

    // === بحث في الحركات ===
    [ObservableProperty]
    private string _searchText = string.Empty;

    // === إحصائيات ===
    [ObservableProperty]
    private int _totalPartsCount;

    [ObservableProperty]
    private int _lowStockCount;

    [ObservableProperty]
    private int _outOfStockCount;

    [ObservableProperty]
    private decimal _totalStockValue;

    [ObservableProperty]
    private string _currencySymbol = "ر.س";

    public string StockValueLabel => $"قيمة المخزون ({CurrencySymbol})";

    public StockMovementViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = LoadDataAsync();
        _ = LoadCurrencySymbolAsync();
    }

    private async Task LoadCurrencySymbolAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
            CurrencySymbol = await settingService.GetAsync("Currency", "ر.س");
        }
        catch
        {
            CurrencySymbol = "ر.س";
        }
    }

    partial void OnCurrencySymbolChanged(string value)
    {
        OnPropertyChanged(nameof(StockValueLabel));
    }

    // === أنواع الحركات للقائمة المنسدلة ===
    public List<MovementType> MovementTypes => new()
    {
        MovementType.In,
        MovementType.Out,
        MovementType.Adjustment,
        MovementType.Return
    };

    // === أسماء أنواع الحركات بالعربي ===
    public string GetMovementTypeName(MovementType type) => type switch
    {
        MovementType.In => "إدخال",
        MovementType.Out => "إخراج",
        MovementType.Adjustment => "تسوية",
        MovementType.Return => "مرتجع",
        _ => type.ToString()
    };

    [RelayCommand]
    private async Task AddStockAsync()
    {
        if (SelectedSparePart == null)
        {
            StatusMessage = "يرجى اختيار القطعة";
            HasStatusMessage = true;
            return;
        }

        if (AddQuantity <= 0)
        {
            StatusMessage = "يرجى إدخال كمية صحيحة أكبر من صفر";
            HasStatusMessage = true;
            return;
        }

        if (MovementType == MovementType.Adjustment && AddQuantity < 0)
        {
            StatusMessage = "كمية التسوية يجب أن تكون صفر أو أكثر";
            HasStatusMessage = true;
            return;
        }

        IsAddingStock = true;
        HasStatusMessage = false;

        try
        {
            var dto = new StockAdjustmentDto
            {
                SparePartId = SelectedSparePart.Id,
                MovementType = MovementType,
                Quantity = AddQuantity,
                Notes = AddNotes
            };

            using var scope = _scopeFactory.CreateScope();
            var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();
            await stockService.AdjustStockAsync(dto);

            StatusMessage = $"تم {GetMovementTypeName(MovementType)} بنجاح - القطعة: {SelectedSparePart.Name}، الكمية: {AddQuantity}";
            HasStatusMessage = true;

            // Reset form
            AddQuantity = 0;
            AddNotes = string.Empty;
            SelectedSparePart = null;
            MovementType = MovementType.In;

            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ: {ex.Message}";
            HasStatusMessage = true;
        }
        finally
        {
            IsAddingStock = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task SearchMovementsAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            await LoadMovementsAsync();
            return;
        }

        var keyword = SearchText.Trim().ToLower();
        var filtered = Movements.Where(m =>
            (m.SparePartName?.ToLower().Contains(keyword) == true) ||
            (m.Notes?.ToLower().Contains(keyword) == true) ||
            (m.MovementType?.ToLower().Contains(keyword) == true) ||
            (m.UserName?.ToLower().Contains(keyword) == true)
        ).ToList();

        Movements = new ObservableCollection<StockMovementDto>(filtered);
    }

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _ = LoadMovementsAsync();
        }
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();

            // تحميل تنبيهات المخزون المنخفض
            var lowStock = await stockService.GetLowStockAlertsAsync();
            LowStockItems = new ObservableCollection<SparePartDto>(lowStock);

            // تحميل كل قطع الغيار
            var allParts = await sparePartService.GetAllAsync();
            SpareParts = new ObservableCollection<SparePartDto>(allParts);

            // حساب الإحصائيات
            TotalPartsCount = allParts.Count;
            LowStockCount = allParts.Count(p => p.IsLowStock);
            OutOfStockCount = allParts.Count(p => p.CurrentStock == 0);
            TotalStockValue = allParts.Sum(p => p.CurrentStock * p.PurchasePrice);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل البيانات: {ex.Message}";
            HasStatusMessage = true;
        }
        finally
        {
            IsLoading = false;
        }

        // Load movements in a separate scope
        await LoadMovementsAsync();
    }

    private async Task LoadMovementsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();
            var movements = await stockService.GetAllMovementsAsync(200);
            Movements = new ObservableCollection<StockMovementDto>(movements);
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل الحركات: {ex.Message}";
            HasStatusMessage = true;
        }
    }
}
