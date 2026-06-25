// Core/Entities/DTOs/GoogleLoginRequest.cs
namespace HidrometroApp.Core.Entities.DTOs;

public class GoogleLoginRequest
{
    /// <summary>JWT id_token obtido pelo frontend via Google Identity Services.</summary>
    public string IdToken { get; set; } = string.Empty;
}

/// <summary>Payload extraído e validado do id_token Google.</summary>
public record GooglePayload(string Email, string Nome);
