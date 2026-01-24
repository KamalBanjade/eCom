using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountDistributionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Orders");

            migrationBuilder.AddColumn<bool>(
                name: "IsDiscountDistributed",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDiscountDistributed",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAllocated",
                table: "OrderItems",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountDistributionPercentage",
                table: "OrderItems",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EffectivePrice",
                table: "OrderItems",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDiscountDistributed",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TotalDiscountDistributed",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAllocated",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountDistributionPercentage",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "EffectivePrice",
                table: "OrderItems");

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "Orders",
                type: "text",
                nullable: true);
        }
    }
}
