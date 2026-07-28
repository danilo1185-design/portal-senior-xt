using System.ComponentModel.DataAnnotations;

namespace PortalSenior.Api.Models.Sales;

/// <summary>Filtros da consulta de vendas (notas fiscais de saída), item a item.</summary>
public sealed class SalesByCustomerRequest
{
    [Required(ErrorMessage = "Informe a empresa.")]
    public int? CodEmp { get; set; }

    [Required(ErrorMessage = "Informe a filial.")]
    public int? CodFil { get; set; }

    [Required(ErrorMessage = "Informe a data inicial.")]
    public DateOnly? DateStart { get; set; }

    [Required(ErrorMessage = "Informe a data final.")]
    public DateOnly? DateEnd { get; set; }

    /// <summary>Filtro opcional por código de cliente.</summary>
    public int? CodCli { get; set; }

    /// <summary>Filtro opcional por código de produto (aplicado aos itens da nota).</summary>
    public string? Produto { get; set; }

    /// <summary>Filtro opcional por número da nota fiscal de venda.</summary>
    public int? NumNfv { get; set; }
}

/// <summary>Uma linha do relatório = um item de uma nota fiscal de saída.</summary>
public sealed record SalesItemRow
{
    /// <summary>Número da nota fiscal (numNfv).</summary>
    public int NumNfv { get; init; }

    /// <summary>Data de emissão (datEmi).</summary>
    public DateOnly? DataEmissao { get; init; }

    /// <summary>Código do cliente (codCli).</summary>
    public int CodCli { get; init; }

    /// <summary>Apelido do cliente (apeCli), quando o WS de clientes está disponível.</summary>
    public string? ApelidoCliente { get; init; }

    /// <summary>Código do item/produto (codPro).</summary>
    public string CodPro { get; init; } = string.Empty;

    /// <summary>Descrição do item (cplIpv — complemento do item de venda).</summary>
    public string Descricao { get; init; } = string.Empty;

    /// <summary>Quantidade vendida (qtdVen).</summary>
    public decimal Quantidade { get; init; }

    /// <summary>Valor líquido do item (vlrLiq).</summary>
    public decimal Valor { get; init; }

    /// <summary>Transação do item (tnsPro) — base do filtro VENFAT.</summary>
    public string Tns { get; init; } = string.Empty;
}

public sealed class SalesItemsResponse
{
    /// <summary>
    /// False quando o ERP recusou a consulta por falta de configuração de integração.
    /// Nesse caso Rows vem vazio e Message explica.
    /// </summary>
    public bool IntegrationConfigured { get; init; } = true;

    public string? Message { get; init; }

    public IReadOnlyList<SalesItemRow> Rows { get; init; } = [];

    public int TotalItems { get; init; }

    public decimal TotalValue { get; init; }

    /// <summary>Notas lidas do ERP antes da projeção (diagnóstico).</summary>
    public int InvoicesRead { get; init; }

    /// <summary>
    /// True quando o filtro VENFAT=S está ativo (há lista de transações configurada em
    /// Senior:VenfatTns). False = a lista está vazia e nenhum filtro de transação foi aplicado.
    /// </summary>
    public bool VenfatFilterActive { get; init; }

    /// <summary>
    /// True quando o apelido do cliente pôde ser resolvido pelo WS de clientes.
    /// False indica que o WS de clientes ainda não está liberado (apelido vem vazio).
    /// </summary>
    public bool ClientNamesResolved { get; init; }
}
