using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "storage_directories",
                columns: table => new
                {
                    directory_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_directory_id = table.Column<Guid>(type: "uuid", nullable: true),
                    directory_name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    normalized_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storage_directories", x => x.directory_id);
                });

            migrationBuilder.CreateTable(
                name: "storage_files",
                columns: table => new
                {
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_directory_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    normalized_path = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    bucket_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    content_hash = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storage_files", x => x.file_id);
                });

            migrationBuilder.CreateTable(
                name: "storage_nonces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storage_nonces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "file_metadata_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    value = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_file_metadata_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_file_metadata_entries_storage_files_file_id",
                        column: x => x.file_id,
                        principalTable: "storage_files",
                        principalColumn: "file_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "storage_file_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    group_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    access_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storage_file_access", x => x.id);
                    table.ForeignKey(
                        name: "fk_storage_file_access_storage_files_file_id",
                        column: x => x.file_id,
                        principalTable: "storage_files",
                        principalColumn: "file_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "storage_shared_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_direct = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storage_shared_links", x => x.id);
                    table.ForeignKey(
                        name: "fk_storage_shared_links_storage_files_file_id",
                        column: x => x.file_id,
                        principalTable: "storage_files",
                        principalColumn: "file_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_metadata_entries_file_id",
                table: "file_metadata_entries",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_directories_directory_id",
                table: "storage_directories",
                column: "directory_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_directories_owner_id_normalized_path",
                table: "storage_directories",
                columns: new[] { "owner_id", "normalized_path" });

            migrationBuilder.CreateIndex(
                name: "ix_storage_file_access_file_id",
                table: "storage_file_access",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_files_file_id",
                table: "storage_files",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_files_owner_id_normalized_path",
                table: "storage_files",
                columns: new[] { "owner_id", "normalized_path" });

            migrationBuilder.CreateIndex(
                name: "ix_storage_nonces_owner_id_operation_name_token",
                table: "storage_nonces",
                columns: new[] { "owner_id", "operation_name", "token" });

            migrationBuilder.CreateIndex(
                name: "ix_storage_shared_links_file_id",
                table: "storage_shared_links",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_storage_shared_links_token",
                table: "storage_shared_links",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_metadata_entries");

            migrationBuilder.DropTable(
                name: "storage_directories");

            migrationBuilder.DropTable(
                name: "storage_file_access");

            migrationBuilder.DropTable(
                name: "storage_nonces");

            migrationBuilder.DropTable(
                name: "storage_shared_links");

            migrationBuilder.DropTable(
                name: "storage_files");
        }
    }
}
