using AutoPartsShop.Application.DTOs.PurchaseInvoices;
using System.Drawing;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.IO;
using Brush = System.Drawing.Brush;
using Color = System.Drawing.Color;
using FontFamily = System.Drawing.FontFamily;
using FontStyle = System.Drawing.FontStyle;
using SolidBrush = System.Drawing.SolidBrush;

namespace AutoPartsShop.UI.Helpers;

/// <summary>
/// طباعة فاتورة المشتريات إلى PDF باستخدام GDI+ مع دعم RTL العربي
/// </summary>
public static class PurchaseInvoicePrintHelper
{
    #region Print Context

    private class PrintContext
    {
        public PurchaseInvoiceDto Invoice = null!;
        public string ShopName = string.Empty;
        public string ShopAddress = string.Empty;
        public string ShopPhone = string.Empty;
        public string ShopLogoPath = string.Empty;
        public string CurrencySymbol = string.Empty;
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
    private static readonly Color BorderColor = Color.FromArgb(226, 232, 240);
    private static readonly Color LightGray = Color.FromArgb(203, 213, 225);

    #endregion

    #region String Formats

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

    public static bool PrintToPdf(
        PurchaseInvoiceDto invoice,
        string shopName,
        string shopAddress,
        string shopPhone,
        string shopLogoPath,
        string currencySymbol)
    {
        _ctx = new PrintContext
        {
            Invoice = invoice,
            ShopName = shopName,
            ShopAddress = shopAddress,
            ShopPhone = shopPhone,
            ShopLogoPath = shopLogoPath,
            CurrencySymbol = currencySymbol,
            ItemIndex = 0,
            TotalsPrinted = false,
            HeaderDrawn = false
        };

        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fileName = $"فاتورة_مشتريات_{invoice.InvoiceNumber.Replace("/", "-")}.pdf";
            var filePath = Path.Combine(desktopPath, fileName);

            var printDoc = new PrintDocument();

            // البحث عن طابعة Microsoft Print to PDF
            var pdfPrinterName = FindPdfPrinter();
            if (pdfPrinterName == null)
            {
                System.Windows.MessageBox.Show(
                    "لم يتم العثور على طابعة PDF. يرجى تثبيت 'Microsoft Print to PDF' من إعدادات Windows.",
                    "خطأ", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }

            printDoc.PrinterSettings.PrinterName = pdfPrinterName;
            printDoc.PrinterSettings.PrintToFile = true;
            printDoc.PrinterSettings.PrintFileName = filePath;
            printDoc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            printDoc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);
            printDoc.DefaultPageSettings.Landscape = false;
            printDoc.PrintController = new StandardPrintController();

            printDoc.PrintPage += OnA4PrintPage;
            printDoc.Print();
            printDoc.PrintPage -= OnA4PrintPage;

            if (File.Exists(filePath))
            {
                System.Windows.MessageBox.Show($"تم حفظ الفاتورة بنجاح في:\n{filePath}", "طباعة PDF",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return true;
            }
            else
            {
                System.Windows.MessageBox.Show("لم يتم حفظ ملف PDF بنجاح", "تنبيه",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"خطأ في تصدير PDF: {ex.Message}", "خطأ",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return false;
        }
        finally
        {
            _ctx = null;
        }
    }

    private static string? FindPdfPrinter()
    {
        var pdfPrinterNames = new[] { "Microsoft Print to PDF", "Microsoft XPS Document Writer", "PDF", "PDFCreator" };

        foreach (var name in pdfPrinterNames)
        {
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                if (printer.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return printer;
            }
        }

        // Fallback: try setting directly
        var ps = new PrinterSettings { PrinterName = "Microsoft Print to PDF" };
        if (ps.IsValid) return "Microsoft Print to PDF";

        return null;
    }

    #endregion

    #region A4 Print Page Handler

    private static void OnA4PrintPage(object? sender, PrintPageEventArgs e)
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
                        float logoX = rightEdge - logoSize;
                        float logoY = y;
                        DrawLogoUniform(g, logoImage, logoX, logoY, logoSize);
                    }
                    catch { hasLogo = false; logoAreaWidth = 0f; }
                }

