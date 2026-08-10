using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DefenderAntivirusEnabled",
                table: "devices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenderEngineVersion",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DefenderLastFullScan",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DefenderLastQuickScan",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefenderRealtimeProtectionEnabled",
                table: "devices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefenderSignatureAgeDays",
                table: "devices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenderSignatureVersion",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SecurityStatusUpdatedAt",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "device_threats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: true),
                    DetectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remediated = table.Column<bool>(type: "boolean", nullable: false),
                    Resource = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_threats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_threats_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_threats_DeviceId",
                table: "device_threats",
                column: "DeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_threats");

            migrationBuilder.DropColumn(
                name: "DefenderAntivirusEnabled",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "DefenderEngineVersion",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "DefenderLastFullScan",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "DefenderLastQuickScan",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "DefenderRealtimeProtectionEnabled",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "DefenderSignatureAgeDays",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "DefenderSignatureVersion",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "SecurityStatusUpdatedAt",
                table: "devices");
        }
    }
}
