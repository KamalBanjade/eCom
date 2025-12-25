using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixStockQuantityMappingFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.RenameColumn(
            //     name: "StockQuantity",
            //     table: "ProductVariants",
            //     newName: "AvailableStock");

            // migrationBuilder.AlterColumn<bool>(
            //     name: "IsActive",
            //     table: "ProductVariants",
            //     type: "boolean",
            //     nullable: false,
            //     defaultValue: true,
            //     oldClrType: typeof(bool),
            //     oldType: "boolean");

            // migrationBuilder.AlterColumn<decimal>(
            //     name: "DiscountPrice",
            //     table: "ProductVariants",
            //     type: "numeric(18,2)",
            //     precision: 18,
            //     scale: 2,
            //     nullable: true,
            //     oldClrType: typeof(decimal),
            //     oldType: "numeric",
            //     oldNullable: true);

            // migrationBuilder.AlterColumn<int>(
            //     name: "AvailableStock",
            //     table: "ProductVariants",
            //     type: "integer",
            //     nullable: false,
            //     defaultValue: 0,
            //     oldClrType: typeof(int),
            //     oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.RenameColumn(
            //     name: "AvailableStock",
            //     table: "ProductVariants",
            //     newName: "StockQuantity");

            // migrationBuilder.AlterColumn<bool>(
            //     name: "IsActive",
            //     table: "ProductVariants",
            //     type: "boolean",
            //     nullable: false,
            //     oldClrType: typeof(bool),
            //     oldType: "boolean",
            //     oldDefaultValue: true);

            // migrationBuilder.AlterColumn<decimal>(
            //     name: "DiscountPrice",
            //     table: "ProductVariants",
            //     type: "numeric",
            //     nullable: true,
            //     oldClrType: typeof(decimal),
            //     oldType: "numeric(18,2)",
            //     oldPrecision: 18,
            //     oldScale: 2,
            //     oldNullable: true);

            // migrationBuilder.AlterColumn<int>(
            //     name: "StockQuantity",
            //     table: "ProductVariants",
            //     type: "integer",
            //     nullable: false,
            //     oldClrType: typeof(int),
            //     oldType: "integer",
            //     oldDefaultValue: 0);
        }
    }
}
