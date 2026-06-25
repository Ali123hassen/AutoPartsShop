using AutoPartsShop.Application.DTOs.Reports;
using AutoPartsShop.Application.DTOs.Returns;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.Interfaces;
using AutoPartsShop.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace AutoPartsShop.UI.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private int _selectedReportType; // 0=DailySales, 1=Profit, 2=Stock, 3=TopSelling, 4=Returns

    [ObservableProperty]
    private DateTime _fromDate = DateTime.Today.AddDays(-30);

    [ObservableProperty]
    private DateTime _toDate = DateTime.Today;

    [ObservableProperty]
    private object? _reportData;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasReport;

    [ObservableProperty]
    private string _reportTitle = string.Empty;

    [ObservableProperty]
    private ObservableCollection<DailySalesReportDto> _dailySalesData = [];

    [ObservableProperty]
    private ProfitReportDto? _profitData;

    [ObservableProperty]
    private StockReportDto? _stockData;

    [ObservableProperty]
    private ObservableCollection<TopSellingPartDto> _topSellingData = [];

    [ObservableProperty]
    private ObservableCollection<ReturnDto> _returnsData = [];

    [ObservableProperty]
    private string _currencySymbol = "ر.س";

    [ObservableProperty]
    private string _shopName = string.Empty;

    public ObservableCollection<ReportTypeItem> ReportTypes { get; } =
    [
        new() { Id = 0, Name = "مبيعات يومية" },
        new() { Id = 1, Name = "الأرباح" },
        new() { Id = 2, Name = "المخزون" },
        new() { Id = 3, Name = "الأكثر مبيعاً" },
        new() { Id = 4, Name = "المرتجعات" }
    ];

    /// <summary>
    /// عرض التاريخ المنسق للتقرير
    /// </summary>
    public string ReportDateDisplay
    {
        get
        {
            if (!HasReport) return string.Empty;

            return SelectedReportType switch
            {
                2 => $"تقرير المخزون - {DateTime.Now:yyyy/MM/dd}",
                _ => $"من {FromDate:yyyy/MM/dd} إلى {ToDate:yyyy/MM/dd}"
            };
        }
    }

    /// <summary>
    /// هل التقرير الحالي هو تقرير المبيعات اليومية
    /// </summary>
    public bool IsDailySalesReport => HasReport && SelectedReportType == 0;

    /// <summary>
    /// هل التقرير الحالي هو تقرير الأرباح
    /// </summary>
    public bool IsProfitReport => HasReport && SelectedReportType == 1;

    /// <summary>
    /// هل التقرير الحالي هو تقرير المخزون
    /// </summary>
    public bool IsStockReport => HasReport && SelectedReportType == 2;

    /// <summary>
    /// هل التقرير الحالي هو تقرير الأكثر مبيعاً
    /// </summary>
    public bool IsTopSellingReport => HasReport && SelectedReportType == 3;

    /// <summary>
    /// هل التقرير الحالي هو تقرير المرتجعات
    /// </summary>
    public bool IsReturnsReport => HasReport && SelectedReportType == 4;

    /// <summary>
    /// نسبة القطع المتوفرة من إجمالي المخزون
    /// </summary>
    public decimal AvailableStockPercent
    {
        get
        {
            if (StockData == null || StockData.TotalParts == 0) return 0;
            var available = StockData.TotalParts - StockData.LowStockParts - StockData.OutOfStockParts;
            return Math.Round((decimal)available / StockData.TotalParts * 100, 1);
        }
    }

    /// <summary>
    /// نسبة القطع منخفضة المخزون
    /// </summary>
    public decimal LowStockPercent
    {
        get
        {
            if (StockData == null || StockData.TotalParts == 0) return 0;
            return Math.Round((decimal)StockData.LowStockParts / StockData.TotalParts * 100, 1);
        }
    }

    /// <summary>
    /// نسبة القطع النافدة من المخزون
    /// </summary>
    public decimal OutOfStockPercent
    {
        get
        {
            if (StockData == null || StockData.TotalParts == 0) return 0;
            return Math.Round((decimal)StockData.OutOfStockParts / StockData.TotalParts * 100, 1);
        }
    }

    public ReportsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = LoadShopSettingsAsync();
    }

    private async Task LoadShopSettingsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();
            ShopName = await settingService.GetAsync("ShopName", "متجر قطع الغيار");
            CurrencySymbol = await settingService.GetAsync("Currency", "ر.س");
        }
        catch
        {
            ShopName = "متجر قطع الغيار";
            CurrencySymbol = "ر.س";
        }
    }

    partial void OnSelectedReportTypeChanged(int value)
    {
        // إعادة تعيين حالة التقرير عند تغيير النوع
        HasReport = false;
        OnPropertyChanged(nameof(IsDailySalesReport));
        OnPropertyChanged(nameof(IsProfitReport));
        OnPropertyChanged(nameof(IsStockReport));
        OnPropertyChanged(nameof(IsTopSellingReport));
        OnPropertyChanged(nameof(IsReturnsReport));
        OnPropertyChanged(nameof(ReportDateDisplay));
    }

    partial void OnHasReportChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDailySalesReport));
        OnPropertyChanged(nameof(IsProfitReport));
        OnPropertyChanged(nameof(IsStockReport));
        OnPropertyChanged(nameof(IsTopSellingReport));
        OnPropertyChanged(nameof(IsReturnsReport));
        OnPropertyChanged(nameof(ReportDateDisplay));
    }

    partial void OnStockDataChanged(StockReportDto? value)
    {
        OnPropertyChanged(nameof(AvailableStockPercent));
        OnPropertyChanged(nameof(LowStockPercent));
        OnPropertyChanged(nameof(OutOfStockPercent));
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        IsLoading = true;

        try
        {
            switch (SelectedReportType)
            {
                case 0: // Daily Sales
                    await LoadDailySalesReportAsync();
                    break;
                case 1: // Profit
                    await LoadProfitReportAsync();
                    break;
                case 2: // Stock
                    await LoadStockReportAsync();
                    break;
                case 3: // Top Selling
                    await LoadTopSellingReportAsync();
                    break;
                case 4: // Returns
                    await LoadReturnsReportAsync();
                    break;
            }

            HasReport = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"خطأ في إنشاء التقرير: {ex.Message}", "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void PrintReport()
    {
        try
        {
            if (!HasReport)
            {
                System.Windows.MessageBox.Show("لا توجد بيانات للطباعة. يرجى إنشاء التقرير أولاً.", "تنبيه",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var ownerWindow = System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current.MainWindow;

            ReportPrintHelper.PrintReport(
                SelectedReportType, ShopName, CurrencySymbol, ReportTitle,
                FromDate, ToDate,
                DailySalesData.FirstOrDefault(),
                ProfitData,
                StockData,
                TopSellingData,
                ReturnsData,
                ownerWindow);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"خطأ في الطباعة: {ex.Message}", "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ExportReport()
    {
        try
        {
            if (!HasReport)
            {
                System.Windows.MessageBox.Show("لا توجد بيانات للتصدير. يرجى إنشاء التقرير أولاً.", "تنبيه",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PDF Files|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"تقرير_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                var success = ReportPrintHelper.ExportReportToPdf(
                    SelectedReportType, ShopName, CurrencySymbol, ReportTitle,
                    FromDate, ToDate,
                    DailySalesData.FirstOrDefault(),
                    ProfitData,
                    StockData,
                    TopSellingData,
                    ReturnsData,
                    dialog.FileName);

                if (success)
                {
                    System.Windows.MessageBox.Show("تم تصدير التقرير بنجاح", "تصدير",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                    // فتح الملف بعد التصدير
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"خطأ في التصدير: {ex.Message}", "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task LoadDailySalesReportAsync()
    {
        ReportTitle = "تقرير المبيعات";
        using var scope = _scopeFactory.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        var report = await reportService.GetDailySalesReportAsync(FromDate, ToDate);
        DailySalesData = new ObservableCollection<DailySalesReportDto> { report };
        ReportData = report;
    }

    private async Task LoadProfitReportAsync()
    {
        ReportTitle = "تقرير الأرباح";
        using var scope = _scopeFactory.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        ProfitData = await reportService.GetProfitReportAsync(FromDate, ToDate);
        ReportData = ProfitData;
    }

    private async Task LoadStockReportAsync()
    {
        ReportTitle = "تقرير المخزون";
        using var scope = _scopeFactory.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        StockData = await reportService.GetStockReportAsync();
        ReportData = StockData;
    }

    private async Task LoadTopSellingReportAsync()
    {
        ReportTitle = "تقرير الأكثر مبيعاً";
        using var scope = _scopeFactory.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        var data = await reportService.GetTopSellingPartsAsync(FromDate, ToDate, 20);
        TopSellingData = new ObservableCollection<TopSellingPartDto>(data);
        ReportData = TopSellingData;
    }

    private async Task LoadReturnsReportAsync()
    {
        ReportTitle = "تقرير المرتجعات";
        using var scope = _scopeFactory.CreateScope();
        var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
        var result = await reportService.GetReturnsReportAsync(FromDate, ToDate, 1, 100);
        ReturnsData = new ObservableCollection<ReturnDto>(result.Items);
        ReportData = ReturnsData;
    }
}

public class ReportTypeItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
