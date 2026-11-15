export type FinanceMaterialStatus = 'pending' | 'reimbursed' | 'deleted';
export type FinanceInvoiceStatus = 'pending' | 'recognized' | 'confirmed' | 'failed';
export type FinanceFormKind = 'apply' | 'reimburse';

export interface FinancePaged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface FinanceInvoice {
  id: string;
  materialId?: string | null;
  materialNo?: string | null;
  activityName?: string | null;
  materialStatus?: string | null;
  materialStatusLabel?: string | null;
  type?: string | null;
  code?: string | null;
  number?: string | null;
  issueDate?: string | null;
  buyer?: string | null;
  seller?: string | null;
  item?: string | null;
  qty?: number | null;
  price?: number | null;
  unit?: string | null;
  amount: number;
  tax: number;
  total: number;
  confidence: Record<string, number>;
  status: FinanceInvoiceStatus;
  statusLabel: string;
  confirmed: boolean;
  hasFile: boolean;
  fileName?: string | null;
  contentType?: string | null;
  recognitionError?: string | null;
  recognizedAt?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface FinanceMaterial {
  id: string;
  activityName?: string | null;
  activityDate?: string | null;
  activityType?: string | null;
  location?: string | null;
  headcount?: number | null;
  owner?: string | null;
  phone?: string | null;
  summary?: string | null;
  activityBudget: number;
  totalAmount: number;
  status: FinanceMaterialStatus;
  statusLabel: string;
  remark?: string | null;
  academicYear: string;
  formVersion: number;
  hasApplyForm: boolean;
  hasReimburseForm: boolean;
  generatedAt?: string | null;
  invoiceCount: number;
  confirmedInvoiceCount: number;
  unconfirmedAmount: number;
  confirmedAt?: string | null;
  confirmedBy?: string | null;
  deletedAt?: string | null;
  canRestore: boolean;
  revokeReason?: string | null;
  createdBy?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface FinanceMaterialDetail extends FinanceMaterial {
  invoices: FinanceInvoice[];
  estimatedDeduction: number;
}

export interface FinanceFlow {
  id: string;
  materialId: string;
  materialNo: string;
  activityName?: string | null;
  academicYear: string;
  amount: number;
  type: 'deduct' | 'rollback';
  typeLabel: string;
  status: 'active' | 'voided';
  reason?: string | null;
  operatorId?: string | null;
  operatorName?: string | null;
  createdAt: string;
}

export interface FinanceLog {
  id: string;
  operatorId?: string | null;
  operatorName?: string | null;
  role?: string | null;
  roleLabel?: string | null;
  ip?: string | null;
  userAgent?: string | null;
  action: string;
  targetType?: string | null;
  targetId?: string | null;
  before?: string | null;
  after?: string | null;
  result: 'success' | 'failure' | string;
  message?: string | null;
  createdAt: string;
}

export interface FinanceAccount {
  id: string;
  academicYear: string;
  budgetAmount: number;
  remark?: string | null;
  createdAt: string;
  updatedAt: string;
  editable: boolean;
}

export interface FinanceFundSummary {
  academicYear: string;
  budgetAmount: number;
  usedAmount: number;
  inTransitAmount: number;
  remainingAmount: number;
  usagePercent: number;
  warningLevel: 'normal' | 'warning' | 'danger';
  accountExists: boolean;
  editable: boolean;
  reimbursedCount: number;
  pendingCount: number;
  recentFlows: FinanceFlow[];
}

export interface FinanceTemplate {
  id: string;
  kind: FinanceFormKind;
  kindLabel: string;
  version: number;
  originalFileName?: string | null;
  enabled: boolean;
  createdAt: string;
  createdBy?: string | null;
}

export interface FinanceMeta {
  clubName: string;
  currentAcademicYear: string;
  years: string[];
  activityTypes: string[];
  lowConfidenceThreshold: number;
  ocrConfigured: boolean;
  pdfConfigured: boolean;
  maxFileSizeMb: number;
  maxFilesPerUpload: number;
  supportedExtensions: string[];
}

export interface FinanceInvoiceSaveResult {
  invoice: FinanceInvoice | null;
  errors: string[];
  warnings: string[];
  saved: boolean;
}

export interface FinanceMaterialUpsertDto {
  activityName?: string | null;
  activityDate?: string | null;
  activityType?: string | null;
  location?: string | null;
  headcount?: number | null;
  owner?: string | null;
  phone?: string | null;
  summary?: string | null;
  activityBudget: number;
  remark?: string | null;
}

export interface FinanceInvoiceUpdateDto {
  type?: string | null;
  code?: string | null;
  number?: string | null;
  issueDate?: string | null;
  buyer?: string | null;
  seller?: string | null;
  item?: string | null;
  qty?: number | null;
  price?: number | null;
  unit?: string | null;
  amount: number;
  tax: number;
  total: number;
  confirm: boolean;
}

export interface FinanceBatchExportDto {
  ids?: string[] | null;
  materialNo?: string | null;
  activityName?: string | null;
  from?: string | null;
  to?: string | null;
  status?: string | null;
  kind?: FinanceFormKind;
}
