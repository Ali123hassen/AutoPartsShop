using AutoPartsShop.Application.DTOs.Invoices;
using AutoPartsShop.Application.DTOs.Reports;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace AutoPartsShop.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;

    // ===== بطاقات الإحصائيات العلوية =====

    [ObservableProperty]
    private string _welcomeMessage = "مرحباً بك";

    [ObservableProperty]
    private string _shopName = "متجر قطع الغيار";

    [ObservableProperty]
    private string _todayDateDisplay = DateTime.Now.ToString("dddd، dd MMMM yyyy");

    [ObservableProperty]
    private decimal _todaySales;

    [ObservableProperty]
    private int _todayInvoices;

    [ObservableProperty]
    private decimal _todayProfit;

    [ObservableProperty]
    private string _todayProfitMarginDisplay = "0%";

    [ObservableProperty]
    private decimal _todayReturns;

    [ObservableProperty]
    private int _todayReturnCount;

    [ObservableProperty]
    private string _todayReturnsDisplay = "0 ر.س";

    // ===== بطاقات المخزون =====

    [ObservableProperty]
    private string _totalStockValueDisplay = "0 ر.س";

    [ObservableProperty]
    private int _totalParts;

    [ObservableProperty]
    private int _lowStockAlerts;

    [ObservableProperty]
    private string _lowStockDisplay = "0";

    [ObservableProperty]
    private string _outOfStockDisplay = "0";

    // ===== القوائم =====

    [ObservableProperty]
    private ObservableCollection<InvoiceDto> _recentInvoices = [];

    [ObservableProperty]
    private ObservableCollection<SparePartDto> _lowStockItems = [];

    [ObservableProperty]
    private ObservableCollection<TopSellingPartDto> _topSellingParts = [];

    // ===== الرسم البياني =====

    [ObservableProperty]
    private ISeries[] _salesChartSeries = [];

    [ObservableProperty]
    private Axis[] _salesChartXAxes = [];

    [ObservableProperty]
    private Axis[] _salesChartYAxes = [];

    // ===== حالة التحميل =====

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _currencySymbol = "ر.س";

    // ===== خصائص مساعدة =====

    private decimal _totalStockValueRaw;

    // ===== الخصائص المحسوبة =====

    public string TodaySalesDisplay => $"{TodaySales:N0} {CurrencySymbol}";
    public string TodayProfitDisplay => $"{TodayProfit:N0} {CurrencySymbol}";

    public DashboardViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = LoadDashboardDataAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        if (IsLoading) return;

        IsLoading = true;

        try
        {
            await LoadSettingsAsync();
            await LoadDailySalesAsync();
            await LoadDailyProfitAsync();
            await LoadTodayReturnsAsync();
            await LoadRecentInvoicesAsync();
            await LoadStockReportAsync();
            await LoadLowStockItemsAsync();
            await LoadTopSellingPartsAsync();
            await LoadSalesChartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingService = scope.ServiceProvider.GetRequiredService<ISettingService>();

            CurrencySymbol = await settingService.GetAsync("Currency", "ر.س");
            ShopName = await settingService.GetAsync("ShopName", "متجر قطع الغيار");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Settings error: {ex.Message}");
        }
    }

    private async Task LoadDailySalesAsync()
    {
        // يتم حساب مبيعات اليوم وأرباحه معاً في LoadDailyProfitAsync
        // من خلال تقرير المبيعات اليومي لضمان الاتساق
        await Task.CompletedTask;
    }

    private async Task LoadDailyProfitAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

            var today = DateTime.Today;
            var dailyReport = await reportService.GetDailySalesReportAsync(today, today);

            // مبيعات اليوم = صافي المبيعات بعد خصم المرتجعات (بعد الخصم، قبل الضريبة)
            TodaySales = dailyReport.NetSales;

            // أرباح اليوم = صافي المبيعات (بعد خصم مرتجعات فواتير اليوم) - صافي التكاليف
            TodayProfit = dailyReport.TotalProfit;
            TodayInvoices = dailyReport.TotalInvoices;

            if (dailyReport.NetSales > 0)
            {
                var margin = Math.Round((dailyReport.TotalProfit / dailyReport.NetSales) * 100, 1);
                TodayProfitMarginDisplay = $"{margin}%";
            }
            else
            {
                TodayProfitMarginDisplay = "0%";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Daily profit error: {ex.Message}");
            TodayProfit = 0;
            TodaySales = 0;
            TodayProfitMarginDisplay = "0%";
        }
    }

    private async Task LoadTodayReturnsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var returnService = scope.ServiceProvider.GetRequiredService<IReturnService>();

            var today = DateTime.Today;
            var endOfDay = today.AddDays(1).AddTicks(-1);
            var returnsResult = await returnService.GetPagedAsync(1, 100, today, endOfDay);

            TodayReturnCount = returnsResult.TotalCount;
            TodayReturns = returnsResult.Items.Sum(r => r.RefundAmount);
            TodayReturnsDisplay = $"{TodayReturns:N0} {CurrencySymbol}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Returns error: {ex.Message}");
            TodayReturnCount = 0;
            TodayReturns = 0;
            TodayReturnsDisplay = $"0 {CurrencySymbol}";
        }
    }

    private async Task LoadRecentInvoicesAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

            var invoiceResult = await invoiceService.GetPagedAsync(1, 10);
            RecentInvoices = new ObservableCollection<InvoiceDto>(invoiceResult.Items);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Recent invoices error: {ex.Message}");
            RecentInvoices = new ObservableCollection<InvoiceDto>();
        }
    }

    private async Task LoadStockReportAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

            var stockReport = await reportService.GetStockReportAsync();
            TotalParts = stockReport.TotalParts;
            LowStockAlerts = stockReport.LowStockParts;
            _totalStockValueRaw = stockReport.TotalStockValue;
            TotalStockValueDisplay = $"{stockReport.TotalStockValue:N0} {CurrencySymbol}";
            LowStockDisplay = stockReport.LowStockParts.ToString();
            OutOfStockDisplay = stockReport.OutOfStockParts.ToString();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Stock report error: {ex.Message}");
            TotalStockValueDisplay = $"0 {CurrencySymbol}";
            LowStockDisplay = "0";
            OutOfStockDisplay = "0";
        }
    }

    private async Task LoadLowStockItemsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sparePartService = scope.ServiceProvider.GetRequiredService<ISparePartService>();

            var lowStock = await sparePartService.GetLowStockAsync();
            LowStockItems = new ObservableCollection<SparePartDto>(lowStock.Take(10));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Low stock items error: {ex.Message}");
            LowStockItems = new ObservableCollection<SparePartDto>();
        }
    }

    private async Task LoadTopSellingPartsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var topParts = await reportService.GetTopSellingPartsAsync(startOfMonth, today, 5);
            TopSellingParts = new ObservableCollection<TopSellingPartDto>(topParts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Top selling error: {ex.Message}");
            TopSellingParts = new ObservableCollection<TopSellingPartDto>();
        }
    }

    private async Task LoadSalesChartAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

            var today = DateTime.Today;
            var startDate = today.AddDays(-6);

            // استخدام الطريقة المحسنة: استدعاء واحد بدلاً من 7 استدعاءات
            var chartData = await reportService.GetDailySalesChartDataAsync(startDate, today);

            var salesValues = new List<double>();
            var profitValues = new List<double>();
            var labels = new List<string>();

            for (var date = startDate; date <= today; date = date.AddDays(1))
            {
                var data = chartData.GetValueOrDefault(date);
                salesValues.Add((double)data.NetSales);
                profitValues.Add((double)data.Profit);
                labels.Add(date.ToString("dd/MM"));
            }

            SalesChartSeries = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = salesValues,
                    Name = "المبيعات",
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(79, 129, 189)) { StrokeThickness = 3 },
                    GeometrySize = 8,
                    GeometryStroke = new SolidColorPaint(new SKColor(79, 129, 189)) { StrokeThickness = 2 },
                    GeometryFill = new SolidColorPaint(SKColors.White)
                },
                new LineSeries<double>
                {
                    Values = profitValues,
                    Name = "الأرباح",
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(130, 180, 80)) { StrokeThickness = 3 },
                    GeometrySize = 8,
                    GeometryStroke = new SolidColorPaint(new SKColor(130, 180, 80)) { StrokeThickness = 2 },
                    GeometryFill = new SolidColorPaint(SKColors.White)
                }
            };

            SalesChartXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsRotation = 0,
                    SeparatorsPaint = new SolidColorPaint(new SKColor(230, 230, 230)) { StrokeThickness = 1 }
                }
            };

            SalesChartYAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0,
                    SeparatorsPaint = new SolidColorPaint(new SKColor(230, 230, 230)) { StrokeThickness = 1 },
                    Labeler = value => value.ToString("N0")
                }
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Chart error: {ex.Message}");
            SalesChartSeries = Array.Empty<ISeries>();
        }
    }

    // ===== تحديث الخصائص المحسوبة عند تغير القيم =====

    partial void OnTodaySalesChanged(decimal value)
    {
        OnPropertyChanged(nameof(TodaySalesDisplay));
    }

    partial void OnTodayProfitChanged(decimal value)
    {
        OnPropertyChanged(nameof(TodayProfitDisplay));
    }

    partial void OnTodayReturnsChanged(decimal value)
    {
        TodayReturnsDisplay = $"{value:N0} {CurrencySymbol}";
    }

    partial void OnCurrencySymbolChanged(string value)
    {
        OnPropertyChanged(nameof(TodaySalesDisplay));
        OnPropertyChanged(nameof(TodayProfitDisplay));
        TodayReturnsDisplay = $"{TodayReturns:N0} {value}";
        TotalStockValueDisplay = $"{_totalStockValueRaw:N0} {value}";
    }

    partial void OnLowStockAlertsChanged(int value)
    {
        LowStockDisplay = value.ToString();
    }
}
