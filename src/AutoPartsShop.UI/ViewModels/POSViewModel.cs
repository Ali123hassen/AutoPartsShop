using AutoPartsShop.Application.DTOs.Invoices;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Globalization;

namespace AutoPartsShop.UI.ViewModels;

public partial class POSViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    // Tax rate loaded from settings (percentage, e.g. 15 means 15%)
    private decimal _taxRatePercent = 15m;

    // Cancellation token source for search debouncing
    private CancellationTokenSource? _searchCts;

    // Guard flag to prevent circular updates between Text and Decimal properties
    private bool _isUpdatingFromCode;

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
    private ObservableCollection<CartItemModel> _cartItems = [];

    [ObservableProperty]
    private int _cartItemCount;

    [ObservableProperty]
    private CartItemModel? _selectedCartItem;

    #endregion

    #region Settings Properties

    [ObservableProperty]
    private string _currencySymbol = "ر.س";

    [ObservableProperty]
    private string _taxLabel = "الضريبة (15%):";

    #endregion

    #region Totals

    [ObservableProperty]
    private decimal _subTotal;

    [ObservableProperty]
    private decimal _taxAmount;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private decimal _totalAmount;

    [ObservableProperty]
    private decimal _paidAmount;

    [ObservableProperty]
    private string _paidAmountText = "0";

    [ObservableProperty]
    private decimal _changeAmount;

    #endregion

    #region Display Properties (formatted with currency)

    public string SubTotalDisplay => $"{SubTotal:N2} {CurrencySymbol}";
    public string TaxAmountDisplay => $"{TaxAmount:N2} {CurrencySymbol}";
    public string TotalDiscountDisplay => DiscountAmount > 0 ? $"-{DiscountAmount:N2} {CurrencySymbol}" : $"0.00 {CurrencySymbol}";
    public string TotalAmountDisplay => $"{TotalAmount:N2} {CurrencySymbol}";
    public string ChangeAmountDisplay => $"{ChangeAmount:N2} {CurrencySymbol}";
    public string PaidAmountDisplay => $"{PaidAmount:N2} {CurrencySymbol}";

    #endregion

    #region Customer & Payment

    [ObservableProperty]
    private string _customerName = string.Empty;

    [ObservableProperty]
    private PaymentMethod _paymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private bool _isCashPayment = true;

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

    [ObservableProperty]
    private string _lastInvoiceNumber = string.Empty;

    [ObservableProperty]
    private bool _canPrintLastInvoice;

    private InvoiceDto? _lastInvoice;

    #endregion

    public POSViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = LoadSettingsAsync();
    }

    #region Load Settings

    private async Task LoadSettingsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            var taxRateStr = await settingService.GetAsync("TaxRate", "15");
            if (decimal.TryParse(taxRateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedRate) && parsedRate >= 0)
            {
                _taxRatePercent = parsedRate;
            }

            var currency = await settingService.GetAsync("Currency", "ر.س");
            CurrencySymbol = currency;

            UpdateTaxLabel();
            RecalculateTotals();
        }
        catch
        {
            // Use default values if settings can't be loaded
            _taxRatePercent = 15m;
            CurrencySymbol = "ر.س";
            UpdateTaxLabel();
        }
    }

    private void UpdateTaxLabel()
    {
        TaxLabel = $"الضريبة ({_taxRatePercent}%):";
    }

    #endregion

    #region Search Commands

    partial void OnSearchTextChanged(string value)
    {
        // Cancel any previous search before starting a new one
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        if (!string.IsNullOrWhiteSpace(value) && value.Length >= 2)
        {
            var token = _searchCts.Token;
            _ = SearchProductsAsync(token);
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
        }
    }

    [RelayCommand]
    private async Task SearchProductsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Length < 2)
        {
            SearchResults.Clear();
            return;
        }

        IsSearching = true;
        try
        {
            // Add a small delay for debouncing (300ms)
            await Task.Delay(300, cancellationToken);

            // Create a NEW scope → fresh DbContext for this search
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

            // Check if search was cancelled while waiting
            if (cancellationToken.IsCancellationRequested)
                return;

            SearchResults.Clear();
            foreach (var item in result.Items)
            {
                SearchResults.Add(item);
            }
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled by a newer search, ignore silently
        }
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
        if (string.IsNullOrWhiteSpace(searchText))
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();

            // First try exact barcode match
            var sparePart = await sparePartService.GetByBarcodeAsync(searchText);

            if (sparePart != null)
            {
                // Found by barcode — add to cart automatically
                AddToCart(sparePart);
            }
            else
            {
                // No exact barcode match — try PartNumber match
                sparePart = await sparePartService.GetByPartNumberAsync(searchText);

                if (sparePart != null)
                {
                    AddToCart(sparePart);
                }
                else
                {
                    ShowStatus($"لم يتم العثور على قطعة بالباركود أو الرقم: {searchText}", false);
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في البحث بالباركود: {ex.Message}", false);
        }
    }

    [RelayCommand]
    private void AddToCart(SparePartDto? sparePart)
    {
        if (sparePart == null) return;

        // Check if already in cart
        var existingItem = CartItems.FirstOrDefault(i => i.SparePartId == sparePart.Id);
        if (existingItem != null)
        {
            // Check stock before incrementing
            if (existingItem.Quantity >= sparePart.CurrentStock)
            {
                ShowStatus($"الكمية المتوفرة من '{sparePart.Name}' هي {sparePart.CurrentStock} فقط", false);
                return;
            }
            existingItem.Quantity++;
        }
        else
        {
            // Check stock
            if (sparePart.CurrentStock <= 0)
            {
                ShowStatus($"القطعة '{sparePart.Name}' غير متوفرة في المخزون", false);
                return;
            }

            var cartItem = new CartItemModel
            {
                SparePartId = sparePart.Id,
                PartName = sparePart.Name,
                PartNumber = sparePart.PartNumber,
                Location = sparePart.Location,
                CompatibleCar = sparePart.CompatibleCar,
                CarModel = sparePart.CarModel,
                CarYear = sparePart.CarYear,
                Quantity = 1,
                UnitPrice = sparePart.SalePrice,
                DiscountAmount = 0,
                AvailableStock = sparePart.CurrentStock,
                CurrencySymbol = CurrencySymbol
            };
            cartItem.SetParent(this);
            CartItems.Add(cartItem);
        }

        ShowStatus($"تمت إضافة: {sparePart.Name}", true);
        CartItemCount = CartItems.Count;
        RecalculateTotals();

        // Clear search after adding
        SearchText = string.Empty;
        SearchResults.Clear();
    }

    #endregion

    #region Cart Commands

    [RelayCommand]
    private void RemoveItem(CartItemModel? item)
    {
        if (item == null) return;
        CartItems.Remove(item);
        CartItemCount = CartItems.Count;
        RecalculateTotals();
    }

    [RelayCommand]
    private void IncrementQuantity(CartItemModel? item)
    {
        if (item == null) return;

        if (item.Quantity >= item.AvailableStock)
        {
            ShowStatus($"الكمية المتوفرة هي {item.AvailableStock} فقط", false);
            return;
        }

        item.Quantity++;
        RecalculateTotals();
    }

    [RelayCommand]
    private void DecrementQuantity(CartItemModel? item)
    {
        if (item == null) return;

        if (item.Quantity > 1)
        {
            item.Quantity--;
            RecalculateTotals();
        }
        else
        {
            CartItems.Remove(item);
            CartItemCount = CartItems.Count;
            RecalculateTotals();
        }
    }

    [RelayCommand]
    private void ClearCart()
    {
        CartItems.Clear();
        CustomerName = string.Empty;
        PaidAmount = 0;
        CartItemCount = 0;
        LastInvoiceNumber = string.Empty;
        CanPrintLastInvoice = false;
        _lastInvoice = null;
        StatusMessage = string.Empty;
        HasStatusMessage = false;
        RecalculateTotals();
    }

    #endregion

    #region Payment Commands

    [RelayCommand]
    private void SetCashPayment()
    {
        IsCashPayment = true;
        PaymentMethod = PaymentMethod.Cash;
    }

    [RelayCommand]
    private void SetCardPayment()
    {
        IsCashPayment = false;
        PaymentMethod = PaymentMethod.Card;
        PaidAmount = TotalAmount; // Card payment is exact
    }

    [RelayCommand]
    private async Task CompleteSaleAsync()
    {
        if (CartItems.Count == 0)
        {
            ShowStatus("السلة فارغة، أضف منتجات أولاً", false);
            return;
        }

        if (IsCashPayment && PaidAmount < TotalAmount)
        {
            ShowStatus("المبلغ المدفوع أقل من الإجمالي", false);
            return;
        }

        IsProcessing = true;

        try
        {
            // Create a NEW scope → fresh DbContext for this sale
            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            var createDto = new CreateInvoiceDto
            {
                Items = CartItems.Select(i => i.ToInvoiceItemDto()).ToList(),
                DiscountAmount = DiscountAmount,
                TaxRate = _taxRatePercent,
                PaidAmount = PaidAmount,
                PaymentMethod = PaymentMethod,
                CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? null : CustomerName,
                Notes = null
            };

            var invoice = await invoiceService.CreateInvoiceAsync(createDto);

            LastInvoiceNumber = invoice.InvoiceNumber;
            _lastInvoice = invoice;
            CanPrintLastInvoice = true;
            ShowStatus($"تمت عملية البيع بنجاح - فاتورة رقم: {invoice.InvoiceNumber}", true);

            // Clear cart after successful sale
            CartItems.Clear();
            CustomerName = string.Empty;
            PaidAmount = 0;
            CartItemCount = 0;
            RecalculateTotals();
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في إتمام البيع: {ex.Message}", false);
        }
        finally
        {
            IsProcessing = false;
        }
    }

    #endregion

    #region Paid Amount

    partial void OnPaidAmountChanged(decimal value)
    {
        if (_isUpdatingFromCode) return;
        _isUpdatingFromCode = true;
        try
        {
            // Sync text property when decimal value changes programmatically
            PaidAmountText = value.ToString(CultureInfo.InvariantCulture);
            ChangeAmount = PaidAmount - TotalAmount;
            if (ChangeAmount < 0) ChangeAmount = 0;
            NotifyDisplayPropertiesChanged();
        }
        finally
        {
            _isUpdatingFromCode = false;
        }
    }

    partial void OnPaidAmountTextChanged(string value)
    {
        if (_isUpdatingFromCode) return;
        _isUpdatingFromCode = true;
        try
        {
            // Parse user input gracefully — empty or invalid text becomes 0
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result) && result >= 0)
            {
                PaidAmount = result;
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                PaidAmount = 0;
            }
            // If invalid (non-numeric), keep previous PaidAmount, don't crash

            // Must update change amount here because OnPaidAmountChanged is skipped
            // while _isUpdatingFromCode guard is active
            ChangeAmount = PaidAmount - TotalAmount;
            if (ChangeAmount < 0) ChangeAmount = 0;
            NotifyDisplayPropertiesChanged();
        }
        finally
        {
            _isUpdatingFromCode = false;
        }
    }

    #endregion

    #region Print Commands

    [RelayCommand]
    private async Task PrintLastInvoiceAsync()
    {
        if (_lastInvoice == null)
        {
            ShowStatus("لا توجد فاتورة للطباعة", false);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            var shopName = await settingService.GetAsync("ShopName", "محل قطع الغيار");
            var shopAddress = await settingService.GetAsync("ShopAddress", "");
            var shopPhone = await settingService.GetAsync("ShopPhone", "");
            var shopLogoPath = await settingService.GetAsync("ShopLogoPath", "");
            var currencySymbol = await settingService.GetAsync("Currency", "ر.س");
            var taxRateStr = await settingService.GetAsync("TaxRate", "15");
            var taxLabel = "الضريبة (15%):";
            if (decimal.TryParse(taxRateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var taxRate))
            {
                taxLabel = $"الضريبة ({taxRate}%):";
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                InvoicePrintHelper.ShowPrintDialog(
                    _lastInvoice,
                    shopName,
                    shopAddress,
                    shopPhone,
                    shopLogoPath,
                    currencySymbol,
                    taxLabel,
                    false,
                    System.Windows.Application.Current.MainWindow);
            });
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في الطباعة: {ex.Message}", false);
        }
    }

    [RelayCommand]
    private async Task ViewLastInvoiceAsync()
    {
        if (_lastInvoice == null)
        {
            ShowStatus("لا توجد فاتورة للعرض", false);
            return;
        }

        try
        {
            // Fetch fresh invoice data from service
            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            var freshInvoice = await invoiceService.GetByIdAsync(_lastInvoice.Id);

            if (freshInvoice != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var detailWindow = new Views.Invoices.InvoiceDetailWindow(freshInvoice)
                    {
                        Owner = System.Windows.Application.Current.MainWindow
                    };
                    detailWindow.ShowDialog();
                });
            }
            else
            {
                ShowStatus("لم يتم العثور على الفاتورة", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في عرض الفاتورة: {ex.Message}", false);
        }
    }

    #endregion

    #region Helper Methods

    public void RecalculateTotals()
    {
        // Total discount = sum of all line discounts (DiscountAmount is already total per line)
        DiscountAmount = CartItems.Sum(i => i.DiscountAmount);

        SubTotal = CartItems.Sum(i => i.LineTotal);
        TaxAmount = SubTotal * (_taxRatePercent / 100m);
        TotalAmount = SubTotal + TaxAmount;

        if (TotalAmount < 0) TotalAmount = 0;

        ChangeAmount = PaidAmount - TotalAmount;
        if (ChangeAmount < 0) ChangeAmount = 0;

        NotifyDisplayPropertiesChanged();
    }

    private void NotifyDisplayPropertiesChanged()
    {
        OnPropertyChanged(nameof(SubTotalDisplay));
        OnPropertyChanged(nameof(TaxAmountDisplay));
        OnPropertyChanged(nameof(TotalDiscountDisplay));
        OnPropertyChanged(nameof(TotalAmountDisplay));
        OnPropertyChanged(nameof(ChangeAmountDisplay));
        OnPropertyChanged(nameof(PaidAmountDisplay));
    }

    partial void OnCurrencySymbolChanged(string value)
    {
        NotifyDisplayPropertiesChanged();
    }

    private void ShowStatus(string message, bool isSuccess)
    {
        StatusMessage = message;
        HasStatusMessage = true;
        IsSuccessMessage = isSuccess;

        // Auto-clear success messages after 5 seconds
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
