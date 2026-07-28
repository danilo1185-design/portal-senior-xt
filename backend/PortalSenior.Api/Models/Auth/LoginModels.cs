using System.ComponentModel.DataAnnotations;

namespace PortalSenior.Api.Models.Auth;

/// <summary>Credenciais do usuário cadastradas no SGU do ERP Senior.</summary>
public sealed class LoginRequest
{
    [Required(ErrorMessage = "Informe o usuário.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public required string Token { get; init; }

    public required string Username { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }
}
