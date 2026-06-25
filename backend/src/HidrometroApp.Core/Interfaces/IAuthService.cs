using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Models;

namespace HidrometroApp.Core.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> LoginGoogleAsync(string idToken);
    Task<Usuario?> ObterPorIdAsync(int id);
}
