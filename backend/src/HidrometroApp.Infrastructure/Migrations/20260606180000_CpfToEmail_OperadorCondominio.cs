using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HidrometroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CpfToEmail_OperadorCondominio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename Cpf → Email (drop old index, rename column, create new index)
            migrationBuilder.DropIndex(
                name: "IX_usuarios_Cpf",
                table: "usuarios");

            migrationBuilder.RenameColumn(
                name: "Cpf",
                table: "usuarios",
                newName: "Email");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "usuarios",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Email",
                table: "usuarios",
                column: "Email",
                unique: true);

            // Create operador_condominios table
            migrationBuilder.CreateTable(
                name: "operador_condominios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OperadorId = table.Column<int>(type: "integer", nullable: false),
                    CondominioId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operador_condominios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_operador_condominios_condominios_CondominioId",
                        column: x => x.CondominioId,
                        principalTable: "condominios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_operador_condominios_usuarios_OperadorId",
                        column: x => x.OperadorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operador_condominios_CondominioId",
                table: "operador_condominios",
                column: "CondominioId");

            migrationBuilder.CreateIndex(
                name: "IX_operador_condominios_OperadorId_CondominioId",
                table: "operador_condominios",
                columns: new[] { "OperadorId", "CondominioId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operador_condominios");

            migrationBuilder.DropIndex(
                name: "IX_usuarios_Email",
                table: "usuarios");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "usuarios",
                newName: "Cpf");

            migrationBuilder.AlterColumn<string>(
                name: "Cpf",
                table: "usuarios",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Cpf",
                table: "usuarios",
                column: "Cpf",
                unique: true);
        }
    }
}
