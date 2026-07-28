using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PortalSenior.Api.Configuration;
using PortalSenior.Api.Services.Session;

namespace PortalSenior.Api.Services.Senior;

/// <summary>
/// Consulta com.senior.g5.co.cad.clientes (ConsultarGeral) para resolver o apelido (apeCli)
/// dos clientes das notas. Os apelidos são cacheados (mudam pouco) e a falha do WS não
/// derruba o relatório de vendas — apenas deixa o apelido vazio.
/// </summary>
public sealed class SeniorClientService : ISeniorClientService
{
    private const string ServiceName = "com_senior_g5_co_cad_clientes";
    private const int BatchSize = 100;

    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Senior = "http://services.senior.com.br";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    private readonly HttpClient _http;
    private readonly SeniorOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SeniorClientService> _logger;

    public SeniorClientService(
        HttpClient http, IOptions<SeniorOptions> options, IMemoryCache cache, ILogger<SeniorClientService> logger)
    {
        _http = http;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ClientApelidos> GetApelidosAsync(
        SeniorCredentials credentials, int codEmp, int codFil,
        IReadOnlyCollection<int> codClis, CancellationToken ct = default)
    {
        var result = new Dictionary<int, string>();
        var pending = new List<int>();

        foreach (var cod in codClis.Where(c => c > 0).Distinct())
        {
            if (_cache.TryGetValue(CacheKey(codEmp, codFil, cod), out string? apelido) && apelido is not null)
            {
                result[cod] = apelido;
            }
            else
            {
                pending.Add(cod);
            }
        }

        if (pending.Count == 0)
        {
            return new ClientApelidos(result, true);
        }

        var endpoint = _options.BuildSyncEndpoint(ServiceName);

        foreach (var batch in Chunk(pending, BatchSize))
        {
            var (map, ok) = await QueryBatchAsync(endpoint, credentials, codEmp, codFil, batch, ct);
            if (!ok)
            {
                // WS indisponível/recusado: devolve o que já tem e sinaliza indisponível.
                return new ClientApelidos(result, false);
            }

            foreach (var kvp in map)
            {
                result[kvp.Key] = kvp.Value;
                _cache.Set(CacheKey(codEmp, codFil, kvp.Key), kvp.Value, CacheTtl);
            }
        }

        return new ClientApelidos(result, true);
    }

    private async Task<(Dictionary<int, string> Map, bool Ok)> QueryBatchAsync(
        string endpoint, SeniorCredentials credentials, int codEmp, int codFil, List<int> batch, CancellationToken ct)
    {
        var map = new Dictionary<int, string>();

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(BuildEnvelope(credentials, codEmp, codFil, batch), Encoding.UTF8, "text/xml"),
        };
        request.Headers.Add("SOAPAction", string.Empty);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao consultar apelidos de clientes; relatório seguirá sem apelido.");
            return (map, false);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("WS de clientes retornou HTTP {Status}; seguindo sem apelido.", (int)response.StatusCode);
                return (map, false);
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(body);
            }
            catch (XmlException)
            {
                _logger.LogWarning("Resposta do WS de clientes não é XML válido; seguindo sem apelido.");
                return (map, false);
            }

            // Erros de configuração (não parametrizado / tipo de informação inativo / tipoRetorno=-1).
            var erro = doc.Descendants().FirstOrDefault(e => e.Name.LocalName is "erroExecucao" or "mensagemErro")?.Value;
            var tipoRetorno = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "tipoRetorno")?.Value;
            if (!string.IsNullOrWhiteSpace(erro) || tipoRetorno == "-1")
            {
                _logger.LogWarning("WS de clientes recusou a consulta ({Erro}); relatório seguirá sem apelido.",
                    erro ?? "tipoRetorno=-1");
                return (map, false);
            }

            // Cada registro de cliente é um elemento com filhos codCli e apeCli.
            foreach (var cli in doc.Descendants().Where(e => e.Elements().Any(c => c.Name.LocalName == "apeCli")))
            {
                var cod = ReadInt(cli, "codCli");
                var apelido = cli.Elements().FirstOrDefault(e => e.Name.LocalName == "apeCli")?.Value?.Trim();
                if (cod is int c && !string.IsNullOrWhiteSpace(apelido))
                {
                    map[c] = apelido;
                }
            }

            return (map, true);
        }
    }

    private string BuildEnvelope(SeniorCredentials credentials, int codEmp, int codFil, List<int> batch)
    {
        // Ordem (xs:sequence alfabética): codCli, codEmp, codFil, identificadorSistema, indicePagina, limitePagina.
        var parameters = new XElement("parameters");
        foreach (var cod in batch)
        {
            parameters.Add(new XElement("codCli", new XElement("codCli", cod)));
        }
        parameters.Add(new XElement("codEmp", codEmp));
        parameters.Add(new XElement("codFil", codFil));
        if (!string.IsNullOrWhiteSpace(_options.IdentificadorSistema))
        {
            parameters.Add(new XElement("identificadorSistema", _options.IdentificadorSistema));
        }
        parameters.Add(new XElement("indicePagina", 1));
        parameters.Add(new XElement("limitePagina", BatchSize));

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

    private static int? ReadInt(XElement element, string localName)
    {
        var value = element.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static string CacheKey(int codEmp, int codFil, int codCli) => $"cliente-apelido:{codEmp}:{codFil}:{codCli}";

    private static IEnumerable<List<int>> Chunk(List<int> source, int size)
    {
        for (var i = 0; i < source.Count; i += size)
        {
            yield return source.GetRange(i, Math.Min(size, source.Count - i));
        }
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
