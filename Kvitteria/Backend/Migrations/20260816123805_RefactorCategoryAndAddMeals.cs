using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCategoryAndAddMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                table: "Categories");

            migrationBuilder.DropTable(
                name: "ItemCategories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategoryId",
                table: "Categories");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "ReceiptLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MealId",
                table: "ReceiptLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Categories",
                type: "citext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "CanonicalItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultMealId",
                table: "CanonicalItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_CategoryId",
                table: "ReceiptLines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_MealId",
                table: "ReceiptLines",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DisplayName",
                table: "Categories",
                column: "DisplayName",
                unique: true,
                filter: "\"ParentCategoryId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId_DisplayName",
                table: "Categories",
                columns: new[] { "ParentCategoryId", "DisplayName" },
                unique: true,
                filter: "\"ParentCategoryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalItems_CategoryId",
                table: "CanonicalItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalItems_DefaultMealId",
                table: "CanonicalItems",
                column: "DefaultMealId");

            migrationBuilder.CreateIndex(
                name: "IX_Meals_DisplayName",
                table: "Meals",
                column: "DisplayName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CanonicalItems_Categories_CategoryId",
                table: "CanonicalItems",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CanonicalItems_Meals_DefaultMealId",
                table: "CanonicalItems",
                column: "DefaultMealId",
                principalTable: "Meals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                table: "Categories",
                column: "ParentCategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptLines_Categories_CategoryId",
                table: "ReceiptLines",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptLines_Meals_MealId",
                table: "ReceiptLines",
                column: "MealId",
                principalTable: "Meals",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CanonicalItems_Categories_CategoryId",
                table: "CanonicalItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CanonicalItems_Meals_DefaultMealId",
                table: "CanonicalItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptLines_Categories_CategoryId",
                table: "ReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptLines_Meals_MealId",
                table: "ReceiptLines");

            migrationBuilder.DropTable(
                name: "Meals");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptLines_CategoryId",
                table: "ReceiptLines");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptLines_MealId",
                table: "ReceiptLines");

            migrationBuilder.DropIndex(
                name: "IX_Categories_DisplayName",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentCategoryId_DisplayName",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_CanonicalItems_CategoryId",
                table: "CanonicalItems");

            migrationBuilder.DropIndex(
                name: "IX_CanonicalItems_DefaultMealId",
                table: "CanonicalItems");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "ReceiptLines");

            migrationBuilder.DropColumn(
                name: "MealId",
                table: "ReceiptLines");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "CanonicalItems");

            migrationBuilder.DropColumn(
                name: "DefaultMealId",
                table: "CanonicalItems");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Categories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "citext");

            migrationBuilder.CreateTable(
                name: "ItemCategories",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemCategories", x => new { x.CategoryId, x.CanonicalItemId });
                    table.ForeignKey(
                        name: "FK_ItemCategories_CanonicalItems_CanonicalItemId",
                        column: x => x.CanonicalItemId,
                        principalTable: "CanonicalItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemCategories_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentCategoryId",
                table: "Categories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemCategories_CanonicalItemId",
                table: "ItemCategories",
                column: "CanonicalItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentCategoryId",
                table: "Categories",
                column: "ParentCategoryId",
                principalTable: "Categories",
                principalColumn: "Id");
        }
    }
}
