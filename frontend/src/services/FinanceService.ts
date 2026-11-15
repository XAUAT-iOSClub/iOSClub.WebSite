import { url } from './Url'
import { apiRequest } from './ApiService'
import type {
  FinanceAccount,
  FinanceBatchExportDto,
  FinanceFlow,
  FinanceFundSummary,
  FinanceInvoice,
  FinanceInvoiceSaveResult,
  FinanceInvoiceUpdateDto,
  FinanceLog,
  FinanceMaterial,
  FinanceMaterialDetail,
  FinanceMaterialUpsertDto,
  FinanceMeta,
  FinancePaged,
  FinanceTemplate
} from '../models'

function token() {
  return localStorage.getItem('accessToken') || ''
}

function parseFileName(disposition: string | null, fallback: string) {
  if (!disposition) return fallback
  const star = disposition.match(/filename\*=UTF-8''([^;]+)/i)
  if (star) {
    try { return decodeURIComponent(star[1].trim()) } catch { return star[1].trim() }
  }
  const plain = disposition.match(/filename="?([^";]+)"?/i)
  return plain ? plain[1].trim() : fallback
}

async function download(path: string, fallback: string, init: RequestInit = {}) {
  const isForm = typeof FormData !== 'undefined' && init.body instanceof FormData
  const headers: Record<string, string> = {
    Authorization: `Bearer ${token()}`,
    ...(isForm || !init.body ? {} : { 'Content-Type': 'application/json' }),
    ...((init.headers as Record<string, string>) || {})
  }

  const res = await fetch(`${url}${path}`, { ...init, headers })
  if (!res.ok) {
    let message = '下载失败'
    try {
      const payload = await res.json()
      message = payload?.message || message
    } catch {
      // 非 JSON 错误响应，沿用默认提示
    }
    throw new Error(message)
  }

  const blob = await res.blob()
  const filename = parseFileName(res.headers.get('Content-Disposition'), fallback)
  const href = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = href
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  URL.revokeObjectURL(href)
}

function query(params: Record<string, string | number | boolean | undefined | null>) {
  const search = new URLSearchParams()
  Object.entries(params).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  })
  const text = search.toString()
  return text ? `?${text}` : ''
}

export class FinanceService {
  static meta(): Promise<FinanceMeta> {
    return apiRequest<FinanceMeta>({ url: `${url}/Finance/meta`, method: 'GET' })
  }

  // ===== 发票 =====
  static invoices(params: {
    status?: string
    materialStatus?: string
    excludeReimbursedMaterial?: boolean
    materialId?: string
    unassigned?: boolean
    keyword?: string
    from?: string
    to?: string
    page?: number
    pageSize?: number
  } = {}): Promise<FinancePaged<FinanceInvoice>> {
    return apiRequest<FinancePaged<FinanceInvoice>>({
      url: `${url}/Finance/invoices${query(params)}`,
      method: 'GET'
    })
  }

  static uploadInvoice(file: File): Promise<FinanceInvoice[]> {
    const form = new FormData()
    form.append('files', file, file.name)
    return apiRequest<FinanceInvoice[]>({ url: `${url}/Finance/invoices/upload`, method: 'POST', body: form })
  }

  static recognizeInvoice(id: string): Promise<FinanceInvoiceSaveResult> {
    return apiRequest<FinanceInvoiceSaveResult>({
      url: `${url}/Finance/invoices/${id}/recognize`,
      method: 'POST'
    })
  }

  static updateInvoice(id: string, dto: FinanceInvoiceUpdateDto): Promise<FinanceInvoiceSaveResult> {
    return apiRequest<FinanceInvoiceSaveResult>({
      url: `${url}/Finance/invoices/${id}`,
      method: 'PUT',
      body: dto as unknown as Record<string, unknown>
    })
  }

  static deleteInvoice(id: string): Promise<void> {
    return apiRequest<void>({ url: `${url}/Finance/invoices/${id}`, method: 'DELETE' })
  }

  static assignableInvoices(keyword = '', take = 100): Promise<FinanceInvoice[]> {
    return apiRequest<FinanceInvoice[]>({
      url: `${url}/Finance/invoices/assignable${query({ keyword, take })}`,
      method: 'GET'
    })
  }

  /** 读取发票原件并生成可在 <img> 中使用的 blob URL，调用方负责 revoke。 */
  static async invoiceObjectUrl(id: string): Promise<string> {
    const res = await fetch(`${url}/Finance/invoices/${id}/file`, {
      headers: { Authorization: `Bearer ${token()}` }
    })
    if (!res.ok) throw new Error('发票原件读取失败')
    return URL.createObjectURL(await res.blob())
  }

  // ===== 材料 =====
  static materials(params: {
    status?: string
    keyword?: string
    academicYear?: string
    includeDeleted?: boolean
    page?: number
    pageSize?: number
  } = {}): Promise<FinancePaged<FinanceMaterial>> {
    return apiRequest<FinancePaged<FinanceMaterial>>({
      url: `${url}/Finance/materials${query(params)}`,
      method: 'GET'
    })
  }

  static material(id: string): Promise<FinanceMaterialDetail> {
    return apiRequest<FinanceMaterialDetail>({ url: `${url}/Finance/materials/${id}`, method: 'GET' })
  }

