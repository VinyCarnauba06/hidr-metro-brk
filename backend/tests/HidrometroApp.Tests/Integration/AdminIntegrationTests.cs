using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HidrometroApp.Tests.Integration;

public class AdminIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AdminIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.SeedAsync();
        _client = _factory.CreateClient();
        _client.SetBearer(_factory.AdminId, "Admin Teste", "Admin");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CriarCondominio_DadosValidos_Retorna201()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/condominios", new
        {
            nome = "Novo Condo Teste",
            endereco = "Rua X, 123",
            qtdUnidades = 5,
            tipoMedidor = "AguaFria"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Novo Condo Teste", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CriarCondominio_ComNumerosCustomizados_CriaUnidadesComNumerosReais()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/condominios", new
        {
            nome = "Condo Numeracao Real",
            endereco = "Rua Y, 456",
            qtdUnidades = 4,
            tipoMedidor = "AguaFria",
            numeros = new[] { "101", "102", "201", "202" }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var condoId = body.GetProperty("id").GetInt32();

        var listResponse = await _client.GetAsync("/api/admin/condominios");
        var lista = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains("Condo Numeracao Real", lista, StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HidrometroApp.Infrastructure.Data.HidrometroDbContext>();
        var numeros = db.Unidades.Where(u => u.CondominioId == condoId).Select(u => u.Numero).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "101", "102", "201", "202" }, numeros);
    }

    [Fact]
    public async Task CriarCondominio_NumerosNaoBateComQtdUnidades_Retorna422()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/condominios", new
        {
            nome = "Condo Inconsistente",
            endereco = "Rua Z, 789",
            qtdUnidades = 3,
            tipoMedidor = "AguaFria",
            numeros = new[] { "101", "102" }
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CriarCondominio_NumerosDuplicados_Retorna422()
    {
        var response = await _client.PostAsJsonAsync("/api/admin/condominios", new
        {
            nome = "Condo Duplicado",
            endereco = "Rua W, 000",
            qtdUnidades = 3,
            tipoMedidor = "AguaFria",
            numeros = new[] { "101", "101", "102" }
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CriarOrdem_Duplicada_Retorna409()
    {
        // Tenta criar OS com mesmo condomínio/mês/ano que já existe no seed
        var response = await _client.PostAsJsonAsync("/api/admin/ordens", new
        {
            condominioId = _factory.CondominioId,
            mes = DateTime.Now.Month,
            ano = DateTime.Now.Year
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CriarUsuario_EmailDuplicado_Retorna409()
    {
        // Email "admin@prolar.com" já foi cadastrado no seed como Admin
        var response = await _client.PostAsJsonAsync("/api/admin/usuarios", new
        {
            nome = "Admin Duplicado",
            email = "admin@prolar.com",
            senha = "Senha@123",
            perfil = "Admin"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Auditoria_AposAcaoAdmin_RetornaRegistro()
    {
        // Cria um usuário novo para gerar log de auditoria
        await _client.PostAsJsonAsync("/api/admin/usuarios", new
        {
            nome = "Usuario Auditoria",
            email = "auditoria@teste.com",
            senha = "Senha@123",
            perfil = "Fiscal"
        });

        var response = await _client.GetAsync("/api/admin/auditoria");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("INSERT", body, StringComparison.OrdinalIgnoreCase);
    }
}
