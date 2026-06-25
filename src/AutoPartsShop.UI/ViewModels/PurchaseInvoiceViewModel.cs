using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.PurchaseInvoices;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Globalization;

namespace AutoPartsShop.UI.ViewModels;

/// <summary>
/// Represents a single item in the purchase cart.
/// </summary>
public partial class PurchaseCartItemModel : ObservableObject
{
    [ObservableProperty]
    private int _sparePartId;

    [ObservableProperty]
    private string _partName = string.Empty;

    [ObservableProperty]
    private string _partNumber = string.Empty;

    [ObservableProperty]
    private int _quantity = 1;

    [ObservableProperty]
    private decimal _costPrice;

    [ObservableProperty]
    private decimal _salePrice;

    [ObservableProperty]
    private string _currencySymbol = "ر.س";

    // --- حقول المخزون والأسعار السابقة ---
    [ObservableProperty]
    private int _currentStock;

    [ObservableProperty]
    private decimal _previousCostPrice;

    [ObservableProperty]
    private decimal _previousSalePrice;

    [ObservableProperty]
    private decimal? _previousMinSalePrice;

    // --- الحد الأدنى للبيع ---
    [ObservableProperty]
    private decimal? _minSalePrice;

    /// <summary>
    /// عرض الحد الأدنى للبيع
    /// </summary>
    public string MinSalePriceDisplay => MinSalePrice.HasValue ? $"{MinSalePrice.Value:N2}" : "-";

    /// <summary>
    /// عرض الحد الأدنى السابق
    /// </summary>
    public string PreviousMinSalePriceDisplay => PreviousMinSalePrice.HasValue ? $"{PreviousMinSalePrice.Value:N2}" : "-";

    partial void OnMinSalePriceChanged(decimal? value) => OnPropertyChanged(nameof(MinSalePriceDisplay));
    partial void OnPreviousMinSalePriceChanged(decimal? value) => OnPropertyChanged(nameof(PreviousMinSalePriceDisplay));

    // --- حسابات ---
    public decimal LineTotal => Quantity * CostPrice;

    public string LineTotalDisplay => $"{LineTotal:N2} {CurrencySymbol}";

    /// <summary>
    /// الربح لكل وحدة = سعر البيع - سعر التكلفة
    /// </summary>
    public decimal ProfitPerUnit => SalePrice - CostPrice;

    /// <summary>
    /// إجمالي الربح = الربح لكل وحدة × الكمية
    /// </summary>
    public decimal TotalProfit => ProfitPerUnit * Quantity;

    /// <summary>
    /// نسبة الربح % = (الربح لكل وحدة ÷ سعر التكلفة) × 100
    /// </summary>
    public decimal ProfitMarginPercent => CostPrice > 0
        ? Math.Round((ProfitPerUnit / CostPrice) * 100, 1)
        : 0;

    /// <summary>
    /// عرض نسبة الربح
    /// </summary>
    public string ProfitMarginDisplay => $"{ProfitMarginPercent}%";

    /// <summary>
    /// عرض إجمالي الربح
    /// </summary>
    public string TotalProfitDisplay => $"{TotalProfit:N2} {CurrencySymbol}";

    partial void OnQuantityChanged(int value) => NotifyCalcChanged();
    partial void OnCostPriceChanged(decimal value) => NotifyCalcChanged();
    partial void OnSalePriceChanged(decimal value) => NotifyCalcChanged();

    private void NotifyCalcChanged()
    {
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(LineTotalDisplay));
        OnPropertyChanged(nameof(ProfitPerUnit));
        OnPropertyChanged(nameof(TotalProfit));
        OnPropertyChanged(nameof(ProfitMarginPercent));
        OnPropertyChanged(nameof(ProfitMarginDisplay));
        OnPropertyChanged(nameof(TotalProfitDisplay));
    }
}

