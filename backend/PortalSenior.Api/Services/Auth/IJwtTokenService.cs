namespace PortalSenior.Api.Services.Auth;

/// <summary>Emite o token JWT usado pelo frontend do portal.</summary>
public interface IJwtTokenService
{
    string CreateToken(string username, string sessionId, DateTime expiresAtUtc);
}
