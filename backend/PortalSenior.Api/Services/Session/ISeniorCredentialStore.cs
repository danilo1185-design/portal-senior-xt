namespace PortalSenior.Api.Services.Session;

/// <summary>Credenciais do usuário no ERP Senior, usadas em cada chamada aos web services.</summary>
public sealed record SeniorCredentials(string Username, string Password);

/// <summary>
/// Guarda, de forma cifrada e no servidor, as credenciais do usuário autenticado.
/// Necessário porque o ERP Senior exige user/password em toda chamada — o token JWT
/// do portal sozinho não autentica no ERP.
/// </summary>
public interface ISeniorCredentialStore
{
    /// <summary>Armazena as credenciais e devolve o id de sessão que vai no claim "sid" do JWT.</summary>
    string Store(string username, string password, TimeSpan lifetime);

    /// <summary>Recupera as credenciais da sessão, ou null se expirou/não existe.</summary>
    SeniorCredentials? Retrieve(string sessionId);

    void Remove(string sessionId);
}
