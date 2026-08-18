using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMealCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TracksDayCoverage",
                table: "Meals",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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
                name: "IX_ReceiptMealCoverages_MealId",
                table: "ReceiptMealCoverages",
                column: "MealId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptMealCoverages_ReceiptId1",
                table: "ReceiptMealCoverages",
                column: "ReceiptId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptMealCoverages");

            migrationBuilder.DropColumn(
                name: "TracksDayCoverage",
                table: "Meals");
        }
    }
}
