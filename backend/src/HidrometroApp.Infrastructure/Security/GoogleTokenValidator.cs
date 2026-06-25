// Infrastructure/Security/GoogleTokenValidator.cs
using Google.Apis.Auth;
using HidrometroApp.Core.Entities.DTOs;
using HidrometroApp.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace HidrometroApp.Infrastructure.Security;

public class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly string? _clientId;

    public GoogleTokenValidator(IConfiguration config)
    {
        _clientId = config["GOOGLE_CLIENT_ID"];
    }

    public async Task<GooglePayload> ValidarAsync(string idToken)
    {
        var settings = string.IsNullOrWhiteSpace(_clientId)
            ? new GoogleJsonWebSignature.ValidationSettings()
            : new GoogleJsonWebSignature.ValidationSettings { Audience = [_clientId] };

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            throw new UnauthorizedAccessException($"id_token Google inválido: {ex.Message}", ex);
        }

        var email = payload.Email;
        var nome = payload.Name ?? payload.Email;

        if (string.IsNullOrWhiteSpace(email))
            throw new UnauthorizedAccessException("id_token Google não contém email.");

        return new GooglePayload(email, nome);
    }
}
