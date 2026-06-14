using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HidrometroApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "condominios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Endereco = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    QtdUnidades = table.Column<int>(type: "integer", nullable: false),
                    TipoMedidor = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condominios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Cpf = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Perfil = table.Column<string>(type: "text", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "unidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CondominioId = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: true),
                    Ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_unidades_condominios_CondominioId",
                        column: x => x.CondominioId,
                        principalTable: "condominios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    Tabela = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Acao = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RegistroId = table.Column<int>(type: "integer", nullable: true),
                    DadosAntes = table.Column<string>(type: "text", nullable: true),
                    DadosDepois = table.Column<string>(type: "text", nullable: true),
                    Origem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Motivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_auditoria_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ordens_servico",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CondominioId = table.Column<int>(type: "integer", nullable: false),
                    FiscalId = table.Column<int>(type: "integer", nullable: true),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    DataInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataConclusao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordens_servico", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ordens_servico_condominios_CondominioId",
                        column: x => x.CondominioId,
                        principalTable: "condominios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ordens_servico_usuarios_FiscalId",
                        column: x => x.FiscalId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "historico_troca_hidrometro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UnidadeId = table.Column<int>(type: "integer", nullable: false),
                    DataTroca = table.Column<DateOnly>(type: "date", nullable: true),
                    NumeroSerieAnterior = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NumeroSerieNovo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    CriadoPorId = table.Column<int>(type: "integer", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_troca_hidrometro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historico_troca_hidrometro_unidades_UnidadeId",
                        column: x => x.UnidadeId,
                        principalTable: "unidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_historico_troca_hidrometro_usuarios_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "historico_consumo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UnidadeId = table.Column<int>(type: "integer", nullable: false),
                    OsId = table.Column<int>(type: "integer", nullable: true),
                    ConsumoM3 = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    LeituraAnterior = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    LeituraAtual = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Mes = table.Column<int>(type: "integer", nullable: false),
                    Ano = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_consumo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historico_consumo_ordens_servico_OsId",
                        column: x => x.OsId,
                        principalTable: "ordens_servico",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_historico_consumo_unidades_UnidadeId",
                        column: x => x.UnidadeId,
                        principalTable: "unidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leituras_hidrometro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OsId = table.Column<int>(type: "integer", nullable: false),
                    UnidadeId = table.Column<int>(type: "integer", nullable: false),
                    FotoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ValorM3 = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    ValorLitros = table.Column<int>(type: "integer", nullable: false),
                    ValorM3Validado = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Origem = table.Column<string>(type: "text", nullable: false),
                    ConfiancaIa = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    Tentativas = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    QualidadeFoto = table.Column<string>(type: "text", nullable: false),
                    SuspeitaVazamento = table.Column<bool>(type: "boolean", nullable: false),
                    RecomendacaoRevisao = table.Column<bool>(type: "boolean", nullable: false),
                    Observacao = table.Column<string>(type: "text", nullable: true),
                    MotivoRejeicao = table.Column<string>(type: "text", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CriadoPorId = table.Column<int>(type: "integer", nullable: true),
                    ValidadoPorId = table.Column<int>(type: "integer", nullable: true),
                    ValidadoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leituras_hidrometro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leituras_hidrometro_ordens_servico_OsId",
                        column: x => x.OsId,
                        principalTable: "ordens_servico",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leituras_hidrometro_unidades_UnidadeId",
                        column: x => x.UnidadeId,
                        principalTable: "unidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leituras_hidrometro_usuarios_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_leituras_hidrometro_usuarios_ValidadoPorId",
                        column: x => x.ValidadoPorId,
                        principalTable: "usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_auditoria_UsuarioId",
                table: "auditoria",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_historico_consumo_OsId",
                table: "historico_consumo",
                column: "OsId");

            migrationBuilder.CreateIndex(
                name: "IX_historico_consumo_UnidadeId",
                table: "historico_consumo",
                column: "UnidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_historico_troca_hidrometro_CriadoPorId",
                table: "historico_troca_hidrometro",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_historico_troca_hidrometro_UnidadeId",
                table: "historico_troca_hidrometro",
                column: "UnidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_leituras_hidrometro_CriadoPorId",
                table: "leituras_hidrometro",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_leituras_hidrometro_OsId",
                table: "leituras_hidrometro",
                column: "OsId");

            migrationBuilder.CreateIndex(
                name: "IX_leituras_hidrometro_UnidadeId",
                table: "leituras_hidrometro",
                column: "UnidadeId");

            migrationBuilder.CreateIndex(
                name: "IX_leituras_hidrometro_ValidadoPorId",
                table: "leituras_hidrometro",
                column: "ValidadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_ordens_servico_CondominioId_Mes_Ano",
                table: "ordens_servico",
                columns: new[] { "CondominioId", "Mes", "Ano" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ordens_servico_FiscalId",
                table: "ordens_servico",
                column: "FiscalId");

            migrationBuilder.CreateIndex(
                name: "IX_unidades_CondominioId_Numero",
                table: "unidades",
                columns: new[] { "CondominioId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_usuarios_Cpf",
                table: "usuarios",
                column: "Cpf",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditoria");

            migrationBuilder.DropTable(
                name: "historico_consumo");

            migrationBuilder.DropTable(
                name: "historico_troca_hidrometro");

            migrationBuilder.DropTable(
                name: "leituras_hidrometro");

            migrationBuilder.DropTable(
                name: "ordens_servico");

            migrationBuilder.DropTable(
                name: "unidades");

            migrationBuilder.DropTable(
                name: "usuarios");

            migrationBuilder.DropTable(
                name: "condominios");
        }
    }
}
