using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WareHouseFileArchiver.Migrations.WareHouseDb
{
    /// <inheritdoc />
    public partial class EnableCascadeDeleteForItemArchiveFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchiveFiles_Items_ItemId",
                table: "ArchiveFiles");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchiveFiles_Items_ItemId",
                table: "ArchiveFiles",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArchiveFiles_Items_ItemId",
                table: "ArchiveFiles");

            migrationBuilder.AddForeignKey(
                name: "FK_ArchiveFiles_Items_ItemId",
                table: "ArchiveFiles",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id");
        }
    }
}
