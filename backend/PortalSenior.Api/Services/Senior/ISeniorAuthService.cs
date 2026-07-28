namespace PortalSenior.Api.Services.Senior;

/// <summary>
/// Valida credenciais contra o cadastro de usuários do ERP Senior (SGU).
/// </summary>
public interface ISeniorAuthService
{
    Task<SeniorAuthResult> AuthenticateAsync(string username, string password, CancellationToken ct = default);
}
