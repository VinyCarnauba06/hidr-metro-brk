namespace HidrometroApp.Infrastructure.Security;

public static class PasswordHasher
{
    public static string Hash(string senha) => BCrypt.Net.BCrypt.HashPassword(senha);

    public static bool Verify(string senha, string hash) => BCrypt.Net.BCrypt.Verify(senha, hash);
}
