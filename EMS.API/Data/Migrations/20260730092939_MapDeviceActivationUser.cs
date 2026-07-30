using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class MapDeviceActivationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "devices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivatedByUserId",
                table: "devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_ActivatedByUserId",
                table: "devices",
                column: "ActivatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_devices_app_users_ActivatedByUserId",
                table: "devices",
                column: "ActivatedByUserId",
                principalTable: "app_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_app_users_ActivatedByUserId",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_ActivatedByUserId",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "ActivatedByUserId",
                table: "devices");
        }
    }
}
