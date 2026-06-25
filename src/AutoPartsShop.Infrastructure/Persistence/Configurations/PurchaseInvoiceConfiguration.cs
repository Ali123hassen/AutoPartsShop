using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoPartsShop.Infrastructure.Persistence.Configurations;

public class PurchaseInvoiceConfiguration : IEntityTypeConfiguration<PurchaseInvoice>
{
    public void Configure(EntityTypeBuilder<PurchaseInvoice> builder)
    {
        builder.ToTable("PurchaseInvoices");

        builder.Property(i => i.InvoiceNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.InvoiceDate)
            .IsRequired();

        builder.Property(i => i.TotalAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.SupplierName)
            .HasMaxLength(200);

        builder.Property(i => i.Notes)
            .HasMaxLength(500);

        builder.Property(i => i.Status)
            .HasDefaultValue(PurchaseInvoiceStatus.Completed);

        // Unique index on InvoiceNumber
        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        // Relationship to User (many-to-one, Restrict delete)
        builder.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to Items (one-to-many with cascade delete)
        builder.HasMany(i => i.Items)
            .WithOne(ii => ii.PurchaseInvoice)
            .HasForeignKey(ii => ii.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
