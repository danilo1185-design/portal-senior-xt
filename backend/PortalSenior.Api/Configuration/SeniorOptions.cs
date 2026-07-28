namespace PortalSenior.Api.Configuration;

/// <summary>
/// Configurações de conexão com o middleware de web services do ERP Senior XT.
/// </summary>
public sealed class SeniorOptions
{
    public const string SectionName = "Senior";

    /// <summary>URL base do middleware. Ex.: http://10.64.16.5:8080</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Caminho onde os serviços são publicados. Normalmente "g5-senior-services".</summary>
    public string ServicesPath { get; set; } = "g5-senior-services";

    /// <summary>
    /// Prefixo do sistema no nome do serviço (ex.: "sapiens" em sapiens_SyncMCWFUsers).
    /// Para confirmar, abra {BaseUrl}/{ServicesPath}/ no navegador e veja os serviços publicados.
    /// </summary>
    public string ServicePrefix { get; set; } = "sapiens";

    /// <summary>
    /// Tipo de criptografia das credenciais. 0 = texto UTF-8, padrão documentado
    /// pela Senior para integração a partir de sistemas de terceiros.
    /// </summary>
    public int Encryption { get; set; }

    /// <summary>
    /// "Sigla de sistema" cadastrada no ERP (tela de integrações / F000CWI), enviada
    /// como identificadorSistema nos web services de consulta (ex.: ConsultarGeral).
    /// Sem uma sigla válida, o ERP recusa a consulta com "Sigla de sistema não cadastrada".
    /// </summary>
    public string IdentificadorSistema { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Códigos de transação (E140NFV.TNSPRO, cabeçalho) que NÃO são venda e devem ficar de fora
    /// da consulta — equivale ao "TNSPRO NOT IN (...)" usado no ERP. Ex.: ["5102S","6102S","5949S","6949S"].
    /// Vazio = nenhuma exclusão (todas as notas entram).
    /// </summary>
    public string[] TnsNaoVenda { get; set; } = [];

    /// <summary>
    /// Monta a URL do endpoint síncrono de um serviço.
    /// Ex.: BuildSyncEndpoint("MCWFUsers") => http://host:8080/g5-senior-services/sapiens_SyncMCWFUsers
    /// </summary>
    public string BuildSyncEndpoint(string serviceName) =>
        $"{BaseUrl.TrimEnd('/')}/{ServicesPath.Trim('/')}/{ServicePrefix}_Sync{serviceName}";
}
