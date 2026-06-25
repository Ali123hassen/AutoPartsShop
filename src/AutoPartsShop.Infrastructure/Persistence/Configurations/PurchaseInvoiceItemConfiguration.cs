using AutoPartsShop.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoPartsShop.Infrastructure.Persistence.Configurations;

public class PurchaseInvoiceItemConfiguration : IEntityTypeConfiguration<PurchaseInvoiceItem>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoiceItem> builder)
    {
        builder.ToTable("PurchaseInvoiceItems");

        builder.Property(ii => ii.PartName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ii => ii.CostPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(ii => ii.SalePrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(ii => ii.LineTotal)
            .HasColumnType("decimal(18,2)");

        // Relationship to PurchaseInvoice (many-to-one with cascade delete)
        builder.HasOne(ii => ii.PurchaseInvoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => ii.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship to SparePart (many-to-one with restrict delete)
        builder.HasOne(ii => ii.SparePart)
            .WithMany()
            .HasForeignKey(ii => ii.SparePartId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
