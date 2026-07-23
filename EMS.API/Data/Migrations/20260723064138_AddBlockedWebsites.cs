using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockedWebsites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blocked_websites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocked_websites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_blocked_websites_devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_blocked_websites_DeviceId_Domain",
                table: "blocked_websites",
                columns: new[] { "DeviceId", "Domain" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blocked_websites");
        }
    }
}
