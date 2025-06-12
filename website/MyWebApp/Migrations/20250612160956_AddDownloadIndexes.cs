using Microsoft.EntityFrameworkCore.Migrations;

namespace MyWebApp.Migrations
{
    public partial class AddDownloadIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Downloads_DownloadTime",
                table: "Downloads",
                column: "DownloadTime");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_IsSuccessful",
                table: "Downloads",
                column: "IsSuccessful");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_UserIP",
                table: "Downloads",
                column: "UserIP");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Downloads_DownloadTime",
                table: "Downloads");

            migrationBuilder.DropIndex(
                name: "IX_Downloads_IsSuccessful",
                table: "Downloads");

            migrationBuilder.DropIndex(
                name: "IX_Downloads_UserIP",
                table: "Downloads");
        }
    }
}
