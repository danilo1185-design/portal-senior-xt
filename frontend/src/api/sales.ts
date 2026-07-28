import { api } from './client'
import type { SalesByCustomerRequest, SalesItemsResponse } from './types'

export async function getSalesByCustomer(
  request: SalesByCustomerRequest,
): Promise<SalesItemsResponse> {
  const { data } = await api.post<SalesItemsResponse>('/sales/by-customer', request)
  return data
}
