using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Exceptions;
using HidrometroApp.Core.Interfaces;
using HidrometroApp.Core.Models;
using HidrometroApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HidrometroApp.Core.Services;

public class AuthService : IAuthService
{
    private readonly HidrometroDbContext _db;
    private readonly ITokenGenerator _token;
    private readonly IGoogleTokenValidator _google;
    private readonly string? _allowedGoogleDomain;

    public AuthService(HidrometroDbContext db, ITokenGenerator token, IGoogleTokenValidator google, IConfiguration config)
    {
        _db = db;
        _token = token;
        _google = google;
        _allowedGoogleDomain = config["GOOGLE_ALLOWED_DOMAIN"];
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

        // Restringe SSO ao domínio do Google Workspace configurado (ex: prolarage.com.br).
        // GOOGLE_ALLOWED_DOMAIN vazio desabilita a checagem (aceitável em dev; setar sempre em produção).
        if (!string.IsNullOrWhiteSpace(_allowedGoogleDomain))
        {
            var dominio = _allowedGoogleDomain.Trim().ToLowerInvariant().TrimStart('@');
            if (!email.EndsWith($"@{dominio}", StringComparison.Ordinal))
                throw new UnauthorizedException($"Conta Google fora do domínio autorizado ({dominio}).");
        }

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
