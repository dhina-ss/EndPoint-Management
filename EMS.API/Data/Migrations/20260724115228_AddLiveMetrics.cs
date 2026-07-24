using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BatteryCharging",
                table: "device_heartbeats",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BatteryPercent",
                table: "device_heartbeats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CpuUsagePercent",
                table: "device_heartbeats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiskTotalGb",
                table: "device_heartbeats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DiskUsagePercent",
                table: "device_heartbeats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiskUsedGb",
                table: "device_heartbeats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasBattery",
                table: "device_heartbeats",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemoryTotalMb",
                table: "device_heartbeats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MemoryUsagePercent",
                table: "device_heartbeats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemoryUsedMb",
                table: "device_heartbeats",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NetworkReceivedKbps",
                table: "device_heartbeats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NetworkSentKbps",
                table: "device_heartbeats",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "UptimeSeconds",
                table: "device_heartbeats",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryCharging",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "BatteryPercent",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "CpuUsagePercent",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "DiskTotalGb",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "DiskUsagePercent",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "DiskUsedGb",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "HasBattery",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "MemoryTotalMb",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "MemoryUsagePercent",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "MemoryUsedMb",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "NetworkReceivedKbps",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "NetworkSentKbps",
                table: "device_heartbeats");

            migrationBuilder.DropColumn(
                name: "UptimeSeconds",
                table: "device_heartbeats");
        }
    }
}
