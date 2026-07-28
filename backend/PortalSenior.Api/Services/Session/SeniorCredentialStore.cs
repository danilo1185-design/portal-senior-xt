using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace PortalSenior.Api.Services.Session;

/// <summary>
/// Implementação em memória, com as credenciais cifradas via Data Protection.
/// Observação: por ser em memória, as sessões caem no restart e não são compartilhadas
/// entre instâncias. Para escalar, trocar o IMemoryCache por Redis (previsto no README).
/// </summary>
public sealed class SeniorCredentialStore : ISeniorCredentialStore
{
    private const string Purpose = "PortalSenior.SeniorCredentials.v1";
    private const char Separator = '\n';

    private readonly IMemoryCache _cache;
    private readonly IDataProtector _protector;

    public SeniorCredentialStore(IMemoryCache cache, IDataProtectionProvider protectionProvider)
    {
        _cache = cache;
        _protector = protectionProvider.CreateProtector(Purpose);
    }

    public string Store(string username, string password, TimeSpan lifetime)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var payload = _protector.Protect($"{username}{Separator}{password}");

        _cache.Set(CacheKey(sessionId), payload, lifetime);
        return sessionId;
    }

    public SeniorCredentials? Retrieve(string sessionId)
    {
        if (!_cache.TryGetValue(CacheKey(sessionId), out string? payload) || payload is null)
        {
            return null;
        }

        try
        {
            // Split em 2 partes: o usuário nunca contém quebra de linha, a senha pode.
            var parts = _protector.Unprotect(payload).Split(Separator, 2);
            return parts.Length == 2 ? new SeniorCredentials(parts[0], parts[1]) : null;
        }
        catch (CryptographicException)
        {
            // Chave de proteção rotacionada/inválida: trata como sessão inexistente.
            return null;
        }
    }

    public void Remove(string sessionId) => _cache.Remove(CacheKey(sessionId));

    private static string CacheKey(string sessionId) => $"senior-cred:{sessionId}";
}
