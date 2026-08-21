using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PipelineSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImapPollIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    ImapMaxMessagesPerPoll = table.Column<int>(type: "integer", nullable: false),
                    OllamaModel = table.Column<string>(type: "text", nullable: false),
                    OllamaThinkingMode = table.Column<bool>(type: "boolean", nullable: false),
                    OllamaNumCtx = table.Column<int>(type: "integer", nullable: false),
                    OllamaKeepAlive = table.Column<string>(type: "text", nullable: false),
                    OllamaMaxExtractionAttempts = table.Column<int>(type: "integer", nullable: false),
                    OllamaRetryTemperature = table.Column<double>(type: "double precision", nullable: false),
                    OllamaReconciliationToleranceKr = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineSettings");
        }
    }
}
