using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.IO;
using AutoPartsShop.Application.DTOs.Reports;
using AutoPartsShop.Application.DTOs.Returns;
using System.Collections.ObjectModel;

// Resolve conflicts between System.Drawing and System.Windows/System.Windows.Media
using Color = System.Drawing.Color;
using Brush = System.Drawing.Brush;
using SolidBrush = System.Drawing.SolidBrush;
using FontFamily = System.Drawing.FontFamily;
using FontStyle = System.Drawing.FontStyle;
using MessageBox = System.Windows.MessageBox;
using Window = System.Windows.Window;
using WpfPrintDialog = System.Windows.Controls.PrintDialog;

namespace AutoPartsShop.UI.Helpers;

/// <summary>
/// يولّد ويطبع التقارير باستخدام GDI+ مع دعم كامل للعربية و RTL
/// يدعم: طباعة A4 + تصدير PDF عبر Microsoft Print to PDF
/// لا يحتاج أي مكتبة خارجية
/// </summary>
public static class ReportPrintHelper
{
    #region Report Context

    private class ReportContext
    {
        public int ReportType = 0; // 0=DailySales, 1=Profit, 2=Stock, 3=TopSelling, 4=Returns
        public string ShopName = string.Empty;
        public string CurrencySymbol = string.Empty;
        public string ReportTitle = string.Empty;
        public DateTime FromDate;
        public DateTime ToDate;

        // Report Data
        public DailySalesReportDto? DailySalesData;
        public ProfitReportDto? ProfitData;
        public StockReportDto? StockData;
        public ObservableCollection<TopSellingPartDto>? TopSellingData;
        public ObservableCollection<ReturnDto>? ReturnsData;

        // State
        public int ItemIndex;
        public bool HeaderDrawn;
    }

    private static ReportContext? _rctx;

    #endregion

    #region Colors

    private static readonly Color DarkBlue = Color.FromArgb(30, 58, 95);
    private static readonly Color MediumGray = Color.FromArgb(100, 116, 139);
    private static readonly Color DarkText = Color.FromArgb(30, 41, 59);
    private static readonly Color HeaderBg = Color.FromArgb(241, 245, 249);
    private static readonly Color AltRowBg = Color.FromArgb(248, 250, 252);
    private static readonly Color RedColor = Color.FromArgb(239, 68, 68);
    private static readonly Color GreenColor = Color.FromArgb(22, 163, 74);
    private static readonly Color LightGray = Color.FromArgb(203, 213, 225);
    private static readonly Color BorderColor = Color.FromArgb(226, 232, 240);
    private static readonly Color ReturnRed = Color.FromArgb(198, 40, 40);
    private static readonly Color ReturnLightBg = Color.FromArgb(255, 245, 245);
    private static readonly Color ProfitGreenBg = Color.FromArgb(240, 253, 244);

    #endregion

    #region String Formats for RTL Arabic

