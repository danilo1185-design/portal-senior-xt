using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PortalSenior.Api.Configuration;

namespace PortalSenior.Api.Controllers;

[ApiController]
[Route("api/health")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    public const string HealthClientName = "senior-health";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SeniorOptions _options;

    public HealthController(IHttpClientFactory httpClientFactory, IOptions<SeniorOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    /// <summary>Verifica se o portal está no ar.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", timestampUtc = DateTime.UtcNow });

    /// <summary>
    /// Diagnóstico de conectividade com o middleware de web services do ERP Senior.
    /// Use para confirmar liberação de rede/firewall antes de tentar o login.
    /// </summary>
    /// <response code="200">Middleware alcançável.</response>
    /// <response code="503">Middleware inalcançável.</response>
    [HttpGet("senior")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Senior(CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/{_options.ServicesPath.Trim('/')}/";
        var authEndpoint = _options.BuildSyncEndpoint("MCWFUsers");
        var client = _httpClientFactory.CreateClient(HealthClientName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await client.GetAsync(url, ct);
            stopwatch.Stop();

            return Ok(new
            {
                reachable = true,
                url,
                authEndpoint,
                httpStatus = (int)response.StatusCode,
                elapsedMs = stopwatch.ElapsedMilliseconds,
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                reachable = false,
                url,
                authEndpoint,
                elapsedMs = stopwatch.ElapsedMilliseconds,
                error = ex.Message,
                hint = "Confirme com a TI o IP/porta do middleware e a liberação de firewall desta máquina até o host.",
            });
        }
    }
}
