using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PortalSenior.Api.Configuration;
using PortalSenior.Api.Models.Auth;
using PortalSenior.Api.Services.Auth;
using PortalSenior.Api.Services.Senior;
using PortalSenior.Api.Services.Session;

namespace PortalSenior.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly ISeniorAuthService _seniorAuth;
    private readonly ISeniorCredentialStore _credentials;
    private readonly IJwtTokenService _jwt;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ISeniorAuthService seniorAuth,
        ISeniorCredentialStore credentials,
        IJwtTokenService jwt,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthController> logger)
    {
        _seniorAuth = seniorAuth;
        _credentials = credentials;
        _jwt = jwt;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Autentica o usuário com as credenciais cadastradas no ERP Senior (SGU).
    /// </summary>
    /// <response code="200">Login efetuado. Retorna o token JWT do portal.</response>
    /// <response code="401">Usuário ou senha inválidos.</response>
    /// <response code="503">ERP Senior indisponível (rede/firewall/middleware fora do ar).</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _seniorAuth.AuthenticateAsync(request.Username, request.Password, ct);

        // ERP fora do ar é diferente de credencial errada: o usuário precisa saber a diferença.
        if (result.CommunicationFailure)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = result.ErrorMessage ?? "ERP Senior indisponível no momento.",
            });
        }

        if (!result.Authenticated)
        {
            _logger.LogWarning("Login negado para {Usuario}", request.Username);
            return Unauthorized(new { message = result.ErrorMessage ?? "Usuário ou senha inválidos." });
        }

        var lifetime = TimeSpan.FromMinutes(_jwtOptions.ExpirationMinutes);
        var expiresAtUtc = DateTime.UtcNow.Add(lifetime);

        // As credenciais ficam cifradas no servidor: serão reusadas nas próximas chamadas ao ERP.
        var sessionId = _credentials.Store(request.Username, request.Password, lifetime);
        var token = _jwt.CreateToken(request.Username, sessionId, expiresAtUtc);

        return Ok(new LoginResponse
        {
            Token = token,
            Username = request.Username,
            ExpiresAtUtc = expiresAtUtc,
        });
    }

    /// <summary>Dados do usuário autenticado no portal.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var sessionId = User.FindFirst(JwtTokenService.SessionIdClaim)?.Value;
        var hasSeniorSession = sessionId is not null && _credentials.Retrieve(sessionId) is not null;

        return Ok(new
        {
            username = User.Identity?.Name,
            // Falso indica token válido mas sessão do ERP expirada: o usuário precisa refazer login.
            seniorSessionActive = hasSeniorSession,
        });
    }

    /// <summary>Encerra a sessão, descartando as credenciais guardadas no servidor.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        var sessionId = User.FindFirst(JwtTokenService.SessionIdClaim)?.Value;
        if (sessionId is not null)
        {
            _credentials.Remove(sessionId);
        }

        return NoContent();
    }
}
