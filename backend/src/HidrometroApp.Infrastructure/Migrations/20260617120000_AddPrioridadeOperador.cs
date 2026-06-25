using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HidrometroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrioridadeOperador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PrioridadeOperador",
                table: "leituras_hidrometro",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrioridadeOperador",
                table: "leituras_hidrometro");
        }
    }
}
