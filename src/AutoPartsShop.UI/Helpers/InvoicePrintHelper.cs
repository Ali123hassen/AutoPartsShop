using AutoPartsShop.Application.DTOs.Invoices;
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Windows;
using Brush = System.Drawing.Brush;
// Resolve conflicts between System.Drawing and System.Windows/System.Windows.Media
// (caused by <UseWindowsForms>true</UseWindowsForms> + <UseWPF>true</UseWPF> in project file)
using Color = System.Drawing.Color;
using FontFamily = System.Drawing.FontFamily;
using FontStyle = System.Drawing.FontStyle;
// WPF types that conflict with System.Drawing/System.Windows.Forms
using MessageBox = System.Windows.MessageBox;
using SolidBrush = System.Drawing.SolidBrush;
using Window = System.Windows.Window;
using WpfPrintDialog = System.Windows.Controls.PrintDialog;

namespace AutoPartsShop.UI.Helpers;

/// <summary>
/// Generates and prints invoices with Arabic RTL support using GDI+ (System.Drawing).
/// WPF FlowDocument has a known bug where RTL Arabic text renders as vertical columns
/// when printed, so we use System.Drawing.Printing.PrintDocument with GDI+ text
/// rendering which handles Arabic correctly via StringFormatFlags.DirectionRightToLeft.
/// Supports both A4 (regular printer) and 80mm thermal receipt formats.
/// </summary>
public static class InvoicePrintHelper
{
    #region Print Context

    private class PrintContext
    {
        public InvoiceDto Invoice = null!;
        public string ShopName = string.Empty;
        public string ShopAddress = string.Empty;
        public string ShopPhone = string.Empty;
        public string ShopLogoPath = string.Empty;
        public string CurrencySymbol = string.Empty;
        public string TaxLabel = string.Empty;
        public bool IsThermal;
        public bool ShowCostAndPrice;
        public int ItemIndex;
        public bool TotalsPrinted;
        public bool HeaderDrawn;
    }

    private static PrintContext? _ctx;

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
    private static readonly Color ReturnDarkRed = Color.FromArgb(127, 29, 29);
    private static readonly Color ReturnBg = Color.FromArgb(254, 226, 226);

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

    #region Public API

    public static void ShowPrintDialog(
        InvoiceDto invoice,
        string shopName,
        string shopAddress,
        string shopPhone,
        string shopLogoPath,
        string currencySymbol,
        string taxLabel,
        bool showCostAndPrice,
        Window owner)
    {
        var result = MessageBox.Show(
            owner,
            "اختر نوع الطباعة:\n\nنعم = طباعة A4 (ورق عادي)\nلا = طباعة حرارية (80mm)",
            "نوع الطباعة",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result == MessageBoxResult.Yes)
        {
            PrintInvoiceA4(invoice, shopName, shopAddress, shopPhone, shopLogoPath, currencySymbol, taxLabel, showCostAndPrice);
        }
        else if (result == MessageBoxResult.No)
        {
            PrintInvoiceThermal(invoice, shopName, shopAddress, shopPhone, shopLogoPath, currencySymbol, taxLabel, showCostAndPrice);
        }
    }

    public static void PrintInvoiceA4(
        InvoiceDto invoice,
        string shopName,
        string shopAddress,
        string shopPhone,
        string shopLogoPath,
        string currencySymbol,
        string taxLabel,
        bool showCostAndPrice = false)
    {
        _ctx = new PrintContext
        {
            Invoice = invoice,
            ShopName = shopName,
            ShopAddress = shopAddress,
            ShopPhone = shopPhone,
            ShopLogoPath = shopLogoPath,
            CurrencySymbol = currencySymbol,
            TaxLabel = taxLabel,
            IsThermal = false,
            ShowCostAndPrice = showCostAndPrice,
            ItemIndex = 0,
            TotalsPrinted = false,
            HeaderDrawn = false
        };

        try
        {
            // Show WPF PrintDialog for printer selection
            var wpfDialog = new WpfPrintDialog();
            if (wpfDialog.ShowDialog() != true) return;

            var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = wpfDialog.PrintQueue.FullName;
            printDoc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
            printDoc.DefaultPageSettings.Landscape = false;
            printDoc.PrintController = new StandardPrintController();

            printDoc.PrintPage += OnA4PrintPage;
            printDoc.Print();
            printDoc.PrintPage -= OnA4PrintPage;
        }
        finally
        {
            _ctx = null;
        }
    }

