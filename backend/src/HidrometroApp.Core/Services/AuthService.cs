using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HidrometroApp.Core.Services;

public class AuthService : IAuthService
{
    private readonly HidrometroDbContext _db;
    private readonly ITokenGenerator _token;

    public AuthService(HidrometroDbContext db, ITokenGenerator token)
    {
        _db = db;
        _token = token;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var cpf = request.Cpf.Replace(".", "").Replace("-", "");

        var usuario = await _db.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Cpf == cpf && u.Ativo);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(request.Senha, usuario.SenhaHash))
            throw new UnauthorizedException("CPF ou senha inválidos");

        var (tokenStr, expira) = _token.Gerar(usuario);

        return new LoginResponse
        {
            Token = tokenStr,
            Nome = usuario.Nome,
            Perfil = usuario.Perfil.ToString(),
            ExpiraEm = expira
        };
    }

    public async Task<Usuario?> ObterPorIdAsync(int id)
    {
        return await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }
}
