using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using PortalSenior.Api.Configuration;
using PortalSenior.Api.Models.Sales;
using PortalSenior.Api.Services.Session;

namespace PortalSenior.Api.Services.Senior;

/// <summary>
/// Consulta com.senior.g5.co.ven.notafiscalsaida (ConsultarGeral), pagina os resultados e
/// projeta os itens (elemento &lt;produto&gt;) em uma linha cada, aplicando o filtro opcional de
/// produto e o filtro VENFAT=S (por lista de transações). O apelido do cliente é resolvido pelo
/// WS de clientes. Binding rpc/literal.
/// </summary>
public sealed class SeniorSalesService : ISeniorSalesService
{
    private const string ServiceName = "com_senior_g5_co_ven_notafiscalsaida";
    private const int PageSize = 100;
    private const int MaxPages = 200; // trava de segurança: até 20.000 notas por consulta

    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Senior = "http://services.senior.com.br";

    private readonly HttpClient _http;
    private readonly SeniorOptions _options;
    private readonly ISeniorClientService _clients;
    private readonly ILogger<SeniorSalesService> _logger;

    public SeniorSalesService(
        HttpClient http, IOptions<SeniorOptions> options, ISeniorClientService clients, ILogger<SeniorSalesService> logger)
    {
        _http = http;
        _options = options.Value;
        _clients = clients;
        _logger = logger;
    }

    public async Task<SalesItemsResponse> GetSalesItemsAsync(
        SeniorCredentials credentials, SalesByCustomerRequest filters, CancellationToken ct = default)
    {
        var endpoint = _options.BuildSyncEndpoint(ServiceName);

        var tnsExcluidas = new HashSet<string>(
            _options.TnsNaoVenda.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()),
            StringComparer.OrdinalIgnoreCase);
        var filtroTnsAtivo = tnsExcluidas.Count > 0;

        var rows = new List<SalesItemRow>();
        var invoicesRead = 0;

