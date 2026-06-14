using HidrometroApp.Core.Models;
using HidrometroApp.Core.Services;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HidrometroApp.Tests.Unit.Services;

public class AnomaliaServiceTests
{
    private HidrometroDbContext CriarDb()
    {
        var opts = new DbContextOptionsBuilder<HidrometroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HidrometroDbContext(opts);
    }

    [Fact]
    public async Task ValidarLeitura_Negativa_Retorna_Invalida()
    {
        var db = CriarDb();
        var svc = new AnomaliaService(db);

        var (valida, motivo) = await svc.ValidarLeituraAsync(1, -1);

        Assert.False(valida);
        Assert.NotNull(motivo);
    }

    [Fact]
    public async Task ValidarLeitura_AcimaLimite_Retorna_Invalida()
    {
        var db = CriarDb();
        var svc = new AnomaliaService(db);

        var (valida, motivo) = await svc.ValidarLeituraAsync(1, 1_000_000);

        Assert.False(valida);
        Assert.Contains("máximo", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidarLeitura_Regressiva_SemTroca_Retorna_Invalida()
    {
        var db = CriarDb();

        // Simular leitura anterior validada
        var condo = new Condominio { Nome = "Teste", QtdUnidades = 1 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var unidade = new Unidade { CondominioId = condo.Id, Numero = "101" };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        var os = new OrdemServico { CondominioId = condo.Id, Mes = 3, Ano = 2026 };
        db.OrdensServico.Add(os);
        await db.SaveChangesAsync();

        db.LeiturasHidrometro.Add(new LeituraHidrometro
        {
            OsId = os.Id,
            UnidadeId = unidade.Id,
            Status = StatusLeitura.Validado,
            ValorM3Validado = 500,
            ValidadoEm = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new AnomaliaService(db);
        var (valida, motivo) = await svc.ValidarLeituraAsync(unidade.Id, 100);

        Assert.False(valida);
        Assert.Contains("inferior", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerificarVazamento_ConsumoExcessivo_Retorna_True()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Teste", QtdUnidades = 1 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var unidade = new Unidade { CondominioId = condo.Id, Numero = "101" };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        // Histórico: 6 meses com consumo de 20 m³
        for (int i = 1; i <= 6; i++)
        {
            db.HistoricoConsumo.Add(new HistoricoConsumo
            {
                UnidadeId = unidade.Id,
                ConsumoM3 = 20,
                Mes = i,
                Ano = 2025
            });
        }
        await db.SaveChangesAsync();

        var svc = new AnomaliaService(db);
        // Consumo de 35 = 175% da média (20) — acima de 150% = vazamento
        var suspeita = await svc.VerificarSuspeitaVazamentoAsync(unidade.Id, 35);

        Assert.True(suspeita);
    }

    [Fact]
    public async Task ValidarLeitura_ComTrocaRecente_Aceita()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Teste", QtdUnidades = 1 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var unidade = new Unidade { CondominioId = condo.Id, Numero = "101" };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        var os = new OrdemServico { CondominioId = condo.Id, Mes = 3, Ano = 2026 };
        db.OrdensServico.Add(os);
        await db.SaveChangesAsync();

        // Leitura anterior validada com valor alto
        db.LeiturasHidrometro.Add(new LeituraHidrometro
        {
            OsId = os.Id,
            UnidadeId = unidade.Id,
            Status = StatusLeitura.Validado,
            ValorM3Validado = 500m,
            ValidadoEm = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Troca recente registrada (hoje)
        db.HistoricoTrocaHidrometro.Add(new HistoricoTrocaHidrometro
        {
            UnidadeId = unidade.Id,
            DataTroca = DateOnly.FromDateTime(DateTime.Today)
        });
        await db.SaveChangesAsync();

        var svc = new AnomaliaService(db);
        var (valida, motivo) = await svc.ValidarLeituraAsync(unidade.Id, 10m);

        Assert.True(valida);
        Assert.Null(motivo);
    }

    [Fact]
    public async Task VerificarOutlier_HistoricoInsuficiente_RetornaFalse()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Teste", QtdUnidades = 1 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var unidade = new Unidade { CondominioId = condo.Id, Numero = "101" };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        // Somente 3 entradas (mínimo exige 4)
        for (int i = 1; i <= 3; i++)
        {
            db.HistoricoConsumo.Add(new HistoricoConsumo
            {
                UnidadeId = unidade.Id,
                ConsumoM3 = 20m,
                Mes = i,
                Ano = 2025
            });
        }
        await db.SaveChangesAsync();

        var svc = new AnomaliaService(db);
        var resultado = await svc.VerificarOutlierAsync(unidade.Id, 100m);

        Assert.False(resultado);
    }

    [Fact]
    public async Task VerificarVazamento_HistoricoInsuficiente_RetornaFalse()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Teste", QtdUnidades = 1 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var unidade = new Unidade { CondominioId = condo.Id, Numero = "101" };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        // Somente 1 entrada (mínimo exige 2)
        db.HistoricoConsumo.Add(new HistoricoConsumo
        {
            UnidadeId = unidade.Id,
            ConsumoM3 = 20m,
            Mes = 1,
            Ano = 2025
        });
        await db.SaveChangesAsync();

        var svc = new AnomaliaService(db);
        var resultado = await svc.VerificarSuspeitaVazamentoAsync(unidade.Id, 100m);

        Assert.False(resultado);
    }

    [Fact]
    public async Task VerificarVazamento_ConsumoNormal_RetornaFalse()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Teste", QtdUnidades = 1 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var unidade = new Unidade { CondominioId = condo.Id, Numero = "101" };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        // Histórico: 6 meses com consumo de 20 m³
        for (int i = 1; i <= 6; i++)
        {
            db.HistoricoConsumo.Add(new HistoricoConsumo
            {
                UnidadeId = unidade.Id,
                ConsumoM3 = 20m,
                Mes = i,
                Ano = 2025
            });
        }
        await db.SaveChangesAsync();

        var svc = new AnomaliaService(db);
        // 25 = 125% da média 20 — abaixo de 150%, não é vazamento
        var resultado = await svc.VerificarSuspeitaVazamentoAsync(unidade.Id, 25m);

        Assert.False(resultado);
    }
}
