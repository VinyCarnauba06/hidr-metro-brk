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
        db.Usuarios.Add(new Usuario { Nome = "Teste", Cpf = "12345678901", SenhaHash = hash, Perfil = PerfilUsuario.Fiscal, Ativo = true });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken());
        var result = await svc.LoginAsync(new LoginRequest { Cpf = "12345678901", Senha = "Senha@123" });

        Assert.NotEmpty(result.Token);
        Assert.Equal("Fiscal", result.Perfil);
    }

    [Fact]
    public async Task Login_SenhaErrada_LancaUnauthorized()
    {
        var db = CriarDb();
        var hash = BCrypt.Net.BCrypt.HashPassword("Senha@123");
        db.Usuarios.Add(new Usuario { Nome = "Teste", Cpf = "12345678901", SenhaHash = hash, Perfil = PerfilUsuario.Fiscal, Ativo = true });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            svc.LoginAsync(new LoginRequest { Cpf = "12345678901", Senha = "SenhaErrada" }));
    }

    [Fact]
    public async Task Login_UsuarioInativo_LancaUnauthorized()
    {
        var db = CriarDb();
        var hash = BCrypt.Net.BCrypt.HashPassword("Senha@123");
        db.Usuarios.Add(new Usuario { Nome = "Inativo", Cpf = "99999999999", SenhaHash = hash, Perfil = PerfilUsuario.Fiscal, Ativo = false });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            svc.LoginAsync(new LoginRequest { Cpf = "99999999999", Senha = "Senha@123" }));
    }

    [Fact]
    public async Task Login_CpfComMascara_Normaliza()
    {
        var db = CriarDb();
        var hash = BCrypt.Net.BCrypt.HashPassword("Senha@123");
        db.Usuarios.Add(new Usuario { Nome = "Teste", Cpf = "12345678901", SenhaHash = hash, Perfil = PerfilUsuario.Fiscal, Ativo = true });
        await db.SaveChangesAsync();

        var svc = new AuthService(db, MockToken());
        // CPF com pontuação deve ser normalizado
        var result = await svc.LoginAsync(new LoginRequest { Cpf = "123.456.789-01", Senha = "Senha@123" });

        Assert.NotEmpty(result.Token);
    }
}
