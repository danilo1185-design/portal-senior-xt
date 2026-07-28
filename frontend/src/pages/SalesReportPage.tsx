import { useState } from 'react'
import type { FormEvent } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from '@mui/material'
import { DatePicker } from '@mui/x-date-pickers/DatePicker'
import dayjs from 'dayjs'
import type { Dayjs } from 'dayjs'
import { getSalesByCustomer } from '../api/sales'
import type { SalesByCustomerRequest } from '../api/types'
import { extractErrorMessage } from '../api/client'
import { SalesResult } from '../components/SalesResult'

function parseOptionalInt(value: string): number | undefined {
  const trimmed = value.trim()
  if (trimmed === '') {
    return undefined
  }
  const n = Number(trimmed)
  return Number.isInteger(n) ? n : undefined
}

export function SalesReportPage() {
  const [codEmp, setCodEmp] = useState('1')
  const [codFil, setCodFil] = useState('1')
  const [dateStart, setDateStart] = useState<Dayjs | null>(dayjs().startOf('month'))
  const [dateEnd, setDateEnd] = useState<Dayjs | null>(dayjs())
  const [codCli, setCodCli] = useState('')
  const [produto, setProduto] = useState('')
  const [numNfv, setNumNfv] = useState('')
  const [formError, setFormError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: (request: SalesByCustomerRequest) => getSalesByCustomer(request),
  })

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setFormError(null)

    const emp = parseOptionalInt(codEmp)
    const fil = parseOptionalInt(codFil)
    if (emp === undefined || fil === undefined) {
      setFormError('Informe empresa e filial (números inteiros).')
      return
    }
    if (!dateStart || !dateEnd || !dateStart.isValid() || !dateEnd.isValid()) {
      setFormError('Informe um período válido (data inicial e final).')
      return
    }
    if (dateEnd.isBefore(dateStart, 'day')) {
      setFormError('A data final não pode ser anterior à data inicial.')
      return
    }

    const request: SalesByCustomerRequest = {
      codEmp: emp,
      codFil: fil,
      dateStart: dateStart.format('YYYY-MM-DD'),
      dateEnd: dateEnd.format('YYYY-MM-DD'),
      codCli: parseOptionalInt(codCli) ?? null,
      produto: produto.trim() === '' ? null : produto.trim(),
      numNfv: parseOptionalInt(numNfv) ?? null,
    }
    mutation.mutate(request)
  }

  const data = mutation.data

  return (
    <Stack spacing={3}>
      <Typography variant="h5">Vendas — itens de nota fiscal</Typography>

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Box component="form" onSubmit={handleSubmit}>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2, alignItems: 'flex-start' }}>
            <TextField
              label="Empresa"
              value={codEmp}
              onChange={(e) => setCodEmp(e.target.value)}
              type="number"
              required
              sx={{ width: 120 }}
            />
            <TextField
              label="Filial"
              value={codFil}
              onChange={(e) => setCodFil(e.target.value)}
              type="number"
              required
              sx={{ width: 120 }}
            />
            <DatePicker
              label="Data inicial"
              value={dateStart}
              onChange={(value) => setDateStart(value)}
              format="DD/MM/YYYY"
              slotProps={{ textField: { required: true, sx: { width: 180 } } }}
            />
            <DatePicker
              label="Data final"
              value={dateEnd}
              onChange={(value) => setDateEnd(value)}
              format="DD/MM/YYYY"
              slotProps={{ textField: { required: true, sx: { width: 180 } } }}
            />
            <TextField
              label="Cliente"
              value={codCli}
              onChange={(e) => setCodCli(e.target.value)}
              type="number"
              helperText="opcional"
              sx={{ width: 150 }}
            />
            <TextField
              label="Produto"
              value={produto}
              onChange={(e) => setProduto(e.target.value)}
              helperText="opcional"
              sx={{ width: 170 }}
            />
            <TextField
              label="Nota fiscal"
              value={numNfv}
              onChange={(e) => setNumNfv(e.target.value)}
              type="number"
              helperText="opcional"
              sx={{ width: 160 }}
            />
            <Button
              type="submit"
              variant="contained"
              size="large"
              disabled={mutation.isPending}
              sx={{ height: 56 }}
            >
              {mutation.isPending ? 'Consultando…' : 'Consultar'}
            </Button>
          </Box>
        </Box>
      </Paper>

      {formError && <Alert severity="warning">{formError}</Alert>}

      {mutation.isError && (
        <Alert severity="error">
          {extractErrorMessage(mutation.error, 'Falha ao consultar vendas.')}
        </Alert>
      )}

      {data && !data.integrationConfigured && (
        <Alert severity="warning">
          {data.message ?? 'Integração de vendas não configurada no ERP.'}
        </Alert>
      )}

      {data && data.integrationConfigured && data.rows.length === 0 && (
        <Alert severity="info">
          Nenhum item encontrado para os filtros informados. (Notas lidas do ERP:{' '}
          {data.invoicesRead})
        </Alert>
      )}

      {data && data.integrationConfigured && data.rows.length > 0 && (
        <>
          {!data.venfatFilterActive && (
            <Alert severity="warning">
              Filtro VENFAT=S não configurado: mostrando itens de todas as transações. Informe os
              códigos de transação de venda em Senior:VenfatTns (veja a coluna TNS).
            </Alert>
          )}
          {!data.clientNamesResolved && (
            <Alert severity="info">
              Apelido do cliente indisponível (o web service de clientes ainda não está liberado no
              ERP). A coluna Cliente ficará vazia; o código do cliente continua disponível.
            </Alert>
          )}
          <SalesResult data={data} />
        </>
      )}
    </Stack>
  )
}
