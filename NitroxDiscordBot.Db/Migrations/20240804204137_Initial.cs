using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NitroxDiscordBot.Db.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutoResponses",
                columns: table => new
                {
                    AutoResponseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoResponses", x => x.AutoResponseId);
                });

            migrationBuilder.CreateTable(
                name: "Cleanups",
                columns: table => new
                {
                    CleanupId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AgeThreshold = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    CronSchedule = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cleanups", x => x.CleanupId);
                });

            migrationBuilder.CreateTable(
                name: "AutoResponseFilters",
                columns: table => new
                {
                    FilterId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    AutoResponseId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoResponseFilters", x => x.FilterId);
                    table.ForeignKey(
                        name: "FK_AutoResponseFilters_AutoResponses_AutoResponseId",
                        column: x => x.AutoResponseId,
                        principalTable: "AutoResponses",
                        principalColumn: "AutoResponseId");
                });

            migrationBuilder.CreateTable(
                name: "AutoResponseResponses",
                columns: table => new
                {
                    ResponseId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    AutoResponseId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoResponseResponses", x => x.ResponseId);
                    table.ForeignKey(
                        name: "FK_AutoResponseResponses_AutoResponses_AutoResponseId",
                        column: x => x.AutoResponseId,
                        principalTable: "AutoResponses",
                        principalColumn: "AutoResponseId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoResponseFilters_AutoResponseId",
                table: "AutoResponseFilters",
                column: "AutoResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_AutoResponseResponses_AutoResponseId",
                table: "AutoResponseResponses",
                column: "AutoResponseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoResponseFilters");

            migrationBuilder.DropTable(
                name: "AutoResponseResponses");

            migrationBuilder.DropTable(
                name: "Cleanups");

            migrationBuilder.DropTable(
                name: "AutoResponses");
        }
    }
}
