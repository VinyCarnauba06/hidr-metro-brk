using System.Net;
using System.Net.Http.Json;
using HidrometroApp.Core.Models;
using HidrometroApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HidrometroApp.Tests.Integration;

public class OperadorIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public OperadorIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.SeedAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> CriarLeituraPendenteAsync(int? unidadeId = null, int? osId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HidrometroDbContext>();
        var leitura = new LeituraHidrometro
        {
            OsId = osId ?? _factory.OsId,
            UnidadeId = unidadeId ?? _factory.Unidade1Id,
            Status = StatusLeitura.Pendente,
            ValorM3 = 100m
        };
        db.LeiturasHidrometro.Add(leitura);
        await db.SaveChangesAsync();
        return leitura.Id;
    }

    [Fact]
    public async Task OrdensAguardando_ComoFiscal_Retorna403()
    {
        _client.SetBearer(_factory.FiscalId, "Fiscal Teste", "Fiscal");
        var response = await _client.GetAsync("/api/operador/ordens-aguardando");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Validar_LeituraExistente_RetornaStatusValidado()
    {
        _client.SetBearer(_factory.OperadorId, "Operador Teste", "Operador");

        // Cria condo/unidade/OS isolados para evitar interferência entre testes
        int leituraId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HidrometroDbContext>();
            var condo = new Condominio { Nome = "Condo Validar", QtdUnidades = 1 };
            db.Condominios.Add(condo);
            await db.SaveChangesAsync();
            var unidade = new Unidade { CondominioId = condo.Id, Numero = "V01", Ativa = true };
            db.Unidades.Add(unidade);
            await db.SaveChangesAsync();
            var os = new OrdemServico { CondominioId = condo.Id, Mes = 1, Ano = 2025, Status = StatusOS.Aberta };
            db.OrdensServico.Add(os);
            await db.SaveChangesAsync();
            var leitura = new LeituraHidrometro
            {
                OsId = os.Id, UnidadeId = unidade.Id, Status = StatusLeitura.Pendente, ValorM3 = 150m
            };
            db.LeiturasHidrometro.Add(leitura);
            await db.SaveChangesAsync();
            leituraId = leitura.Id;
        }

        var response = await _client.PatchAsJsonAsync($"/api/operador/leituras/{leituraId}/validar", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Validado", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Corrigir_SemValorCorrigido_RetornaBadRequest()
    {
        _client.SetBearer(_factory.OperadorId, "Operador Teste", "Operador");
        var leituraId = await CriarLeituraPendenteAsync();

        // Envia request sem ValorM3Corrigido — deve lançar HidrometroValidationException → 400
        var response = await _client.PatchAsJsonAsync($"/api/operador/leituras/{leituraId}/corrigir", new
        {
            valorM3Corrigido = (decimal?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejeitar_SemMotivo_RetornaBadRequest()
    {
        _client.SetBearer(_factory.OperadorId, "Operador Teste", "Operador");
        var leituraId = await CriarLeituraPendenteAsync(_factory.Unidade2Id);

        var response = await _client.PatchAsJsonAsync($"/api/operador/leituras/{leituraId}/rejeitar", new
        {
            motivoRejeicao = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GerarExcel_OsIncompleta_Retorna400()
    {
        _client.SetBearer(_factory.OperadorId, "Operador Teste", "Operador");

        // Cria OS com 2 unidades e nenhuma leitura → FaltandoRegistrar = 2
        int osId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HidrometroDbContext>();
            var condo = new Condominio { Nome = "Condo Incompleta", QtdUnidades = 2 };
            db.Condominios.Add(condo);
            await db.SaveChangesAsync();
            db.Unidades.AddRange(
                new Unidade { CondominioId = condo.Id, Numero = "I01", Ativa = true },
                new Unidade { CondominioId = condo.Id, Numero = "I02", Ativa = true });
            await db.SaveChangesAsync();
            var os = new OrdemServico { CondominioId = condo.Id, Mes = 3, Ano = 2025, Status = StatusOS.Aberta };
            db.OrdensServico.Add(os);
            await db.SaveChangesAsync();
            osId = os.Id;
        }

        var response = await _client.PostAsync($"/api/operador/relatorio/{osId}/excel", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GerarExcel_OsCompleta_Retorna200EBytesXlsx()
    {
        _client.SetBearer(_factory.OperadorId, "Operador Teste", "Operador");

        // Cria OS com 1 unidade + 1 leitura validada → FaltandoRegistrar = 0
        int osId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HidrometroDbContext>();
            var condo = new Condominio { Nome = "Condo Completa", QtdUnidades = 1 };
            db.Condominios.Add(condo);
            await db.SaveChangesAsync();
            var unidade = new Unidade { CondominioId = condo.Id, Numero = "C01", Ativa = true };
            db.Unidades.Add(unidade);
            await db.SaveChangesAsync();
            var os = new OrdemServico { CondominioId = condo.Id, Mes = 4, Ano = 2025, Status = StatusOS.EmProgresso };
            db.OrdensServico.Add(os);
            await db.SaveChangesAsync();
            db.LeiturasHidrometro.Add(new LeituraHidrometro
            {
                OsId = os.Id,
                UnidadeId = unidade.Id,
                Status = StatusLeitura.Validado,
                ValorM3Validado = 200m,
                Origem = OrigemLeitura.Ia
            });
            await db.SaveChangesAsync();
            osId = os.Id;
        }

        var response = await _client.PostAsync($"/api/operador/relatorio/{osId}/excel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
        // XLSX files start with PK (ZIP header: 50 4B)
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }
}