    private static readonly StringFormat RtlNear = new(StringFormatFlags.DirectionRightToLeft)
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Near,
        Trimming = StringTrimming.Word
    };

    private static readonly StringFormat RtlCenter = new(StringFormatFlags.DirectionRightToLeft)
    {
        Alignment = StringAlignment.Center,
        LineAlignment = StringAlignment.Near,
        Trimming = StringTrimming.Word
    };

    private static readonly StringFormat RtlFar = new(StringFormatFlags.DirectionRightToLeft)
    {
        Alignment = StringAlignment.Far,
        LineAlignment = StringAlignment.Near,
        Trimming = StringTrimming.Word
    };

    #endregion

    #region Public API - Print

    /// <summary>
    /// يطبع التقرير عبر نافذة اختيار الطابعة
    /// </summary>
    public static void PrintReport(
        int reportType,
        string shopName,
        string currencySymbol,
        string reportTitle,
        DateTime fromDate,
        DateTime toDate,
        DailySalesReportDto? dailySalesData,
        ProfitReportDto? profitData,
        StockReportDto? stockData,
        ObservableCollection<TopSellingPartDto>? topSellingData,
        ObservableCollection<ReturnDto>? returnsData,
        Window owner)
    {
        _rctx = new ReportContext
        {
            ReportType = reportType,
            ShopName = shopName,
            CurrencySymbol = currencySymbol,
            ReportTitle = reportTitle,
            FromDate = fromDate,
            ToDate = toDate,
            DailySalesData = dailySalesData,
            ProfitData = profitData,
            StockData = stockData,
            TopSellingData = topSellingData,
            ReturnsData = returnsData,
            ItemIndex = 0,
            HeaderDrawn = false
        };

        try
        {
            var wpfDialog = new WpfPrintDialog();
            if (wpfDialog.ShowDialog() != true) return;

            var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = wpfDialog.PrintQueue.FullName;
            printDoc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
            printDoc.DefaultPageSettings.Landscape = false;
            printDoc.PrintController = new StandardPrintController();

            printDoc.PrintPage += OnReportPrintPage;
            printDoc.Print();
            printDoc.PrintPage -= OnReportPrintPage;
        }
        finally
        {
            _rctx = null;
        }
    }

    /// <summary>
    /// يصدّر التقرير كملف PDF باستخدام Microsoft Print to PDF
    /// </summary>
    public static bool ExportReportToPdf(
        int reportType,
        string shopName,
        string currencySymbol,
        string reportTitle,
        DateTime fromDate,
        DateTime toDate,
        DailySalesReportDto? dailySalesData,
        ProfitReportDto? profitData,
        StockReportDto? stockData,
        ObservableCollection<TopSellingPartDto>? topSellingData,
        ObservableCollection<ReturnDto>? returnsData,
        string outputPath)
    {
        _rctx = new ReportContext
        {
            ReportType = reportType,
            ShopName = shopName,
            CurrencySymbol = currencySymbol,
            ReportTitle = reportTitle,
            FromDate = fromDate,
            ToDate = toDate,
            DailySalesData = dailySalesData,
            ProfitData = profitData,
            StockData = stockData,
            TopSellingData = topSellingData,
            ReturnsData = returnsData,
            ItemIndex = 0,
            HeaderDrawn = false
        };

        try
        {
            var printDoc = new PrintDocument();

            // البحث عن طابعة Microsoft Print to PDF
            var pdfPrinterName = FindPdfPrinter();
            if (pdfPrinterName == null)
            {
                MessageBox.Show("لم يتم العثور على طابعة PDF. يرجى تثبيت 'Microsoft Print to PDF' من إعدادات Windows.",
                    "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }

            printDoc.PrinterSettings.PrinterName = pdfPrinterName;
            printDoc.PrinterSettings.PrintToFile = true;
            printDoc.PrinterSettings.PrintFileName = outputPath;
            printDoc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
            printDoc.DefaultPageSettings.Landscape = false;
            printDoc.PrintController = new StandardPrintController();

            printDoc.PrintPage += OnReportPrintPage;
            printDoc.Print();
            printDoc.PrintPage -= OnReportPrintPage;

            return File.Exists(outputPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"خطأ في تصدير PDF: {ex.Message}", "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return false;
        }
        finally
        {
            _rctx = null;
        }
    }

    private static string? FindPdfPrinter()
    {
        // البحث عن طابعة PDF المتوفرة
        var pdfPrinterNames = new[] { "Microsoft Print to PDF", "Microsoft XPS Document Writer", "PDF", "PDFCreator" };

        foreach (var name in pdfPrinterNames)
        {
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                if (printer.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return printer;
            }
        }

        // إذا لم نجدها، نحاول إنشاء طابعة PDF مؤقتة
        try
        {
            var ps = new PrinterSettings();
            ps.PrinterName = "Microsoft Print to PDF";
            if (ps.IsValid) return "Microsoft Print to PDF";
        }
        catch { }

        return null;
    }

    #endregion

    #region Report Print Page Handler

    private static void OnReportPrintPage(object? sender, PrintPageEventArgs e)
    {
        if (_rctx == null) { e.HasMorePages = false; return; }

        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PageUnit = GraphicsUnit.Inch;

        float pageWidth = e.PageSettings.PaperSize.Width / 100f;
        float pageHeight = e.PageSettings.PaperSize.Height / 100f;
        float leftMargin = e.PageSettings.Margins.Left / 100f;
        float rightMargin = e.PageSettings.Margins.Right / 100f;
        float topMargin = e.PageSettings.Margins.Top / 100f;
        float bottomEdge = pageHeight - e.PageSettings.Margins.Bottom / 100f;

        float contentWidth = pageWidth - leftMargin - rightMargin;
        float rightEdge = pageWidth - rightMargin;
        float leftX = leftMargin;

        float y;

        // رسم الرأس دائماً في الصفحة الأولى
        if (!_rctx.HeaderDrawn)
        {
            y = topMargin;

            // === رأس التقرير ===
            using (var titleFont = new Font("Tahoma", 16f, FontStyle.Bold, GraphicsUnit.Point))
            using (var subtitleFont = new Font("Tahoma", 11f, FontStyle.Regular, GraphicsUnit.Point))
            {
                y = DrawRtlText(g, _rctx.ShopName, titleFont, DarkBlue, rightEdge, y, contentWidth, RtlCenter);
                y += 0.03f;
                y = DrawRtlText(g, _rctx.ReportTitle, subtitleFont, DarkBlue, rightEdge, y, contentWidth, RtlCenter);
                y += 0.02f;

                if (_rctx.ReportType != 2) // ليس تقرير المخزون
                    y = DrawRtlText(g, $"من {_rctx.FromDate:yyyy/MM/dd} إلى {_rctx.ToDate:yyyy/MM/dd}",
                        subtitleFont, MediumGray, rightEdge, y, contentWidth, RtlCenter);
                else
                    y = DrawRtlText(g, $"تاريخ التقرير: {DateTime.Now:yyyy/MM/dd}",
                        subtitleFont, MediumGray, rightEdge, y, contentWidth, RtlCenter);
            }

            y += 0.06f;
            DrawLine(g, leftX, y, rightEdge, y, DarkBlue, 0.01f);
            y += 0.1f;

            _rctx.HeaderDrawn = true;
        }
        else
        {
            // صفحة متابعة - نرسم رأس صغير
            y = topMargin;
            using (var subtitleFont = new Font("Tahoma", 9f, FontStyle.Regular, GraphicsUnit.Point))
            {
                y = DrawRtlText(g, $"{_rctx.ShopName} - {_rctx.ReportTitle} (تابع)",
                    subtitleFont, MediumGray, rightEdge, y, contentWidth, RtlCenter);
            }
            y += 0.05f;
            DrawLine(g, leftX, y, rightEdge, y, BorderColor, 0.005f);
            y += 0.08f;
        }

        // رسم محتوى التقرير حسب النوع
        using (var normalFont = new Font("Tahoma", 10f, FontStyle.Regular, GraphicsUnit.Point))
        using (var boldFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
        using (var sectionFont = new Font("Tahoma", 12f, FontStyle.Bold, GraphicsUnit.Point))
        using (var grandFont = new Font("Tahoma", 14f, FontStyle.Bold, GraphicsUnit.Point))
        using (var smallFont = new Font("Tahoma", 9f, FontStyle.Regular, GraphicsUnit.Point))
        using (var headerFont = new Font("Tahoma", 9f, FontStyle.Bold, GraphicsUnit.Point))
        {
            switch (_rctx.ReportType)
            {
                case 0: // Daily Sales
                    y = DrawDailySalesReport(g, _rctx, rightEdge, y, contentWidth, leftX, bottomEdge,
                        normalFont, boldFont, sectionFont, grandFont, smallFont, headerFont);
                    break;
                case 1: // Profit
                    y = DrawProfitReport(g, _rctx, rightEdge, y, contentWidth, leftX, bottomEdge,
                        normalFont, boldFont, sectionFont, grandFont, smallFont, headerFont);
                    break;
                case 2: // Stock
                    y = DrawStockReport(g, _rctx, rightEdge, y, contentWidth, leftX, bottomEdge,
                        normalFont, boldFont, sectionFont, grandFont, smallFont, headerFont);
                    break;
                case 3: // Top Selling
                    y = DrawTopSellingReport(g, _rctx, rightEdge, y, contentWidth, leftX, bottomEdge,
                        normalFont, boldFont, sectionFont, grandFont, smallFont, headerFont);
                    break;
                case 4: // Returns
                    y = DrawReturnsReport(g, _rctx, rightEdge, y, contentWidth, leftX, bottomEdge,
                        normalFont, boldFont, sectionFont, grandFont, smallFont, headerFont);
                    break;
            }
        }

        // تذييل
        y += 0.1f;
        DrawLine(g, leftX, y, rightEdge, y, BorderColor, 0.005f);
        y += 0.05f;
        using (var footerFont = new Font("Tahoma", 8f, FontStyle.Regular, GraphicsUnit.Point))
        {
            DrawRtlText(g, $"تم إنشاء التقرير تلقائياً - {DateTime.Now:yyyy/MM/dd HH:mm}",
                footerFont, MediumGray, rightEdge, y, contentWidth, RtlCenter);
        }

        e.HasMorePages = false;
    }

    #endregion

    #region Daily Sales Report

    private static float DrawDailySalesReport(Graphics g, ReportContext ctx, float rightX, float y,
        float contentWidth, float leftX, float bottomEdge,
        Font normalFont, Font boldFont, Font sectionFont, Font grandFont, Font smallFont, Font headerFont)
    {
        var data = ctx.DailySalesData;
        if (data == null) return y;

        // === قسم المبيعات ===
        y = DrawSectionTitle(g, "ملخص المبيعات", rightX, y, contentWidth, leftX, sectionFont);
        y += 0.04f;

        y = DrawLabelValue(g, "عدد الفواتير", data.TotalInvoices.ToString(),
            normalFont, boldFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "إجمالي المبيعات قبل الخصم", $"{data.TotalSalesBeforeDiscount:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "إجمالي الخصومات", $"{data.TotalDiscounts:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, RedColor, RedColor, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "صافي المبيعات بعد الخصم", $"{data.TotalSales:N2} {ctx.CurrencySymbol}",
            normalFont, boldFont, MediumGray, DarkBlue, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "إجمالي الضريبة", $"{data.TotalTax:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "صافي الضريبة", $"{data.NetTax:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);

        // الإجمالي بعد الضريبة
        y += 0.03f;
        DrawLine(g, leftX, y, rightX, y, DarkBlue, 0.008f);
        y += 0.05f;
        y = DrawLabelValue(g, "الإجمالي بعد الضريبة", $"{data.TotalSalesWithTax:N2} {ctx.CurrencySymbol}",
            grandFont, grandFont, DarkBlue, DarkBlue, rightX, y, contentWidth, 0.55f);

        // === قسم المرتجعات ===
        if (data.TotalReturns > 0)
        {
            y += 0.1f;
            DrawDashedLine(g, leftX, y, rightX, y, ReturnRed);
            y += 0.08f;
            y = DrawRtlText(g, "↩ تفاصيل المرتجعات", boldFont, ReturnRed, rightX, y, contentWidth, RtlNear);
            y += 0.04f;

            // خلفية حمراء فاتحة
            float sectionTop = y - 0.02f;
            float sectionH = 4 * (normalFont.GetHeight(g) + 0.02f) + 0.15f;
            DrawFilledRect(g, leftX, sectionTop, contentWidth, sectionH, ReturnLightBg);

            y = DrawLabelValue(g, "المرتجعات قبل الخصم", $"{data.ReturnsBeforeDiscount:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);
            y = DrawLabelValue(g, "المرتجعات بعد الخصم", $"{data.TotalReturnsBeforeTax:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);
            y = DrawLabelValue(g, "ضريبة المرتجعات", $"{data.ReturnsTax:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);
            y = DrawLabelValue(g, "إجمالي المرتجعات", $"{data.TotalReturns:N2} {ctx.CurrencySymbol}",
                boldFont, boldFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);
        }

        // === صافي الربح ===
        y += 0.1f;
        var profitColor = data.TotalProfit >= 0 ? GreenColor : RedColor;
        var profitBg = data.TotalProfit >= 0 ? ProfitGreenBg : ReturnLightBg;
        DrawLine(g, leftX, y, rightX, y, profitColor, 0.01f);
        y += 0.06f;

        float profitTop = y - 0.02f;
        float profitH = 2 * (normalFont.GetHeight(g) + 0.02f) + grandFont.GetHeight(g) + 0.1f;
        DrawFilledRect(g, leftX, profitTop, contentWidth, profitH, profitBg);

        y = DrawLabelValue(g, "صافي التكاليف", $"{data.NetCost:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, profitColor, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, data.TotalProfit >= 0 ? "صافي الربح" : "صافي الخسارة",
            $"{data.TotalProfit:N2} {ctx.CurrencySymbol}",
            grandFont, grandFont, profitColor, profitColor, rightX, y, contentWidth, 0.55f);

        return y;
    }

    #endregion

    #region Profit Report

    private static float DrawProfitReport(Graphics g, ReportContext ctx, float rightX, float y,
        float contentWidth, float leftX, float bottomEdge,
        Font normalFont, Font boldFont, Font sectionFont, Font grandFont, Font smallFont, Font headerFont)
    {
        var data = ctx.ProfitData;
        if (data == null) return y;

        // === قسم الإيرادات ===
        y = DrawSectionTitle(g, "الإيرادات", rightX, y, contentWidth, leftX, sectionFont);
        y += 0.04f;

        y = DrawLabelValue(g, "الإيرادات قبل الخصم", $"{data.TotalRevenueBeforeDiscount:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "إجمالي الخصومات", $"{data.TotalDiscounts:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, RedColor, RedColor, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "صافي الإيرادات بعد الخصم", $"{data.TotalRevenue:N2} {ctx.CurrencySymbol}",
            normalFont, boldFont, MediumGray, DarkBlue, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "إجمالي الضريبة", $"{data.TotalTax:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "صافي الضريبة", $"{data.NetTax:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "عدد القطع المباعة", data.TotalItemsSold.ToString(),
            normalFont, normalFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);

        y += 0.03f;
        DrawLine(g, leftX, y, rightX, y, DarkBlue, 0.008f);
        y += 0.05f;
        y = DrawLabelValue(g, "الإيرادات بعد الضريبة", $"{data.TotalRevenueWithTax:N2} {ctx.CurrencySymbol}",
            boldFont, boldFont, DarkBlue, DarkBlue, rightX, y, contentWidth, 0.55f);

        // === قسم المرتجعات ===
        if (data.TotalReturns > 0)
        {
            y += 0.1f;
            DrawDashedLine(g, leftX, y, rightX, y, ReturnRed);
            y += 0.08f;
            y = DrawRtlText(g, "↩ تفاصيل المرتجعات", boldFont, ReturnRed, rightX, y, contentWidth, RtlNear);
            y += 0.04f;

            float sectionTop = y - 0.02f;
            float sectionH = 5 * (normalFont.GetHeight(g) + 0.02f) + 0.15f;
            DrawFilledRect(g, leftX, sectionTop, contentWidth, sectionH, ReturnLightBg);

            y = DrawLabelValue(g, "المرتجعات قبل الخصم", $"{data.ReturnsBeforeDiscount:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);
            y = DrawLabelValue(g, "المرتجعات بعد الخصم", $"{data.TotalReturnsBeforeTax:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);
            y = DrawLabelValue(g, "ضريبة المرتجعات", $"{data.ReturnsTax:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);
            y = DrawLabelValue(g, "تكلفة الأصناف المرتجعة", $"{data.ReturnedCost:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);
            y = DrawLabelValue(g, "إجمالي المرتجعات", $"{data.TotalReturns:N2} {ctx.CurrencySymbol}",
                boldFont, boldFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);
        }

        // === قسم الأرباح ===
        y += 0.1f;
        var profitColor = data.RealNetProfit >= 0 ? GreenColor : RedColor;
        var profitBg = data.RealNetProfit >= 0 ? ProfitGreenBg : ReturnLightBg;
        DrawLine(g, leftX, y, rightX, y, profitColor, 0.01f);
        y += 0.06f;

        float profitTop = y - 0.02f;
        float profitH = 4 * (normalFont.GetHeight(g) + 0.02f) + grandFont.GetHeight(g) + 0.12f;
        DrawFilledRect(g, leftX, profitTop, contentWidth, profitH, profitBg);

        y = DrawLabelValue(g, "صافي الإيرادات", $"{data.NetRevenue:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, profitColor, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "صافي التكاليف", $"{data.NetCost:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, profitColor, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, data.RealNetProfit >= 0 ? "صافي الربح" : "صافي الخسارة",
            $"{data.RealNetProfit:N2} {ctx.CurrencySymbol}",
            grandFont, grandFont, profitColor, profitColor, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "هامش الربح", $"{data.ProfitMargin:N2}%",
            boldFont, boldFont, profitColor, profitColor, rightX, y, contentWidth, 0.55f);

        return y;
    }

    #endregion

    #region Stock Report

    private static float DrawStockReport(Graphics g, ReportContext ctx, float rightX, float y,
        float contentWidth, float leftX, float bottomEdge,
        Font normalFont, Font boldFont, Font sectionFont, Font grandFont, Font smallFont, Font headerFont)
    {
        var data = ctx.StockData;
        if (data == null) return y;

        // === بطاقات إحصائية ===
        y = DrawSectionTitle(g, "ملخص المخزون", rightX, y, contentWidth, leftX, sectionFont);
        y += 0.06f;

        float cardWidth = contentWidth / 2f - 0.05f;

        // صف 1: إجمالي القطع + قيمة المخزون
        DrawStatCard(g, rightX, y, cardWidth, "إجمالي القطع", data.TotalParts.ToString(), DarkBlue);
        DrawStatCard(g, rightX - cardWidth - 0.1f, y, cardWidth, "قيمة المخزون", $"{data.TotalStockValue:N2} {ctx.CurrencySymbol}", DarkBlue);
        y += 0.6f;

        // صف 2: منخفض المخزون + نافد
        DrawStatCard(g, rightX, y, cardWidth, "منخفض المخزون", data.LowStockParts.ToString(), Color.FromArgb(245, 158, 11));
        DrawStatCard(g, rightX - cardWidth - 0.1f, y, cardWidth, "نافد المخزون", data.OutOfStockParts.ToString(), RedColor);
        y += 0.6f;

        // === أشرطة النسب ===
        y += 0.05f;
        y = DrawSectionTitle(g, "توزيع المخزون", rightX, y, contentWidth, leftX, sectionFont);
        y += 0.06f;

        if (data.TotalParts > 0)
        {
            var available = data.TotalParts - data.LowStockParts - data.OutOfStockParts;

            // متوفر
            y = DrawLabelValue(g, $"متوفر ({available})", $"{(decimal)available / data.TotalParts * 100:N1}%",
                normalFont, boldFont, GreenColor, GreenColor, rightX, y, contentWidth, 0.7f);
            DrawFilledRect(g, leftX, y, contentWidth * (float)available / data.TotalParts, 0.08f, GreenColor);
            y += 0.15f;

            // منخفض
            y = DrawLabelValue(g, $"منخفض ({data.LowStockParts})", $"{(decimal)data.LowStockParts / data.TotalParts * 100:N1}%",
                normalFont, boldFont, Color.FromArgb(245, 158, 11), Color.FromArgb(245, 158, 11), rightX, y, contentWidth, 0.7f);
            DrawFilledRect(g, leftX, y, contentWidth * (float)data.LowStockParts / data.TotalParts, 0.08f, Color.FromArgb(245, 158, 11));
            y += 0.15f;

            // نافد
            y = DrawLabelValue(g, $"نافد ({data.OutOfStockParts})", $"{(decimal)data.OutOfStockParts / data.TotalParts * 100:N1}%",
                normalFont, boldFont, RedColor, RedColor, rightX, y, contentWidth, 0.7f);
            DrawFilledRect(g, leftX, y, contentWidth * (float)data.OutOfStockParts / data.TotalParts, 0.08f, RedColor);
            y += 0.12f;
        }

        return y;
    }

    private static void DrawStatCard(Graphics g, float rightX, float y, float width,
        string label, string value, Color accentColor)
    {
        float height = 0.5f;
        float leftX = rightX - width;

        // خلفية البطاقة
        DrawFilledRect(g, leftX, y, width, height, HeaderBg);
        // خط علوي ملون
        DrawFilledRect(g, leftX, y, width, 0.04f, accentColor);

        using var valueFont = new Font("Tahoma", 16f, FontStyle.Bold, GraphicsUnit.Point);
        using var labelFont = new Font("Tahoma", 9f, FontStyle.Regular, GraphicsUnit.Point);

        // القيمة
        var valueSize = g.MeasureString(value, valueFont, new SizeF(width, 999f), RtlCenter);
        g.DrawString(value, valueFont, new SolidBrush(accentColor),
            new RectangleF(leftX, y + 0.1f, width, valueSize.Height), RtlCenter);

        // التسمية
        var labelSize = g.MeasureString(label, labelFont, new SizeF(width, 999f), RtlCenter);
        g.DrawString(label, labelFont, new SolidBrush(MediumGray),
            new RectangleF(leftX, y + 0.32f, width, labelSize.Height), RtlCenter);
    }

    #endregion

    #region Top Selling Report

    private static float DrawTopSellingReport(Graphics g, ReportContext ctx, float rightX, float y,
        float contentWidth, float leftX, float bottomEdge,
        Font normalFont, Font boldFont, Font sectionFont, Font grandFont, Font smallFont, Font headerFont)
    {
        var data = ctx.TopSellingData;
        if (data == null) return y;

        y = DrawSectionTitle(g, $"الأكثر مبيعاً ({data.Count} صنف)", rightX, y, contentWidth, leftX, sectionFont);
        y += 0.06f;

        // رأس الجدول
        float colIndex = 0.3f;
        float colName = contentWidth * 0.20f;
        float colPartNum = contentWidth * 0.13f;
        float colPrice = 0.5f;
        float colQty = 0.4f;
        float colReturned = 0.4f;
        float colNetQty = 0.4f;
        float colDiscount = 0.5f;
        float colNetRevenue = 0.75f;
        float remaining = contentWidth - colIndex - colName - colPartNum - colPrice - colQty - colReturned - colNetQty - colDiscount - colNetRevenue;
        colName += remaining;

        float rowH = 0.28f;

        // رسم رأس الجدول
        DrawFilledRect(g, leftX, y, contentWidth, rowH, HeaderBg);
        float textY = y + 0.05f;
        float x = rightX;

        using var headerBrush = new SolidBrush(MediumGray);

        x -= colIndex;
        g.DrawString("#", headerFont, headerBrush, new RectangleF(x, textY, colIndex, 0.2f), RtlCenter);
        x -= colName;
        g.DrawString("المنتج", headerFont, headerBrush, new RectangleF(x, textY, colName, 0.2f), RtlCenter);
        x -= colPartNum;
        g.DrawString("رقم القطعة", headerFont, headerBrush, new RectangleF(x, textY, colPartNum, 0.2f), RtlCenter);
        x -= colPrice;
        g.DrawString("السعر", headerFont, headerBrush, new RectangleF(x, textY, colPrice, 0.2f), RtlCenter);
        x -= colQty;
        g.DrawString("المباعة", headerFont, headerBrush, new RectangleF(x, textY, colQty, 0.2f), RtlCenter);
        x -= colReturned;
        g.DrawString("المرتجع", headerFont, headerBrush, new RectangleF(x, textY, colReturned, 0.2f), RtlCenter);
        x -= colNetQty;
        g.DrawString("الصافي", headerFont, headerBrush, new RectangleF(x, textY, colNetQty, 0.2f), RtlCenter);
        x -= colDiscount;
        g.DrawString("الخصم", headerFont, headerBrush, new RectangleF(x, textY, colDiscount, 0.2f), RtlCenter);
        x -= colNetRevenue;
        g.DrawString("صافي الإيرادات", headerFont, headerBrush, new RectangleF(x, textY, colNetRevenue, 0.2f), RtlCenter);

        y += rowH;
        DrawLine(g, leftX, y, rightX, y, BorderColor, 0.005f);

        // صفوف البيانات
        using var dataBrush = new SolidBrush(DarkText);
        using var boldBrush = new SolidBrush(DarkBlue);
        using var returnedBrush = new SolidBrush(RedColor);
        using var netBrush = new SolidBrush(GreenColor);

        for (int i = ctx.ItemIndex; i < data.Count; i++)
        {
            if (y + rowH > bottomEdge)
            {
                ctx.ItemIndex = i;
                // Note: in this simple version we don't support multi-page for table data
                break;
            }

            var item = data[i];
            if (i % 2 == 1)
                DrawFilledRect(g, leftX, y, contentWidth, rowH, AltRowBg);

            textY = y + 0.04f;
            x = rightX;

            x -= colIndex;
            g.DrawString((i + 1).ToString(), smallFont, dataBrush, new RectangleF(x, textY, colIndex, 0.2f), RtlCenter);
            x -= colName;
            g.DrawString(item.PartName, normalFont, dataBrush, new RectangleF(x, textY, colName, 0.2f), RtlNear);
            x -= colPartNum;
            g.DrawString(item.PartNumber, smallFont, dataBrush, new RectangleF(x, textY, colPartNum, 0.2f), RtlCenter);
            x -= colPrice;
            g.DrawString($"{item.UnitPrice:N2}", smallFont, dataBrush, new RectangleF(x, textY, colPrice, 0.2f), RtlCenter);
            x -= colQty;
            g.DrawString(item.TotalQuantitySold.ToString(), normalFont, dataBrush, new RectangleF(x, textY, colQty, 0.2f), RtlCenter);
            x -= colReturned;
            var retText = item.ReturnedQuantity > 0 ? item.ReturnedQuantity.ToString() : "---";
            g.DrawString(retText, normalFont, item.ReturnedQuantity > 0 ? returnedBrush : dataBrush, new RectangleF(x, textY, colReturned, 0.2f), RtlCenter);
            x -= colNetQty;
            g.DrawString(item.NetQuantitySold.ToString(), boldFont, boldBrush, new RectangleF(x, textY, colNetQty, 0.2f), RtlCenter);
            x -= colDiscount;
            var discText = item.TotalDiscount > 0 ? $"{item.TotalDiscount:N2}" : "---";
            g.DrawString(discText, smallFont, item.TotalDiscount > 0 ? returnedBrush : dataBrush, new RectangleF(x, textY, colDiscount, 0.2f), RtlCenter);
            x -= colNetRevenue;
            g.DrawString($"{item.NetRevenue:N2} {ctx.CurrencySymbol}", boldFont, netBrush, new RectangleF(x, textY, colNetRevenue, 0.2f), RtlCenter);

            y += rowH;
            DrawLine(g, leftX, y, rightX, y, BorderColor, 0.003f);
        }

        ctx.ItemIndex = data.Count;
        return y;
    }

    #endregion

    #region Returns Report

    private static float DrawReturnsReport(Graphics g, ReportContext ctx, float rightX, float y,
        float contentWidth, float leftX, float bottomEdge,
        Font normalFont, Font boldFont, Font sectionFont, Font grandFont, Font smallFont, Font headerFont)
    {
        var data = ctx.ReturnsData;
        if (data == null) return y;

        y = DrawSectionTitle(g, $"المرتجعات ({data.Count} سجل)", rightX, y, contentWidth, leftX, sectionFont);
        y += 0.06f;

        // رأس الجدول
        float colIndex = 0.3f;
        float colRetNum = contentWidth * 0.12f;
        float colInvNum = contentWidth * 0.12f;
        float colDate = 0.65f;
        float colProduct = contentWidth * 0.18f;
        float colQty = 0.4f;
        float colAmount = 0.65f;
        float colReason = contentWidth - colIndex - colRetNum - colInvNum - colDate - colProduct - colQty - colAmount;

        float rowH = 0.28f;

        // رسم رأس الجدول
        DrawFilledRect(g, leftX, y, contentWidth, rowH, ReturnLightBg);
        float textY = y + 0.05f;
        float x = rightX;

        using var headerBrush = new SolidBrush(ReturnRed);

        x -= colIndex;
        g.DrawString("#", headerFont, headerBrush, new RectangleF(x, textY, colIndex, 0.2f), RtlCenter);
        x -= colRetNum;
        g.DrawString("رقم المرتجع", headerFont, headerBrush, new RectangleF(x, textY, colRetNum, 0.2f), RtlCenter);
        x -= colInvNum;
        g.DrawString("رقم الفاتورة", headerFont, headerBrush, new RectangleF(x, textY, colInvNum, 0.2f), RtlCenter);
        x -= colDate;
        g.DrawString("التاريخ", headerFont, headerBrush, new RectangleF(x, textY, colDate, 0.2f), RtlCenter);
        x -= colProduct;
        g.DrawString("المنتج", headerFont, headerBrush, new RectangleF(x, textY, colProduct, 0.2f), RtlCenter);
        x -= colQty;
        g.DrawString("الكمية", headerFont, headerBrush, new RectangleF(x, textY, colQty, 0.2f), RtlCenter);
        x -= colAmount;
        g.DrawString("المبلغ", headerFont, headerBrush, new RectangleF(x, textY, colAmount, 0.2f), RtlCenter);
        x -= colReason;
        g.DrawString("السبب", headerFont, headerBrush, new RectangleF(x, textY, colReason, 0.2f), RtlCenter);

        y += rowH;
        DrawLine(g, leftX, y, rightX, y, ReturnRed, 0.005f);

        // صفوف البيانات
        using var dataBrush = new SolidBrush(DarkText);
        using var redBrush = new SolidBrush(ReturnRed);

        for (int i = ctx.ItemIndex; i < data.Count; i++)
        {
            if (y + rowH > bottomEdge)
            {
                ctx.ItemIndex = i;
                break;
            }

            var item = data[i];
            if (i % 2 == 1)
                DrawFilledRect(g, leftX, y, contentWidth, rowH, ReturnLightBg);

            textY = y + 0.04f;
            x = rightX;

            x -= colIndex;
            g.DrawString((i + 1).ToString(), smallFont, dataBrush, new RectangleF(x, textY, colIndex, 0.2f), RtlCenter);
            x -= colRetNum;
            g.DrawString(item.ReturnNumber, smallFont, dataBrush, new RectangleF(x, textY, colRetNum, 0.2f), RtlNear);
            x -= colInvNum;
            g.DrawString(item.InvoiceNumber ?? "---", smallFont, dataBrush, new RectangleF(x, textY, colInvNum, 0.2f), RtlNear);
            x -= colDate;
            g.DrawString(item.ReturnDate.ToString("yyyy/MM/dd"), smallFont, dataBrush, new RectangleF(x, textY, colDate, 0.2f), RtlCenter);
            x -= colProduct;
            g.DrawString(item.PartName, smallFont, dataBrush, new RectangleF(x, textY, colProduct, 0.2f), RtlNear);
            x -= colQty;
            g.DrawString(item.Quantity.ToString(), normalFont, redBrush, new RectangleF(x, textY, colQty, 0.2f), RtlCenter);
            x -= colAmount;
            g.DrawString($"{item.RefundAmount:N2}", normalFont, redBrush, new RectangleF(x, textY, colAmount, 0.2f), RtlCenter);
            x -= colReason;
            g.DrawString(item.Reason ?? "---", smallFont, dataBrush, new RectangleF(x, textY, colReason, 0.2f), RtlNear);

            y += rowH;
            DrawLine(g, leftX, y, rightX, y, BorderColor, 0.003f);
        }

        ctx.ItemIndex = data.Count;

        // ملخص المرتجعات
        y += 0.08f;
        var totalReturns = data.Sum(r => r.RefundAmount);
        var totalQty = data.Sum(r => r.Quantity);
        DrawLine(g, leftX, y, rightX, y, ReturnRed, 0.008f);
        y += 0.06f;

        y = DrawLabelValue(g, "إجمالي عدد المرتجعات", data.Count.ToString(),
            normalFont, boldFont, MediumGray, ReturnRed, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "إجمالي الكميات المرتجعة", totalQty.ToString(),
            normalFont, boldFont, MediumGray, ReturnRed, rightX, y, contentWidth, 0.55f);
        y = DrawLabelValue(g, "إجمالي مبالغ المرتجعات", $"{totalReturns:N2} {ctx.CurrencySymbol}",
            boldFont, grandFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);

        return y;
    }

    #endregion

    #region Drawing Helpers (shared with InvoicePrintHelper pattern)

    private static float DrawRtlText(Graphics g, string text, Font font, Color color,
        float rightX, float y, float maxWidth, StringFormat format)
    {
        using var brush = new SolidBrush(color);
        var layoutRect = new RectangleF(rightX - maxWidth, y, maxWidth, 999f);
        var size = g.MeasureString(text, font, new SizeF(maxWidth, 999f), format);
        layoutRect.Height = size.Height;
        g.DrawString(text, font, brush, layoutRect, format);
        return y + size.Height;
    }

    private static void DrawLine(Graphics g, float x1, float y1, float x2, float y2,
        Color color, float thickness)
    {
        using var pen = new Pen(color, thickness);
        g.DrawLine(pen, x1, y1, x2, y2);
    }

    private static void DrawDashedLine(Graphics g, float x1, float y1, float x2, float y2, Color color)
    {
        using var pen = new Pen(color, 0.005f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        g.DrawLine(pen, x1, y1, x2, y2);
    }

    private static void DrawFilledRect(Graphics g, float x, float y, float width, float height, Color color)
    {
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, x, y, width, height);
    }

    private static float DrawLabelValue(Graphics g, string label, string value,
        Font labelFont, Font valueFont, Color labelColor, Color valueColor,
        float rightX, float y, float totalWidth, float labelRatio)
    {
        float labelWidth = totalWidth * labelRatio;
        float valueWidth = totalWidth * (1f - labelRatio);

        using var lBrush = new SolidBrush(labelColor);
        using var vBrush = new SolidBrush(valueColor);

        var labelRect = new RectangleF(rightX - labelWidth, y, labelWidth, 0.3f);
        g.DrawString(label, labelFont, lBrush, labelRect, RtlNear);

        var valueRect = new RectangleF(rightX - totalWidth, y, valueWidth, 0.3f);
        g.DrawString(value, valueFont, vBrush, valueRect, RtlNear);

        float h = Math.Max(labelFont.GetHeight(g), valueFont.GetHeight(g));
        return y + h + 0.02f;
    }

    private static float DrawSectionTitle(Graphics g, string title, float rightX, float y,
        float contentWidth, float leftX, Font sectionFont)
    {
        // خلفية العنوان
        float h = sectionFont.GetHeight(g) + 0.12f;
        DrawFilledRect(g, leftX, y, contentWidth, h, DarkBlue);

        // نص العنوان
        using var whiteBrush = new SolidBrush(Color.White);
        var rect = new RectangleF(rightX - contentWidth, y + 0.04f, contentWidth, h - 0.04f);
        g.DrawString(title, sectionFont, whiteBrush, rect, RtlCenter);

        return y + h;
    }

    #endregion
}
