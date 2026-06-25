using AutoPartsShop.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoPartsShop.Infrastructure.Persistence.Configurations;

public class ReturnConfiguration : IEntityTypeConfiguration<Return>
{
    public void Configure(EntityTypeBuilder<Return> builder)
    {
        builder.ToTable("Returns");

        builder.Property(r => r.ReturnNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.ReturnType)
            .IsRequired();

        builder.Property(r => r.RefundAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(r => r.Reason)
            .HasMaxLength(500);

        // Unique index on ReturnNumber
        builder.HasIndex(r => r.ReturnNumber)
            .IsUnique();

        // Relationship to Invoice (Set Null on delete)
        builder.HasOne(r => r.Invoice)
            .WithMany(i => i.Returns)
            .HasForeignKey(r => r.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Relationship to SparePart (Restrict delete)
        builder.HasOne(r => r.SparePart)
            .WithMany()
            .HasForeignKey(r => r.SparePartId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to ReplacementPart (Nullable FK to SparePart, Restrict delete)
        builder.HasOne(r => r.ReplacementPart)
            .WithMany()
            .HasForeignKey(r => r.ReplacementPartId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to User (Restrict delete)
        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
