using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HidrometroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataLimiteToOrdemServico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataLimite",
                table: "ordens_servico",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataLimite",
                table: "ordens_servico");
        }
    }
}
