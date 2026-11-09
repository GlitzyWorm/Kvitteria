using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptReconciliationAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reconciled",
                table: "Receipts");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedById",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedDifference",
                table: "Receipts",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedNote",
                table: "Receipts",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_AcceptedById",
                table: "Receipts",
                column: "AcceptedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_AppUsers_AcceptedById",
                table: "Receipts",
                column: "AcceptedById",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_AppUsers_AcceptedById",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_AcceptedById",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "AcceptedById",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "AcceptedDifference",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "AcceptedNote",
                table: "Receipts");

            migrationBuilder.AddColumn<bool>(
                name: "Reconciled",
                table: "Receipts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The column is derivable, so a downgrade gets the value the figures give rather than false everywhere.
            migrationBuilder.Sql(@"
                UPDATE ""Receipts"" r
                SET ""Reconciled"" = ABS(r.""GrandTotal"" - COALESCE((SELECT SUM(l.""LineTotal"") FROM ""ReceiptLines"" l WHERE l.""ReceiptId"" = r.""Id""), 0) - r.""PantTotal"")
                    <= ABS(COALESCE((SELECT ""OllamaReconciliationToleranceKr"" FROM ""PipelineSettings"" LIMIT 1), 0.01));");
        }
    }
}
