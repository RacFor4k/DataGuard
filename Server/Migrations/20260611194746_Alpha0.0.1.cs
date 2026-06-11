using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class Alpha001 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_groups_icons_icon_id",
                schema: "identity",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "pin_code_hash",
                schema: "identity",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "public_key",
                schema: "identity",
                table: "users",
                newName: "server_salt");

            migrationBuilder.RenameColumn(
                name: "master_encrypted_key",
                schema: "identity",
                table: "users",
                newName: "master_key");

            migrationBuilder.RenameColumn(
                name: "uuid",
                schema: "identity",
                table: "users",
                newName: "user_id");

            migrationBuilder.AddColumn<byte[]>(
                name: "client_salt",
                schema: "identity",
                table: "users",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "encrypted_pin",
                schema: "identity",
                table: "users",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "server_pin_hash",
                schema: "identity",
                table: "users",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "companies",
                schema: "identity",
                columns: table => new
                {
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    logo = table.Column<string>(type: "text", nullable: true),
                    public_key = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.company_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_company_id",
                schema: "identity",
                table: "users",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_groups_company_id",
                schema: "identity",
                table: "groups",
                column: "company_id");

            migrationBuilder.AddForeignKey(
                name: "fk_groups_companies_company_id",
                schema: "identity",
                table: "groups",
                column: "company_id",
                principalSchema: "identity",
                principalTable: "companies",
                principalColumn: "company_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_groups_icons_icon_id",
                schema: "identity",
                table: "groups",
                column: "icon_id",
                principalSchema: "identity",
                principalTable: "icons",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_users_companies_company_id",
                schema: "identity",
                table: "users",
                column: "company_id",
                principalSchema: "identity",
                principalTable: "companies",
                principalColumn: "company_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_groups_companies_company_id",
                schema: "identity",
                table: "groups");

            migrationBuilder.DropForeignKey(
                name: "fk_groups_icons_icon_id",
                schema: "identity",
                table: "groups");

            migrationBuilder.DropForeignKey(
                name: "fk_users_companies_company_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropTable(
                name: "companies",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "ix_users_company_id",
                schema: "identity",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_groups_company_id",
                schema: "identity",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "client_salt",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "encrypted_pin",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "server_pin_hash",
                schema: "identity",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "server_salt",
                schema: "identity",
                table: "users",
                newName: "public_key");

            migrationBuilder.RenameColumn(
                name: "master_key",
                schema: "identity",
                table: "users",
                newName: "master_encrypted_key");

            migrationBuilder.RenameColumn(
                name: "user_id",
                schema: "identity",
                table: "users",
                newName: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "pin_code_hash",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "fk_groups_icons_icon_id",
                schema: "identity",
                table: "groups",
                column: "icon_id",
                principalSchema: "identity",
                principalTable: "icons",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