  static createMaterial(dto: FinanceMaterialUpsertDto): Promise<FinanceMaterialDetail> {
    return apiRequest<FinanceMaterialDetail>({
      url: `${url}/Finance/materials`,
      method: 'POST',
      body: dto as unknown as Record<string, unknown>
    })
  }

  static updateMaterial(id: string, dto: FinanceMaterialUpsertDto): Promise<FinanceMaterialDetail> {
    return apiRequest<FinanceMaterialDetail>({
      url: `${url}/Finance/materials/${id}`,
      method: 'PUT',
      body: dto as unknown as Record<string, unknown>
    })
  }

  static assignInvoices(id: string, invoiceIds: string[], action: 'assign' | 'remove'): Promise<FinanceMaterialDetail> {
    return apiRequest<FinanceMaterialDetail>({
      url: `${url}/Finance/materials/${id}/invoices`,
      method: 'POST',
      body: { invoiceIds, action }
    })
  }

  static generate(id: string, kind: 'apply' | 'reimburse'): Promise<FinanceMaterialDetail> {
    return apiRequest<FinanceMaterialDetail>({
      url: `${url}/Finance/materials/${id}/generate`,
      method: 'POST',
      body: { kind }
    })
  }

  static confirm(id: string): Promise<FinanceMaterialDetail> {
    return apiRequest<FinanceMaterialDetail>({ url: `${url}/Finance/materials/${id}/confirm`, method: 'POST' })
  }

  static revoke(id: string, reason: string): Promise<FinanceMaterialDetail> {
    return apiRequest<FinanceMaterialDetail>({
      url: `${url}/Finance/materials/${id}/revoke`,
      method: 'POST',
      body: { reason }
    })
  }

  static deleteMaterial(id: string): Promise<void> {
    return apiRequest<void>({ url: `${url}/Finance/materials/${id}`, method: 'DELETE' })
  }

  static restoreMaterial(id: string): Promise<FinanceMaterialDetail> {
    return apiRequest<FinanceMaterialDetail>({ url: `${url}/Finance/materials/${id}/restore`, method: 'POST' })
  }

  static materialLogs(id: string, take = 50): Promise<FinanceLog[]> {
    return apiRequest<FinanceLog[]>({
      url: `${url}/Finance/materials/${id}/logs${query({ take })}`,
      method: 'GET'
    })
  }

  static exportMaterial(id: string, kind: 'apply' | 'reimburse', format: 'docx' | 'pdf' = 'docx'): Promise<void> {
    return download(
      `/Finance/materials/${id}/export?kind=${kind}&format=${format}`,
      `财务表单.${format}`
    )
  }

  static batchExport(dto: FinanceBatchExportDto): Promise<void> {
    return download('/Finance/materials/export', '财务表单导出.zip', {
      method: 'POST',
      body: JSON.stringify(dto)
    })
  }

  // ===== 经费 =====
  static summary(academicYear?: string): Promise<FinanceFundSummary> {
    return apiRequest<FinanceFundSummary>({
      url: `${url}/Finance/funds/summary${query({ academicYear })}`,
      method: 'GET'
    })
  }

  static flows(params: {
    academicYear?: string
    type?: string
    keyword?: string
    from?: string
    to?: string
    page?: number
    pageSize?: number
  } = {}): Promise<FinancePaged<FinanceFlow>> {
    return apiRequest<FinancePaged<FinanceFlow>>({
      url: `${url}/Finance/funds/flows${query(params)}`,
      method: 'GET'
    })
  }

  static accounts(): Promise<FinanceAccount[]> {
    return apiRequest<FinanceAccount[]>({ url: `${url}/Finance/funds/accounts`, method: 'GET' })
  }

  static updateAccount(academicYear: string, budgetAmount: number, remark?: string): Promise<FinanceAccount> {
    return apiRequest<FinanceAccount>({
      url: `${url}/Finance/funds/account`,
      method: 'PUT',
      body: { academicYear, budgetAmount, remark }
    })
  }

  // ===== 日志 =====
  static logs(params: {
    action?: string
    operatorName?: string
    targetType?: string
    targetId?: string
    result?: string
    from?: string
    to?: string
    page?: number
    pageSize?: number
  } = {}): Promise<FinancePaged<FinanceLog>> {
    return apiRequest<FinancePaged<FinanceLog>>({
      url: `${url}/Finance/logs${query(params)}`,
      method: 'GET'
    })
  }

  static exportLogs(params: Record<string, string | undefined> = {}): Promise<void> {
    return download(`/Finance/logs/export${query(params)}`, `财务操作日志_${Date.now()}.csv`)
  }

  // ===== 模板 =====
  static templates(): Promise<FinanceTemplate[]> {
    return apiRequest<FinanceTemplate[]>({ url: `${url}/Finance/templates`, method: 'GET' })
  }

  static uploadTemplate(file: File, kind: 'apply' | 'reimburse'): Promise<FinanceTemplate> {
    const form = new FormData()
    form.append('file', file, file.name)
    form.append('kind', kind)
    return apiRequest<FinanceTemplate>({ url: `${url}/Finance/templates`, method: 'POST', body: form })
  }

  static enableTemplate(id: string): Promise<void> {
    return apiRequest<void>({ url: `${url}/Finance/templates/${id}/enable`, method: 'POST' })
  }

  static deleteTemplate(id: string): Promise<void> {
    return apiRequest<void>({ url: `${url}/Finance/templates/${id}`, method: 'DELETE' })
  }
}
