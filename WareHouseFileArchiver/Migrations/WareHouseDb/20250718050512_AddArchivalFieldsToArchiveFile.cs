using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WareHouseFileArchiver.Migrations.WareHouseDb
{
    /// <inheritdoc />
    public partial class AddArchivalFieldsToArchiveFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "ArchiveFiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedReason",
                table: "ArchiveFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchivedDueToInactivity",
                table: "ArchiveFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "ArchiveFiles");

            migrationBuilder.DropColumn(
                name: "ArchivedReason",
                table: "ArchiveFiles");

            migrationBuilder.DropColumn(
                name: "IsArchivedDueToInactivity",
                table: "ArchiveFiles");
        }
    }
}
