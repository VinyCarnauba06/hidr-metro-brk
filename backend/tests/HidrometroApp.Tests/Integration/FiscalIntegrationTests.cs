using System.Net;
using System.Net.Http.Json;
using HidrometroApp.Core.Models;
using HidrometroApp.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HidrometroApp.Tests.Integration;

public class FiscalIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public FiscalIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

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

    [Fact]
    public async Task OrdensAbertas_SemAuth_Retorna401()
    {
        var response = await _client.GetAsync("/api/fiscal/ordens-abertas");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OrdensAbertas_ComoFiscal_Retorna200()
    {
        _client.SetBearer(_factory.FiscalId, "Fiscal Teste", "Fiscal");
        var response = await _client.GetAsync("/api/fiscal/ordens-abertas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Condo Integração", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFoto_ComFotoValida_Retorna200EConfiancaMaiorQueZero()
    {
        _client.SetBearer(_factory.FiscalId, "Fiscal Teste", "Fiscal");

        var fotoBytes = IntegrationTestHelper.CriarFotoFake(55);
        var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(fotoBytes);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(imageContent, "foto", "foto.jpg");
        content.Add(new StringContent(_factory.OsId.ToString()), "osId");
        content.Add(new StringContent(_factory.Unidade1Id.ToString()), "unidadeId");

        var response = await _client.PostAsync("/api/fiscal/leitura/upload", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("confiancaIa", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadFoto_SemFoto_Retorna400()
    {
        _client.SetBearer(_factory.FiscalId, "Fiscal Teste", "Fiscal");

        var content = new MultipartFormDataContent();
        content.Add(new StringContent(_factory.OsId.ToString()), "osId");
        content.Add(new StringContent(_factory.Unidade1Id.ToString()), "unidadeId");

        var response = await _client.PostAsync("/api/fiscal/leitura/upload", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recurso_ComValorNegativo_Retorna422()
    {
        _client.SetBearer(_factory.FiscalId, "Fiscal Teste", "Fiscal");

        // Cria uma leitura pendente para usar no recurso
        int leituraId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HidrometroDbContext>();
            var leitura = new LeituraHidrometro
            {
                OsId = _factory.OsId,
                UnidadeId = _factory.Unidade1Id,
                Status = StatusLeitura.Rejeitado,
                ValorM3 = 100m
            };
            db.LeiturasHidrometro.Add(leitura);
            await db.SaveChangesAsync();
            leituraId = leitura.Id;
        }

        var response = await _client.PostAsJsonAsync($"/api/fiscal/leitura/{leituraId}/recurso", new
        {
            valorM3 = -5m,
            observacao = "Tentativa de valor negativo"
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Progresso_Retorna200EPercentualValido()
    {
        _client.SetBearer(_factory.FiscalId, "Fiscal Teste", "Fiscal");

        var response = await _client.GetAsync($"/api/fiscal/os/{_factory.OsId}/progresso");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("percentualConcluido", body, StringComparison.OrdinalIgnoreCase);
    }
}
