export type TableStatus = 'Available' | 'Occupied' | 'Paused'
export type TableKind = 'Billiard' | 'Regular'

export interface Table {
  id: string
  name: string
  hourlyRate: number
  billingIncrementMinutes: number
  status: TableStatus
  activeSessionId: string | null
  startedAtUtc: string | null
  totalPausedSeconds: number
  pausedAtUtc: string | null
  kind: TableKind
  consumptionTotal: number
}

export interface SessionResult {
  sessionId: string
  tableId: string
  status: 'Active' | 'Closed'
  elapsedSeconds: number
  billedMinutes: number
  billiardCharge: number
  consumptionTotal: number
  total: number
  saleId: string | null
  startedAtUtc: string
  endedAtUtc: string | null
}


export interface Product {
  id: string
  name: string
  sku?: string | null
  categoryId?: string | null
  salePrice: number
  averageCost: number
  currentStock: number
  minimumStock: number
  criticalStock: number
  isActive: boolean
}

export interface Consumption {
  id: string
  productId: string
  descriptionSnapshot: string
  quantity: number
  unitPrice: number
  subtotal: number
}

export interface SessionDetail extends SessionResult {
  consumptions: Consumption[]
}


export type ExpensePaymentMethod = 'Cash' | 'Nequi' | 'Daviplata' | 'Card' | 'BankTransfer' | 'Other';

export interface CashShift {
  id: string
  userId: string
  openedAt: string
  closedAt: string | null
  openingCash: number
  cashSales: number
  cashExpenses: number
  expectedCash: number
  status: 'Open' | 'Closed'
}

export interface CashShiftHistory {
  id: string
  userId: string
  openedAt: string
  closedAt: string | null
  openingCash: number
  cashSales: number
  cashExpenses: number
  expectedCash: number
  countedCash: number | null
  difference: number | null
  status: 'Closed'
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  user: { id: string; fullName: string; email: string; roles: string[] }
}



export interface DashboardResponse {
  businessDate: string
  grossSales: number
  productRevenue: number
  billiardRevenue: number
  expenses: number
  expectedCash: number
  activeTables: number
  totalTables: number
  lowStockProducts: number
  paymentBreakdown: Array<{ method: string; amount: number }>
  recentSales: Array<{ id: string; sequentialNumber: number; createdAt: string; total: number; status: string; userId: string; billiardTableName: string | null; billiardMinutes: number | null; billiardAmount: number | null }>
  lowStockItems: Array<{ id: string; name: string; currentStock: number; minimumStock: number; criticalStock: number }>
}
export interface SaleResponse { id: string; sequentialNumber: number; subtotal: number; discount: number; total: number; status: string; itemCount: number }

export interface PurchaseResponse { id: string; purchaseDate: string; total: number; itemCount: number }

export interface InventoryMovementResponse { id: string; productId: string; productName: string; movementType: string; quantity: number; unitCost: number | null; notes: string | null; createdAt: string }
export interface PhysicalCountResponse { id: string; countedAt: string; itemCount: number; adjustedItemCount: number }
export interface ReportMetrics {
  grossSales: number
  productRevenue: number
  billiardRevenue: number
  costOfGoodsSold: number
  grossProfit: number
  expenses: number
  operatingResult: number
  cashPayments: number
  electronicPayments: number
  billiardHours: number
  completedSales: number
  cancelledSales: number
  paymentBreakdown: Array<{ method: string; amount: number }>
  salesByDay: Array<{ date: string; amount: number }>
  salesByEmployee: Array<{ userId: string; employeeName: string; amount: number; salesCount: number }>
  salesByProduct: Array<{ productId: string | null; productName: string; quantity: number; revenue: number; cost: number; grossProfit: number }>
  salesByHour: Array<{ hour: number; amount: number; salesCount: number }>
  billiardByTable: Array<{ tableId: string; tableName: string; hours: number; sessions: number }>
}
export interface ReportResponse {
  from: string
  to: string
  previousFrom: string
  previousTo: string
  current: ReportMetrics
  previous: ReportMetrics
}export interface EmployeeResponse { id: string; email: string; fullName: string; roles: string[]; isActive: boolean; createdAt: string }
export interface AuditLogResponse { id: string; userId: string | null; action: string; entityType: string; entityId: string | null; oldValuesJson: string | null; newValuesJson: string | null; ipAddress: string | null; createdAt: string }
export interface NotificationResponse { id: string; channel: string; type: string; status: string; title: string; body: string; referenceType: string | null; referenceId: string | null; createdAt: string; sentAt: string | null; readAt: string | null }

export interface NotificationPreferenceResponse { saleNotificationMode: 'Never' | 'AllSales' | 'AboveAmount'; saleNotificationAmount: number; pushEnabled: boolean; whatsAppEnabled: boolean; whatsAppPhoneNumber: string | null; emailAddress: string | null }

export interface SaleDetailResponse {
  id: string
  sequentialNumber: number
  createdAt: string
  status: string
  subtotal: number
  discount: number
  total: number
  cashierName: string | null
  customerName: string | null
  tableName: string | null
  billiardMinutes: number | null
  items: Array<{ description: string; quantity: number; unitPrice: number; subtotal: number; itemType: string }>
  payments: Array<{ method: string; amount: number }>
  billiardStartedAt: string | null
  billiardEndedAt: string | null
}
