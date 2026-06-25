using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HidrometroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeituraStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_leituras_status",
                table: "leituras_hidrometro",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_leituras_status",
                table: "leituras_hidrometro");
        }
    }
}
