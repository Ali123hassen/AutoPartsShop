using AutoPartsShop.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoPartsShop.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");

        builder.Property(sm => sm.MovementType)
            .IsRequired();

        builder.Property(sm => sm.ReferenceType)
            .HasMaxLength(50);

        builder.Property(sm => sm.Notes)
            .HasMaxLength(500);

        // Relationship to SparePart (Restrict delete)
        builder.HasOne(sm => sm.SparePart)
            .WithMany(sp => sp.StockMovements)
            .HasForeignKey(sm => sm.SparePartId)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to User (Restrict delete)
        builder.HasOne(sm => sm.User)
            .WithMany()
            .HasForeignKey(sm => sm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite index on SparePartId and CreatedAt
        builder.HasIndex(sm => new { sm.SparePartId, sm.CreatedAt });
    }
}
