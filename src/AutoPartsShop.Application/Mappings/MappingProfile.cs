using AutoMapper;
using AutoPartsShop.Application.DTOs.Invoices;
using AutoPartsShop.Application.DTOs.PurchaseInvoices;
using AutoPartsShop.Application.DTOs.Returns;
using AutoPartsShop.Application.DTOs.SpareParts;
using AutoPartsShop.Application.DTOs.Stock;
using AutoPartsShop.Core.Entities;

namespace AutoPartsShop.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<SparePart, SparePartDto>()
            .ForMember(d => d.CategoryName, opt => opt.MapFrom(s => s.Category != null ? s.Category.Name : null));

        CreateMap<CreateSparePartDto, SparePart>()
            .ForMember(d => d.PurchasePrice, opt => opt.MapFrom(_ => 0m))
            .ForMember(d => d.SalePrice, opt => opt.MapFrom(_ => 0m))
            .ForMember(d => d.MinSalePrice, opt => opt.MapFrom(_ => (decimal?)null))
            .ForMember(d => d.CurrentStock, opt => opt.MapFrom(_ => 0))
            .ForMember(d => d.MinStockLevel, opt => opt.MapFrom(_ => 5))
            .ForMember(d => d.MaxStockLevel, opt => opt.MapFrom(_ => (int?)null))
            .ForMember(d => d.SupplierName, opt => opt.MapFrom(_ => (string?)null))
            .ForMember(d => d.SupplierPhone, opt => opt.MapFrom(_ => (string?)null))
            .ForMember(d => d.LastPurchaseDate, opt => opt.MapFrom(_ => (DateTime?)null))
            .ForMember(d => d.IsActive, opt => opt.MapFrom(_ => true));
        CreateMap<UpdateSparePartDto, SparePart>()
            .ForMember(d => d.PurchasePrice, opt => opt.Ignore())       // يُدار عبر فواتير المشتريات
            .ForMember(d => d.CurrentStock, opt => opt.Ignore())         // يُدار عبر فواتير المشتريات والمبيعات
            .ForMember(d => d.LastPurchaseDate, opt => opt.Ignore())     // يُحدّث تلقائياً
            .ForMember(d => d.SupplierName, opt => opt.Ignore())         // يُدار عبر فواتير المشتريات
            .ForMember(d => d.SupplierPhone, opt => opt.Ignore())        // يُدار عبر فواتير المشتريات
            .ForMember(d => d.CreatedAt, opt => opt.Ignore())            // لا يُعدّل
            .ForMember(d => d.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow));
        // Category mappings
        CreateMap<Category, CategoryDto>();

        CreateMap<Category, CategoryDto>();

        CreateMap<Core.Entities.User, DTOs.Auth.UserDto>()
            .ForMember(d => d.RoleName, opt => opt.MapFrom(s => s.Role != null ? s.Role.Name : null));

        CreateMap<Role, DTOs.Auth.RoleDto>();

        CreateMap<Invoice, InvoiceDto>()
            .ForMember(d => d.UserName, opt => opt.MapFrom(s => s.User != null ? s.User.Username : null))
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()));

        CreateMap<InvoiceItem, InvoiceItemDto>()
            .ForMember(d => d.PartName, opt => opt.MapFrom(s =>
                !string.IsNullOrEmpty(s.PartName) ? s.PartName :
                (s.SparePart != null ? s.SparePart.Name : null)))
            .ForMember(d => d.ReturnedQty, opt => opt.Ignore())
            .ForMember(d => d.ItemReturnStatus, opt => opt.Ignore())
            .ForMember(d => d.LineNumber, opt => opt.Ignore());

        // Return mappings
        CreateMap<Return, ReturnDto>()
            .ForMember(d => d.InvoiceNumber, opt => opt.MapFrom(s => s.Invoice != null ? s.Invoice.InvoiceNumber : null))
            .ForMember(d => d.ReturnType, opt => opt.MapFrom(s => s.ReturnType.ToString()))
            .ForMember(d => d.PartName, opt => opt.MapFrom(s => s.SparePart != null ? s.SparePart.Name : string.Empty))
            .ForMember(d => d.ReplacementPartName, opt => opt.MapFrom(s => s.ReplacementPart != null ? s.ReplacementPart.Name : null))
            .ForMember(d => d.UserName, opt => opt.MapFrom(s => s.User != null ? s.User.Username : string.Empty));

        // StockMovement mappings
        CreateMap<StockMovement, StockMovementDto>()
            .ForMember(d => d.SparePartName, opt => opt.MapFrom(s => s.SparePart != null ? s.SparePart.Name : string.Empty))
            .ForMember(d => d.UserName, opt => opt.MapFrom(s => s.User != null ? s.User.Username : string.Empty))
            .ForMember(d => d.MovementType, opt => opt.MapFrom(s => s.MovementType.ToString()));

        // BackupHistory mappings
        CreateMap<BackupHistory, BackupHistoryDto>()
            .ForMember(d => d.BackupType, opt => opt.MapFrom(s => s.BackupType == 0 ? "Full" : "Incremental"));

        // PurchaseInvoice mappings
        CreateMap<PurchaseInvoice, PurchaseInvoiceDto>()
            .ForMember(d => d.UserName, opt => opt.MapFrom(s => s.User != null ? s.User.Username : null))
            .ForMember(d => d.Status, opt => opt.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.ItemsCount, opt => opt.MapFrom(s => s.Items != null ? s.Items.Count : 0));

        CreateMap<PurchaseInvoiceItem, PurchaseInvoiceItemDto>();
    }
}