    public static void PrintInvoiceThermal(
        InvoiceDto invoice,
        string shopName,
        string shopAddress,
        string shopPhone,
        string shopLogoPath,
        string currencySymbol,
        string taxLabel,
        bool showCostAndPrice = false)
    {
        _ctx = new PrintContext
        {
            Invoice = invoice,
            ShopName = shopName,
            ShopAddress = shopAddress,
            ShopPhone = shopPhone,
            ShopLogoPath = shopLogoPath,
            CurrencySymbol = currencySymbol,
            TaxLabel = taxLabel,
            IsThermal = true,
            ShowCostAndPrice = showCostAndPrice,
            ItemIndex = 0,
            TotalsPrinted = false,
            HeaderDrawn = false
        };

        try
        {
            var wpfDialog = new WpfPrintDialog();
            if (wpfDialog.ShowDialog() != true) return;

            var printDoc = new PrintDocument();
            printDoc.PrinterSettings.PrinterName = wpfDialog.PrintQueue.FullName;
            printDoc.DefaultPageSettings.PaperSize = new PaperSize("80mm", 315, 1170);
            printDoc.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);
            printDoc.DefaultPageSettings.Landscape = false;
            printDoc.PrintController = new StandardPrintController();

            printDoc.PrintPage += OnThermalPrintPage;
            printDoc.Print();
            printDoc.PrintPage -= OnThermalPrintPage;
        }
        finally
        {
            _ctx = null;
        }
    }

    #endregion

    #region A4 Print Page Handler

    private static void OnA4PrintPage(object? sender, PrintPageEventArgs e)
    {
        if (_ctx == null) { e.HasMorePages = false; return; }

        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PageUnit = GraphicsUnit.Inch;

        // Convert from hundredths of an inch to inches
        float pageWidth = e.PageSettings.PaperSize.Width / 100f;
        float pageHeight = e.PageSettings.PaperSize.Height / 100f;
        float leftMargin = e.PageSettings.Margins.Left / 100f;
        float rightMargin = e.PageSettings.Margins.Right / 100f;
        float topMargin = e.PageSettings.Margins.Top / 100f;
        float bottomEdge = pageHeight - e.PageSettings.Margins.Bottom / 100f;

        float contentWidth = pageWidth - leftMargin - rightMargin;
        float rightEdge = pageWidth - rightMargin;

        float y;

        if (!_ctx.HeaderDrawn)
        {
            y = topMargin;

            // === Shop Header (Logo + Name) ===
            using (var titleFont = new Font("Tahoma", 18f, FontStyle.Bold, GraphicsUnit.Point))
            using (var infoFont = new Font("Tahoma", 11f, FontStyle.Regular, GraphicsUnit.Point))
            {
                // Draw logo if available
                bool hasLogo = !string.IsNullOrWhiteSpace(_ctx.ShopLogoPath) && System.IO.File.Exists(_ctx.ShopLogoPath);
                float logoSize = 0.85f; // inches - حجم أكبر لعرض الشعار بشكل كامل
                float logoAreaWidth = hasLogo ? logoSize + 0.15f : 0f;

                if (hasLogo)
                {
                    try
                    {
                        using var logoImage = System.Drawing.Image.FromFile(_ctx.ShopLogoPath);
                        // Draw logo on the right side (RTL), text on the left
                        float logoX = rightEdge - logoSize;
                        float logoY = y;
                        DrawLogoUniform(g, logoImage, logoX, logoY, logoSize);
                    }
                    catch { hasLogo = false; logoAreaWidth = 0f; }
                }

                // Shop name centered (adjusted if logo present)
                float textWidth = hasLogo ? contentWidth - logoAreaWidth : contentWidth;
                float textRightX = hasLogo ? rightEdge - logoAreaWidth : rightEdge;

                y = DrawRtlText(g, _ctx.ShopName, titleFont, DarkBlue, textRightX, y, textWidth, RtlCenter);
                y += 0.03f;

                if (!string.IsNullOrWhiteSpace(_ctx.ShopAddress))
                    y = DrawRtlText(g, _ctx.ShopAddress, infoFont, MediumGray, textRightX, y, textWidth, RtlCenter);

                if (!string.IsNullOrWhiteSpace(_ctx.ShopPhone))
                    y = DrawRtlText(g, _ctx.ShopPhone, infoFont, MediumGray, textRightX, y, textWidth, RtlCenter);

                // Ensure y is past the logo
                float logoBottom = topMargin + logoSize;
                if (hasLogo && y < logoBottom)
                    y = logoBottom;
            }

            y += 0.08f;
            DrawLine(g, leftMargin, y, rightEdge, y, BorderColor, 0.008f);
            y += 0.12f;

            // === Invoice Info ===
            using (var labelFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
            using (var valueFont = new Font("Tahoma", 10f, FontStyle.Regular, GraphicsUnit.Point))
            {
                y = DrawA4InvoiceInfo(g, _ctx, rightEdge, y, contentWidth, labelFont, valueFont);
            }

            y += 0.05f;
            DrawLine(g, leftMargin, y, rightEdge, y, BorderColor, 0.008f);
            y += 0.12f;

            // === Items Table Header ===
            using (var headerFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
            {
                y = DrawA4TableHeader(g, rightEdge, y, contentWidth, leftMargin, headerFont);
            }

            _ctx.HeaderDrawn = true;
        }
        else
        {
            // Continuation page
            y = topMargin;
            using (var headerFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
            {
                y = DrawA4TableHeader(g, rightEdge, y, contentWidth, leftMargin, headerFont);
            }
        }

        // Draw items
        using (var normalFont = new Font("Tahoma", 10f, FontStyle.Regular, GraphicsUnit.Point))
        using (var boldFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
        {
            var result = DrawA4Items(g, _ctx, rightEdge, y, contentWidth, leftMargin, bottomEdge, normalFont, boldFont);
            if (result < 0)
            {
                e.HasMorePages = true;
                return;
            }
            y = result;
        }

        // Check room for totals
        if (y + 2.0f > bottomEdge)
        {
            e.HasMorePages = true;
            return;
        }

        if (!_ctx.TotalsPrinted)
        {
            y += 0.08f;
            DrawLine(g, leftMargin, y, rightEdge, y, BorderColor, 0.008f);
            y += 0.12f;

            using (var normalFont = new Font("Tahoma", 10f, FontStyle.Regular, GraphicsUnit.Point))
            using (var boldFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
            using (var grandFont = new Font("Tahoma", 14f, FontStyle.Bold, GraphicsUnit.Point))
            using (var smallFont = new Font("Tahoma", 9f, FontStyle.Regular, GraphicsUnit.Point))
            {
                y = DrawTotalsSection(g, _ctx, rightEdge, y, contentWidth, leftMargin, normalFont, boldFont, grandFont);
                y = DrawPaidChangeSection(g, _ctx, rightEdge, y, contentWidth, boldFont);
                y = DrawReturnSection(g, _ctx, rightEdge, y, contentWidth, leftMargin, normalFont, boldFont, grandFont);

                y += 0.08f;
                DrawLine(g, leftMargin, y, rightEdge, y, BorderColor, 0.008f);
                y += 0.12f;
                y = DrawRtlText(g, "شكراً لتعاملكم معنا", boldFont, DarkBlue, rightEdge, y, contentWidth, RtlCenter);
                y += 0.02f;
                DrawRtlText(g, "نتمنى لكم يوماً سعيداً", smallFont, MediumGray, rightEdge, y, contentWidth, RtlCenter);
            }

            _ctx.TotalsPrinted = true;
        }

        e.HasMorePages = false;
    }

    #endregion

    #region Thermal Print Page Handler

    private static void OnThermalPrintPage(object? sender, PrintPageEventArgs e)
    {
        if (_ctx == null) { e.HasMorePages = false; return; }

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

        float y = topMargin;

        using (var titleFont = new Font("Tahoma", 12f, FontStyle.Bold, GraphicsUnit.Point))
        using (var infoFont = new Font("Tahoma", 9f, FontStyle.Regular, GraphicsUnit.Point))
        using (var normalFont = new Font("Tahoma", 9f, FontStyle.Regular, GraphicsUnit.Point))
        using (var boldFont = new Font("Tahoma", 9f, FontStyle.Bold, GraphicsUnit.Point))
        using (var smallFont = new Font("Tahoma", 8f, FontStyle.Regular, GraphicsUnit.Point))
        using (var grandFont = new Font("Tahoma", 11f, FontStyle.Bold, GraphicsUnit.Point))
        {
            // Shop Header (compact) with logo
            bool hasLogo = !string.IsNullOrWhiteSpace(_ctx.ShopLogoPath) && System.IO.File.Exists(_ctx.ShopLogoPath);
            float thermalLogoSize = 0.55f; // inches - حجم أكبر لعرض الشعار بشكل كامل

            if (hasLogo)
            {
                try
                {
                    using var logoImage = System.Drawing.Image.FromFile(_ctx.ShopLogoPath);
                    // Center the logo
                    float logoX = rightEdge - contentWidth / 2f - thermalLogoSize / 2f;
                    DrawLogoUniform(g, logoImage, logoX, y, thermalLogoSize);
                    y += thermalLogoSize + 0.03f;
                }
                catch { hasLogo = false; }
            }

            y = DrawRtlText(g, _ctx.ShopName, titleFont, DarkBlue, rightEdge, y, contentWidth, RtlCenter);
            y += 0.02f;

            if (!string.IsNullOrWhiteSpace(_ctx.ShopAddress))
                y = DrawRtlText(g, _ctx.ShopAddress, infoFont, MediumGray, rightEdge, y, contentWidth, RtlCenter);

            if (!string.IsNullOrWhiteSpace(_ctx.ShopPhone))
                y = DrawRtlText(g, _ctx.ShopPhone, infoFont, MediumGray, rightEdge, y, contentWidth, RtlCenter);

            y += 0.04f;
            DrawDashedLine(g, leftMargin, y, rightEdge, y, LightGray);
            y += 0.08f;

            // Invoice Info (compact)
            y = DrawThermalInvoiceInfo(g, _ctx, rightEdge, y, contentWidth, boldFont, normalFont);

            y += 0.02f;
            DrawDashedLine(g, leftMargin, y, rightEdge, y, LightGray);
            y += 0.08f;

            // Items (compact)
            y = DrawThermalItems(g, _ctx, rightEdge, y, contentWidth, bottomEdge, boldFont, normalFont);

            y += 0.04f;
            DrawDashedLine(g, leftMargin, y, rightEdge, y, LightGray);
            y += 0.08f;

            // Totals
            y = DrawTotalsSection(g, _ctx, rightEdge, y, contentWidth, leftMargin, normalFont, boldFont, grandFont);

            // Paid / Change
            y = DrawPaidChangeSection(g, _ctx, rightEdge, y, contentWidth, boldFont);

            // Return Financial Summary
            y = DrawReturnSection(g, _ctx, rightEdge, y, contentWidth, leftMargin, normalFont, boldFont, grandFont);

            // Footer
            y += 0.04f;
            DrawDashedLine(g, leftMargin, y, rightEdge, y, LightGray);
            y += 0.08f;
            y = DrawRtlText(g, "شكراً لتعاملكم معنا", boldFont, DarkBlue, rightEdge, y, contentWidth, RtlCenter);
            y += 0.02f;
            DrawRtlText(g, "نتمنى لكم يوماً سعيداً", smallFont, MediumGray, rightEdge, y, contentWidth, RtlCenter);
        }

        e.HasMorePages = false;
    }

    #endregion

    #region Drawing Helpers

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

    /// <summary>
    /// يرسم صورة اللوقو مع الحفاظ على نسبة العرض للارتفاع (Uniform)
    /// ومركزها داخل المساحة المحددة
    /// يستخدم مصدر ووجهة صريحين لتجنب مشاكل DPI
    /// </summary>
    private static void DrawLogoUniform(Graphics g, System.Drawing.Image logo, float areaX, float areaY, float areaSize)
    {
        float imgW = logo.Width;
        float imgH = logo.Height;
        float aspect = imgW / imgH;

        float drawW, drawH;
        if (aspect >= 1f)
        {
            // صورة عريضة - العرض هو المحدد
            drawW = areaSize;
            drawH = areaSize / aspect;
        }
        else
        {
            // صورة طويلة - الارتفاع هو المحدد
            drawH = areaSize;
            drawW = areaSize * aspect;
        }

        // توسيط داخل المساحة
        float drawX = areaX + (areaSize - drawW) / 2f;
        float drawY = areaY + (areaSize - drawH) / 2f;

        // استخدام مصدر ووجهة صريحين لضمان رسم الصورة بالكامل
        var destRect = new RectangleF(drawX, drawY, drawW, drawH);
        var srcRect = new RectangleF(0f, 0f, imgW, imgH);

        var oldMode = g.InterpolationMode;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.DrawImage(logo, destRect, srcRect, GraphicsUnit.Pixel);
        g.InterpolationMode = oldMode;
    }

    /// <summary>
    /// Draws a label-value pair in RTL direction. Label on the right, value on the left.
    /// </summary>
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

    #endregion

    #region A4 Invoice Info

    private static float DrawA4InvoiceInfo(Graphics g, PrintContext ctx, float rightX, float y,
        float contentWidth, Font labelFont, Font valueFont)
    {
        var invoice = ctx.Invoice;
        float halfWidth = contentWidth / 2f;

        // Row 1: Invoice number + Date
        DrawLabelValue(g, "رقم الفاتورة:", invoice.InvoiceNumber, labelFont, valueFont,
            MediumGray, DarkText, rightX, y, halfWidth, 0.45f);
        DrawLabelValue(g, "التاريخ:", invoice.InvoiceDate.ToString("yyyy/MM/dd HH:mm"),
            labelFont, valueFont, MediumGray, DarkText, rightX - halfWidth, y, halfWidth, 0.35f);
        y += 0.22f;

        // Row 2: Customer + Cashier
        DrawLabelValue(g, "اسم العميل:", invoice.CustomerName ?? "---",
            labelFont, valueFont, MediumGray, DarkText, rightX, y, halfWidth, 0.45f);
        DrawLabelValue(g, "الكاشير:", invoice.UserName,
            labelFont, valueFont, MediumGray, DarkText, rightX - halfWidth, y, halfWidth, 0.35f);
        y += 0.22f;

        // Row 3: Payment + Status
        var paymentMethod = invoice.PaymentMethod == "Card" ? "بطاقة" : "نقدي";
        var status = invoice.Status switch
        {
            "Completed" => "مكتملة",
            "PartialReturn" => "↩ مرتجع جزئي",
            "FullReturn" => "↩ مرتجع كامل",
            "Cancelled" => "ملغاة",
            _ => invoice.Status
        };
        DrawLabelValue(g, "طريقة الدفع:", paymentMethod,
            labelFont, valueFont, MediumGray, DarkText, rightX, y, halfWidth, 0.45f);
        DrawLabelValue(g, "الحالة:", status,
            labelFont, valueFont, MediumGray, DarkText, rightX - halfWidth, y, halfWidth, 0.35f);
        y += 0.22f;

        // Notes
        if (!string.IsNullOrWhiteSpace(invoice.Notes))
        {
            DrawLabelValue(g, "ملاحظات:", invoice.Notes,
                labelFont, valueFont, MediumGray, DarkText, rightX, y, contentWidth, 0.2f);
            y += 0.22f;
        }

        return y;
    }

    #endregion

    #region A4 Items Table

    private static float DrawA4TableHeader(Graphics g, float rightX, float y, float contentWidth,
        float leftX, Font headerFont)
    {
        // Column widths (inches) from right to left (RTL visual order)
        float colIndex = 0.3f;
        float colProduct = contentWidth * 0.16f;
        float colQty = 0.55f;
        float colReturned = 0.6f;
        float colRemaining = 0.6f;
        float colCost = _ctx?.ShowCostAndPrice == true ? 0.65f : 0f;
        float colSale = _ctx?.ShowCostAndPrice == true ? 0.65f : 0f;
        float colPrice = 0.72f;
        float colDiscount = 0.6f;
        float colTotal = 0.82f;
        float usedWidth = colIndex + colProduct + colQty + colReturned + colRemaining + colCost + colSale + colPrice + colDiscount + colTotal;
        float remaining = contentWidth - usedWidth;
        colProduct += remaining;

        float rowHeight = 0.35f;

        // Header background
        DrawFilledRect(g, leftX, y, contentWidth, rowHeight, HeaderBg);

        float textY = y + 0.07f;
        float x = rightX;

        using var brush = new SolidBrush(MediumGray);

        x -= colIndex;
        g.DrawString("#", headerFont, brush, new RectangleF(x, textY, colIndex, 0.25f), RtlCenter);
        x -= colProduct;
        g.DrawString("المنتج", headerFont, brush, new RectangleF(x, textY, colProduct, 0.25f), RtlCenter);
        x -= colQty;
        g.DrawString("الكمية", headerFont, brush, new RectangleF(x, textY, colQty, 0.25f), RtlCenter);
        x -= colReturned;
        g.DrawString("المرتجع", headerFont, brush, new RectangleF(x, textY, colReturned, 0.25f), RtlCenter);
        x -= colRemaining;
        g.DrawString("المتبقي", headerFont, brush, new RectangleF(x, textY, colRemaining, 0.25f), RtlCenter);
        if (_ctx?.ShowCostAndPrice == true)
        {
            x -= colCost;
            g.DrawString("تكلفة الوحدة", headerFont, brush, new RectangleF(x, textY, colCost, 0.25f), RtlCenter);
            x -= colSale;
            g.DrawString("سعر البيع", headerFont, brush, new RectangleF(x, textY, colSale, 0.25f), RtlCenter);
        }
        x -= colPrice;
        g.DrawString("سعر الوحدة", headerFont, brush, new RectangleF(x, textY, colPrice, 0.25f), RtlCenter);
        x -= colDiscount;
        g.DrawString("الخصم", headerFont, brush, new RectangleF(x, textY, colDiscount, 0.25f), RtlCenter);
        x -= colTotal;
        g.DrawString("الإجمالي", headerFont, brush, new RectangleF(x, textY, colTotal, 0.25f), RtlCenter);

        y += rowHeight;
        DrawLine(g, leftX, y, rightX, y, BorderColor, 0.005f);
        return y;
    }

    private static void GetA4ColumnWidths(float contentWidth, out float colIndex, out float colProduct,
        out float colQty, out float colReturned, out float colRemaining, out float colCost,
        out float colSale, out float colPrice, out float colDiscount, out float colTotal)
    {
        colIndex = 0.3f;
        colProduct = contentWidth * 0.16f;
        colQty = 0.55f;
        colReturned = 0.6f;
        colRemaining = 0.6f;
        colCost = _ctx?.ShowCostAndPrice == true ? 0.65f : 0f;
        colSale = _ctx?.ShowCostAndPrice == true ? 0.65f : 0f;
        colPrice = 0.72f;
        colDiscount = 0.6f;
        colTotal = 0.82f;
        float usedWidth = colIndex + colProduct + colQty + colReturned + colRemaining + colCost + colSale + colPrice + colDiscount + colTotal;
        float remaining = contentWidth - usedWidth;
        colProduct += remaining;
    }

    private static float DrawA4Items(Graphics g, PrintContext ctx, float rightX, float y,
        float contentWidth, float leftX, float bottomEdge, Font normalFont, Font boldFont)
    {
        var invoice = ctx.Invoice;
        GetA4ColumnWidths(contentWidth, out var colIndex, out var colProduct, out var colQty,
            out var colReturned, out var colRemaining, out var colCost, out var colSale,
            out var colPrice, out var colDiscount, out var colTotal);

        float rowHeight = 0.26f;

        using var dataBrush = new SolidBrush(DarkText);
        using var boldBrush = new SolidBrush(DarkText);
        using var returnedBrush = new SolidBrush(RedColor);
        using var remainingBrush = new SolidBrush(GreenColor);
        using var costBrush = new SolidBrush(Color.FromArgb(198, 40, 40));  // Red for cost
        using var saleBrush = new SolidBrush(Color.FromArgb(22, 163, 74));  // Green for sale price

        for (int i = ctx.ItemIndex; i < invoice.Items.Count; i++)
        {
            if (y + rowHeight > bottomEdge)
            {
                ctx.ItemIndex = i;
                return -1; // need more pages
            }

            var item = invoice.Items[i];

            // Alternating background, but use light red for returned items
            if (item.ReturnedQty > 0)
                DrawFilledRect(g, leftX, y, contentWidth, rowHeight, ReturnLightBg);
            else if (i % 2 == 1)
                DrawFilledRect(g, leftX, y, contentWidth, rowHeight, AltRowBg);

            float textY = y + 0.04f;
            float x = rightX;

            // #
            x -= colIndex;
            g.DrawString((i + 1).ToString(), normalFont, dataBrush, new RectangleF(x, textY, colIndex, 0.2f), RtlCenter);
            // Product
            x -= colProduct;
            g.DrawString(item.PartName, normalFont, dataBrush, new RectangleF(x, textY, colProduct, 0.2f), RtlNear);
            // Quantity
            x -= colQty;
            g.DrawString(item.Quantity.ToString(), normalFont, dataBrush, new RectangleF(x, textY, colQty, 0.2f), RtlCenter);
            // Returned Qty
            x -= colReturned;
            var retText = item.ReturnedQty > 0 ? item.ReturnedQty.ToString() : "---";
            var retBrush = item.ReturnedQty > 0 ? returnedBrush : dataBrush;
            g.DrawString(retText, normalFont, retBrush, new RectangleF(x, textY, colReturned, 0.2f), RtlCenter);
            // Remaining Qty
            x -= colRemaining;
            var remText = item.ReturnedQty > 0 ? item.RemainingQty.ToString() : "---";
            var remBrush = item.ReturnedQty > 0 ? remainingBrush : dataBrush;
            g.DrawString(remText, normalFont, remBrush, new RectangleF(x, textY, colRemaining, 0.2f), RtlCenter);
            // Cost Price (conditional)
            if (ctx.ShowCostAndPrice)
            {
                x -= colCost;
                g.DrawString($"{item.CostAtSale:N2} {ctx.CurrencySymbol}", normalFont, costBrush, new RectangleF(x, textY, colCost, 0.2f), RtlCenter);
                x -= colSale;
                g.DrawString($"{item.UnitPrice:N2} {ctx.CurrencySymbol}", normalFont, saleBrush, new RectangleF(x, textY, colSale, 0.2f), RtlCenter);
            }
            // Unit Price
            x -= colPrice;
            g.DrawString($"{item.UnitPrice:N2} {ctx.CurrencySymbol}", normalFont, dataBrush, new RectangleF(x, textY, colPrice, 0.2f), RtlCenter);
            // Discount
            x -= colDiscount;
            var discAmt = item.UnitPrice * (item.DiscountPercent / 100m);
            var discText = discAmt > 0 ? $"{discAmt:N2} {ctx.CurrencySymbol}" : "---";
            g.DrawString(discText, normalFont, dataBrush, new RectangleF(x, textY, colDiscount, 0.2f), RtlCenter);
            // Line Total
            x -= colTotal;
            g.DrawString($"{item.LineTotal:N2} {ctx.CurrencySymbol}", boldFont, boldBrush, new RectangleF(x, textY, colTotal, 0.2f), RtlCenter);

            y += rowHeight;
            DrawLine(g, leftX, y, rightX, y, BorderColor, 0.003f);
        }

        ctx.ItemIndex = invoice.Items.Count;
        return y;
    }

    #endregion

    #region Thermal Invoice Info

    private static float DrawThermalInvoiceInfo(Graphics g, PrintContext ctx, float rightX, float y,
        float contentWidth, Font labelFont, Font valueFont)
    {
        var invoice = ctx.Invoice;
        float labelWidth = contentWidth * 0.32f;
        float valueWidth = contentWidth - labelWidth - 0.02f;

        using var lBrush = new SolidBrush(MediumGray);
        using var vBrush = new SolidBrush(DarkText);

        void Line(string label, string value)
        {
            var labelRect = new RectangleF(rightX - labelWidth, y, labelWidth, 0.15f);
            g.DrawString(label, labelFont, lBrush, labelRect, RtlNear);
            var valueRect = new RectangleF(rightX - contentWidth, y, valueWidth, 0.15f);
            g.DrawString(value, valueFont, vBrush, valueRect, RtlNear);
            y += labelFont.GetHeight(g) + 0.01f;
        }

        Line("فاتورة رقم:", invoice.InvoiceNumber);
        Line("التاريخ:", invoice.InvoiceDate.ToString("yyyy/MM/dd HH:mm"));
        Line("العميل:", invoice.CustomerName ?? "---");
        Line("الكاشير:", invoice.UserName);
        var pm = invoice.PaymentMethod == "Card" ? "بطاقة" : "نقدي";
        Line("الدفع:", pm);
        var statusText = invoice.Status switch
        {
            "Completed" => "مكتملة",
            "PartialReturn" => "↩ مرتجع جزئي",
            "FullReturn" => "↩ مرتجع كامل",
            "Cancelled" => "ملغاة",
            _ => invoice.Status
        };
        Line("الحالة:", statusText);

        return y;
    }

    #endregion

    #region Thermal Items

    private static float DrawThermalItems(Graphics g, PrintContext ctx, float rightX, float y,
        float contentWidth, float bottomEdge, Font boldFont, Font normalFont)
    {
        var invoice = ctx.Invoice;

        using var costBrush = new SolidBrush(Color.FromArgb(198, 40, 40));
        using var saleBrush = new SolidBrush(Color.FromArgb(22, 163, 74));

        for (int i = ctx.ItemIndex; i < invoice.Items.Count; i++)
        {
            if (y + 0.35f > bottomEdge)
            {
                ctx.ItemIndex = i;
                return y;
            }

            var item = invoice.Items[i];

            // Light red background for returned items
            if (item.ReturnedQty > 0)
            {
                float itemHeight = item.ReturnedQty > 0 ? 0.55f : 0.35f;
                DrawFilledRect(g, rightX - contentWidth, y, contentWidth, itemHeight, ReturnLightBg);
            }

            // Item name (with return indicator)
            var namePrefix = item.ReturnedQty > 0 ? "↩ " : "";
            y = DrawRtlText(g, $"{i + 1}. {namePrefix}{item.PartName}", boldFont,
                item.ReturnedQty > 0 ? ReturnRed : DarkText, rightX, y, contentWidth, RtlNear);

            // Detail line
            var detail = $"{item.Quantity} x {item.UnitPrice:N2}";
            var discAmt = item.UnitPrice * (item.DiscountPercent / 100m);
            if (discAmt > 0)
                detail += $" (خصم {discAmt:N2} {ctx.CurrencySymbol})";
            detail += $" = {item.LineTotal:N2} {ctx.CurrencySymbol}";

            y = DrawRtlText(g, detail, normalFont, MediumGray, rightX - 0.06f, y, contentWidth - 0.06f, RtlNear);

            // Cost and Sale price line (conditional)
            if (ctx.ShowCostAndPrice)
            {
                var costLine = $"تكلفة: {item.CostAtSale:N2} {ctx.CurrencySymbol}  |  بيع: {item.UnitPrice:N2} {ctx.CurrencySymbol}";
                y = DrawRtlText(g, costLine, normalFont, Color.FromArgb(198, 40, 40), rightX - 0.06f, y, contentWidth - 0.06f, RtlNear);
            }

            // Return info line (only if there are returns)
            if (item.ReturnedQty > 0)
            {
                var returnInfo = $"مرتجع: {item.ReturnedQty}  |  متبقي: {item.RemainingQty}";
                y = DrawRtlText(g, returnInfo, normalFont, RedColor, rightX - 0.06f, y, contentWidth - 0.06f, RtlNear);
            }

            y += 0.02f;
        }

        ctx.ItemIndex = invoice.Items.Count;
        return y;
    }

    #endregion

    #region Totals Section

    private static float DrawTotalsSection(Graphics g, PrintContext ctx, float rightX, float y,
        float contentWidth, float leftX, Font normalFont, Font boldFont, Font grandFont)
    {
        var invoice = ctx.Invoice;

        // Subtotal
        y = DrawLabelValue(g, "الإجمالي الفرعي", $"{invoice.SubTotal:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);

        // Tax
        y = DrawLabelValue(g, ctx.TaxLabel, $"{invoice.TaxAmount:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, MediumGray, DarkText, rightX, y, contentWidth, 0.55f);

        // Discount
        if (invoice.DiscountAmount > 0)
        {
            y = DrawLabelValue(g, "الخصم", $"-{invoice.DiscountAmount:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, RedColor, RedColor, rightX, y, contentWidth, 0.55f);
        }

        // Separator
        y += 0.04f;
        DrawLine(g, leftX, y, rightX, y, DarkBlue, 0.012f);
        y += 0.06f;

        // Grand total
        y = DrawLabelValue(g, "الإجمالي", $"{invoice.TotalAmount:N2} {ctx.CurrencySymbol}",
            grandFont, grandFont, DarkBlue, DarkBlue, rightX, y, contentWidth, 0.55f);

        return y;
    }

    #endregion

    #region Paid / Change Section

    private static float DrawPaidChangeSection(Graphics g, PrintContext ctx, float rightX, float y,
        float contentWidth, Font boldFont)
    {
        var invoice = ctx.Invoice;

        y = DrawLabelValue(g, "المبلغ المدفوع", $"{invoice.PaidAmount:N2} {ctx.CurrencySymbol}",
            boldFont, boldFont, DarkText, GreenColor, rightX, y, contentWidth, 0.55f);

        if (invoice.ChangeAmount > 0)
        {
            y = DrawLabelValue(g, "الباقي", $"{invoice.ChangeAmount:N2} {ctx.CurrencySymbol}",
                boldFont, boldFont, DarkText, MediumGray, rightX, y, contentWidth, 0.55f);
        }

        return y;
    }

    #endregion

    #region Return Financial Summary Section

    /// <summary>
    /// يرسم قسم تفاصيل المرتجع المالي (الإجمالي الفرعي، الخصم، بعد الخصم، الضريبة، الإجمالي)
    /// يظهر فقط عند وجود مرتجع (ReturnSubTotal > 0)
    /// </summary>
    private static float DrawReturnSection(Graphics g, PrintContext ctx, float rightX, float y,
        float contentWidth, float leftX, Font normalFont, Font boldFont, Font grandFont)
    {
        var invoice = ctx.Invoice;

        // لا ترسم القسم إذا لم يكن هناك مرتجع
        if (invoice.ReturnSubTotal <= 0) return y;

        // فاصل علوي
        y += 0.06f;
        DrawDashedLine(g, leftX, y, rightX, y, ReturnRed);
        y += 0.08f;

        // عنوان القسم
        y = DrawRtlText(g, "↩ تفاصيل المرتجع", boldFont, ReturnRed, rightX, y, contentWidth, RtlNear);
        y += 0.04f;

        // حساب ارتفاع القسم تقريبياً لرسم الخلفية
        float sectionTop = y;
        float rowH = normalFont.GetHeight(g) + 0.02f;
        int rows = 2; // الإجمالي الفرعي + بعد الخصم
        if (invoice.ReturnDiscount > 0) rows++;
        if (invoice.ReturnTax > 0) rows++;
        float totalRowH = grandFont.GetHeight(g) + 0.02f;
        float sectionHeight = rows * rowH + 0.07f + totalRowH + 0.04f;

        // رسم الخلفية الخفيفة
        DrawFilledRect(g, leftX, sectionTop - 0.02f, contentWidth, sectionHeight, ReturnLightBg);

        // الإجمالي الفرعي للمرتجع (قبل الخصم)
        y = DrawLabelValue(g, "الإجمالي الفرعي", $"{invoice.ReturnSubTotal:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, ReturnDarkRed, ReturnDarkRed, rightX, y, contentWidth, 0.55f);

        // خصم المرتجع
        if (invoice.ReturnDiscount > 0)
        {
            y = DrawLabelValue(g, "خصم المرتجع", $"-{invoice.ReturnDiscount:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, ReturnDarkRed, RedColor, rightX, y, contentWidth, 0.55f);
        }

        // المرتجع بعد الخصم
        y = DrawLabelValue(g, "بعد الخصم", $"{invoice.ReturnAfterDiscount:N2} {ctx.CurrencySymbol}",
            normalFont, normalFont, ReturnDarkRed, ReturnDarkRed, rightX, y, contentWidth, 0.55f);

        // ضريبة المرتجع
        if (invoice.ReturnTax > 0)
        {
            y = DrawLabelValue(g, ctx.TaxLabel, $"{invoice.ReturnTax:N2} {ctx.CurrencySymbol}",
                normalFont, normalFont, ReturnDarkRed, ReturnDarkRed, rightX, y, contentWidth, 0.55f);
        }

        // فاصل قبل الإجمالي
        y += 0.02f;
        DrawLine(g, leftX, y, rightX, y, ReturnRed, 0.008f);
        y += 0.05f;

        // إجمالي المرتجع (بارز) بخلفية مميزة
        float totalTop = y - 0.02f;
        float totalHeight = grandFont.GetHeight(g) + 0.06f;
        DrawFilledRect(g, leftX, totalTop, contentWidth, totalHeight, ReturnBg);
        y = DrawLabelValue(g, "إجمالي المرتجع", $"{invoice.ReturnTotal:N2} {ctx.CurrencySymbol}",
            grandFont, grandFont, ReturnRed, ReturnRed, rightX, y, contentWidth, 0.55f);

        return y;
    }

    #endregion
}
