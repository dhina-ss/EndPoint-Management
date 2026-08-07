using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGpsLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "GpsAccuracyMeters",
                table: "devices",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GpsCity",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GpsCountry",
                table: "devices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GpsLatitude",
                table: "devices",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GpsLongitude",
                table: "devices",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GpsUpdatedAt",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GpsAccuracyMeters",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GpsCity",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GpsCountry",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GpsLatitude",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GpsLongitude",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "GpsUpdatedAt",
                table: "devices");
        }
    }
}
