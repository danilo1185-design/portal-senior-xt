using PortalSenior.Api.Models.Sales;
using PortalSenior.Api.Services.Session;

namespace PortalSenior.Api.Services.Senior;

/// <summary>Consulta notas fiscais de saída no ERP e projeta os itens (uma linha por item).</summary>
public interface ISeniorSalesService
{
    Task<SalesItemsResponse> GetSalesItemsAsync(
        SeniorCredentials credentials,
        SalesByCustomerRequest filters,
        CancellationToken ct = default);
}
