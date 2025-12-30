using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_InventoryRefactoring : Migration
    {
        /// <inheritdoc />
       protected override void Up(MigrationBuilder migrationBuilder)
{
    // Drop old Inventories table
    migrationBuilder.DropTable(
        name: "Inventories");

    // Drop old index if it exists (safe)
    migrationBuilder.Sql(@"
        DROP INDEX IF EXISTS ""IX_StockReservations_ProductVariantId"";
    ");

    // Create StockReservations table if it doesn't exist
    migrationBuilder.Sql(@"
        CREATE TABLE IF NOT EXISTS ""StockReservations"" (
            ""Id"" uuid NOT NULL PRIMARY KEY,
            ""ProductVariantId"" uuid NOT NULL,
            ""Quantity"" integer NOT NULL,
            ""UserId"" varchar(255),
            ""ExpiresAt"" timestamp with time zone NOT NULL,
            ""IsReleased"" boolean NOT NULL DEFAULT false,
            ""IsConfirmed"" boolean NOT NULL DEFAULT false,
            ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
            ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
            
            CONSTRAINT ""FK_StockReservations_ProductVariants"" 
            FOREIGN KEY (""ProductVariantId"") 
            REFERENCES ""ProductVariants""(""Id"") 
            ON DELETE CASCADE
        );
    ");

    // Create StockAuditLogs table
    migrationBuilder.CreateTable(
        name: "StockAuditLogs",
        columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
            Action = table.Column<string>(type: "text", nullable: false),
            QuantityChanged = table.Column<int>(type: "integer", nullable: false),
            StockBefore = table.Column<int>(type: "integer", nullable: false),
            StockAfter = table.Column<int>(type: "integer", nullable: false),
            UserId = table.Column<string>(type: "text", nullable: true),
            ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
            Reason = table.Column<string>(type: "text", nullable: true),
            Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_StockAuditLogs", x => x.Id);
        });

    // Create the filtered unique index on StockReservations
    migrationBuilder.CreateIndex(
        name: "IX_StockReservations_ProductVariantId_UserId",
        table: "StockReservations",
        columns: new[] { "ProductVariantId", "UserId" },
        unique: true,
        filter: "\"IsReleased\" = false AND \"IsConfirmed\" = false");

    // Add additional indexes for performance
    migrationBuilder.CreateIndex(
        name: "IX_StockReservations_ProductVariantId",
        table: "StockReservations",
        column: "ProductVariantId");

    migrationBuilder.CreateIndex(
        name: "IX_StockAuditLogs_ProductVariantId",
        table: "StockAuditLogs",
        column: "ProductVariantId");

    migrationBuilder.CreateIndex(
        name: "IX_StockAuditLogs_Timestamp",
        table: "StockAuditLogs",
        column: "Timestamp");
}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_StockReservations_ProductVariantId_UserId",
                table: "StockReservations");

            migrationBuilder.CreateTable(
                name: "Inventories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableQuantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReorderLevel = table.Column<int>(type: "integer", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Inventories_ProductVariants_ProductVariantId",
                        column: x => x.ProductVariantId,
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_ProductVariantId",
                table: "StockReservations",
                column: "ProductVariantId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventories_ProductVariantId",
                table: "Inventories",
                column: "ProductVariantId",
                unique: true);
        }
    }
}
