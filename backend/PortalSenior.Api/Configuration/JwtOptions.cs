namespace PortalSenior.Api.Configuration;

/// <summary>
/// Configurações do token JWT emitido pelo portal após a validação das credenciais no ERP.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "PortalSeniorXT";

    public string Audience { get; set; } = "PortalSeniorXT";

    /// <summary>Chave de assinatura (mínimo 32 caracteres). Em produção, use variável de ambiente ou secret manager.</summary>
    public string SecretKey { get; set; } = string.Empty;

    public int ExpirationMinutes { get; set; } = 60;
}
