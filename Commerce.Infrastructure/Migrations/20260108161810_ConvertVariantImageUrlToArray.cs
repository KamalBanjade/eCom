using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Commerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertVariantImageUrlToArray : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add new ImageUrls column (JSONB array)
            migrationBuilder.AddColumn<string>(
                name: "ImageUrls",
                table: "ProductVariants",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            // Step 2: Migrate existing ImageUrl data to ImageUrls array
            migrationBuilder.Sql(@"
                UPDATE ""ProductVariants""
                SET ""ImageUrls"" = CASE 
                    WHEN ""ImageUrl"" IS NULL OR ""ImageUrl"" = '' THEN '[]'::jsonb
                    ELSE jsonb_build_array(""ImageUrl"")
                END;
            ");

            // Step 3: Drop old ImageUrl column
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "ProductVariants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add back ImageUrl column
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "ProductVariants",
                type: "text",
                nullable: true);

            // Step 2: Take first image from array (or NULL if empty)
            migrationBuilder.Sql(@"
                UPDATE ""ProductVariants"" 
                SET ""ImageUrl"" = CASE 
                    WHEN jsonb_array_length(""ImageUrls"") > 0 THEN ""ImageUrls""->0
                    ELSE NULL
                END;
            ");

            // Step 3: Drop ImageUrls column
            migrationBuilder.DropColumn(
                name: "ImageUrls",
                table: "ProductVariants");
        }
    }
}
