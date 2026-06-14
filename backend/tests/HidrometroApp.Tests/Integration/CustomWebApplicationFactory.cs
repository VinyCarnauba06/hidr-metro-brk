using HidrometroApp.Core.Models;
using HidrometroApp.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HidrometroApp.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    public int AdminId { get; private set; }
    public int OperadorId { get; private set; }
    public int FiscalId { get; private set; }
    public int CondominioId { get; private set; }
    public int Unidade1Id { get; private set; }
    public int Unidade2Id { get; private set; }
    public int OsId { get; private set; }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Set before Program.cs inline reads (JWT_SECRET and DATABASE_URL are read at startup)
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_URL")))
            Environment.SetEnvironmentVariable("DATABASE_URL", "Host=localhost;Database=test_placeholder");

        Environment.SetEnvironmentVariable("JWT_SECRET", IntegrationTestHelper.JwtSecret);
        Environment.SetEnvironmentVariable("AZURE_VISION_ENDPOINT", "");
        Environment.SetEnvironmentVariable("AZURE_VISION_KEY", "");

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Replace Npgsql with InMemory — unique DB per factory instance
            var descriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<HidrometroDbContext>)
                         || d.ServiceType == typeof(HidrometroDbContext))
                .ToList();
            foreach (var d in descriptors) services.Remove(d);

            services.AddDbContext<HidrometroDbContext>(opts =>
                opts.UseInMemoryDatabase(_dbName));
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HidrometroDbContext>();

        if (await db.Usuarios.AnyAsync()) return; // idempotent

        var admin = new Usuario
        {
            Nome = "Admin Teste",
            Email = "admin@prolar.com",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Perfil = PerfilUsuario.Admin,
            Ativo = true
        };
        var operador = new Usuario
        {
            Nome = "Operador Teste",
            Email = "operador@prolar.com",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("Operador@123"),
            Perfil = PerfilUsuario.Operador,
            Ativo = true
        };
        var fiscal = new Usuario
        {
            Nome = "Fiscal Teste",
            Email = "fiscal@prolar.com",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("Fiscal@123"),
            Perfil = PerfilUsuario.Fiscal,
            Ativo = true
        };

        db.Usuarios.AddRange(admin, operador, fiscal);
        await db.SaveChangesAsync();

        AdminId = admin.Id;
        OperadorId = operador.Id;
        FiscalId = fiscal.Id;

        var condo = new Condominio { Nome = "Condo Integração", QtdUnidades = 2 };
        db.Condominios.Add(condo);
        await db.SaveChangesAsync();
        CondominioId = condo.Id;

        var u1 = new Unidade { CondominioId = condo.Id, Numero = "101", Ativa = true };
        var u2 = new Unidade { CondominioId = condo.Id, Numero = "102", Ativa = true };
        db.Unidades.AddRange(u1, u2);
        await db.SaveChangesAsync();

        Unidade1Id = u1.Id;
        Unidade2Id = u2.Id;

        var os = new OrdemServico
        {
            CondominioId = condo.Id,
            Mes = DateTime.Now.Month,
            Ano = DateTime.Now.Year,
            Status = StatusOS.Aberta
        };
        db.OrdensServico.Add(os);

        db.OperadorCondominios.Add(new OperadorCondominio
        {
            OperadorId = operador.Id,
            CondominioId = condo.Id
        });

        await db.SaveChangesAsync();

        OsId = os.Id;
    }
}
