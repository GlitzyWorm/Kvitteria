using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddIdToItemAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ItemAliases",
                table: "ItemAliases");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "ItemAliases",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_ItemAliases",
                table: "ItemAliases",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ItemAliases",
                table: "ItemAliases");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ItemAliases");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ItemAliases",
                table: "ItemAliases",
                column: "RawName");
        }
    }
}
