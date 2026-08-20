export interface TillSession {
  id: string
  registerId: string
  registerName: string
  openedByUserId: string
  openedByName: string
  openedAt: string
  openingFloat: number
  status: 'Open' | 'Closed'
}

export interface TillReconciliation {
  id: string
  registerId: string
  openedAt: string
  closedAt: string
  openingFloat: number
  cashSalesTotal: number
  expectedCashAtClose: number
  countedCashAtClose: number
  variance: number
  mpesaSalesTotal: number
  cardSalesTotal: number
}