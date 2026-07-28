using PortalSenior.Api.Services.Session;

namespace PortalSenior.Api.Services.Senior;

/// <summary>
/// Apelidos de clientes resolvidos. <see cref="ServiceAvailable"/> é false quando o WS de
/// clientes recusou a consulta (não parametrizado / tipo de informação inativo) — nesse caso
/// os apelidos vêm vazios e o relatório mostra apenas o código do cliente.
/// </summary>
public sealed record ClientApelidos(IReadOnlyDictionary<int, string> Apelidos, bool ServiceAvailable);

/// <summary>Consulta o cadastro de clientes no ERP para obter o apelido (apeCli) por código.</summary>
public interface ISeniorClientService
{
    Task<ClientApelidos> GetApelidosAsync(
        SeniorCredentials credentials,
        int codEmp,
        int codFil,
        IReadOnlyCollection<int> codClis,
        CancellationToken ct = default);
}
