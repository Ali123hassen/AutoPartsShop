using AutoPartsShop.Application.DTOs.Invoices;
using AutoPartsShop.Application.DTOs.Returns;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.Core.Enums;
using AutoPartsShop.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace AutoPartsShop.UI.ViewModels;

public partial class ReturnViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    // === Search / Filter ===
    [ObservableProperty]
    private string _invoiceSearch = string.Empty;

    /// <summary>
    /// Auto-suggest popup: list of matching invoices shown as the user types 3+ chars.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<InvoiceDto> _invoiceSuggestions = [];

    /// <summary>Controls visibility of the invoice suggestions popup.</summary>
    [ObservableProperty]
    private bool _isInvoiceSuggestionsOpen;

    // Cancellation token for the debounced invoice-suggest query
    private CancellationTokenSource? _invoiceSuggestCts;

    partial void OnInvoiceSearchChanged(string value)
    {
        _invoiceSuggestCts?.Cancel();
        _invoiceSuggestCts = new CancellationTokenSource();
        var token = _invoiceSuggestCts.Token;

        if (string.IsNullOrWhiteSpace(value) || value.Length < 3)
        {
            InvoiceSuggestions.Clear();
            IsInvoiceSuggestionsOpen = false;
            return;
        }

        _ = LoadInvoiceSuggestionsAsync(token);
    }

    /// <summary>
    /// Loads up to 10 invoices whose InvoiceNumber starts with the typed text.
    /// </summary>
    private async Task LoadInvoiceSuggestionsAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(250, token);  // debounce
            if (token.IsCancellationRequested) return;

            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            // Load up to 50 recent invoices and filter client-side by prefix.
            var result = await invoiceService.GetPagedAsync(1, 50, null, null);
            if (token.IsCancellationRequested) return;

            var prefix = InvoiceSearch.Trim();
            var matches = result.Items
                .Where(i => !string.IsNullOrEmpty(i.InvoiceNumber)
                            && i.InvoiceNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .ToList();

            InvoiceSuggestions.Clear();
            foreach (var m in matches)
                InvoiceSuggestions.Add(m);

            IsInvoiceSuggestionsOpen = matches.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Ignore — a newer search has started
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReturnViewModel] Suggest error: {ex.Message}");
            IsInvoiceSuggestionsOpen = false;
        }
    }

    /// <summary>
    /// Called when the user clicks an invoice suggestion.
    /// Loads the invoice items and closes the popup.
    /// </summary>
    [RelayCommand]
    private async Task SelectInvoiceSuggestionAsync(InvoiceDto? invoice)
    {
        if (invoice == null) return;

        // Close popup + populate the search field with the chosen invoice number
        IsInvoiceSuggestionsOpen = false;
        InvoiceSearch = invoice.InvoiceNumber;

        // Trigger the existing search flow so invoice items get loaded
        await SearchInvoiceAsync();
    }

    [ObservableProperty]
    private DateTime? _filterFromDate;

    [ObservableProperty]
    private DateTime? _filterToDate;

    partial void OnFilterFromDateChanged(DateTime? value) => _ = LoadReturnsAsync();
    partial void OnFilterToDateChanged(DateTime? value) => _ = LoadReturnsAsync();

    // === Return Form ===
    [ObservableProperty]
    private ReturnType _selectedReturnType = ReturnType.Refund;

    [ObservableProperty]
    private int _selectedReturnTypeIndex = 0; // 0=Refund, 1=Exchange

    partial void OnSelectedReturnTypeIndexChanged(int value)
    {
        SelectedReturnType = value == 1 ? ReturnType.Exchange : ReturnType.Refund;
        OnPropertyChanged(nameof(IsExchangeType));
    }

    public bool IsExchangeType => SelectedReturnType == ReturnType.Exchange;

    [ObservableProperty]
    private InvoiceDto? _foundInvoice;

    /// <summary>
    /// Whether an invoice was found — used for visibility binding in XAML.
    /// </summary>
    public bool HasFoundInvoice => FoundInvoice != null;

    partial void OnFoundInvoiceChanged(InvoiceDto? value)
    {
        OnPropertyChanged(nameof(HasFoundInvoice));
    }

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private SparePartDto? _selectedReplacementPart;

    // === Invoice Items for Return ===
    [ObservableProperty]
    private ObservableCollection<InvoiceReturnItemModel> _invoiceItems = [];

    /// <summary>
    /// Total refund amount (before tax) from all selected items.
    /// </summary>
    public decimal TotalRefundBeforeTax => InvoiceItems
        .Where(i => i.IsSelected)
        .Sum(i => i.CalculatedRefundBeforeTax);

    /// <summary>
    /// Total discount amount from all selected items.
    /// </summary>
    public decimal TotalDiscountAmount => InvoiceItems
        .Where(i => i.IsSelected)
        .Sum(i => i.CalculatedDiscountAmount);

    /// <summary>
    /// Total tax amount for all selected items.
    /// </summary>
    public decimal TotalTaxAmount => InvoiceItems
        .Where(i => i.IsSelected)
        .Sum(i => i.CalculatedTaxAmount);

    /// <summary>
    /// Total refund amount (including tax) from all selected items.
    /// </summary>
    public decimal TotalRefundAmount => InvoiceItems
        .Where(i => i.IsSelected)
        .Sum(i => i.CalculatedRefundAmount);

    /// <summary>
    /// Number of selected items for return.
    /// </summary>
    public int SelectedItemsCount => InvoiceItems.Count(i => i.IsSelected);

    /// <summary>
    /// Whether any items are selected for return.
    /// </summary>
    public bool HasSelectedItems => SelectedItemsCount > 0;

    // === Data ===
    [ObservableProperty]
    private ObservableCollection<ReturnDto> _returns = [];

    [ObservableProperty]
    private ObservableCollection<SparePartDto> _replacementParts = [];

    [ObservableProperty]
    private ReturnDto? _selectedReturn;

    // === Return Detail Dialog ===
    [ObservableProperty]
    private bool _isDetailDialogOpen;

    [ObservableProperty]
    private ReturnDetailDto? _returnDetail;

    public bool HasReturnDetail => ReturnDetail != null;

    partial void OnReturnDetailChanged(ReturnDetailDto? value)
    {
        OnPropertyChanged(nameof(HasReturnDetail));
    }

    /// <summary>
    /// Whether the return type is Refund (for detail display).
    /// </summary>
    public bool IsDetailRefundType => ReturnDetail?.ReturnType == "Refund";

    /// <summary>
    /// Whether the return type is Exchange (for detail display).
    /// </summary>
    public bool IsDetailExchangeType => ReturnDetail?.ReturnType == "Exchange";

    /// <summary>
    /// Whether there is a discount to display (for detail display).
    /// </summary>
    public bool HasDetailDiscount => ReturnDetail != null && ReturnDetail.DiscountPercent > 0;

    /// <summary>
    /// Whether there is an invoice linked (for detail display).
    /// </summary>
    public bool HasDetailInvoice => ReturnDetail?.InvoiceNumber != null;

    // === Pagination ===
    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalCount;

    private const int PageSize = 20;

    // === UI State ===
    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isSubmitting;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isAddDialogOpen;

    [ObservableProperty]
    private string _currencySymbol = "ر.ي";

    public ReturnViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Called from the View's Loaded event to initialize data.
    /// </summary>
    [RelayCommand]
    private async Task LoadReturnsAsync()
    {
        if (!_loadLock.Wait(0)) return; // Prevent concurrent loads

        try
        {
            IsLoading = true;
            using var scope = _scopeFactory.CreateScope();
            var returnService = scope.ServiceProvider.GetRequiredService<IReturnService>();
            var result = await returnService.GetPagedAsync(CurrentPage, PageSize, FilterFromDate, FilterToDate);

            Returns = new ObservableCollection<ReturnDto>(result.Items);
            TotalPages = result.TotalPages;
            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في تحميل المرتجعات: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            _loadLock.Release();
        }
    }

    // === View Return Details ===

    [RelayCommand]
    private async Task ViewReturnDetailsAsync(ReturnDto? returnItem)
    {
        var target = returnItem ?? SelectedReturn;
        if (target == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var returnService = scope.ServiceProvider.GetRequiredService<IReturnService>();
            var detail = await returnService.GetReturnDetailAsync(target.Id);

            if (detail != null)
            {
                ReturnDetail = detail;
                OnPropertyChanged(nameof(IsDetailRefundType));
                OnPropertyChanged(nameof(IsDetailExchangeType));
                OnPropertyChanged(nameof(HasDetailDiscount));
                OnPropertyChanged(nameof(HasDetailInvoice));
                IsDetailDialogOpen = true;
            }
            else
            {
                ShowStatus("لم يتم العثور على تفاصيل المرتجع");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في تحميل التفاصيل: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CloseDetailDialog()
    {
        IsDetailDialogOpen = false;
    }

    [RelayCommand]
    private void CopyToClipboard(string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            System.Windows.Clipboard.SetText(text);
            ShowStatus($"تم النسخ: {text}");
        }
    }

    // === Add Return Dialog ===

    [RelayCommand]
    private async Task OpenAddDialogAsync()
    {
        // Reset form
        FoundInvoice = null;
        SelectedReplacementPart = null;
        Reason = string.Empty;
        InvoiceSearch = string.Empty;
        InvoiceItems = new ObservableCollection<InvoiceReturnItemModel>();
        SelectedReturnType = ReturnType.Refund;
        SelectedReturnTypeIndex = 0;
        HasStatusMessage = false;

        await LoadReplacementPartsAsync();
        IsAddDialogOpen = true;
    }

    [RelayCommand]
    private void CloseAddDialog()
    {
        IsAddDialogOpen = false;
    }

    [RelayCommand]
    private async Task LoadReplacementPartsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();
            var parts = await sparePartService.GetAllAsync();
            ReplacementParts = new ObservableCollection<SparePartDto>(parts);
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في تحميل قطع الغيار: {ex.Message}");
        }
    }

    // === Search Invoice ===

    [RelayCommand]
    private async Task SearchInvoiceAsync()
    {
        if (string.IsNullOrWhiteSpace(InvoiceSearch))
            return;

        IsSearching = true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            var invoice = await invoiceService.GetByNumberAsync(InvoiceSearch.Trim());
            FoundInvoice = invoice;

            if (invoice == null)
            {
                ShowStatus("الفاتورة غير موجودة");
                InvoiceItems = new ObservableCollection<InvoiceReturnItemModel>();
            }
            else
            {
                // Load invoice return items from the service
                var returnService = scope.ServiceProvider.GetRequiredService<IReturnService>();
                var returnItems = await returnService.GetInvoiceReturnItemsAsync(invoice.Id);

                // Map to UI models
                var models = returnItems.Select(ri => new InvoiceReturnItemModel
                {
                    SparePartId = ri.SparePartId,
                    PartName = ri.PartName,
                    PartNumber = ri.PartNumber,
                    SoldQuantity = ri.SoldQuantity,
                    PreviouslyReturnedQuantity = ri.PreviouslyReturnedQuantity,
                    UnitPrice = ri.UnitPrice,
                    DiscountPercent = ri.DiscountPercent,
                    DiscountAmount = ri.DiscountAmount,
                    LineTotal = ri.LineTotal,
                    TaxRate = ri.TaxRate
                }).ToList();

                InvoiceItems = new ObservableCollection<InvoiceReturnItemModel>(models);

                // Subscribe to property changes for real-time total updates
                foreach (var item in InvoiceItems)
                {
                    item.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(InvoiceReturnItemModel.IsSelected) ||
                            e.PropertyName == nameof(InvoiceReturnItemModel.ReturnQuantity))
                        {
                            OnPropertyChanged(nameof(TotalRefundBeforeTax));
                            OnPropertyChanged(nameof(TotalDiscountAmount));
                            OnPropertyChanged(nameof(TotalTaxAmount));
                            OnPropertyChanged(nameof(TotalRefundAmount));
                            OnPropertyChanged(nameof(SelectedItemsCount));
                            OnPropertyChanged(nameof(HasSelectedItems));
                        }
                    };
                }

                ShowStatus($"تم العثور على الفاتورة: {invoice.InvoiceNumber} - {returnItems.Count} قطعة");
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ في البحث: {ex.Message}");
        }
        finally
        {
            IsSearching = false;
        }
    }

    // === Submit Return ===

    [RelayCommand]
    private async Task SubmitReturnAsync()
    {
        var selectedItems = InvoiceItems.Where(i => i.IsSelected && i.ReturnQuantity > 0).ToList();

        if (selectedItems.Count == 0)
        {
            ShowStatus("يرجى اختيار قطعة واحدة على الأقل للإرجاع");
            return;
        }

        // Validate quantities
        foreach (var item in selectedItems)
        {
            if (item.ReturnQuantity > item.AvailableToReturn)
            {
                ShowStatus($"الكمية المطلوب إرجاعها لـ '{item.PartName}' تتجاوز المتاح ({item.AvailableToReturn})");
                return;
            }
        }

        if (SelectedReturnType == ReturnType.Exchange && SelectedReplacementPart == null)
        {
            ShowStatus("يرجى اختيار قطعة الاستبدال");
            return;
        }

        IsSubmitting = true;

        try
        {
            var batchDto = new CreateBatchReturnDto
            {
                InvoiceId = FoundInvoice!.Id,
                ReturnType = SelectedReturnType,
                ReplacementPartId = SelectedReturnType == ReturnType.Exchange ? SelectedReplacementPart?.Id : null,
                Reason = Reason,
                Items = selectedItems.Select(i => new BatchReturnItemDto
                {
                    SparePartId = i.SparePartId,
                    Quantity = i.ReturnQuantity,
                    RefundAmount = i.CalculatedRefundAmount
                }).ToList()
            };

            using var scope = _scopeFactory.CreateScope();
            var returnService = scope.ServiceProvider.GetRequiredService<IReturnService>();
            await returnService.CreateBatchReturnAsync(batchDto);

            ShowStatus("تم تسجيل المرتجعات بنجاح");
            IsAddDialogOpen = false;

            await LoadReturnsAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"خطأ: {ex.Message}");
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    // === Pagination ===

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadReturnsAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadReturnsAsync();
        }
    }

    // === Refresh ===

    [RelayCommand]
    private async Task RefreshReturnsAsync()
    {
        CurrentPage = 1;
        FilterFromDate = null;
        FilterToDate = null;
        await LoadReturnsAsync();
    }

    // === Helpers ===

    private DispatcherTimer? _statusTimer;

    public void ShowStatus(string message, int autoDismissMs = 4000)
    {
        // إذا كان الـ Dialog مفتوح، نعرض الرسالة كـ MessageBox فوق كل شيء
        // بدلاً من شريط الحالة الذي يظهر خلف الـ Dialog
        if (IsAddDialogOpen || IsDetailDialogOpen)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // الحصول على النافذة النشطة كنافذة أم لـ MessageBox
                var owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>()
                    .LastOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;
                System.Windows.MessageBox.Show(owner, message, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
            return;
        }

        StatusMessage = message;
        HasStatusMessage = true;

        // Auto-dismiss after specified duration
        _statusTimer?.Stop();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(autoDismissMs) };
        _statusTimer.Tick += (s, e) =>
        {
            _statusTimer.Stop();
            HasStatusMessage = false;
        };
        _statusTimer.Start();
    }

    public void DismissStatus()
    {
        _statusTimer?.Stop();
        HasStatusMessage = false;
    }
}
