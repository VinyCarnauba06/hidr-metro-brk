using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace HidrometroApp.Tests.Integration;

public static class IntegrationTestHelper
{
    public const string JwtSecret = "test-secret-minimo-32-chars-aqui!!";

    public static string GerarToken(int userId, string nome, string perfil)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, nome),
            new Claim(ClaimTypes.Role, perfil)
        };

        var token = new JwtSecurityToken(
            issuer: "HidrometroApp",
            audience: "HidrometroApp",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void SetBearer(this HttpClient client, int userId, string nome, string perfil)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GerarToken(userId, nome, perfil));
    }

    /// <summary>Cria bytes JPEG falsos com tamanho >= 50KB para passar ValidarQualidadeFoto.</summary>
    public static byte[] CriarFotoFake(int tamanhoKb = 55)
    {
        var bytes = new byte[tamanhoKb * 1024];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xE0;
        for (int i = 4; i < bytes.Length; i++)
            bytes[i] = (byte)(i % 200 + 20);
        return bytes;
    }
}
