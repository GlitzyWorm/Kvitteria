using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptImageIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_MerchantId",
                table: "Receipts");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Receipts",
                type: "timestamp with time zone",
                nullable: false,
                // Receipts stored before this share the migration's time; the key breaks the tie.
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PossibleDuplicateOfId",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CapturedAt",
                table: "ReceiptIntakes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "ReceiptIntakes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ReceiptIntakes",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // Every intake that exists before this migration came from mail.
                defaultValue: "Mail");

            migrationBuilder.CreateTable(
                name: "ReceiptImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    Sha256 = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ImageId",
                table: "Receipts",
                column: "ImageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_MerchantId_GrandTotal_PurchaseDate",
                table: "Receipts",
                columns: new[] { "MerchantId", "GrandTotal", "PurchaseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_PossibleDuplicateOfId",
                table: "Receipts",
                column: "PossibleDuplicateOfId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptIntakes_ImageId",
                table: "ReceiptIntakes",
                column: "ImageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptImages_Sha256",
                table: "ReceiptImages",
                column: "Sha256",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptIntakes_ReceiptImages_ImageId",
                table: "ReceiptIntakes",
                column: "ImageId",
                principalTable: "ReceiptImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_ReceiptImages_ImageId",
                table: "Receipts",
                column: "ImageId",
                principalTable: "ReceiptImages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_Receipts_PossibleDuplicateOfId",
                table: "Receipts",
                column: "PossibleDuplicateOfId",
                principalTable: "Receipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Existing households get the new permission for members, as a new household does from the
            // seeder. An admin who later removes it is not overruled: this runs once.
            migrationBuilder.Sql(
                """
                UPDATE "HouseholdSettings"
                SET "MemberPermissions" = array_append("MemberPermissions", 'AddReceipts')
                WHERE NOT ('AddReceipts' = ANY("MemberPermissions"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The enum value would no longer parse once the code is rolled back.
            migrationBuilder.Sql(
                """
                UPDATE "HouseholdSettings" SET "MemberPermissions" = array_remove("MemberPermissions", 'AddReceipts');
                UPDATE "AppUsers" SET "PermissionOverrides" = array_remove("PermissionOverrides", 'AddReceipts')
                WHERE "PermissionOverrides" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptIntakes_ReceiptImages_ImageId",
                table: "ReceiptIntakes");

            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_ReceiptImages_ImageId",
                table: "Receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_Receipts_PossibleDuplicateOfId",
                table: "Receipts");

            migrationBuilder.DropTable(
                name: "ReceiptImages");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_ImageId",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_MerchantId_GrandTotal_PurchaseDate",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_PossibleDuplicateOfId",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptIntakes_ImageId",
                table: "ReceiptIntakes");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "PossibleDuplicateOfId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "CapturedAt",
                table: "ReceiptIntakes");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "ReceiptIntakes");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ReceiptIntakes");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_MerchantId",
                table: "Receipts",
                column: "MerchantId");
        }
    }
}
