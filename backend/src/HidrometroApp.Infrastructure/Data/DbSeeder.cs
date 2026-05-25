using HidrometroApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace HidrometroApp.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(HidrometroDbContext context)
    {
        if (await context.Usuarios.AnyAsync()) return;

        var senhaHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");

        var admin = new Usuario
        {
            Nome = "Administrador",
            Cpf = "00000000000",
            SenhaHash = senhaHash,
            Perfil = PerfilUsuario.Admin,
            Ativo = true
        };

        var operador = new Usuario
        {
            Nome = "Operador Padrão",
            Cpf = "11111111111",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("Operador@123"),
            Perfil = PerfilUsuario.Operador,
            Ativo = true
        };

        var fiscal = new Usuario
        {
            Nome = "Fiscal Padrão",
            Cpf = "22222222222",
            SenhaHash = BCrypt.Net.BCrypt.HashPassword("Fiscal@123"),
            Perfil = PerfilUsuario.Fiscal,
            Ativo = true
        };

        context.Usuarios.AddRange(admin, operador, fiscal);

        var condo = new Condominio
        {
            Nome = "Residencial Teste",
            Endereco = "Rua Teste, 100 - Maceió/AL",
            QtdUnidades = 10,
            TipoMedidor = TipoMedidor.AguaFria
        };

        context.Condominios.Add(condo);
        await context.SaveChangesAsync();

        for (int i = 1; i <= 10; i++)
        {
            context.Unidades.Add(new Unidade
            {
                CondominioId = condo.Id,
                Numero = $"10{i}",
                Tipo = "Apartamento",
                Ativa = true
            });
        }

        await context.SaveChangesAsync();
    }
}
