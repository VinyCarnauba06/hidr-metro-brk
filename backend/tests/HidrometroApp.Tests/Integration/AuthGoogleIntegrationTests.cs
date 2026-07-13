// backend/tests/HidrometroApp.Tests/Integration/AuthGoogleIntegrationTests.cs
using System.Net;
using System.Net.Http.Json;
using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HidrometroApp.Tests.Integration;

/// <summary>
/// Fake para IGoogleTokenValidator em testes de integração: trata o idToken recebido
/// como o próprio email, evitando dependência de rede/credenciais reais do Google.
/// </summary>
public class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    public Task<GooglePayload> ValidarAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new UnauthorizedAccessException("id_token vazio.");

        return Task.FromResult(new GooglePayload(idToken, "Usuário Teste Google"));
    }
}

public class AuthGoogleIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthGoogleIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

    // Cria um client próprio por teste (via WithWebHostBuilder) para poder variar
    // GOOGLE_ALLOWED_DOMAIN sem afetar a factory compartilhada com os outros testes de Auth.
    private HttpClient CreateClient(string? allowedDomain)
    {
        var webFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GOOGLE_ALLOWED_DOMAIN"] = allowedDomain
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleTokenValidator>();
                services.AddSingleton<IGoogleTokenValidator, FakeGoogleTokenValidator>();
            });
        });

        return webFactory.CreateClient();
    }

    [Fact]
    public async Task LoginGoogle_UsuarioCadastradoSemRestricaoDominio_Retorna200EToken()
    {
        await _factory.SeedAsync();
        var client = CreateClient(allowedDomain: null);

        var response = await client.PostAsJsonAsync("/api/auth/google", new { idToken = "admin@prolar.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Token);
        Assert.Equal("Admin", body.Perfil);
    }

    [Fact]
    public async Task LoginGoogle_DominioNaoAutorizado_Retorna401()
    {
        await _factory.SeedAsync();
        var client = CreateClient(allowedDomain: "prolarage.com.br");

        // admin@prolar.com não termina em @prolarage.com.br — deve ser barrado antes de checar cadastro
        var response = await client.PostAsJsonAsync("/api/auth/google", new { idToken = "admin@prolar.com" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginGoogle_DominioAutorizadoEUsuarioCadastrado_Retorna200()
    {
        await _factory.SeedAsync();
        var client = CreateClient(allowedDomain: "prolar.com");

        var response = await client.PostAsJsonAsync("/api/auth/google", new { idToken = "admin@prolar.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LoginGoogle_UsuarioNaoCadastrado_Retorna401()
    {
        await _factory.SeedAsync();
        var client = CreateClient(allowedDomain: null);

        var response = await client.PostAsJsonAsync("/api/auth/google", new { idToken = "desconhecido@prolar.com" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginGoogle_IdTokenVazio_Retorna401()
    {
        await _factory.SeedAsync();
        var client = CreateClient(allowedDomain: null);

        var response = await client.PostAsJsonAsync("/api/auth/google", new { idToken = "" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
