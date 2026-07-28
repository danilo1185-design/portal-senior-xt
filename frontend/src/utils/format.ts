const currencyFormatter = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
})
const integerFormatter = new Intl.NumberFormat('pt-BR')
const quantityFormatter = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })

export function formatCurrency(value: number): string {
  return currencyFormatter.format(value)
}

export function formatInteger(value: number): string {
  return integerFormatter.format(value)
}

export function formatQuantity(value: number): string {
  return quantityFormatter.format(value)
}

/** Converte uma data ISO "yyyy-MM-dd" para "dd/MM/yyyy". Devolve o original se não casar. */
export function formatDateBr(iso: string | null): string {
  if (!iso) {
    return ''
  }
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso)
  return match ? `${match[3]}/${match[2]}/${match[1]}` : iso
}
