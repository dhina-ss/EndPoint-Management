using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "devices",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationCity",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationCountry",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationRegion",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LocationUpdatedAt",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "devices",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicIPAddress",
                table: "devices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LocationCity",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LocationCountry",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LocationRegion",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "LocationUpdatedAt",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "PublicIPAddress",
                table: "devices");
        }
    }
}
