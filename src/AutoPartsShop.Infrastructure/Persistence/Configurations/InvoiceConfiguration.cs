using AutoPartsShop.Core.Entities;
using AutoPartsShop.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoPartsShop.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.Property(i => i.InvoiceNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.InvoiceDate)
            .IsRequired();

        builder.Property(i => i.SubTotal)
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.DiscountAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m);

        builder.Property(i => i.TaxRate)
            .HasColumnType("decimal(5,2)")
            .HasDefaultValue(0m);

        builder.Property(i => i.TaxAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m);

        builder.Property(i => i.TotalAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.PaidAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(i => i.ChangeAmount)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m);

        builder.Property(i => i.Status)
            .HasDefaultValue(InvoiceStatus.Completed);

        builder.Property(i => i.PaymentMethod)
            .HasDefaultValue(PaymentMethod.Cash);

        builder.Property(i => i.CustomerName)
            .HasMaxLength(100);

        builder.Property(i => i.Notes)
            .HasMaxLength(500);

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
            .WithOne(ii => ii.Invoice)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship to Returns (one-to-many)
        builder.HasMany(i => i.Returns)
            .WithOne(r => r.Invoice)
            .HasForeignKey(r => r.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