        for (var page = 1; page <= MaxPages; page++)
        {
            var (notas, error) = await QueryPageAsync(endpoint, credentials, filters, page, ct);

            if (error is not null)
            {
                return new SalesItemsResponse { IntegrationConfigured = false, Message = error };
            }

            if (notas.Count == 0)
            {
                break;
            }

            invoicesRead += notas.Count;
            foreach (var nota in notas)
            {
                // Transação do cabeçalho da nota (E140NFV.TNSPRO). Notas de transação "não venda"
                // (ex.: 5102S/6102S/5949S/6949S) ficam de fora — equivale ao TNSPRO NOT IN (...) do ERP.
                var tns = ReadString(nota, "tnsPro");
                if (tnsExcluidas.Contains(tns))
                {
                    continue;
                }

                var numNfv = ReadInt(nota, "numNfv") ?? 0;
                var datEmi = ReadDate(nota, "datEmi");
                var codCli = ReadInt(nota, "codCli") ?? 0;

                foreach (var item in nota.Elements().Where(e => e.Name.LocalName == "produto"))
                {
                    var codPro = ReadString(item, "codPro");

                    if (!string.IsNullOrWhiteSpace(filters.Produto) &&
                        !codPro.Equals(filters.Produto.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    rows.Add(new SalesItemRow
                    {
                        NumNfv = numNfv,
                        DataEmissao = datEmi,
                        CodCli = codCli,
                        CodPro = codPro,
                        Descricao = ReadString(item, "cplIpv"),
                        Quantidade = ReadDecimal(item, "qtdVen"),
                        Valor = ReadDecimal(item, "vlrLiq"),
                        Tns = tns,
                    });
                }
            }

            if (notas.Count < PageSize)
            {
                break; // última página
            }
        }

        // Resolve os apelidos dos clientes presentes no resultado (WS de clientes, com cache).
        var clientResult = new ClientApelidos(new Dictionary<int, string>(), false);
        if (rows.Count > 0)
        {
            var codClis = rows.Select(r => r.CodCli).Where(c => c > 0).Distinct().ToList();
            if (codClis.Count > 0)
            {
                clientResult = await _clients.GetApelidosAsync(
                    credentials, filters.CodEmp ?? 0, filters.CodFil ?? 0, codClis, ct);
            }
        }

        if (clientResult.Apelidos.Count > 0)
        {
            rows = rows
                .Select(r => clientResult.Apelidos.TryGetValue(r.CodCli, out var apelido)
                    ? r with { ApelidoCliente = apelido }
                    : r)
                .ToList();
        }

        var ordered = rows
            .OrderBy(r => r.NumNfv)
            .ThenBy(r => r.CodPro, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SalesItemsResponse
        {
            IntegrationConfigured = true,
            Rows = ordered,
            TotalItems = ordered.Count,
            TotalValue = ordered.Sum(r => r.Valor),
            InvoicesRead = invoicesRead,
            VenfatFilterActive = filtroTnsAtivo,
            ClientNamesResolved = clientResult.ServiceAvailable,
        };
    }

    private async Task<(List<XElement> Notas, string? Error)> QueryPageAsync(
        string endpoint, SeniorCredentials credentials, SalesByCustomerRequest filters, int page, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(BuildEnvelope(credentials, filters, page), Encoding.UTF8, "text/xml"),
        };
        request.Headers.Add("SOAPAction", string.Empty);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Falha de comunicação com o ERP ao consultar vendas em {Endpoint}", endpoint);
            return ([], $"Não foi possível contatar o ERP Senior em {endpoint}. Verifique rede/firewall.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ERP retornou HTTP {Status} ao consultar vendas.", (int)response.StatusCode);
                return ([], $"O ERP Senior retornou HTTP {(int)response.StatusCode} na consulta de vendas.");
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(body);
            }
            catch (XmlException ex)
            {
                _logger.LogError(ex, "Resposta de vendas não é XML válido.");
                return ([], "Resposta inválida do ERP Senior (XML malformado).");
            }

            // Erro de execução do serviço: <result><erroExecucao>...</erroExecucao> (vem com tipoRetorno=0).
            var erroExecucao = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "erroExecucao")?.Value;
            if (!string.IsNullOrWhiteSpace(erroExecucao))
            {
                _logger.LogWarning("ERP retornou erro de execução na consulta de vendas: {Erro}", erroExecucao);
                return ([], DescribeBusinessError(erroExecucao));
            }

