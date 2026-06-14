// backend/tests/HidrometroApp.Tests/Unit/Services/LeituraServiceTests.cs
using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Core.Services;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HidrometroApp.Tests.Unit.Services;

public class LeituraServiceTests
{
    private HidrometroDbContext CriarDb() =>
        new(new DbContextOptionsBuilder<HidrometroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private LeituraService CriarServico(HidrometroDbContext db,
        IAzureVisionService? vision = null,
        IAuditoriaService? auditoria = null)
    {
        vision ??= new Mock<IAzureVisionService>().Object;
        auditoria ??= new Mock<IAuditoriaService>().Object;
        var config = new Mock<IConfiguration>();
        return new LeituraService(db, vision, auditoria, new AnomaliaService(db), config.Object,
            NullLogger<LeituraService>.Instance);
    }

    private async Task<(HidrometroDbContext db, Condominio condo, Unidade unidade, OrdemServico os)> CriarBaseAsync()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Condo Teste", QtdUnidades = 1 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var unidade = new Unidade { CondominioId = condo.Id, Numero = "101", Ativa = true };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        var os = new OrdemServico
        {
            CondominioId = condo.Id,
            Mes = DateTime.Now.Month,
            Ano = DateTime.Now.Year,
            Status = StatusOS.Aberta
        };
        db.OrdensServico.Add(os);
        await db.SaveChangesAsync();

        return (db, condo, unidade, os);
    }

    [Fact]
    public async Task UploadFoto_LimiteAtingido_LancaFotoRejeitada()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();

        for (int i = 0; i < 3; i++)
        {
            db.LeiturasHidrometro.Add(new LeituraHidrometro
            {
                OsId = os.Id,
                UnidadeId = unidade.Id,
                Status = StatusLeitura.Rejeitado
            });
        }
        await db.SaveChangesAsync();

        var svc = CriarServico(db);

        await Assert.ThrowsAsync<FotoRejeitadaException>(() =>
            svc.UploadFotoAsync(os.Id, unidade.Id, 1, Array.Empty<byte>(), "foto.jpg"));
    }

    [Fact]
    public async Task RegistrarManual_LeituraInvalida_LancaException()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();

        var leitura = new LeituraHidrometro
        {
            OsId = os.Id,
            UnidadeId = unidade.Id,
            Status = StatusLeitura.Rejeitado
        };
        db.LeiturasHidrometro.Add(leitura);
        await db.SaveChangesAsync();

        var svc = CriarServico(db);

        await Assert.ThrowsAsync<LeituraInvalidaException>(() =>
            svc.RegistrarManualAsync(leitura.Id, 1, new RecursoManualRequest { ValorM3 = -5m }));
    }

    [Fact]
    public async Task ValidarLeitura_AtualizaStatus_ERegistraHistorico()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();

        var leitura = new LeituraHidrometro
        {
            OsId = os.Id,
            UnidadeId = unidade.Id,
            Status = StatusLeitura.Pendente,
            ValorM3 = 100m
        };
        db.LeiturasHidrometro.Add(leitura);
        await db.SaveChangesAsync();

        var mockAuditoria = new Mock<IAuditoriaService>();
        var svc = CriarServico(db, auditoria: mockAuditoria.Object);

        var result = await svc.ValidarAsync(leitura.Id, 1, new ValidarLeituraRequest());

        Assert.Equal("Validado", result.Status);

        var historico = await db.HistoricoConsumo.Where(h => h.OsId == os.Id).ToListAsync();
        Assert.Single(historico);
    }

    [Fact]
    public async Task RejeitarLeitura_MotivoVazio_LancaValidation()
    {
        var (db, _, unidade, os) = await CriarBaseAsync();

        var leitura = new LeituraHidrometro
        {
            OsId = os.Id,
            UnidadeId = unidade.Id,
            Status = StatusLeitura.Pendente
        };
        db.LeiturasHidrometro.Add(leitura);
        await db.SaveChangesAsync();

        var svc = CriarServico(db);

        await Assert.ThrowsAsync<HidrometroValidationException>(() =>
            svc.RejeitarAsync(leitura.Id, 1, new ValidarLeituraRequest { MotivoRejeicao = "" }));
    }

    [Fact]
    public async Task ObterProgresso_OsCompleta_RetornaPercentual100()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Condo", QtdUnidades = 2 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var u1 = new Unidade { CondominioId = condo.Id, Numero = "101", Ativa = true };
        var u2 = new Unidade { CondominioId = condo.Id, Numero = "102", Ativa = true };
        db.Unidades.AddRange(u1, u2);
        await db.SaveChangesAsync();

        var os = new OrdemServico { CondominioId = condo.Id, Mes = DateTime.Now.Month, Ano = DateTime.Now.Year };
        db.OrdensServico.Add(os);
        await db.SaveChangesAsync();

        db.LeiturasHidrometro.AddRange(
            new LeituraHidrometro { OsId = os.Id, UnidadeId = u1.Id, Status = StatusLeitura.Validado, ValorM3Validado = 100m },
            new LeituraHidrometro { OsId = os.Id, UnidadeId = u2.Id, Status = StatusLeitura.Validado, ValorM3Validado = 200m }
        );
        await db.SaveChangesAsync();

        var svc = CriarServico(db);
        var progresso = await svc.ObterProgressoAsync(os.Id);

        Assert.Equal(100m, progresso.PercentualConcluido);
        Assert.Equal(0, progresso.FaltandoRegistrar);
    }
}
