using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsbBlockingExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UsbBlockingUntil",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsbBlockingUntil",
                table: "devices");
        }
    }
}