            // Erro de negócio: <erros><mensagemErro>...</mensagemErro></erros>.
            var mensagemErro = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "mensagemErro")?.Value;
            if (!string.IsNullOrWhiteSpace(mensagemErro))
            {
                _logger.LogWarning("ERP recusou a consulta de vendas: {Erro}", mensagemErro);
                return ([], DescribeBusinessError(mensagemErro));
            }

            // tipoRetorno=-1 sem detalhe ("Ocorreram erros."): reúne o que houver para reportar.
            var tipoRetorno = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "tipoRetorno")?.Value;
            if (tipoRetorno == "-1")
            {
                var detalhe = string.Join("; ", doc.Descendants()
                    .Where(e => e.Name.LocalName is "erro" or "descricao" or "mensagemRetorno")
                    .Select(e => e.Value.Trim())
                    .Where(v => !string.IsNullOrWhiteSpace(v)));
                if (string.IsNullOrWhiteSpace(detalhe))
                {
                    detalhe = "erro não detalhado pelo ERP";
                }
                _logger.LogWarning("ERP retornou tipoRetorno=-1 na consulta de vendas: {Detalhe}", detalhe);
                return ([], $"O ERP retornou erro na consulta de vendas: {detalhe}");
            }

            // Ignora <notaFiscal/> vazias — a Senior devolve um template vazio quando não há resultados.
            var notas = doc.Descendants()
                .Where(e => e.Name.LocalName == "notaFiscal" && e.HasElements)
                .ToList();

            return (notas, null);
        }
    }

    private string BuildEnvelope(SeniorCredentials credentials, SalesByCustomerRequest filters, int page)
    {
        // Ordem obrigatória (xs:sequence, alfabética): codCli, codEmp, codFil, datEmiFim,
        // datEmiIni, identificadorSistema, indicePagina, limitePagina, numNfv.
        var parameters = new XElement("parameters");

        if (filters.CodCli is int codCli)
        {
            parameters.Add(new XElement("codCli", new XElement("codCli", codCli)));
        }

        parameters.Add(new XElement("codEmp", filters.CodEmp));
        parameters.Add(new XElement("codFil", filters.CodFil));
        parameters.Add(new XElement("datEmiFim", FormatDate(filters.DateEnd)));
        parameters.Add(new XElement("datEmiIni", FormatDate(filters.DateStart)));

        if (!string.IsNullOrWhiteSpace(_options.IdentificadorSistema))
        {
            parameters.Add(new XElement("identificadorSistema", _options.IdentificadorSistema));
        }

        // Paginação POR FAIXA DE REGISTROS (documentação Senior): indicePagina = a partir de qual
        // registro; limitePagina = até qual registro; a diferença não pode passar de 100 (teto do WS).
        // Ex.: página 1 = registros 1..100; página 2 = 101..200; página 3 = 201..300.
        parameters.Add(new XElement("indicePagina", (page - 1) * PageSize + 1));
        parameters.Add(new XElement("limitePagina", page * PageSize));

        if (filters.NumNfv is int numNfv)
        {
            parameters.Add(new XElement("numNfv", new XElement("numNfv", numNfv)));
        }

        var envelope = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", Soap.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ser", Senior.NamespaceName),
                new XElement(Soap + "Header"),
                new XElement(Soap + "Body",
                    new XElement(Senior + "ConsultarGeral",
                        new XElement("user", credentials.Username),
                        new XElement("password", credentials.Password),
                        new XElement("encryption", _options.Encryption),
                        parameters))));

        using var writer = new Utf8StringWriter();
        envelope.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    private static string DescribeBusinessError(string mensagemErro)
    {
        if (mensagemErro.Contains("parametrizado", StringComparison.OrdinalIgnoreCase))
        {
            return "O web service de Nota Fiscal de Saída (Consultar Geral) não está liberado para uso " +
                   "neste ERP Senior. É preciso parametrizar/liberar esse serviço na configuração de " +
                   "integrações da Senior (associado à sigla PLATLOG) para permitir a consulta externa. " +
                   $"Retorno do ERP: {mensagemErro}";
        }

        if (mensagemErro.Contains("tipo de informação", StringComparison.OrdinalIgnoreCase) ||
            mensagemErro.Contains("inativada", StringComparison.OrdinalIgnoreCase))
        {
            return "Integração inativa no ERP: ative o tipo de informação correspondente na tela " +
                   $"F000SXT (Configuração de Tipos de Informação) para a sigla PLATLOG. Retorno do ERP: {mensagemErro}";
        }

        if (mensagemErro.Contains("sigla", StringComparison.OrdinalIgnoreCase))
        {
            return "Integração não configurada no ERP: é preciso cadastrar a \"sigla de sistema\" " +
                   "(tela de integrações / F000CWI) e informá-la em Senior:IdentificadorSistema. " +
                   $"Retorno do ERP: {mensagemErro}";
        }

        return $"O ERP recusou a consulta: {mensagemErro}";
    }

    private static string FormatDate(DateOnly? date) =>
        date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string ReadString(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value?.Trim() ?? string.Empty;

    private static int? ReadInt(XElement parent, string localName)
    {
        var value = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static decimal ReadDecimal(XElement parent, string localName)
    {
        var value = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        // O ERP serializa xs:double com ponto decimal (invariante).
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    private static DateOnly? ReadDate(XElement parent, string localName)
    {
        var value = parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        return DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
