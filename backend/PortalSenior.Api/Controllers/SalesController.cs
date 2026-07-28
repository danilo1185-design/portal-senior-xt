using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PortalSenior.Api.Models.Sales;
using PortalSenior.Api.Services.Auth;
using PortalSenior.Api.Services.Senior;
using PortalSenior.Api.Services.Session;

namespace PortalSenior.Api.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
[Produces("application/json")]
public sealed class SalesController : ControllerBase
{
    private readonly ISeniorSalesService _sales;
    private readonly ISeniorCredentialStore _credentials;
    private readonly ILogger<SalesController> _logger;

    public SalesController(
        ISeniorSalesService sales,
        ISeniorCredentialStore credentials,
        ILogger<SalesController> logger)
    {
        _sales = sales;
        _credentials = credentials;
        _logger = logger;
    }

    /// <summary>
    /// Relatório de vendas por cliente (notas fiscais de saída), com filtros por empresa,
    /// filial, período, cliente, produto e nota fiscal.
    /// </summary>
    /// <response code="200">Consulta executada. Rows pode vir vazio se não houver dados ou a integração não estiver configurada.</response>
    /// <response code="401">Sessão do ERP expirada — refaça o login.</response>
    /// <response code="503">ERP Senior indisponível.</response>
    [HttpPost("by-customer")]
    [ProducesResponseType(typeof(SalesItemsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ByCustomer([FromBody] SalesByCustomerRequest request, CancellationToken ct)
    {
        if (request.DateEnd < request.DateStart)
        {
            return BadRequest(new { message = "A data final não pode ser anterior à data inicial." });
        }

        // Recupera as credenciais do ERP guardadas na sessão (ligadas ao token pelo claim sid).
        var sessionId = User.FindFirst(JwtTokenService.SessionIdClaim)?.Value;
        var credentials = sessionId is null ? null : _credentials.Retrieve(sessionId);
        if (credentials is null)
        {
            _logger.LogInformation("Sessão do ERP não encontrada/expirada ao consultar vendas.");
            return Unauthorized(new { message = "Sessão expirada. Faça login novamente." });
        }

        var result = await _sales.GetSalesItemsAsync(credentials, request, ct);
        return Ok(result);
    }
}
