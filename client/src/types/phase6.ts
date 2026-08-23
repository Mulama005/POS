export type RepairStatus = 'Received' | 'Diagnosing' | 'AwaitingParts' | 'InRepair' | 'Ready' | 'Collected'

export interface Customer {
  id: string
  fullName: string
  phone: string | null
  email: string | null
  currentCreditBalance: number
}

export interface Repair {
  id: string
  ticketNumber: string
  customerId: string
  deviceDescription: string
  reportedFault: string
  quotedCost: number | null
  assignedTechnicianId: string | null
  status: RepairStatus
  diagnosisNotes: string | null
  createdAt: string
  collectedAt: string | null
  statusHistory?: RepairStatusHistory[]
}

export interface RepairStatusHistory {
  id: string
  fromStatus: RepairStatus
  toStatus: RepairStatus
  changedAt?: string
}

export interface LedgerEntry {
  id: string
  type: 'CreditSale' | 'Payment'
  amount: number
  paymentMethod: string | null
  notes: string | null
  balanceAfter: number
  timestamp: string
}

export interface PublicRepairStatus {
  ticketNumber: string
  deviceDescription: string
  status: RepairStatus
  createdAt: string
  collectedAt: string | null
}
