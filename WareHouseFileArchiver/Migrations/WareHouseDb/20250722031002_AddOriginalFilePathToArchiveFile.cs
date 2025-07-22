using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WareHouseFileArchiver.Migrations.WareHouseDb
{
    /// <inheritdoc />
    public partial class AddOriginalFilePathToArchiveFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFilePath",
                table: "ArchiveFiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalFilePath",
                table: "ArchiveFiles");
        }
    }
}