                float textWidth = hasLogo ? contentWidth - logoAreaWidth : contentWidth;
                float textRightX = hasLogo ? rightEdge - logoAreaWidth : rightEdge;

                y = DrawRtlText(g, _ctx.ShopName, titleFont, DarkBlue, textRightX, y, textWidth, RtlCenter);
                y += 0.03f;

                if (!string.IsNullOrWhiteSpace(_ctx.ShopAddress))
                    y = DrawRtlText(g, _ctx.ShopAddress, infoFont, MediumGray, textRightX, y, textWidth, RtlCenter);

                if (!string.IsNullOrWhiteSpace(_ctx.ShopPhone))
                    y = DrawRtlText(g, _ctx.ShopPhone, infoFont, MediumGray, textRightX, y, textWidth, RtlCenter);

                float logoBottom = topMargin + logoSize;
                if (hasLogo && y < logoBottom)
                    y = logoBottom;
            }

            y += 0.08f;
            DrawLine(g, leftMargin, y, rightEdge, y, BorderColor, 0.008f);
            y += 0.12f;

            // === Invoice Title ===
            using (var subTitleFont = new Font("Tahoma", 14f, FontStyle.Bold, GraphicsUnit.Point))
            {
                y = DrawRtlText(g, "فاتورة مشتريات", subTitleFont, DarkBlue, rightEdge, y, contentWidth, RtlCenter);
                y += 0.08f;
            }

            // === Invoice Info ===
            using (var labelFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
            using (var valueFont = new Font("Tahoma", 10f, FontStyle.Regular, GraphicsUnit.Point))
            {
                var inv = _ctx.Invoice;
                float halfWidth = contentWidth / 2f;

                // Row 1: Invoice number + Date
                DrawLabelValue(g, "رقم الفاتورة:", inv.InvoiceNumber, labelFont, valueFont,
                    MediumGray, DarkText, rightEdge, y, halfWidth, 0.45f);
                DrawLabelValue(g, "التاريخ:", inv.InvoiceDate.ToString("yyyy/MM/dd HH:mm"),
                    labelFont, valueFont, MediumGray, DarkText, rightEdge - halfWidth, y, halfWidth, 0.35f);
                y += 0.22f;

                // Row 2: Supplier name + phone
                DrawLabelValue(g, "اسم المورد:", inv.SupplierName ?? "---",
                    labelFont, valueFont, MediumGray, DarkText, rightEdge, y, halfWidth, 0.45f);
                DrawLabelValue(g, "رقم المورد:", inv.SupplierPhone ?? "---",
                    labelFont, valueFont, MediumGray, DarkText, rightEdge - halfWidth, y, halfWidth, 0.35f);
                y += 0.22f;

                // Row 3: User + Status
                DrawLabelValue(g, "المستخدم:", inv.UserName,
                    labelFont, valueFont, MediumGray, DarkText, rightEdge, y, halfWidth, 0.45f);
                var status = inv.Status == "Cancelled" ? "ملغاة" : "مكتملة";
                DrawLabelValue(g, "الحالة:", status,
                    labelFont, valueFont, MediumGray, DarkText, rightEdge - halfWidth, y, halfWidth, 0.35f);
                y += 0.22f;

                // Notes
                if (!string.IsNullOrWhiteSpace(inv.Notes))
                {
                    DrawLabelValue(g, "ملاحظات:", inv.Notes,
                        labelFont, valueFont, MediumGray, DarkText, rightEdge, y, contentWidth, 0.2f);
                    y += 0.22f;
                }
            }

            y += 0.05f;
            DrawLine(g, leftMargin, y, rightEdge, y, BorderColor, 0.008f);
            y += 0.12f;

            // === Items Table Header ===
            using (var headerFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
            {
                y = DrawTableHeader(g, rightEdge, y, contentWidth, leftMargin, headerFont);
            }

            _ctx.HeaderDrawn = true;
        }
        else
        {
            y = topMargin;
            using (var headerFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
            {
                y = DrawTableHeader(g, rightEdge, y, contentWidth, leftMargin, headerFont);
            }
        }

        // Draw items
        using (var normalFont = new Font("Tahoma", 10f, FontStyle.Regular, GraphicsUnit.Point))
        using (var boldFont = new Font("Tahoma", 10f, FontStyle.Bold, GraphicsUnit.Point))
        {
            var result = DrawItems(g, _ctx, rightEdge, y, contentWidth, leftMargin, bottomEdge, normalFont, boldFont);
            if (result < 0)
            {
                e.HasMorePages = true;
                return;
            }
            y = result;
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
                // Item count
                y = DrawLabelValue(g, "عدد الأصناف:", _ctx.Invoice.ItemsCount.ToString(),
                    normalFont, boldFont, MediumGray, DarkText, rightEdge, y, contentWidth * 0.5f, 0.55f);

                y += 0.08f;
                DrawLine(g, leftMargin, y, rightEdge, y, DarkBlue, 0.012f);
                y += 0.06f;

                // Grand total
                y = DrawLabelValue(g, "الإجمالي", $"{_ctx.Invoice.TotalAmount:N2} {_ctx.CurrencySymbol}",
                    grandFont, grandFont, DarkBlue, DarkBlue, rightEdge, y, contentWidth, 0.55f);

                y += 0.12f;
                DrawLine(g, leftMargin, y, rightEdge, y, BorderColor, 0.008f);
                y += 0.12f;
                DrawRtlText(g, "شكراً لتعاملكم معنا", boldFont, DarkBlue, rightEdge, y, contentWidth, RtlCenter);
                y += 0.02f;
                DrawRtlText(g, "نتمنى لكم يوماً سعيداً", smallFont, MediumGray, rightEdge, y, contentWidth, RtlCenter);
            }

            _ctx.TotalsPrinted = true;
        }

        e.HasMorePages = false;
    }

