// Tipos que espelham os contratos JSON da API (System.Text.Json serializa em camelCase).

export interface LoginResponse {
  token: string
  username: string
  expiresAtUtc: string
}

export interface MeResponse {
  username: string
  seniorSessionActive: boolean
}

/** Filtros da consulta de vendas por cliente. Datas no formato ISO "yyyy-MM-dd". */
export interface SalesByCustomerRequest {
  codEmp: number
  codFil: number
  dateStart: string
  dateEnd: string
  codCli?: number | null
  produto?: string | null
  numNfv?: number | null
}

/** Uma linha do relatório = um item de nota fiscal de saída. */
export interface SalesItemRow {
  numNfv: number
  /** Data de emissão no formato ISO "yyyy-MM-dd". */
  dataEmissao: string | null
  codCli: number
  apelidoCliente: string | null
  codPro: string
  descricao: string
  quantidade: number
  valor: number
  /** Transação do item (tnsPro) — base do filtro VENFAT. */
  tns: string
}

export interface SalesItemsResponse {
  /** False quando o ERP recusou por falta de configuração de integração. */
  integrationConfigured: boolean
  message?: string | null
  rows: SalesItemRow[]
  totalItems: number
  totalValue: number
  /** Notas lidas do ERP antes da projeção (diagnóstico). */
  invoicesRead: number
  /** True quando o filtro VENFAT=S está ativo (há lista de transações configurada). */
  venfatFilterActive: boolean
  /** True quando o apelido do cliente pôde ser resolvido pelo WS de clientes. */
  clientNamesResolved: boolean
}
