using AutoPartsShop.Application.Common;
using AutoPartsShop.Application.DTOs.SpareParts;

namespace AutoPartsShop.Application.Interfaces;

public interface ISparePartService
{
    Task<SparePartDto> CreateAsync(CreateSparePartDto dto);
    Task<SparePartDto> UpdateAsync(UpdateSparePartDto dto);
    Task DeleteAsync(int id);
    Task<SparePartDto?> GetByIdAsync(int id);
    Task<SparePartDto?> GetByBarcodeAsync(string barcode);
    Task<SparePartDto?> GetByPartNumberAsync(string partNumber);
    Task<PaginatedResult<SparePartDto>> SearchAsync(SparePartSearchDto search);
    Task<IReadOnlyList<SparePartDto>> GetLowStockAsync();
    Task<IReadOnlyList<SparePartDto>> GetAllAsync();
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync();
}