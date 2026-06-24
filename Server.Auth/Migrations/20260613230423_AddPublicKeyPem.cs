using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicKeyPem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "public_key",
                schema: "identity",
                table: "companies");

            migrationBuilder.RenameColumn(
                name: "master_key",
                schema: "identity",
                table: "users",
                newName: "backup_encrypted_key");

            migrationBuilder.AddColumn<string>(
                name: "public_key_pem",
                schema: "identity",
                table: "companies",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "public_key_pem",
                schema: "identity",
                table: "companies");

            migrationBuilder.RenameColumn(
                name: "backup_encrypted_key",
                schema: "identity",
                table: "users",
                newName: "master_key");

            migrationBuilder.AddColumn<byte[]>(
                name: "public_key",
                schema: "identity",
                table: "companies",
                type: "bytea",
                nullable: true);
        }
    }
}
