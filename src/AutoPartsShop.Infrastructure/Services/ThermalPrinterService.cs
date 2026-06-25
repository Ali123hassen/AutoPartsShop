using System.Drawing.Printing;

namespace AutoPartsShop.Infrastructure.Services;

public interface IThermalPrinterService
{
    Task PrintInvoiceAsync(byte[] receiptData, string printerName = "");
    Task PrintBarcodeLabelAsync(byte[] barcodeImage, string printerName = "");
    List<string> GetAvailablePrinters();
}

public class ThermalPrinterService : IThermalPrinterService
{
    public async Task PrintInvoiceAsync(byte[] receiptData, string printerName = "")
    {
        await Task.Run(() =>
        {
            var printer = string.IsNullOrEmpty(printerName) ? GetDefaultPrinter() : printerName;
            if (string.IsNullOrEmpty(printer))
                throw new InvalidOperationException("No printer available.");

            var document = new PrintDocument
            {
                PrinterSettings = new PrinterSettings { PrinterName = printer }
            };

            document.PrintPage += (sender, e) =>
            {
                using var ms = new MemoryStream(receiptData);
                using var image = System.Drawing.Image.FromStream(ms);
                e.Graphics!.DrawImage(image, e.MarginBounds);
            };

            document.Print();
        });
    }

    public async Task PrintBarcodeLabelAsync(byte[] barcodeImage, string printerName = "")
    {
        await Task.Run(() =>
        {
            var printer = string.IsNullOrEmpty(printerName) ? GetDefaultPrinter() : printerName;
            if (string.IsNullOrEmpty(printer))
                throw new InvalidOperationException("No printer available.");

            var document = new PrintDocument
            {
                PrinterSettings = new PrinterSettings { PrinterName = printer }
            };

            document.PrintPage += (sender, e) =>
            {
                using var ms = new MemoryStream(barcodeImage);
                using var image = System.Drawing.Image.FromStream(ms);
                e.Graphics!.DrawImage(image, e.MarginBounds);
            };

            document.Print();
        });
    }

    public List<string> GetAvailablePrinters()
    {
        var printers = new List<string>();
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            printers.Add(printer);
        }
        return printers;
    }

    private static string GetDefaultPrinter()
    {
        var settings = new PrinterSettings();
        return settings.IsValid ? settings.PrinterName : string.Empty;
    }
}
