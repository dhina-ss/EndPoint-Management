using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAppBlocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blocked_applications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blocked_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ExecutableName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocked_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_blocked_applications_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blocked_applications_DeviceId_ExecutableName",
                table: "blocked_applications",
                columns: new[] { "DeviceId", "ExecutableName" },
                unique: true);
        }
    }
}
