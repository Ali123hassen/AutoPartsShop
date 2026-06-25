using AutoPartsShop.Application.DTOs.PurchaseInvoices;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace AutoPartsShop.UI.Views.PurchaseInvoices;

public partial class PurchaseInvoiceDetailWindow : Window
{
    private readonly PurchaseInvoiceDto _invoice;

    public PurchaseInvoiceDetailWindow(PurchaseInvoiceDto invoice)
    {
        InitializeComponent();

        _invoice = invoice;

        var viewModel = new PurchaseInvoiceDetailViewModel(invoice);
        DataContext = viewModel;

        Loaded += async (s, e) => await LoadSettingsAsync(viewModel);
    }

    private async Task LoadSettingsAsync(PurchaseInvoiceDetailViewModel viewModel)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            viewModel.ShopName = await settingService.GetAsync("ShopName", "محل قطع الغيار");
            viewModel.ShopAddress = await settingService.GetAsync("ShopAddress", "");
            viewModel.ShopPhone = await settingService.GetAsync("ShopPhone", "");
            viewModel.ShopLogoPath = await settingService.GetAsync("ShopLogoPath", "");
            viewModel.CurrencySymbol = await settingService.GetAsync("Currency", "ر.س");
        }
        catch
        {
            viewModel.ShopName = "محل قطع الغيار";
            viewModel.CurrencySymbol = "ر.س";
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// يُغلق النافذة عند الضغط على مفتاح ESC.
    /// </summary>
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void PrintPdfButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var vm = DataContext as PurchaseInvoiceDetailViewModel;
            if (vm == null) return;

            PurchaseInvoicePrintHelper.PrintToPdf(_invoice, vm.ShopName, vm.ShopAddress, vm.ShopPhone, vm.ShopLogoPath, vm.CurrencySymbol);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء الطباعة:\n{ex.Message}", "خطأ في الطباعة",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

/// <summary>
/// ViewModel لعرض تفاصيل فاتورة المشتريات
/// </summary>
public class PurchaseInvoiceDetailViewModel : INotifyPropertyChanged
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string? SupplierPhone { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ItemsCount { get; set; }
    public List<PurchaseInvoiceItemLineViewModel> Items { get; set; } = [];

    // Shop info from settings
    private string _shopName = string.Empty;
    public string ShopName
    {
        get => _shopName;
        set { _shopName = value; OnPropertyChanged(nameof(ShopName)); OnPropertyChanged(nameof(HasShopName)); }
    }

    private string _shopAddress = string.Empty;
    public string ShopAddress
    {
        get => _shopAddress;
        set { _shopAddress = value; OnPropertyChanged(nameof(ShopAddress)); OnPropertyChanged(nameof(HasShopAddress)); }
    }

    private string _shopPhone = string.Empty;
    public string ShopPhone
    {
        get => _shopPhone;
        set { _shopPhone = value; OnPropertyChanged(nameof(ShopPhone)); OnPropertyChanged(nameof(HasShopPhone)); }
    }

    private string _shopLogoPath = string.Empty;
    public string ShopLogoPath
    {
        get => _shopLogoPath;
        set { _shopLogoPath = value; OnPropertyChanged(nameof(ShopLogoPath)); }
    }

    private string _currencySymbol = "ر.س";
    public string CurrencySymbol
    {
        get => _currencySymbol;
        set { _currencySymbol = value; OnPropertyChanged(nameof(CurrencySymbol)); }
    }

    public bool HasShopName => !string.IsNullOrWhiteSpace(ShopName);
    public bool HasShopAddress => !string.IsNullOrWhiteSpace(ShopAddress);
    public bool HasShopPhone => !string.IsNullOrWhiteSpace(ShopPhone);

    public PurchaseInvoiceDetailViewModel(PurchaseInvoiceDto dto)
    {
        InvoiceNumber = dto.InvoiceNumber;
        InvoiceDate = dto.InvoiceDate;
        UserName = dto.UserName;
        SupplierName = dto.SupplierName;
        SupplierPhone = dto.SupplierPhone;
        TotalAmount = dto.TotalAmount;
        Notes = dto.Notes;
        Status = dto.Status;
        ItemsCount = dto.ItemsCount;

        for (int i = 0; i < dto.Items.Count; i++)
        {
            Items.Add(new PurchaseInvoiceItemLineViewModel(dto.Items[i], i + 1));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// بند فاتورة مشتريات مع رقم السطر
/// </summary>
public class PurchaseInvoiceItemLineViewModel
{
    public int LineNumber { get; set; }
    public string PartName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal? MinSalePrice { get; set; }
    public decimal LineTotal { get; set; }

    public string MinSalePriceDisplay => MinSalePrice.HasValue ? $"{MinSalePrice.Value:N2}" : "—";

    public PurchaseInvoiceItemLineViewModel(PurchaseInvoiceItemDto item, int lineNumber)
    {
        LineNumber = lineNumber;
        PartName = item.PartName;
        Quantity = item.Quantity;
        CostPrice = item.CostPrice;
        SalePrice = item.SalePrice;
        MinSalePrice = item.MinSalePrice;
        LineTotal = item.LineTotal;
    }
}
