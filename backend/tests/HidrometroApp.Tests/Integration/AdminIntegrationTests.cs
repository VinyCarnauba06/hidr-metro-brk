using System.Net;
using System.Net.Http.Json;
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
