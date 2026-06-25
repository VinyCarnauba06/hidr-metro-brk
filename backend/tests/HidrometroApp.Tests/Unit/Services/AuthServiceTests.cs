using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Core.Services;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Login_CredenciaisValidas_RetornaToken()
    {
        var db = CriarDb();
        var hash = BCrypt.Net.BCrypt.HashPassword("Senha@123");
        db.Usuarios.Add(new Usuario { Nome = "Teste", Email = "fiscal@teste.com", SenhaHash = hash, Perfil = PerfilUsuario.Fiscal, Ativo = true });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken(), new Mock<IGoogleTokenValidator>().Object);
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

        var svc = new AuthService(db, MockToken(), new Mock<IGoogleTokenValidator>().Object);

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

        var svc = new AuthService(db, MockToken(), new Mock<IGoogleTokenValidator>().Object);

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

        var svc = new AuthService(db, MockToken(), new Mock<IGoogleTokenValidator>().Object);
        // Email com letras maiúsculas deve ser normalizado para minúsculas
        var result = await svc.LoginAsync(new LoginRequest { Email = "FISCAL@TESTE.COM", Senha = "Senha@123" });

        Assert.NotEmpty(result.Token);
    }
}
