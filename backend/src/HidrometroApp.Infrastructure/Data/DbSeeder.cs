using HidrometroApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HidrometroApp.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(HidrometroDbContext context)
    {
        if (await context.Usuarios.AnyAsync()) return;

        // ── Usuários ────────────────────────────────────────────────────
        var admin = new Usuario
        {
            Nome = "Administrador",
            Email = "admin@prolar.com",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Perfil = PerfilUsuario.Admin,
            Ativo = true
        };
        var operador = new Usuario
        {
            Nome = "Operador Padrão",
            Email = "operador@prolar.com",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("Operador@123"),
            Perfil = PerfilUsuario.Operador,
            Ativo = true
        };
        var fiscal = new Usuario
        {
            Nome = "Fiscal Padrão",
            Email = "fiscal@prolar.com",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("Fiscal@123"),
            Perfil = PerfilUsuario.Fiscal,
            Ativo = true
        };
        context.Usuarios.AddRange(admin, operador, fiscal);

        // ── Condomínios ─────────────────────────────────────────────────
        var aurora = new Condominio
        {
            Nome = "Edifício Aurora",
            Endereco = "Av. Fernandes Lima, 1230 - Maceió/AL",
            QtdUnidades = 8,
            TipoMedidor = TipoMedidor.AguaFria
        };
        var belaVista = new Condominio
        {
            Nome = "Condomínio Bela Vista",
            Endereco = "Rua Barão de Penedo, 450 - Maceió/AL",
            QtdUnidades = 5,
            TipoMedidor = TipoMedidor.AguaFria
        };
        context.Condominios.AddRange(aurora, belaVista);
        await context.SaveChangesAsync();

        // ── Unidades Edifício Aurora (8 aptos) ──────────────────────────
        var numerosAurora = new[] { "101", "102", "103", "104", "201", "202", "301", "302" };
        var unidadesAurora = numerosAurora
            .Select(n => new Unidade { CondominioId = aurora.Id, Numero = n, Tipo = "Apartamento", Ativa = true })
            .ToArray();
        context.Unidades.AddRange(unidadesAurora);

        // ── Unidades Bela Vista (5 aptos) ──────────────────────────────
        var numerosBelaVista = new[] { "101", "102", "103", "201", "202" };
        var unidadesBelaVista = numerosBelaVista
            .Select(n => new Unidade { CondominioId = belaVista.Id, Numero = n, Tipo = "Apartamento", Ativa = true })
            .ToArray();
        context.Unidades.AddRange(unidadesBelaVista);

        // ── Vínculos operador → condomínios ────────────────────────────
        context.OperadorCondominios.AddRange(
            new OperadorCondominio { OperadorId = operador.Id, CondominioId = aurora.Id },
            new OperadorCondominio { OperadorId = operador.Id, CondominioId = belaVista.Id }
        );
        await context.SaveChangesAsync();

        // ── Helper local ────────────────────────────────────────────────
        Unidade U(string n) => unidadesAurora.First(u => u.Numero == n);

        // ── Calendário ──────────────────────────────────────────────────
        var agora = DateTime.UtcNow;
        var mesAtual = agora.Month;
        var anoAtual = agora.Year;
        var mesPrev = mesAtual == 1 ? 12 : mesAtual - 1;
        var anoPrev = mesAtual == 1 ? anoAtual - 1 : anoAtual;

        // ── OS anterior (mês passado, Finalizada) ──────────────────────
        var osAnterior = new OrdemServico
        {
            CondominioId = aurora.Id,
            FiscalId = fiscal.Id,
            Mes = mesPrev,
            Ano = anoPrev,
            Status = StatusOS.Finalizada,
            DataInicio = new DateTime(anoPrev, mesPrev, 1, 0, 0, 0, DateTimeKind.Utc),
            DataConclusao = new DateTime(anoPrev, mesPrev, 28, 0, 0, 0, DateTimeKind.Utc),
            CriadoEm = new DateTime(anoPrev, mesPrev, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.OrdensServico.Add(osAnterior);
        await context.SaveChangesAsync();

        // Leituras baseline do mês passado — estabelece ValorM3Validado para cálculo de consumo atual
        var baselines = new Dictionary<string, decimal>
        {
            ["101"] = 300m, ["102"] = 500m, ["103"] = 1200m, ["104"] = 880m,
            ["201"] = 250m, ["202"] = 750m, ["301"] = 430m,  ["302"] = 610m
        };
        var validadoEmPrev = new DateTime(anoPrev, mesPrev, 28, 12, 0, 0, DateTimeKind.Utc);
        foreach (var u in unidadesAurora)
        {
            context.LeiturasHidrometro.Add(new LeituraHidrometro
            {
                OsId = osAnterior.Id,
                UnidadeId = u.Id,
                ValorM3 = baselines[u.Numero],
                ValorM3Validado = baselines[u.Numero],
                Origem = OrigemLeitura.Ia,
                ConfiancaIa = 0.92m,
                Tentativas = 1,
                Status = StatusLeitura.Validado,
                QualidadeFoto = QualidadeFoto.Ok,
                ValidadoPorId = operador.Id,
                ValidadoEm = validadoEmPrev,
                CriadoPorId = fiscal.Id,
                CriadoEm = new DateTime(anoPrev, mesPrev, 20, 10, 0, 0, DateTimeKind.Utc)
            });
        }
        await context.SaveChangesAsync();

        // ── Histórico de consumo dos meses anteriores (para as unidades suspeitas) ──
        //
        // Regra real (AnomaliaService.VerificarSuspeitaVazamentoAsync):
        //   historico.Count >= 2  →  consumoAtual > media * 1.5
        //
        // Unidade 102: histórico ~10.5 m³/mês → threshold = 15.75 m³
        //   consumo mês atual = 535 - 500 = 35 m³  → 35 > 15.75 ✓ suspeita disparada
        //
        // Unidade 201: histórico ~9.5 m³/mês → threshold = 14.25 m³
        //   consumo mês atual = 290 - 250 = 40 m³  → 40 > 14.25 ✓ suspeita disparada
        var consumos102 = new decimal[] { 11m, 10m, 11m, 10m }; // meses -5 a -2
        var consumos201 = new decimal[] { 10m,  9m, 10m,  9m };
        for (int i = 0; i < 4; i++)
        {
            var offset = 5 - i; // 5, 4, 3, 2 meses atrás
            var m = mesAtual - offset;
            var y = anoAtual;
            while (m <= 0) { m += 12; y--; }

            context.HistoricoConsumo.Add(new HistoricoConsumo
            {
                UnidadeId = U("102").Id,
                ConsumoM3 = consumos102[i],
                Mes = m,
                Ano = y
            });
            context.HistoricoConsumo.Add(new HistoricoConsumo
            {
                UnidadeId = U("201").Id,
                ConsumoM3 = consumos201[i],
                Mes = m,
                Ano = y
            });
        }
        await context.SaveChangesAsync();

        // ── OS atual (mês corrente, EmProgresso) ──────────────────────
        var osAtual = new OrdemServico
        {
            CondominioId = aurora.Id,
            FiscalId = fiscal.Id,
            Mes = mesAtual,
            Ano = anoAtual,
            Status = StatusOS.EmProgresso,
            DataInicio = new DateTime(anoAtual, mesAtual, 1, 0, 0, 0, DateTimeKind.Utc),
            CriadoEm = new DateTime(anoAtual, mesAtual, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.OrdensServico.Add(osAtual);

        // OS Bela Vista (Aberta — aparece na lista do fiscal sem leituras ainda)
        context.OrdensServico.Add(new OrdemServico
        {
            CondominioId = belaVista.Id,
            FiscalId = fiscal.Id,
            Mes = mesAtual,
            Ano = anoAtual,
            Status = StatusOS.Aberta,
            DataInicio = new DateTime(anoAtual, mesAtual, 1, 0, 0, 0, DateTimeKind.Utc),
            CriadoEm = new DateTime(anoAtual, mesAtual, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await context.SaveChangesAsync();

        // ── Leituras do mês atual — Edifício Aurora ────────────────────
        // Distribuição: 4 Validado (2 normais + 1 suspeita + 1 normal) | 3 Pendente (1 suspeita) | 1 Rejeitado
        var t0 = new DateTime(anoAtual, mesAtual, 10, 9, 0, 0, DateTimeKind.Utc);

        context.LeiturasHidrometro.AddRange(

            // 101 – Validado, consumo normal 12 m³
            new LeituraHidrometro
            {
                OsId = osAtual.Id, UnidadeId = U("101").Id,
                ValorM3 = 312m, ValorM3Validado = 312m,
                Origem = OrigemLeitura.Ia, ConfiancaIa = 0.95m, Tentativas = 1,
                Status = StatusLeitura.Validado, QualidadeFoto = QualidadeFoto.Ok,
                ValidadoPorId = operador.Id, ValidadoEm = t0.AddHours(4),
                CriadoPorId = fiscal.Id, CriadoEm = t0
            },

            // 102 – Validado + SUSPEITA VAZAMENTO: 35 m³ >> média 10,5 m³ × 1,5 = 15,75 m³
            new LeituraHidrometro
            {
                OsId = osAtual.Id, UnidadeId = U("102").Id,
                ValorM3 = 535m, ValorM3Validado = 535m,
                Origem = OrigemLeitura.Ia, ConfiancaIa = 0.88m, Tentativas = 1,
                Status = StatusLeitura.Validado, QualidadeFoto = QualidadeFoto.Ok,
                SuspeitaVazamento = true,
                ValidadoPorId = operador.Id, ValidadoEm = t0.AddHours(4).AddMinutes(5),
                CriadoPorId = fiscal.Id, CriadoEm = t0.AddMinutes(5)
            },

            // 103 – Validado, consumo normal 9 m³
            new LeituraHidrometro
            {
                OsId = osAtual.Id, UnidadeId = U("103").Id,
                ValorM3 = 1209m, ValorM3Validado = 1209m,
                Origem = OrigemLeitura.Ia, ConfiancaIa = 0.91m, Tentativas = 1,
                Status = StatusLeitura.Validado, QualidadeFoto = QualidadeFoto.Ok,
                ValidadoPorId = operador.Id, ValidadoEm = t0.AddHours(4).AddMinutes(10),
                CriadoPorId = fiscal.Id, CriadoEm = t0.AddMinutes(10)
            },

            // 104 – Validado, consumo normal 7 m³
            new LeituraHidrometro
            {
                OsId = osAtual.Id, UnidadeId = U("104").Id,
                ValorM3 = 887m, ValorM3Validado = 887m,
                Origem = OrigemLeitura.Ia, ConfiancaIa = 0.93m, Tentativas = 1,
                Status = StatusLeitura.Validado, QualidadeFoto = QualidadeFoto.Ok,
                ValidadoPorId = operador.Id, ValidadoEm = t0.AddHours(4).AddMinutes(15),
                CriadoPorId = fiscal.Id, CriadoEm = t0.AddMinutes(15)
            },

            // 201 – Pendente + SUSPEITA VAZAMENTO: 40 m³ >> média 9,5 m³ × 1,5 = 14,25 m³
            //       confiança 0.79 → PrioridadeOperador (< 0.85)
            new LeituraHidrometro
            {
                OsId = osAtual.Id, UnidadeId = U("201").Id,
                ValorM3 = 290m,
                Origem = OrigemLeitura.Ia, ConfiancaIa = 0.79m, Tentativas = 1,
                Status = StatusLeitura.Pendente, QualidadeFoto = QualidadeFoto.Ok,
                SuspeitaVazamento = true, PrioridadeOperador = true,
                CriadoPorId = fiscal.Id, CriadoEm = t0.AddMinutes(20)
            },

            // 202 – Pendente, confiança baixa (prioridade operador)
            new LeituraHidrometro
            {
                OsId = osAtual.Id, UnidadeId = U("202").Id,
                ValorM3 = 764m,
                Origem = OrigemLeitura.Ia, ConfiancaIa = 0.82m, Tentativas = 1,
                Status = StatusLeitura.Pendente, QualidadeFoto = QualidadeFoto.Ok,
                PrioridadeOperador = true,
                CriadoPorId = fiscal.Id, CriadoEm = t0.AddMinutes(25)
            },

            // 301 – Pendente, confiança ok
            new LeituraHidrometro
            {
                OsId = osAtual.Id, UnidadeId = U("301").Id,
                ValorM3 = 444m,
                Origem = OrigemLeitura.Ia, ConfiancaIa = 0.87m, Tentativas = 1,
                Status = StatusLeitura.Pendente, QualidadeFoto = QualidadeFoto.Ok,
                PrioridadeOperador = false,
                CriadoPorId = fiscal.Id, CriadoEm = t0.AddMinutes(30)
            },

            // 302 – Rejeitado (foto borrada, baixa confiança 0.41)
            new LeituraHidrometro
            {
                OsId = osAtual.Id, UnidadeId = U("302").Id,
                Origem = OrigemLeitura.Ia, ConfiancaIa = 0.41m, Tentativas = 1,
                Status = StatusLeitura.Rejeitado, QualidadeFoto = QualidadeFoto.BaixaConfianca,
                MotivoRejeicao = "Foto borrada — hidrômetro fora de foco",
                CriadoPorId = fiscal.Id, CriadoEm = t0.AddMinutes(35)
            }
        );

        // HistoricoConsumo para as 4 leituras validadas do mês atual
        context.HistoricoConsumo.AddRange(
            new HistoricoConsumo { UnidadeId = U("101").Id, OsId = osAtual.Id, LeituraAnterior = 300m,  LeituraAtual = 312m,  ConsumoM3 = 12m, Mes = mesAtual, Ano = anoAtual },
            new HistoricoConsumo { UnidadeId = U("102").Id, OsId = osAtual.Id, LeituraAnterior = 500m,  LeituraAtual = 535m,  ConsumoM3 = 35m, Mes = mesAtual, Ano = anoAtual },
            new HistoricoConsumo { UnidadeId = U("103").Id, OsId = osAtual.Id, LeituraAnterior = 1200m, LeituraAtual = 1209m, ConsumoM3 =  9m, Mes = mesAtual, Ano = anoAtual },
            new HistoricoConsumo { UnidadeId = U("104").Id, OsId = osAtual.Id, LeituraAnterior = 880m,  LeituraAtual = 887m,  ConsumoM3 =  7m, Mes = mesAtual, Ano = anoAtual }
        );

        await context.SaveChangesAsync();
    }
}
