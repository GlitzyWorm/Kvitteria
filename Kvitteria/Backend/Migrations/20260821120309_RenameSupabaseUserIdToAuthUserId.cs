using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class RenameSupabaseUserIdToAuthUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SupabaseUserId",
                table: "AppUsers",
                newName: "AuthUserId");

            migrationBuilder.RenameIndex(
                name: "IX_AppUsers_SupabaseUserId",
                table: "AppUsers",
                newName: "IX_AppUsers_AuthUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AuthUserId",
                table: "AppUsers",
                newName: "SupabaseUserId");

            migrationBuilder.RenameIndex(
                name: "IX_AppUsers_AuthUserId",
                table: "AppUsers",
                newName: "IX_AppUsers_SupabaseUserId");
        }
    }
}