public partial class PurchaseInvoiceViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private CancellationTokenSource? _searchCts;

    #region Search Properties

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SparePartDto> _searchResults = [];

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private SparePartDto? _selectedProduct;

    #endregion

    #region Cart Properties

    [ObservableProperty]
    private ObservableCollection<PurchaseCartItemModel> _cartItems = [];

    [ObservableProperty]
    private int _cartItemCount;

    [ObservableProperty]
    private PurchaseCartItemModel? _selectedCartItem;

    #endregion

    #region Invoice Properties

    [ObservableProperty]
    private string? _supplierName;

    [ObservableProperty]
    private string? _supplierPhone;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private decimal _totalAmount;

    [ObservableProperty]
    private string _totalAmountDisplay = "0.00 ر.س";

    [ObservableProperty]
    private string _currencySymbol = "ر.س";

    #endregion

    #region UI State

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isSuccessMessage;

    #endregion

    #region Purchase Invoice List

    [ObservableProperty]
    private ObservableCollection<PurchaseInvoiceDto> _purchaseInvoices = [];

    [ObservableProperty]
    private PurchaseInvoiceDto? _selectedPurchaseInvoice;

    [ObservableProperty]
    private DateTime? _filterFromDate;

    [ObservableProperty]
    private DateTime? _filterToDate;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private bool _isLoadingList;

    #endregion

    public PurchaseInvoiceViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        await LoadPurchaseInvoicesAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            var currency = await settingService.GetAsync("Currency", "ر.س");
            CurrencySymbol = currency;
            RecalculateTotal();
        }
        catch
        {
            CurrencySymbol = "ر.س";
        }
    }

    #region Search Commands

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        if (!string.IsNullOrWhiteSpace(value) && value.Length >= 2)
        {
            var token = _searchCts.Token;
            _ = SearchPartsAsync(token);
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
        }
    }

    [RelayCommand]
    private async Task SearchPartsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Length < 2)
        {
            SearchResults.Clear();
            return;
        }

        IsSearching = true;
        try
        {
            await Task.Delay(300, cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();

            var searchDto = new SparePartSearchDto
            {
                Keyword = SearchText.Trim(),
                IsActive = true,
                PageNumber = 1,
                PageSize = 50
            };

            var result = await sparePartService.SearchAsync(searchDto);

            if (cancellationToken.IsCancellationRequested) return;

            SearchResults.Clear();
            foreach (var item in result.Items)
            {
                SearchResults.Add(item);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في البحث: {ex.Message}", false);
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task BarcodeSearchAsync()
    {
        var searchText = SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(searchText)) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();

            var sparePart = await sparePartService.GetByBarcodeAsync(searchText)
                ?? await sparePartService.GetByPartNumberAsync(searchText);

            if (sparePart != null)
            {
                AddToCart(sparePart);
            }
            else
            {
                ShowStatus($"لم يتم العثور على قطعة بالباركود أو الرقم: {searchText}", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في البحث: {ex.Message}", false);
        }
    }

    #endregion

    #region Cart Commands

    [RelayCommand]
    private void AddToCart(SparePartDto? sparePart)
    {
        if (sparePart == null) return;

        // Check if already in cart
        var existingItem = CartItems.FirstOrDefault(i => i.SparePartId == sparePart.Id);
        if (existingItem != null)
        {
            existingItem.Quantity++;
            ShowStatus($"تم زيادة كمية: {sparePart.Name}", true);
        }
        else
        {
            var cartItem = new PurchaseCartItemModel
            {
                SparePartId = sparePart.Id,
                PartName = sparePart.Name,
                PartNumber = sparePart.PartNumber,
                Quantity = 1,
                CostPrice = sparePart.PurchasePrice,
                SalePrice = sparePart.SalePrice,
                MinSalePrice = sparePart.MinSalePrice,
                CurrencySymbol = CurrencySymbol,
                CurrentStock = sparePart.CurrentStock,
                PreviousCostPrice = sparePart.PurchasePrice,
                PreviousSalePrice = sparePart.SalePrice,
                PreviousMinSalePrice = sparePart.MinSalePrice
            };
            cartItem.PropertyChanged += CartItem_PropertyChanged;
            CartItems.Add(cartItem);
            ShowStatus($"تمت إضافة: {sparePart.Name}", true);
        }

        CartItemCount = CartItems.Count;
        RecalculateTotal();

        SearchText = string.Empty;
        SearchResults.Clear();
    }

    [RelayCommand]
    private void RemoveItem(PurchaseCartItemModel? item)
    {
        if (item == null) return;
        item.PropertyChanged -= CartItem_PropertyChanged;
        CartItems.Remove(item);
        CartItemCount = CartItems.Count;
        RecalculateTotal();
    }

    [RelayCommand]
    private void ClearCart()
    {
        foreach (var item in CartItems)
            item.PropertyChanged -= CartItem_PropertyChanged;
        CartItems.Clear();
        SupplierName = null;
        SupplierPhone = null;
        Notes = null;
        CartItemCount = 0;
        RecalculateTotal();
        StatusMessage = string.Empty;
        HasStatusMessage = false;
    }

    #endregion

    #region Purchase Invoice Commands

    [RelayCommand]
    private async Task CompletePurchaseAsync()
    {
        if (CartItems.Count == 0)
        {
            ShowStatus("السلة فارغة، أضف منتجات أولاً", false);
            return;
        }

        // Validate all items have cost price > 0
        foreach (var item in CartItems)
        {
            if (item.CostPrice <= 0)
            {
                ShowStatus($"سعر التكلفة لـ '{item.PartName}' يجب أن يكون أكبر من صفر", false);
                return;
            }
            if (item.SalePrice <= 0)
            {
                ShowStatus($"سعر البيع لـ '{item.PartName}' يجب أن يكون أكبر من صفر", false);
                return;
            }
            if (item.Quantity <= 0)
            {
                ShowStatus($"كمية '{item.PartName}' يجب أن تكون أكبر من صفر", false);
                return;
            }
        }

        IsProcessing = true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var purchaseInvoiceService = scope.ServiceProvider.GetRequiredService<IPurchaseInvoiceService>();

            var createDto = new CreatePurchaseInvoiceDto
            {
                Items = CartItems.Select(i => new PurchaseInvoiceItemDto
                {
                    SparePartId = i.SparePartId,
                    PartName = i.PartName,
                    Quantity = i.Quantity,
                    CostPrice = i.CostPrice,
                    SalePrice = i.SalePrice,
                    MinSalePrice = i.MinSalePrice,
                    LineTotal = i.LineTotal
                }).ToList(),
                SupplierName = SupplierName,
                SupplierPhone = SupplierPhone,
                Notes = Notes
            };

            var invoice = await purchaseInvoiceService.CreatePurchaseInvoiceAsync(createDto);

            ShowStatus($"تمت عملية الشراء بنجاح - فاتورة رقم: {invoice.InvoiceNumber}", true);

            // Clear cart
            foreach (var item in CartItems)
                item.PropertyChanged -= CartItem_PropertyChanged;
            CartItems.Clear();
            SupplierName = null;
            SupplierPhone = null;
            Notes = null;
            CartItemCount = 0;
            RecalculateTotal();

            // Refresh invoice list
            await LoadPurchaseInvoicesAsync();
        }
        catch (Exception ex)
        {
            // BUG FIX: previously only showed ex.Message, which for EF Core DbUpdateException
            // is the generic "An error occurred while saving the entity changes" message.
            // The real cause is in InnerException. We now walk the inner-exception chain
            // to surface the actual SQL/database error to the user.
            var fullMessage = GetFullExceptionMessage(ex);
            System.Diagnostics.Debug.WriteLine($"[PurchaseInvoice] Error: {fullMessage}");
            System.Diagnostics.Debug.WriteLine($"[PurchaseInvoice] Stack: {ex}");
            ShowStatus($"خطأ في إتمام الشراء: {fullMessage}", false);

            // Also show a MessageBox for critical errors so they're not missed
            // (the status bar might be dismissed too quickly)
            if (ex is Microsoft.EntityFrameworkCore.DbUpdateException)
            {
                System.Windows.MessageBox.Show(
                    "فشل حفظ فاتورة الشراء في قاعدة البيانات.\n\n" +
                    "تفاصيل الخطأ:\n" + fullMessage +
                    "\n\nإذا كان الخطأ متعلقاً بعمود مفقود، أعد تشغيل البرنامج كمدير " +
                    "لتطبيق تحديثات المخطط تلقائياً. إذا استمر الخطأ، تواصل مع الدعم الفني " +
                    "مع رسالة الخطأ الكاملة.",
                    "خطأ في الحفظ",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Walks the InnerException chain to build the full error message.
    /// EF Core's DbUpdateException hides the real cause in InnerException —
    /// typically a SqlException with the actual SQL error (missing column,
    /// constraint violation, etc.).
    /// </summary>
    private static string GetFullExceptionMessage(Exception ex)
    {
        var messages = new List<string>();
        var current = ex;
        var depth = 0;
        while (current != null && depth < 5)
        {
            if (!string.IsNullOrEmpty(current.Message))
                messages.Add(current.Message);
            current = current.InnerException;
            depth++;
        }
        return string.Join(" → ", messages.Distinct());
    }

    [RelayCommand]
    private async Task CancelPurchaseInvoiceAsync(int invoiceId)
    {
        var result = System.Windows.MessageBox.Show(
            "هل أنت متأكد من إلغاء فاتورة الشراء؟ سيتم خصم الكميات من المخزون.",
            "تأكيد الإلغاء",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var purchaseInvoiceService = scope.ServiceProvider.GetRequiredService<IPurchaseInvoiceService>();
            await purchaseInvoiceService.CancelPurchaseInvoiceAsync(invoiceId);
            ShowStatus("تم إلغاء فاتورة الشراء بنجاح", true);
            await LoadPurchaseInvoicesAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في الإلغاء: {ex.Message}", false);
        }
    }

    #endregion

    #region Invoice List Commands

    [RelayCommand]
    private async Task LoadPurchaseInvoicesAsync()
    {
        IsLoadingList = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var purchaseInvoiceService = scope.ServiceProvider.GetRequiredService<IPurchaseInvoiceService>();

            var result = await purchaseInvoiceService.GetPagedAsync(CurrentPage, 20, FilterFromDate, FilterToDate);
            PurchaseInvoices = new ObservableCollection<PurchaseInvoiceDto>(result.Items);
            TotalPages = result.TotalPages;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PurchaseInvoice] Load error: {ex.Message}");
        }
        finally
        {
            IsLoadingList = false;
        }
    }

    [RelayCommand]
    private async Task FilterInvoicesAsync()
    {
        CurrentPage = 1;
        await LoadPurchaseInvoicesAsync();
    }

    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        FilterFromDate = null;
        FilterToDate = null;
        CurrentPage = 1;
        await LoadPurchaseInvoicesAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadPurchaseInvoicesAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadPurchaseInvoicesAsync();
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// عند تغيّر خاصية في عنصر السلة (كمية، سعر تكلفة، سعر بيع) نعيد حساب الإجمالي
    /// </summary>
    private void CartItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PurchaseCartItemModel.LineTotal) ||
            e.PropertyName == nameof(PurchaseCartItemModel.Quantity) ||
            e.PropertyName == nameof(PurchaseCartItemModel.CostPrice) ||
            e.PropertyName == nameof(PurchaseCartItemModel.SalePrice) ||
            e.PropertyName == nameof(PurchaseCartItemModel.MinSalePrice))
        {
            RecalculateTotal();
        }
    }

    private void RecalculateTotal()
    {
        TotalAmount = CartItems.Sum(i => i.LineTotal);
        TotalAmountDisplay = $"{TotalAmount:N2} {CurrencySymbol}";
    }

    partial void OnCurrencySymbolChanged(string value)
    {
        foreach (var item in CartItems)
            item.CurrencySymbol = value;
        RecalculateTotal();
    }

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
