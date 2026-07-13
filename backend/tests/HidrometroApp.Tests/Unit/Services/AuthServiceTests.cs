using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Core.Services;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace HidrometroApp.Tests.Unit.Services;

public class AuthServiceTests
{
    private HidrometroDbContext CriarDb()
    {
        var opts = new DbContextOptionsBuilder<HidrometroDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HidrometroDbContext(opts);
    }

    private ITokenGenerator MockToken()
    {
        var mock = new Mock<ITokenGenerator>();
        mock.Setup(t => t.Gerar(It.IsAny<Usuario>()))
            .Returns(("token-fake", DateTime.UtcNow.AddHours(8)));
        return mock.Object;
    }

    private static IConfiguration Config(string? allowedDomain = null)
    {
        var dict = new Dictionary<string, string?> { ["GOOGLE_ALLOWED_DOMAIN"] = allowedDomain };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static IGoogleTokenValidator MockGoogle(string email, string nome = "Teste Google")
    {
        var mock = new Mock<IGoogleTokenValidator>();
        mock.Setup(g => g.ValidarAsync(It.IsAny<string>()))
            .ReturnsAsync(new GooglePayload(email, nome));
        return mock.Object;
    }

    [Fact]
    public async Task Login_CredenciaisValidas_RetornaToken()
    {
        var db = CriarDb();
        var hash = BCrypt.Net.BCrypt.HashPassword("Senha@123");
        db.Usuarios.Add(new Usuario { Nome = "Teste", Email = "fiscal@teste.com", SenhaHash = hash, Perfil = PerfilUsuario.Fiscal, Ativo = true });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken(), new Mock<IGoogleTokenValidator>().Object, Config());
        var result = await svc.LoginAsync(new LoginRequest { Email = "fiscal@teste.com", Senha = "Senha@123" });

        Assert.NotEmpty(result.Token);
        Assert.Equal("Fiscal", result.Perfil);
    }

    [Fact]
    public async Task Login_SenhaErrada_LancaUnauthorized()
    {
        var db = CriarDb();
        var hash = BCrypt.Net.BCrypt.HashPassword("Senha@123");
        db.Usuarios.Add(new Usuario { Nome = "Teste", Email = "fiscal@teste.com", SenhaHash = hash, Perfil = PerfilUsuario.Fiscal, Ativo = true });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken(), new Mock<IGoogleTokenValidator>().Object, Config());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            svc.LoginAsync(new LoginRequest { Email = "fiscal@teste.com", Senha = "SenhaErrada" }));
    }

    [Fact]
    public async Task Login_UsuarioInativo_LancaUnauthorized()
    {
        var db = CriarDb();
        var hash = BCrypt.Net.BCrypt.HashPassword("Senha@123");
        db.Usuarios.Add(new Usuario { Nome = "Inativo", Email = "inativo@teste.com", SenhaHash = hash, Perfil = PerfilUsuario.Fiscal, Ativo = false });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken(), new Mock<IGoogleTokenValidator>().Object, Config());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            svc.LoginAsync(new LoginRequest { Email = "inativo@teste.com", Senha = "Senha@123" }));
    }

    [Fact]
    public async Task Login_EmailCaseInsensitivo_Normaliza()
    {
        var db = CriarDb();
        var hash = BCrypt.Net.BCrypt.HashPassword("Senha@123");
        db.Usuarios.Add(new Usuario { Nome = "Teste", Email = "fiscal@teste.com", SenhaHash = hash, Perfil = PerfilUsuario.Fiscal, Ativo = true });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken(), new Mock<IGoogleTokenValidator>().Object, Config());
        // Email com letras maiúsculas deve ser normalizado para minúsculas
        var result = await svc.LoginAsync(new LoginRequest { Email = "FISCAL@TESTE.COM", Senha = "Senha@123" });

        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task LoginGoogle_DominioNaoAutorizado_LancaUnauthorized()
    {
        var db = CriarDb();
        db.Usuarios.Add(new Usuario { Nome = "Op", Email = "operador@gmail.com", SenhaHash = "x", Perfil = PerfilUsuario.Operador, Ativo = true });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken(), MockGoogle("operador@gmail.com"), Config("prolarage.com.br"));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => svc.LoginGoogleAsync("id-token-fake"));
        Assert.Contains("domínio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginGoogle_DominioAutorizado_RetornaToken()
    {
        var db = CriarDb();
        db.Usuarios.Add(new Usuario { Nome = "Op", Email = "operador@prolarage.com.br", SenhaHash = "x", Perfil = PerfilUsuario.Operador, Ativo = true });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken(), MockGoogle("operador@prolarage.com.br"), Config("prolarage.com.br"));
        var result = await svc.LoginGoogleAsync("id-token-fake");

        Assert.NotEmpty(result.Token);
        Assert.Equal("Operador", result.Perfil);
    }

    [Fact]
    public async Task LoginGoogle_SemRestricaoDeDominio_AceitaQualquerDominio()
    {
        var db = CriarDb();
        db.Usuarios.Add(new Usuario { Nome = "Op", Email = "operador@gmail.com", SenhaHash = "x", Perfil = PerfilUsuario.Operador, Ativo = true });
        await db.SaveChangesAsync();

        // GOOGLE_ALLOWED_DOMAIN vazio (default em dev) — não restringe, mas ainda exige usuário cadastrado.
        var svc = new AuthService(db, MockToken(), MockGoogle("operador@gmail.com"), Config());
        var result = await svc.LoginGoogleAsync("id-token-fake");

        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task LoginGoogle_UsuarioNaoCadastrado_LancaUnauthorized()
    {
        var db = CriarDb();

        var svc = new AuthService(db, MockToken(), MockGoogle("desconhecido@prolarage.com.br"), Config("prolarage.com.br"));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => svc.LoginGoogleAsync("id-token-fake"));
        Assert.Contains("não autorizada", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginGoogle_DominioComPontoOuArrobaNaConfig_NormalizaEValida()
    {
        var db = CriarDb();
        db.Usuarios.Add(new Usuario { Nome = "Op", Email = "operador@prolarage.com.br", SenhaHash = "x", Perfil = PerfilUsuario.Operador, Ativo = true });
        await db.SaveChangesAsync();

        // Config com "@" na frente do domínio deve ser aceita (TrimStart('@') no AuthService)
        var svc = new AuthService(db, MockToken(), MockGoogle("operador@prolarage.com.br"), Config("@prolarage.com.br"));
        var result = await svc.LoginGoogleAsync("id-token-fake");

        Assert.NotEmpty(result.Token);
    }
}
