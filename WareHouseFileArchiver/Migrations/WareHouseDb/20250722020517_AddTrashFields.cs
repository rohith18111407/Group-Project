using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WareHouseFileArchiver.Migrations.WareHouseDb
{
    /// <inheritdoc />
    public partial class AddTrashFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ArchiveFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedBy",
                table: "ArchiveFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ArchiveFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ArchiveFiles");

            migrationBuilder.DropColumn(
                name: "DeletedBy",
                table: "ArchiveFiles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ArchiveFiles");
        }
    }
}
