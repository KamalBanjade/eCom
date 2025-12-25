using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncProductCategoryMappingEmpty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropForeignKey(
            //     name: "FK_Products_Categories_CategoryId",
            //     table: "Products");

            // migrationBuilder.RenameColumn(
            //     name: "CategoryId",
            //     table: "Products",
            //     newName: "Category");

            // migrationBuilder.RenameIndex(
            //     name: "IX_Products_CategoryId",
            //     table: "Products",
            //     newName: "IX_Products_Category");

            // migrationBuilder.AddForeignKey(
            //     name: "FK_Products_Categories_Category",
            //     table: "Products",
            //     column: "Category",
            //     principalTable: "Categories",
            //     principalColumn: "Id",
            //     onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropForeignKey(
            //     name: "FK_Products_Categories_Category",
            //     table: "Products");

            // migrationBuilder.RenameColumn(
            //     name: "Category",
            //     table: "Products",
            //     newName: "CategoryId");

            // migrationBuilder.RenameIndex(
            //     name: "IX_Products_Category",
            //     table: "Products",
            //     newName: "IX_Products_CategoryId");

            // migrationBuilder.AddForeignKey(
            //     name: "FK_Products_Categories_CategoryId",
            //     table: "Products",
            //     column: "CategoryId",
            //     principalTable: "Categories",
            //     principalColumn: "Id",
            //     onDelete: ReferentialAction.Restrict);
        }
    }
}
