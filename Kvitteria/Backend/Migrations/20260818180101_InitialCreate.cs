using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "citext", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TracksDayCoverage = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Merchants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Merchants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CanonicalItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultMealId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanonicalItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanonicalItems_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CanonicalItems_Meals_DefaultMealId",
                        column: x => x.DefaultMealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    PantTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    Reconciled = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_Merchants_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "Merchants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RawName = table.Column<string>(type: "text", nullable: false),
                    CanonicalItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemAliases_CanonicalItems_CanonicalItemId",
                        column: x => x.CanonicalItemId,
                        principalTable: "CanonicalItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawName = table.Column<string>(type: "text", nullable: false),
                    CanonicalItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<short>(type: "smallint", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    MealId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_CanonicalItems_CanonicalItemId",
                        column: x => x.CanonicalItemId,
                        principalTable: "CanonicalItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptMealCoverages",
                columns: table => new
                {
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    MealId = table.Column<Guid>(type: "uuid", nullable: false),
                    DaysCovered = table.Column<int>(type: "integer", nullable: false),
                    ReceiptId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptMealCoverages", x => new { x.ReceiptId, x.MealId });
                    table.ForeignKey(
                        name: "FK_ReceiptMealCoverages_Meals_MealId",
                        column: x => x.MealId,
                        principalTable: "Meals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptMealCoverages_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReceiptMealCoverages_Receipts_ReceiptId1",
                        column: x => x.ReceiptId1,
                        principalTable: "Receipts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalItems_CategoryId",
                table: "CanonicalItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CanonicalItems_DefaultMealId",
                table: "CanonicalItems",
                column: "DefaultMealId");

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
                name: "IX_ItemAliases_CanonicalItemId",
                table: "ItemAliases",
                column: "CanonicalItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Meals_DisplayName",
                table: "Meals",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_CanonicalItemId",
                table: "ReceiptLines",
                column: "CanonicalItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_CategoryId",
                table: "ReceiptLines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_MealId",
                table: "ReceiptLines",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_ReceiptId",
                table: "ReceiptLines",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptMealCoverages_MealId",
                table: "ReceiptMealCoverages",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptMealCoverages_ReceiptId1",
                table: "ReceiptMealCoverages",
                column: "ReceiptId1");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_MerchantId",
                table: "Receipts",
                column: "MerchantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemAliases");

            migrationBuilder.DropTable(
                name: "ReceiptLines");

            migrationBuilder.DropTable(
                name: "ReceiptMealCoverages");

            migrationBuilder.DropTable(
                name: "CanonicalItems");

            migrationBuilder.DropTable(
                name: "Receipts");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Meals");

            migrationBuilder.DropTable(
                name: "Merchants");
        }
    }
}
