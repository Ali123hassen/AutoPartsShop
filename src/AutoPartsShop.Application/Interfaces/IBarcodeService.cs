namespace AutoPartsShop.Application.Interfaces;

public interface IBarcodeService
{
    string GenerateBarcode(string prefix = "AP");
    byte[] GenerateBarcodeImage(string barcode, int width = 300, int height = 100);
    bool ValidateBarcode(string barcode);
}
