import { useMemo } from 'react'
import { Box, Paper, Stack, Typography } from '@mui/material'
import { AgGridReact } from 'ag-grid-react'
import { AllCommunityModule, ModuleRegistry } from 'ag-grid-community'
import type { ColDef, ValueFormatterParams } from 'ag-grid-community'
import type { SalesItemRow, SalesItemsResponse } from '../api/types'
import { formatCurrency, formatDateBr, formatInteger, formatQuantity } from '../utils/format'

// AG Grid v33+ exige registro explícito dos módulos (uma vez, no carregamento).
ModuleRegistry.registerModules([AllCommunityModule])

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <Paper variant="outlined" sx={{ p: 2, flex: '1 1 180px' }}>
      <Typography variant="overline" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="h5">{value}</Typography>
    </Paper>
  )
}

function dateCell(p: ValueFormatterParams<SalesItemRow, string | null>): string {
  return formatDateBr(p.value ?? null)
}

function qtyCell(p: ValueFormatterParams<SalesItemRow, number>): string {
  return p.value == null ? '' : formatQuantity(p.value)
}

function currencyCell(p: ValueFormatterParams<SalesItemRow, number>): string {
  return p.value == null ? '' : formatCurrency(p.value)
}

export function SalesResult({ data }: { data: SalesItemsResponse }) {
  const columnDefs = useMemo<ColDef<SalesItemRow>[]>(
    () => [
      { field: 'numNfv', headerName: 'Nº Nota', width: 120, type: 'numericColumn' },
      { field: 'dataEmissao', headerName: 'Emissão', width: 120, valueFormatter: dateCell },
      { field: 'codCli', headerName: 'Cód. Cliente', width: 130, type: 'numericColumn' },
      { field: 'apelidoCliente', headerName: 'Cliente', flex: 1, minWidth: 160 },
      { field: 'codPro', headerName: 'Cód. Item', width: 140 },
      { field: 'descricao', headerName: 'Descrição', flex: 1.4, minWidth: 200 },
      {
        field: 'quantidade',
        headerName: 'Qtd.',
        width: 120,
        type: 'numericColumn',
        valueFormatter: qtyCell,
      },
      {
        field: 'valor',
        headerName: 'Valor',
        width: 150,
        type: 'numericColumn',
        valueFormatter: currencyCell,
      },
      { field: 'tns', headerName: 'TNS', width: 100 },
    ],
    [],
  )

  const defaultColDef = useMemo<ColDef>(
    () => ({ sortable: true, resizable: true, filter: true }),
    [],
  )

  return (
    <Stack spacing={2}>
      <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <StatCard label="Itens" value={formatInteger(data.totalItems)} />
        <StatCard label="Valor total" value={formatCurrency(data.totalValue)} />
        <StatCard label="Notas lidas" value={formatInteger(data.invoicesRead)} />
      </Box>

      <div style={{ width: '100%', height: 540 }}>
        <AgGridReact<SalesItemRow>
          rowData={data.rows}
          columnDefs={columnDefs}
          defaultColDef={defaultColDef}
          pagination
          paginationPageSize={50}
        />
      </div>
    </Stack>
  )
}