    #endregion

    #region Table Header & Items

    private static float DrawTableHeader(Graphics g, float rightX, float y, float contentWidth,
        float leftX, Font headerFont)
    {
        float colIndex = 0.3f;
        float colProduct = contentWidth * 0.28f;
        float colQty = 0.45f;
        float colCost = 0.75f;
        float colSale = 0.75f;
        float colMin = 0.75f;
        float colTotal = 0.82f;
        float remaining = contentWidth - colIndex - colProduct - colQty - colCost - colSale - colMin - colTotal;
        colProduct += remaining;

        float rowHeight = 0.3f;
        DrawFilledRect(g, leftX, y, contentWidth, rowHeight, HeaderBg);

        float textY = y + 0.06f;
        float x = rightX;

        using var brush = new SolidBrush(MediumGray);

        x -= colIndex;
        g.DrawString("#", headerFont, brush, new RectangleF(x, textY, colIndex, 0.2f), RtlCenter);
        x -= colProduct;
        g.DrawString("القطعة", headerFont, brush, new RectangleF(x, textY, colProduct, 0.2f), RtlCenter);
        x -= colQty;
        g.DrawString("الكمية", headerFont, brush, new RectangleF(x, textY, colQty, 0.2f), RtlCenter);
        x -= colCost;
        g.DrawString("سعر التكلفة", headerFont, brush, new RectangleF(x, textY, colCost, 0.2f), RtlCenter);
        x -= colSale;
        g.DrawString("سعر البيع", headerFont, brush, new RectangleF(x, textY, colSale, 0.2f), RtlCenter);
        x -= colMin;
        g.DrawString("أدنى سعر", headerFont, brush, new RectangleF(x, textY, colMin, 0.2f), RtlCenter);
        x -= colTotal;
        g.DrawString("الإجمالي", headerFont, brush, new RectangleF(x, textY, colTotal, 0.2f), RtlCenter);

        y += rowHeight;
        DrawLine(g, leftX, y, rightX, y, BorderColor, 0.005f);
        return y;
    }

