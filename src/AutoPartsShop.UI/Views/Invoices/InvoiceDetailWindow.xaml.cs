using AutoPartsShop.Application.DTOs.Invoices;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using MessageBox = System.Windows.MessageBox;
using System.Windows;

namespace AutoPartsShop.UI.Views.Invoices
{
    public partial class InvoiceDetailWindow : Window
    {
        private readonly InvoiceDto _invoice;

        public InvoiceDetailWindow(InvoiceDto invoice)
        {
            InitializeComponent();

            _invoice = invoice;

            // Create ViewModel with line numbers
            var viewModel = new InvoiceDetailViewModel(invoice);
            DataContext = viewModel;

            // Load settings asynchronously to avoid deadlock
            Loaded += async (s, e) => await LoadSettingsAsync(viewModel);
        }

        private async Task LoadSettingsAsync(InvoiceDetailViewModel viewModel)
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

                var taxRateStr = await settingService.GetAsync("TaxRate", "15");
                if (decimal.TryParse(taxRateStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var taxRate))
                {
                    viewModel.TaxLabel = $"الضريبة ({taxRate}%):";
                }
            }
            catch
            {
                // Use defaults if settings can't be loaded
                viewModel.ShopName = "محل قطع الغيار";
                viewModel.CurrencySymbol = "ر.س";
                viewModel.TaxLabel = "الضريبة (15%):";
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// يُغلق النافذة عند الضغط على مفتاح ESC.
        /// هذا يطبّق على أي عنصر داخل النافذة (TextBox, DataGrid, إلخ).
        /// </summary>
        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var vm = DataContext as InvoiceDetailViewModel;
                if (vm == null) return;

                InvoicePrintHelper.ShowPrintDialog(
                    _invoice,
                    vm.ShopName,
                    vm.ShopAddress,
                    vm.ShopPhone,
                    vm.ShopLogoPath,
                    vm.CurrencySymbol,
                    vm.TaxLabel,
                    vm.ShowCostAndPrice,
                    this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"حدث خطأ أثناء الطباعة:\n{ex.Message}",
                    "خطأ في الطباعة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// ViewModel for invoice detail display - adds line numbers to items and shop settings
    /// </summary>
    public class InvoiceDetailViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ChangeAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? Notes { get; set; }
        public List<InvoiceItemLineViewModel> Items { get; set; } = [];

        // ===== تفاصيل المرتجع المالي =====
        public decimal ReturnSubTotal { get; set; }
        public decimal ReturnDiscount { get; set; }
        public decimal ReturnAfterDiscount { get; set; }
        public decimal ReturnTax { get; set; }
        public decimal ReturnTotal { get; set; }
        public bool HasReturns => ReturnSubTotal > 0;

        // ===== إظهار التكلفة وسعر البيع =====
        private bool _showCostAndPrice;
        public bool ShowCostAndPrice
        {
            get => _showCostAndPrice;
            set { _showCostAndPrice = value; OnPropertyChanged(nameof(ShowCostAndPrice)); }
        }

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

        private string _taxLabel = "الضريبة (15%):";
        public string TaxLabel
        {
            get => _taxLabel;
            set { _taxLabel = value; OnPropertyChanged(nameof(TaxLabel)); }
        }

        public bool HasShopName => !string.IsNullOrWhiteSpace(ShopName);
        public bool HasShopAddress => !string.IsNullOrWhiteSpace(ShopAddress);
        public bool HasShopPhone => !string.IsNullOrWhiteSpace(ShopPhone);

        public InvoiceDetailViewModel(InvoiceDto dto)
        {
            InvoiceNumber = dto.InvoiceNumber;
            InvoiceDate = dto.InvoiceDate;
            UserName = dto.UserName;
            SubTotal = dto.SubTotal;
            TaxRate = dto.TaxRate;
            TaxAmount = dto.TaxAmount;
            DiscountAmount = dto.DiscountAmount;
            TotalAmount = dto.TotalAmount;
            PaidAmount = dto.PaidAmount;
            ChangeAmount = dto.ChangeAmount;
            PaymentMethod = dto.PaymentMethod;
            Status = dto.Status;
            CustomerName = dto.CustomerName;
            Notes = dto.Notes;

            // تفاصيل المرتجع المالي
            ReturnSubTotal = dto.ReturnSubTotal;
            ReturnDiscount = dto.ReturnDiscount;
            ReturnAfterDiscount = dto.ReturnAfterDiscount;
            ReturnTax = dto.ReturnTax;
            ReturnTotal = dto.ReturnTotal;

            // Add line numbers
            for (int i = 0; i < dto.Items.Count; i++)
            {
                Items.Add(new InvoiceItemLineViewModel(dto.Items[i], i + 1));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Invoice item with line number for display
    /// </summary>
    public class InvoiceItemLineViewModel
    {
        public int LineNumber { get; set; }
        public string PartName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        /// <summary>
        /// Discount amount per unit in currency (calculated from DiscountPercent).
        /// </summary>
        public decimal DiscountAmountPerUnit { get; set; }
        public decimal LineTotal { get; set; }
        /// <summary>
        /// سعر التكلفة وقت البيع - لحساب الربح
        /// </summary>
        public decimal CostAtSale { get; set; }

        /// <summary>
        /// الكمية المرتجعة من هذا الصنف
        /// </summary>
        public int ReturnedQty { get; set; }

        /// <summary>
        /// الكمية المتبقية (الكمية - المرتجع)
        /// </summary>
        public int RemainingQty => Quantity - ReturnedQty;

        /// <summary>
        /// حالة الصنف: None / PartialReturn / FullReturn
        /// </summary>
        public string ItemReturnStatus { get; set; } = "None";

        public InvoiceItemLineViewModel(InvoiceItemDto item, int lineNumber)
        {
            LineNumber = lineNumber;
            PartName = item.PartName;
            Quantity = item.Quantity;
            UnitPrice = item.UnitPrice;
            DiscountPercent = item.DiscountPercent;
            DiscountAmountPerUnit = UnitPrice * (DiscountPercent / 100m);
            LineTotal = item.LineTotal;
            CostAtSale = item.CostAtSale;
            ReturnedQty = item.ReturnedQty;
            ItemReturnStatus = item.ItemReturnStatus;
        }
    }
}
