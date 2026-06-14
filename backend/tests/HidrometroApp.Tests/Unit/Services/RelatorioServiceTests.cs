// backend/tests/HidrometroApp.Tests/Unit/Services/RelatorioServiceTests.cs
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Models;
using HidrometroApp.Core.Services;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HidrometroApp.Tests.Unit.Services;

public class RelatorioServiceTests
{
    private HidrometroDbContext CriarDb() =>
        new(new DbContextOptionsBuilder<HidrometroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private async Task<(HidrometroDbContext db, OrdemServico os)> CriarOsComLeituraValidadaAsync()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Condo Rel", QtdUnidades = 1 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var unidade = new Unidade { CondominioId = condo.Id, Numero = "101", Ativa = true };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();

        var os = new OrdemServico { CondominioId = condo.Id, Mes = 5, Ano = 2026, Status = StatusOS.Validada };
        db.OrdensServico.Add(os);
        await db.SaveChangesAsync();

        db.LeiturasHidrometro.Add(new LeituraHidrometro
        {
            OsId = os.Id,
            UnidadeId = unidade.Id,
            Status = StatusLeitura.Validado,
            ValorM3Validado = 123m,
            Origem = OrigemLeitura.Ia
        });
        await db.SaveChangesAsync();

        return (db, os);
    }

    [Fact]
    public async Task GerarExcel_OsNaoEncontrada_LancaNotFound()
    {
        var db = CriarDb();
        var svc = new RelatorioService(db);

        await Assert.ThrowsAsync<NotFoundException>(() => svc.GerarExcelAsync(9999));
    }

    [Fact]
    public async Task GerarExcel_RetornaBytesNaoVazios()
    {
        var (db, os) = await CriarOsComLeituraValidadaAsync();
        var svc = new RelatorioService(db);

        var bytes = await svc.GerarExcelAsync(os.Id);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task GerarPdf_RetornaBytesNaoVazios()
    {
        var (db, os) = await CriarOsComLeituraValidadaAsync();
        var svc = new RelatorioService(db);

        var bytes = await svc.GerarPdfAsync(os.Id);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task ObterDados_IncluiSomenteValidadas()
    {
        var db = CriarDb();

        var condo = new Condominio { Nome = "Condo Mix", QtdUnidades = 2 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();

        var u1 = new Unidade { CondominioId = condo.Id, Numero = "101", Ativa = true };
        var u2 = new Unidade { CondominioId = condo.Id, Numero = "102", Ativa = true };
        db.Unidades.AddRange(u1, u2);
        await db.SaveChangesAsync();

        var os = new OrdemServico { CondominioId = condo.Id, Mes = 5, Ano = 2026 };
        db.OrdensServico.Add(os);
        await db.SaveChangesAsync();

        db.LeiturasHidrometro.AddRange(
            new LeituraHidrometro { OsId = os.Id, UnidadeId = u1.Id, Status = StatusLeitura.Validado, ValorM3Validado = 100m },
            new LeituraHidrometro { OsId = os.Id, UnidadeId = u2.Id, Status = StatusLeitura.Pendente, ValorM3 = 200m }
        );
        await db.SaveChangesAsync();

        var svc = new RelatorioService(db);
        var dados = await svc.ObterDadosRelatorioAsync(os.Id);

        Assert.Single(dados.Itens);
        Assert.Equal("101", dados.Itens[0].Unidade);
    }
}
