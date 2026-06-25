using AutoPartsShop.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoPartsShop.Infrastructure.Persistence.Configurations;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("InvoiceItems");

        builder.Property(ii => ii.PartName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ii => ii.UnitPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(ii => ii.DiscountPercent)
            .HasColumnType("decimal(9,4)")
            .HasDefaultValue(0m);

        builder.Property(ii => ii.DiscountAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m);

        builder.Property(ii => ii.LineTotal)
            .HasColumnType("decimal(18,2)");

        // Relationship to Invoice (many-to-one with cascade delete)
        builder.HasOne(ii => ii.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship to SparePart (many-to-one with restrict delete)
        builder.HasOne(ii => ii.SparePart)
            .WithMany(sp => sp.InvoiceItems)
            .HasForeignKey(ii => ii.SparePartId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
