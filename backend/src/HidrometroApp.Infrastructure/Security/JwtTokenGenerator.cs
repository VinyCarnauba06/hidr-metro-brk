using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HidrometroApp.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HidrometroApp.Infrastructure.Security;

public class JwtTokenGenerator
{
    private readonly IConfiguration _config;

    public JwtTokenGenerator(IConfiguration config)
    {
        _config = config;
    }

    public (string token, DateTime expira) Gerar(Usuario usuario)
    {
        var secret = _config["JWT_SECRET"] ?? _config["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT_SECRET não configurado");
        var horasExpiracao = int.Parse(_config["JWT_EXPIRATION_HOURS"] ?? _config["Jwt:ExpirationHours"] ?? "8");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expira = DateTime.UtcNow.AddHours(horasExpiracao);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Name, usuario.Nome),
            new Claim(ClaimTypes.Role, usuario.Perfil.ToString()),
            new Claim("email", usuario.Email),
        };

        var token = new JwtSecurityToken(
            issuer: "HidrometroApp",
            audience: "HidrometroApp",
            claims: claims,
            expires: expira,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
