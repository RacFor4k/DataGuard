using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class PinToPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "server_pin_hash",
                schema: "identity",
                table: "users",
                newName: "server_password_hash");

            migrationBuilder.RenameColumn(
                name: "encrypted_pin",
                schema: "identity",
                table: "users",
                newName: "encrypted_password");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "server_password_hash",
                schema: "identity",
                table: "users",
                newName: "server_pin_hash");

            migrationBuilder.RenameColumn(
                name: "encrypted_password",
                schema: "identity",
                table: "users",
                newName: "encrypted_pin");
        }
    }
}
