using AutoPartsShop.Application.DTOs.Invoices;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.Helpers;
using AutoPartsShop.UI.Views.Invoices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace AutoPartsShop.UI.ViewModels;

public partial class InvoiceListViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private string _currencySymbol = "ر.س";

    [ObservableProperty]
    private ObservableCollection<InvoiceDto> _invoices = [];

    [ObservableProperty]
    private InvoiceDto? _selectedInvoice;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private DateTime? _filterFromDate;

    [ObservableProperty]
    private DateTime? _filterToDate;

    private const int PageSize = 20;

    // Semaphore to prevent concurrent DB operations
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    // ===== Auto-suggest properties =====
    /// <summary>
    /// List of invoice suggestions shown in the popup as the user types.
    /// Populated when at least 3 characters are typed in the search box.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<InvoiceDto> _invoiceSuggestions = [];

    /// <summary>Controls visibility of the suggestions popup.</summary>
    [ObservableProperty]
    private bool _isSuggestionsOpen;

    /// <summary>The currently highlighted suggestion (for keyboard navigation).</summary>
    [ObservableProperty]
    private InvoiceDto? _selectedSuggestion;

    // Cancellation token for the debounced suggest query
    private CancellationTokenSource? _suggestCts;

    public InvoiceListViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
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

    /// <summary>
    /// When the search text changes, debounce and trigger auto-suggest after 3+ chars.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        _suggestCts?.Cancel();
        _suggestCts = new CancellationTokenSource();
        var token = _suggestCts.Token;

        if (string.IsNullOrWhiteSpace(value) || value.Length < 3)
        {
            InvoiceSuggestions.Clear();
            IsSuggestionsOpen = false;
            return;
        }

        _ = LoadSuggestionsAsync(token);
    }

    /// <summary>
    /// Loads up to 10 invoices whose InvoiceNumber starts with the typed text.
    /// Uses a debounce + cancellation to avoid hammering the DB on every keystroke.
    /// </summary>
    private async Task LoadSuggestionsAsync(CancellationToken token)
    {
        try
        {
            // 250ms debounce — feels responsive without flooding the DB
            await Task.Delay(250, token);
            if (token.IsCancellationRequested) return;

            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            // Load up to 50 recent invoices and filter client-side by prefix.
            // This avoids adding a new service method just for suggestions.
            var result = await invoiceService.GetPagedAsync(1, 50, null, null);
            if (token.IsCancellationRequested) return;

            var prefix = SearchText.Trim();
            var matches = result.Items
                .Where(i => !string.IsNullOrEmpty(i.InvoiceNumber)
                            && i.InvoiceNumber.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .ToList();

            InvoiceSuggestions.Clear();
            foreach (var m in matches)
            {
                InvoiceSuggestions.Add(m);
            }

            // Open the popup only if we have at least one match
            IsSuggestionsOpen = matches.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Ignore — a newer search has started
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InvoiceList] Suggest error: {ex.Message}");
            IsSuggestionsOpen = false;
        }
    }

    /// <summary>
    /// Called when the user clicks a suggestion (or presses Enter on a highlighted one).
    /// Opens the invoice detail window.
    /// </summary>
    [RelayCommand]
    private async Task SelectSuggestionAsync(InvoiceDto? invoice)
    {
        if (invoice == null) return;

        // Close the popup immediately for responsiveness
        IsSuggestionsOpen = false;

        // Open the detail window (reuses the existing flow)
        await ViewInvoiceDetailAsync(invoice);

        // Clear the search text after selection so the user can search again
        SearchText = string.Empty;
    }

    /// <summary>
    /// Closes the suggestions popup (called when the user clicks away or presses ESC).
    /// </summary>
    [RelayCommand]
    private void CloseSuggestions()
    {
        IsSuggestionsOpen = false;
    }

    [RelayCommand]
    private async Task LoadInvoicesAsync()
    {
        // Prevent concurrent loads
        if (!await _loadSemaphore.WaitAsync(0))
            return;

        IsLoading = true;
        try
        {
            // Create a NEW scope for this operation → fresh DbContext
            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            var result = await invoiceService.GetPagedAsync(CurrentPage, PageSize, FilterFromDate, FilterToDate);
            Invoices.Clear();
            foreach (var invoice in result.Items)
            {
                Invoices.Add(invoice);
            }
            TotalCount = result.TotalCount;
            TotalPages = result.TotalPages;
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في تحميل الفواتير: {ex.Message}";
            HasStatusMessage = true;
        }
        finally
        {
            IsLoading = false;
            _loadSemaphore.Release();
        }
    }

    [RelayCommand]
    private async Task SearchInvoicesAsync()
    {
        // Close the suggestions popup since the user is doing a manual filter search
        IsSuggestionsOpen = false;
        CurrentPage = 1;
        await LoadInvoicesAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadInvoicesAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadInvoicesAsync();
        }
    }

    [RelayCommand]
    private async Task ViewInvoiceDetailAsync(InvoiceDto? invoice)
    {
        if (invoice == null)
        {
            StatusMessage = "اختر فاتورة أولاً";
            HasStatusMessage = true;
            return;
        }

        try
        {
            // Create a NEW scope → fresh DbContext
            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            var fullInvoice = await invoiceService.GetByIdAsync(invoice.Id);
            if (fullInvoice == null)
            {
                StatusMessage = "لم يتم العثور على الفاتورة";
                HasStatusMessage = true;
                return;
            }

            // Open detail window
            var detailWindow = new InvoiceDetailWindow(fullInvoice);

            // Set the owner to the main window
            var mainWindow = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
            if (mainWindow != null)
            {
                detailWindow.Owner = mainWindow;
            }

            detailWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في عرض الفاتورة: {ex.Message}";
            HasStatusMessage = true;
        }
    }

    [RelayCommand]
    private async Task CancelInvoiceAsync()
    {
        if (SelectedInvoice == null)
        {
            StatusMessage = "اختر فاتورة أولاً";
            HasStatusMessage = true;
            return;
        }

        if (SelectedInvoice.Status == "Cancelled")
        {
            StatusMessage = "هذه الفاتورة ملغاة بالفعل";
            HasStatusMessage = true;
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"هل أنت متأكد من إلغاء الفاتورة {SelectedInvoice.InvoiceNumber}؟\nسيتم إرجاع الكميات إلى المخزون.",
            "تأكيد الإلغاء",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            // Create a NEW scope → fresh DbContext
            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            await invoiceService.CancelInvoiceAsync(SelectedInvoice.Id);
            StatusMessage = $"تم إلغاء الفاتورة {SelectedInvoice.InvoiceNumber} بنجاح";
            HasStatusMessage = true;
            await LoadInvoicesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطأ في إلغاء الفاتورة: {ex.Message}";
            HasStatusMessage = true;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadInvoicesAsync();
    }

    [RelayCommand]
    private async Task PrintInvoiceAsync(InvoiceDto? invoice)
    {
        if (invoice == null)
        {
            StatusMessage = "اختر فاتورة أولاً";
            HasStatusMessage = true;
            return;
        }

        try
        {
            // Create a NEW scope → fresh DbContext
            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            var fullInvoice = await invoiceService.GetByIdAsync(invoice.Id);
            if (fullInvoice == null)
            {
                StatusMessage = "لم يتم العثور على الفاتورة";
                HasStatusMessage = true;
                return;
            }

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
                    fullInvoice,
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
            StatusMessage = $"خطأ في الطباعة: {ex.Message}";
            HasStatusMessage = true;
        }
    }

    partial void OnFilterFromDateChanged(DateTime? value)
    {
        _ = LoadInvoicesAsync();
    }

    partial void OnFilterToDateChanged(DateTime? value)
    {
        _ = LoadInvoicesAsync();
    }
}