    private static float DrawItems(Graphics g, PrintContext ctx, float rightX, float y,
        float contentWidth, float leftX, float bottomEdge, Font normalFont, Font boldFont)
    {
        var invoice = ctx.Invoice;

        float colIndex = 0.3f;
        float colProduct = contentWidth * 0.28f;
        float colQty = 0.45f;
        float colCost = 0.75f;
        float colSale = 0.75f;
        float colMin = 0.75f;
        float colTotal = 0.82f;
        float remaining = contentWidth - colIndex - colProduct - colQty - colCost - colSale - colMin - colTotal;
        colProduct += remaining;

        float rowHeight = 0.26f;

        using var dataBrush = new SolidBrush(DarkText);
        using var boldBrush = new SolidBrush(DarkText);
        using var grayBrush = new SolidBrush(MediumGray);

        for (int i = ctx.ItemIndex; i < invoice.Items.Count; i++)
        {
            if (y + rowHeight > bottomEdge)
            {
                ctx.ItemIndex = i;
                return -1;
            }

            var item = invoice.Items[i];

            if (i % 2 == 1)
                DrawFilledRect(g, leftX, y, contentWidth, rowHeight, AltRowBg);

            float textY = y + 0.04f;
            float x = rightX;

            x -= colIndex;
            g.DrawString((i + 1).ToString(), normalFont, dataBrush, new RectangleF(x, textY, colIndex, 0.2f), RtlCenter);
            x -= colProduct;
            g.DrawString(item.PartName, normalFont, dataBrush, new RectangleF(x, textY, colProduct, 0.2f), RtlNear);
            x -= colQty;
            g.DrawString(item.Quantity.ToString(), normalFont, dataBrush, new RectangleF(x, textY, colQty, 0.2f), RtlCenter);
            x -= colCost;
            g.DrawString($"{item.CostPrice:N2}", normalFont, dataBrush, new RectangleF(x, textY, colCost, 0.2f), RtlCenter);
            x -= colSale;
            g.DrawString($"{item.SalePrice:N2}", normalFont, dataBrush, new RectangleF(x, textY, colSale, 0.2f), RtlCenter);
            x -= colMin;
            var minText = item.MinSalePrice.HasValue ? $"{item.MinSalePrice.Value:N2}" : "---";
            g.DrawString(minText, normalFont, grayBrush, new RectangleF(x, textY, colMin, 0.2f), RtlCenter);
            x -= colTotal;
            g.DrawString($"{item.LineTotal:N2} {ctx.CurrencySymbol}", boldFont, boldBrush, new RectangleF(x, textY, colTotal, 0.2f), RtlCenter);

            y += rowHeight;
            DrawLine(g, leftX, y, rightX, y, BorderColor, 0.003f);
        }

        ctx.ItemIndex = invoice.Items.Count;
        return y;
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

    private static void DrawFilledRect(Graphics g, float x, float y, float width, float height, Color color)
    {
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, x, y, width, height);
    }

    /// <summary>
    /// يرسم صورة اللوقو مع الحفاظ على نسبة العرض للارتفاع (Uniform)
    /// ومركزها داخل المساحة المحددة
    /// </summary>
    private static void DrawLogoUniform(Graphics g, System.Drawing.Image logo, float areaX, float areaY, float areaSize)
    {
        float imgW = logo.Width;
        float imgH = logo.Height;
        float aspect = imgW / imgH;

        float drawW, drawH;
        if (aspect >= 1f)
        {
            drawW = areaSize;
            drawH = areaSize / aspect;
        }
        else
        {
            drawH = areaSize;
            drawW = areaSize * aspect;
        }

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
}
