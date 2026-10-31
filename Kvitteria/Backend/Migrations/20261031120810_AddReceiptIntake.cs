using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppUserId",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MailboxId",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageAt",
                table: "Mailboxes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReceiptIntakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMessageId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    From = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    MailDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: false),
                    BodySource = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    MailboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptIntakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptIntakes_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptIntakes_Mailboxes_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "Mailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_AppUserId",
                table: "Receipts",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_MailboxId",
                table: "Receipts",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptIntakes_AppUserId",
                table: "ReceiptIntakes",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptIntakes_MailboxId",
                table: "ReceiptIntakes",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptIntakes_SourceMessageId",
                table: "ReceiptIntakes",
                column: "SourceMessageId",
                unique: true,
                filter: "\"SourceMessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptIntakes_Status",
                table: "ReceiptIntakes",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_AppUsers_AppUserId",
                table: "Receipts",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_Mailboxes_MailboxId",
                table: "Receipts",
                column: "MailboxId",
                principalTable: "Mailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_AppUsers_AppUserId",
                table: "Receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_Mailboxes_MailboxId",
                table: "Receipts");

            migrationBuilder.DropTable(
                name: "ReceiptIntakes");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_AppUserId",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_MailboxId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "MailboxId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "LastMessageAt",
                table: "Mailboxes");
        }
    }
}
