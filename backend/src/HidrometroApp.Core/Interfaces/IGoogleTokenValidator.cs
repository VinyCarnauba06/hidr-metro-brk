// Core/Interfaces/IGoogleTokenValidator.cs
using HidrometroApp.Core.Entities.DTOs;

namespace HidrometroApp.Core.Interfaces;

/// <summary>
/// Valida um id_token emitido pelo Google e extrai o payload.
/// Lança <see cref="UnauthorizedAccessException"/> se o token for inválido.
/// </summary>
public interface IGoogleTokenValidator
{
    Task<GooglePayload> ValidarAsync(string idToken);
}
