using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using PortalSenior.Api.Configuration;

namespace PortalSenior.Api.Services.Senior;

/// <summary>
/// Valida credenciais no ERP Senior através do web service MCWFUsers, porta AuthenticateJAAS.
/// O ERP não emite token: toda chamada carrega user/password/encryption no envelope SOAP,
/// por isso o login consiste em confirmar as credenciais junto ao SGU.
/// </summary>
public sealed class SeniorAuthService : ISeniorAuthService
{
    private const string ServiceName = "MCWFUsers";

    /// <summary>Valor de pmLogged que o ERP devolve quando as credenciais são válidas.</summary>
    private const string AuthenticatedCode = "0";

    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Senior = "http://services.senior.com.br";

    private readonly HttpClient _http;
    private readonly SeniorOptions _options;
    private readonly ILogger<SeniorAuthService> _logger;

    public SeniorAuthService(HttpClient http, IOptions<SeniorOptions> options, ILogger<SeniorAuthService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SeniorAuthResult> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var endpoint = _options.BuildSyncEndpoint(ServiceName);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(BuildEnvelope(username, password), Encoding.UTF8, "text/xml")
        };
        request.Headers.Add("SOAPAction", string.Empty);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Falha de comunicação com o middleware Senior em {Endpoint}", endpoint);
            return SeniorAuthResult.Unreachable(
                $"Não foi possível contatar o ERP Senior em {endpoint}. Verifique rede/firewall e se o middleware está no ar.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var fault = TryExtractFault(body);
                _logger.LogError(
                    "Middleware Senior retornou HTTP {Status} para {Endpoint}. Fault: {Fault}",
                    (int)response.StatusCode, endpoint, fault ?? "(nenhum)");

                return SeniorAuthResult.Unreachable(
                    fault ?? $"O ERP Senior retornou HTTP {(int)response.StatusCode}.");
            }

            return ParseResponse(body, username);
        }
    }

    /// <summary>
    /// Monta o envelope do AuthenticateJAAS. As credenciais aparecem duas vezes por design do
    /// serviço: em user/password (autenticação da própria chamada) e em pmUserName/pmUserPassword
    /// (as credenciais que se quer validar). Usar as mesmas garante que, autenticando, elas também
    /// servirão para as chamadas seguintes ao ERP.
    /// </summary>
    private string BuildEnvelope(string username, string password)
    {
        var envelope = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", Soap.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "ser", Senior.NamespaceName),
                new XElement(Soap + "Header"),
                new XElement(Soap + "Body",
                    // Binding é rpc/literal: cada part da mensagem (user, password,
                    // encryption, parameters) vira filho direto da operação, sem namespace.
                    new XElement(Senior + "AuthenticateJAAS",
                        new XElement("user", username),
                        new XElement("password", password),
                        new XElement("encryption", _options.Encryption),
                        // A ordem aqui é obrigatória: o XSD declara xs:sequence
                        // (flowInstanceID, flowName, pmEncrypted, pmUserName, pmUserPassword).
                        // Os dois primeiros são opcionais e não se aplicam ao login.
                        new XElement("parameters",
                            new XElement("pmEncrypted", _options.Encryption),
                            new XElement("pmUserName", username),
                            new XElement("pmUserPassword", password))))));

        using var writer = new Utf8StringWriter();
        envelope.Save(writer, SaveOptions.DisableFormatting);
        return writer.ToString();
    }

    /// <summary>
    /// StringWriter que se declara UTF-8. Sem isto, XDocument.Save escreve
    /// encoding="utf-16" na declaração (o StringWriter padrão é UTF-16) enquanto o
    /// corpo trafega em UTF-8 — divergência que o parser XML da Senior pode recusar.
    /// </summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }

    private SeniorAuthResult ParseResponse(string body, string username)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(body);
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "Resposta do ERP não é um XML válido. Trecho: {Body}", Truncate(body));
            return SeniorAuthResult.Unreachable("Resposta inválida do ERP Senior (XML malformado).");
        }

        var fault = FindValue(doc, "faultstring");
        if (!string.IsNullOrWhiteSpace(fault))
        {
            _logger.LogError("SOAP Fault do ERP Senior: {Fault}", fault);
            return SeniorAuthResult.Unreachable($"O ERP Senior recusou a chamada: {fault}");
        }

        var erroExecucao = FindValue(doc, "erroExecucao");
        if (!string.IsNullOrWhiteSpace(erroExecucao))
        {
            _logger.LogWarning("ERP retornou erroExecucao no login de {Usuario}: {Erro}", username, erroExecucao);
            return SeniorAuthResult.Denied(erroExecucao);
        }

        var pmLogged = FindValue(doc, "pmLogged");
        if (pmLogged is null)
        {
            _logger.LogError("Resposta sem o campo pmLogged. Trecho: {Body}", Truncate(body));
            return SeniorAuthResult.Unreachable("Resposta inesperada do ERP Senior (campo pmLogged ausente).");
        }

        if (IsAuthenticated(pmLogged))
        {
            _logger.LogInformation("Usuário {Usuario} autenticado com sucesso no ERP Senior", username);
            return SeniorAuthResult.Success();
        }

        _logger.LogInformation("ERP negou o login de {Usuario} (pmLogged={PmLogged})", username, pmLogged);
        return SeniorAuthResult.Denied("Usuário ou senha inválidos.");
    }

    private static string? TryExtractFault(string body)
    {
        try
        {
            return FindValue(XDocument.Parse(body), "faultstring");
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>Busca por nome local, ignorando o namespace que o ERP aplicar na resposta.</summary>
    private static string? FindValue(XDocument doc, string localName) =>
        doc.Descendants().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    /// <summary>
    /// pmLogged é um código de status, não um booleano. Verificado contra o ERP 5.10.4:
    /// "0" = autenticado, "-1" = usuário/senha inválidos ou usuário inexistente.
    /// Cuidado ao mexer: tratar "0" como falso (o intuitivo) rejeita logins válidos.
    /// </summary>
    private static bool IsAuthenticated(string pmLogged) =>
        pmLogged.Trim().Equals(AuthenticatedCode, StringComparison.Ordinal);

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "...";
}
