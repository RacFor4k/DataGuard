using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Auth.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20260624_220524 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_workspace",
                schema: "identity",
                table: "groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "has_workspace",
                schema: "identity",
                table: "groups");
        }
    }
}
