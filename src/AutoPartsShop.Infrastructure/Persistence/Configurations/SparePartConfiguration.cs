using AutoPartsShop.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoPartsShop.Infrastructure.Persistence.Configurations;

public class SparePartConfiguration : IEntityTypeConfiguration<SparePart>
{
    public void Configure(EntityTypeBuilder<SparePart> builder)
    {
        builder.ToTable("SpareParts");

        builder.Property(sp => sp.PartNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(sp => sp.Barcode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(sp => sp.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(sp => sp.NameAr)
            .HasMaxLength(200);

        builder.Property(sp => sp.Manufacturer)
            .HasMaxLength(200);

        builder.Property(sp => sp.PurchasePrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(sp => sp.SalePrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(sp => sp.MinSalePrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(sp => sp.CurrentStock)
            .HasDefaultValue(0);

        builder.Property(sp => sp.MinStockLevel)
            .HasDefaultValue(5);

        builder.Property(sp => sp.Location)
            .HasMaxLength(100);

        builder.Property(sp => sp.Unit)
            .HasMaxLength(50)
            .HasDefaultValue("قطعة");

        builder.Property(sp => sp.Notes)
            .HasMaxLength(500);

        builder.Property(sp => sp.IsActive)
            .HasDefaultValue(true);

        builder.Property(sp => sp.SupplierName)
            .HasMaxLength(200);

        builder.Property(sp => sp.SupplierPhone)
            .HasMaxLength(50);

        builder.Property(sp => sp.BarcodeType)
            .HasMaxLength(50);

        builder.Property(sp => sp.BarcodeValue)
            .HasMaxLength(200);

        builder.Property(sp => sp.CompatibleCar)
            .HasMaxLength(200);

        builder.Property(sp => sp.CarModel)
            .HasMaxLength(200);

        builder.Property(sp => sp.CarYear)
            .HasMaxLength(20);

        builder.Property(sp => sp.CountryOfOrigin)
            .HasMaxLength(100);

        builder.Property(sp => sp.Weight)
            .HasColumnType("decimal(10,3)");

        // Unique indexes
        builder.HasIndex(sp => sp.PartNumber)
            .IsUnique();

        builder.HasIndex(sp => sp.Barcode)
            .IsUnique();

        // Index on CategoryId
        builder.HasIndex(sp => sp.CategoryId);

        // Index on Location
        builder.HasIndex(sp => sp.Location);

        // Index on Manufacturer
        builder.HasIndex(sp => sp.Manufacturer);

        // Index on SupplierName
        builder.HasIndex(sp => sp.SupplierName);

        // Relationship to Category (many-to-one with Restrict on delete)
        builder.HasOne(sp => sp.Category)
            .WithMany(c => c.SpareParts)
            .HasForeignKey(sp => sp.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
