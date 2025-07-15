using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WareHouseFileArchiver.Migrations.WareHouseDb
{
    /// <inheritdoc />
    public partial class AddFileDownloadLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileDownloadLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArchiveFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DownloadedBy = table.Column<string>(type: "text", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileDownloadLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileDownloadLogs_ArchiveFiles_ArchiveFileId",
                        column: x => x.ArchiveFileId,
                        principalTable: "ArchiveFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileDownloadLogs_ArchiveFileId",
                table: "FileDownloadLogs",
                column: "ArchiveFileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileDownloadLogs");
        }
    }
}
