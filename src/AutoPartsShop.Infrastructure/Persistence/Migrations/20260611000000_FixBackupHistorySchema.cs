using Microsoft.EntityFrameworkCore.Migrations;

namespace AutoPartsShop.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class FixBackupHistorySchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add new columns that the entity expects
        migrationBuilder.AddColumn<decimal>(
            name: "FileSizeMB",
            table: "BackupHistories",
            type: "decimal(18,2)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "BackupType",
            table: "BackupHistories",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "BackupHistories",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        // Migrate data from old columns to new columns
        // FileSizeMB = FileSizeBytes / (1024 * 1024)
        migrationBuilder.Sql(@"
            UPDATE BackupHistories
            SET FileSizeMB = CASE 
                WHEN FileSizeBytes > 0 THEN ROUND(CAST(FileSizeBytes AS decimal(18,2)) / (1024.0 * 1024.0), 2)
                ELSE 0 
            END,
            Notes = ISNULL(ErrorMessage, '')
            WHERE FileSizeMB IS NULL
        ");

        // Rename UserId column if it was PerformedByUserId
        // Note: We keep the PerformedByUserId column and add UserId mapping
        migrationBuilder.AddColumn<int>(
            name: "UserId",
            table: "BackupHistories",
            type: "int",
            nullable: true);

        // Migrate data from PerformedByUserId to UserId
        migrationBuilder.Sql(@"
            UPDATE BackupHistories
            SET UserId = PerformedByUserId
            WHERE UserId IS NULL AND PerformedByUserId IS NOT NULL
        ");

        // Drop old columns that are no longer in the entity
        migrationBuilder.DropColumn(
            name: "FileSizeBytes",
            table: "BackupHistories");

        migrationBuilder.DropColumn(
            name: "ErrorMessage",
            table: "BackupHistories");

        migrationBuilder.DropColumn(
            name: "PerformedByUserId",
            table: "BackupHistories");

        // Drop the old foreign key if it exists
        migrationBuilder.Sql(@"
            IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_BackupHistories_Users_PerformedByUserId')
                ALTER TABLE BackupHistories DROP CONSTRAINT FK_BackupHistories_Users_PerformedByUserId
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Re-add old columns
        migrationBuilder.AddColumn<long>(
            name: "FileSizeBytes",
            table: "BackupHistories",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<string>(
            name: "ErrorMessage",
            table: "BackupHistories",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PerformedByUserId",
            table: "BackupHistories",
            type: "int",
            nullable: true);

        // Migrate data back
        migrationBuilder.Sql(@"
            UPDATE BackupHistories
            SET FileSizeBytes = CASE 
                WHEN FileSizeMB IS NOT NULL THEN CAST(ROUND(FileSizeMB * 1024.0 * 1024.0, 0) AS bigint)
                ELSE 0 
            END,
            ErrorMessage = Notes,
            PerformedByUserId = UserId
        ");

        // Drop new columns
        migrationBuilder.DropColumn(
            name: "FileSizeMB",
            table: "BackupHistories");

        migrationBuilder.DropColumn(
            name: "BackupType",
            table: "BackupHistories");

        migrationBuilder.DropColumn(
            name: "Notes",
            table: "BackupHistories");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "BackupHistories");
    }
}
