using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Auto_20260624_220524 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "allowed_groups",
                table: "storage_shared_links",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "allowed_users",
                table: "storage_shared_links",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "storage_files",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                table: "storage_directories",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "storage_directories",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_storage_directories_owner_id_parent_directory_id_normalized",
                table: "storage_directories",
                columns: new[] { "owner_id", "parent_directory_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_storage_directories_owner_id_parent_directory_id_normalized",
                table: "storage_directories");

            migrationBuilder.DropColumn(
                name: "allowed_groups",
                table: "storage_shared_links");

            migrationBuilder.DropColumn(
                name: "allowed_users",
                table: "storage_shared_links");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "storage_files");

            migrationBuilder.DropColumn(
                name: "normalized_name",
                table: "storage_directories");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "storage_directories");
        }
    }
}
