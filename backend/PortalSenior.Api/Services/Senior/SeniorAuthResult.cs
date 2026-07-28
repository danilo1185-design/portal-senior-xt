namespace PortalSenior.Api.Services.Senior;

/// <summary>
/// Resultado da tentativa de autenticação de um usuário no ERP Senior.
/// </summary>
public sealed class SeniorAuthResult
{
    /// <summary>True quando o ERP confirmou que o usuário e a senha são válidos.</summary>
    public bool Authenticated { get; private init; }

    /// <summary>
    /// True quando o middleware não pôde ser alcançado (rede, firewall, timeout).
    /// Distingue "credencial errada" de "ERP indisponível".
    /// </summary>
    public bool CommunicationFailure { get; private init; }

    /// <summary>Mensagem retornada pelo ERP (erroExecucao / SOAP Fault) ou de comunicação.</summary>
    public string? ErrorMessage { get; private init; }

    public static SeniorAuthResult Success() => new() { Authenticated = true };

    public static SeniorAuthResult Denied(string? message) =>
        new() { Authenticated = false, ErrorMessage = message };

    public static SeniorAuthResult Unreachable(string message) =>
        new() { Authenticated = false, CommunicationFailure = true, ErrorMessage = message };
}
