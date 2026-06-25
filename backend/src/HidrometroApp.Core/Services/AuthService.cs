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
    private readonly IGoogleTokenValidator _google;

    public AuthService(HidrometroDbContext db, ITokenGenerator token, IGoogleTokenValidator google)
    {
        _db = db;
        _token = token;
        _google = google;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var usuario = await _db.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.Ativo);

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(request.Senha, usuario.SenhaHash))
            throw new UnauthorizedException("Email ou senha inválidos");

        var (tokenStr, expira) = _token.Gerar(usuario);

        return new LoginResponse
        {
            Token = tokenStr,
            Nome = usuario.Nome,
            Perfil = usuario.Perfil.ToString(),
            ExpiraEm = expira
        };
    }

    public async Task<LoginResponse> LoginGoogleAsync(string idToken)
    {
        var payload = await _google.ValidarAsync(idToken);

        var email = payload.Email.Trim().ToLowerInvariant();

        var usuario = await _db.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email && u.Ativo);

        if (usuario == null)
            throw new UnauthorizedException("Conta Google não autorizada. Solicite acesso ao administrador.");

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
