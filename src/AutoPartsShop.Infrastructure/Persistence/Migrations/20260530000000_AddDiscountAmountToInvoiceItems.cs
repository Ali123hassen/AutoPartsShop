using Microsoft.EntityFrameworkCore.Migrations;

namespace AutoPartsShop.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDiscountAmountToInvoiceItems : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add DiscountAmount column to InvoiceItems (exact discount amount for the line)
        migrationBuilder.AddColumn<decimal>(
            name: "DiscountAmount",
            table: "InvoiceItems",
            type: "decimal(18,2)",
            nullable: false,
            defaultValue: 0m);

        // Increase DiscountPercent precision from decimal(5,2) to decimal(9,4)
        // to reduce rounding errors when storing percentage values
        migrationBuilder.AlterColumn<decimal>(
            name: "DiscountPercent",
            table: "InvoiceItems",
            type: "decimal(9,4)",
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "decimal(5,2)",
            oldDefaultValue: 0m);

        // Backfill DiscountAmount from existing DiscountPercent data
        // DiscountAmount = (Quantity * UnitPrice) * (DiscountPercent / 100)
        migrationBuilder.Sql(@"
            UPDATE InvoiceItems
            SET DiscountAmount = ROUND(Quantity * UnitPrice * (DiscountPercent / 100.0), 2)
            WHERE DiscountPercent > 0
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove DiscountAmount column
        migrationBuilder.DropColumn(
            name: "DiscountAmount",
            table: "InvoiceItems");

        // Revert DiscountPercent precision back to decimal(5,2)
        migrationBuilder.AlterColumn<decimal>(
            name: "DiscountPercent",
            table: "InvoiceItems",
            type: "decimal(5,2)",
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "decimal(9,4)",
            oldDefaultValue: 0m);
    }
}
