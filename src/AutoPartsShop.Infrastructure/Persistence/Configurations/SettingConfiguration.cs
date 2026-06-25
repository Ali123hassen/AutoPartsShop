using AutoPartsShop.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutoPartsShop.Infrastructure.Persistence.Configurations;

public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("Settings");

        builder.Property(s => s.SettingKey)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.SettingValue)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasMaxLength(500);

        // Unique index on SettingKey
        builder.HasIndex(s => s.SettingKey)
            .IsUnique();
    }
}
