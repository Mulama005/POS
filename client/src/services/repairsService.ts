import { apiClient } from './apiClient'
import type { Customer, LedgerEntry, PublicRepairStatus, Repair, RepairStatus } from '../types/phase6'

export const listCustomers = async () => (await apiClient.get<Customer[]>('/api/customers')).data
export const createCustomer = async (payload: { fullName: string; phoneNumber: string; email?: string }) =>
  (await apiClient.post<Customer>('/api/customers', payload)).data
export const getCustomer = async (id: string) => (await apiClient.get<Customer>(`/api/customers/${id}`)).data
export const getLedger = async (id: string) => (await apiClient.get<LedgerEntry[]>(`/api/customers/${id}/ledger`)).data
export const recordCreditSale = (id: string, amount: number, notes?: string) =>
  apiClient.post(`/api/customers/${id}/credit-sale`, { amount, relatedSaleId: null, notes: notes || null })
export const recordPayment = (id: string, amount: number, paymentMethod: string, notes?: string) =>
  apiClient.post(`/api/customers/${id}/payment`, { amount, paymentMethod, notes: notes || null })

export const listRepairs = async () => (await apiClient.get<Repair[]>('/api/repairs')).data
export const myRepairQueue = async () => (await apiClient.get<Repair[]>('/api/repairs/my-queue')).data
export const createRepair = async (payload: { customerId: string; deviceDescription: string; reportedFault: string; quotedCost?: number | null; assignedTechnicianId?: string | null }) =>
  (await apiClient.post<Repair>('/api/repairs', payload)).data
export const updateRepairStatus = (id: string, newStatus: RepairStatus, diagnosisNotes: string) =>
  apiClient.put(`/api/repairs/${id}/status`, { newStatus, diagnosisNotes: diagnosisNotes || null })
export const assignRepair = (id: string, technicianId: string) =>
  apiClient.put(`/api/repairs/${id}/assign`, { technicianId })
export const trackRepair = async (ticketNumber: string, phoneLast4: string) =>
  (await apiClient.get<PublicRepairStatus>(`/api/repairs/track/${encodeURIComponent(ticketNumber)}`, { params: { phoneLast4 } })).data
