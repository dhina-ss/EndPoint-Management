using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreGating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "StoreGatingEnabled",
                table: "devices",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoreGatingEnabled",
                table: "devices");
        }
    }
}
