<script setup lang="ts">
import { computed, h, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { NDataTable, NModal, useDialog, useMessage } from 'naive-ui'
import type { DataTableColumns } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import FinanceTabs from './components/FinanceTabs.vue'
import FinanceInvoiceThumb from './components/FinanceInvoiceThumb.vue'
import { FinanceService } from '../../services/FinanceService'
import { useLayoutStore } from '../../stores/LayoutStore'
import type {
  FinanceInvoice,
  FinanceInvoiceUpdateDto,
  FinanceMeta
} from '../../models'

const message = useMessage()
const dialog = useDialog()
const layoutStore = useLayoutStore()

const meta = ref<FinanceMeta | null>(null)
const items = ref<FinanceInvoice[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)
const loadError = ref('')
const statusFilter = ref('')
const keyword = ref('')
const checkedKeys = ref<Array<string | number>>([])

const uploadInput = ref<HTMLInputElement>()
const uploading = ref(false)
const uploadText = ref('')

const editModal = ref(false)
const editReadonly = ref(false)
const editing = ref<FinanceInvoice | null>(null)
const saving = ref(false)
const editForm = reactive({
  type: '',
  number: '',
  issueDate: '',
  buyer: '',
  seller: '',
  item: '',
  qty: null as number | null,
  price: null as number | null,
  unit: '',
  amount: 0,
  tax: 0,
  total: 0,
  confirm: false
})

const statusTabs = [
  { label: '全部', value: '' },
  { label: '识别失败', value: 'failed' },
  { label: '待确认', value: 'recognized' },
  { label: '待报销', value: 'confirmed' },
  { label: '已报销', value: 'reimbursed' }
]

const accept = computed(() =>
    (meta.value?.supportedExtensions ?? ['jpg', 'jpeg', 'png', 'webp', 'bmp', 'docx', 'pdf'])
        .map(ext => `.${ext}`).join(','))

const maxFileBytes = computed(() => (meta.value?.maxFileSizeMb ?? 10) * 1024 * 1024)

const formatMoney = (value: number | undefined | null) =>
    `¥${Number(value ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

const formatDate = (value?: string | null) => {
  if (!value) return '—'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

const formatTime = (value?: string | null) => {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return `${formatDate(value)} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

const toDateInput = (value?: string | null) => formatDate(value) === '—' ? '' : formatDate(value)

const invoiceDisplayStatus = (invoice: FinanceInvoice) =>
    invoice.materialStatus === 'reimbursed' ? 'reimbursed' : invoice.status

const statusClass = (invoice: FinanceInvoice) => {
  const status = invoiceDisplayStatus(invoice)
  if (status === 'reimbursed') return 'reimbursed'
  if (status === 'failed') return 'failed'
  if (status === 'recognized' || status === 'confirmed') return 'pending'
  return 'deleted'
}

const statusLabel = (invoice: FinanceInvoice) => ({
  pending: '识别中', recognized: '待确认', confirmed: '待报销', failed: '识别失败', reimbursed: '已报销'
}[invoiceDisplayStatus(invoice)] ?? invoice.statusLabel ?? invoice.status)

const confidenceOf = (invoice: FinanceInvoice | null, field: string): number | null => {
  if (!invoice?.confidence) return null
  const value = invoice.confidence[field]
  return typeof value === 'number' ? value : null
}

const isLow = (invoice: FinanceInvoice | null, field: string) => {
  const value = confidenceOf(invoice, field)
  return value !== null && value < (meta.value?.lowConfidenceThreshold ?? 0.8)
}

const confidenceTitle = (invoice: FinanceInvoice | null, field: string) => {
  const value = confidenceOf(invoice, field)
  return value === null ? '未识别，需人工填写' : `置信度 ${(value * 100).toFixed(0)}%`
}

const chip = (text: string, className: string) =>
    h('span', { class: `finance-tag ${className}` }, text)

const actionBtn = (text: string, cls: string, onClick: () => void, disabled = false) =>
    h('button', {
      class: `inline-flex items-center rounded-lg px-2.5 py-1 text-xs font-medium mr-1.5 mb-1 transition-all ${cls}`,
      type: 'button',
      title: text,
      disabled,
      onClick: (e: Event) => { e.stopPropagation(); if (!disabled) onClick() }
    }, text)

const allColumns: DataTableColumns<FinanceInvoice> = [
  { type: 'selection', width: 40 },
  {
    title: '原件', key: 'file', width: 56,
    render: row => h(FinanceInvoiceThumb, {
      invoiceId: row.id,
      hasFile: row.hasFile,
      contentType: row.contentType,
      fileName: row.fileName
    })
  },
  {
    title: '发票号', key: 'number', width: 120,
    render: row => h('span', { class: 'font-medium text-gray-900 dark:text-gray-100' }, row.number || '未识别')
  },
  { title: '开票日期', key: 'issueDate', width: 95, render: row => formatDate(row.issueDate) },
  { title: '销售方', key: 'seller', minWidth: 125, render: row => row.seller || '—' },
  { title: '项目', key: 'item', minWidth: 90, render: row => row.item || '—' },
  {
    title: '价税合计', key: 'total', width: 105, align: 'right',
    render: row => h('span', { class: 'font-medium text-gray-900 dark:text-gray-100' }, formatMoney(row.total))
  },
  {
    title: '状态', key: 'status', width: 85,
    render: row => chip(statusLabel(row), statusClass(row))
  },
  {
    title: '操作', key: 'actions', width: 190,
    render: row => {
      const buttons = []
      if (row.status === 'pending') {
        buttons.push(actionBtn('重新识别',
            'bg-blue-50 text-blue-600 hover:bg-blue-100 dark:bg-blue-500/20 dark:text-blue-300',
            () => recognize(row)))
        buttons.push(actionBtn('删除',
            'bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/20 dark:text-red-300',
            () => remove(row)))
      } else if (row.status === 'failed') {
        buttons.push(actionBtn('人工输入',
            'bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-white/10 dark:text-gray-200',
            () => openEdit(row)))
        buttons.push(actionBtn('重新识别',
            'bg-blue-50 text-blue-600 hover:bg-blue-100 dark:bg-blue-500/20 dark:text-blue-300',
            () => recognize(row)))
        buttons.push(actionBtn('删除',
            'bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/20 dark:text-red-300',
            () => remove(row)))
      } else if (row.status === 'recognized') {
        buttons.push(actionBtn('人工确认',
            'bg-emerald-50 text-emerald-600 hover:bg-emerald-100 dark:bg-emerald-500/20 dark:text-emerald-300',
            () => openEdit(row, true)))
        buttons.push(actionBtn('删除',
            'bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/20 dark:text-red-300',
            () => remove(row)))
      } else if (row.status === 'confirmed') {
        buttons.push(actionBtn('回退',
            'bg-amber-50 text-amber-700 hover:bg-amber-100 dark:bg-amber-500/20 dark:text-amber-300',
            () => revertToReview(row)))
        buttons.push(actionBtn('删除',
            'bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/20 dark:text-red-300',
            () => remove(row)))
      }
      return h('div', { class: 'flex flex-wrap items-center' }, buttons)
    }
  }
]

const columns = computed(() => {
  if (statusFilter.value === '') {
    return allColumns.filter(column => (column as { key?: string }).key !== 'actions')
  }
  if (statusFilter.value === 'reimbursed') {
    return allColumns.filter(column => {
      const key = (column as { key?: string }).key
      const type = (column as { type?: string }).type
      return key !== 'actions' && type !== 'selection'
    })
  }
  return allColumns
})

const pagination = computed(() => ({
  page: page.value,
  pageSize: pageSize.value,
  itemCount: total.value,
  showSizePicker: true,
  pageSizes: [10, 20, 50],
  prefix: (info: { itemCount?: number }) => `共 ${info.itemCount ?? 0} 张发票`,
  onUpdatePage: (value: number) => { page.value = value; void load() },
  onUpdatePageSize: (value: number) => { pageSize.value = value; page.value = 1; void load() }
}))

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    if (!meta.value) meta.value = await FinanceService.meta()
    const data = await FinanceService.invoices({
      status: statusFilter.value === 'reimbursed' ? undefined : statusFilter.value || undefined,
      materialStatus: statusFilter.value === 'reimbursed' ? 'reimbursed' : undefined,
      excludeReimbursedMaterial: statusFilter.value === 'confirmed' ? true : undefined,
      keyword: keyword.value.trim() || undefined,
      page: page.value,
      pageSize: pageSize.value
    })
    items.value = data.items
    total.value = data.total
    checkedKeys.value = []
  } catch (e: any) {
    loadError.value = e?.message || '发票列表加载失败'
  } finally {
    loading.value = false
  }
}

function setStatus(value: string) {
  statusFilter.value = value
  page.value = 1
  void load()
}

// ===== 上传 =====
function triggerUpload() {
  uploadInput.value?.click()
}

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const files = Array.from(input.files ?? [])
  input.value = ''
  if (files.length) void handleFiles(files)
}

async function handleFiles(files: File[]) {
  if (uploading.value) return

  const limit = meta.value?.maxFilesPerUpload ?? 20
  if (files.length > limit) {
    message.warning(`单次最多上传 ${limit} 个文件`)
    files = files.slice(0, limit)
  }

  const errors: string[] = []
  const valid = files.filter(file => {
    const ext = file.name.split('.').pop()?.toLowerCase() ?? ''
    const allowed = (meta.value?.supportedExtensions ?? ['jpg', 'jpeg', 'png', 'webp', 'bmp', 'docx', 'pdf'])
        .map(item => item.toLowerCase())
    if (!allowed.includes(ext)) {
      errors.push(`${file.name}：格式不支持`)
      return false
    }
    if (file.size > maxFileBytes.value) {
      errors.push(`${file.name}：超过 ${meta.value?.maxFileSizeMb ?? 10}MB`)
      return false
    }
    return true
  })

  if (errors.length) message.error(errors.join('；'))
  if (!valid.length) return

  uploading.value = true
  let success = 0
  let recognized = 0
  let failed = 0
  try {
    for (let i = 0; i < valid.length; i++) {
      uploadText.value = `识别中 ${i + 1}/${valid.length}`
      try {
        const uploaded = await FinanceService.uploadInvoice(valid[i])
        success += uploaded.length
        for (const invoice of uploaded) {
          try {
            const result = await FinanceService.recognizeInvoice(invoice.id)
            if (result.invoice?.status === 'failed') failed++
            else recognized++
          } catch {
            failed++
          }
        }
      } catch (e: any) {
        message.error(`${valid[i].name} 上传失败：${e?.message || '未知错误'}`)
      }
    }
    if (success > 0) {
      if (failed > 0) {
        message.warning(`已上传 ${success} 张：识别成功 ${recognized} 张，失败 ${failed} 张`)
      } else {
        message.success(`已上传并识别 ${recognized} 张发票`)
      }
      page.value = 1
      await load()
    }
  } finally {
    uploading.value = false
    uploadText.value = ''
  }
}

// ===== 识别 =====
function recognize(invoice: FinanceInvoice) {
  const run = async () => {
    try {
      const result = await FinanceService.recognizeInvoice(invoice.id)
      if (result.invoice?.status === 'failed') {
        message.warning(result.invoice.recognitionError || '识别失败，请手工录入')
      } else {
        if (result.warnings?.length) message.warning(result.warnings.join('；'))
        else message.success('识别完成，请核对字段')
      }
      await load()
    } catch (e: any) {
      message.error(e?.message || '识别失败')
    }
  }

  const alreadyParsed = invoice.status === 'recognized' && (invoice.number || invoice.total > 0)
  if (alreadyParsed) {
    dialog.warning({
      title: '重新识别',
      content: '重新识别会覆盖当前待确认的字段，是否继续？',
      positiveText: '重新识别',
      negativeText: '取消',
      onPositiveClick: run
    })
  } else {
    void run()
  }
}

// ===== 编辑 =====
function invoiceRowProps(row: FinanceInvoice) {
  return {
    style: 'cursor: pointer',
    onClick: (event: MouseEvent) => {
      const target = event.target as HTMLElement | null
      if (target?.closest('button, a, .n-checkbox, .n-checkbox-box')) return
      const readonly = row.status === 'confirmed' || row.status === 'pending' ||
          row.materialStatus === 'reimbursed'
      openEdit(row, row.status === 'recognized' || row.status === 'confirmed', readonly)
    }
  }
}

function openEdit(invoice: FinanceInvoice, confirm = false, readonly = false) {
  editing.value = invoice
  editReadonly.value = readonly
  editForm.type = invoice.type ?? ''
  editForm.number = invoice.number ?? ''
  editForm.issueDate = toDateInput(invoice.issueDate)
  editForm.buyer = invoice.buyer ?? ''
  editForm.seller = invoice.seller ?? ''
  editForm.item = invoice.item ?? ''
  editForm.qty = invoice.qty ?? null
  editForm.price = invoice.price ?? null
  editForm.unit = invoice.unit ?? ''
  editForm.amount = invoice.amount ?? 0
  editForm.tax = invoice.tax ?? 0
  editForm.total = invoice.total ?? 0
  editForm.confirm = confirm || invoice.confirmed
  editModal.value = true
}

async function saveEdit() {
  if (!editing.value) return

  if (editForm.confirm) {
    const missing: string[] = []
    if (!editForm.type.trim()) missing.push('发票类型')
    if (!editForm.number.trim()) missing.push('发票号码')
    if (!editForm.issueDate) missing.push('开票日期')
    if (!editForm.buyer.trim()) missing.push('购买方')
    if (!editForm.seller.trim()) missing.push('销售方')
    if (!editForm.item.trim()) missing.push('项目名称')
    if (editForm.qty === null || Number(editForm.qty) <= 0) missing.push('数量')
    if (editForm.price === null || Number(editForm.price) <= 0) missing.push('单价')
    if (!editForm.unit.trim()) missing.push('单位')
    if (!Number(editForm.amount)) missing.push('金额')
    if (!Number(editForm.total)) missing.push('价税合计')
    if (missing.length) {
      message.warning(`人工确认前请填写：${missing.join('、')}`)
      return
    }
  }

  const dto: FinanceInvoiceUpdateDto = {
    type: editForm.type.trim() || null,
    number: editForm.number.trim() || null,
    issueDate: editForm.issueDate ? new Date(`${editForm.issueDate}T00:00:00`).toISOString() : null,
    buyer: editForm.buyer.trim() || null,
    seller: editForm.seller.trim() || null,
    item: editForm.item.trim() || null,
    qty: editForm.qty === null || Number.isNaN(editForm.qty) ? null : Number(editForm.qty),
    price: editForm.price === null || Number.isNaN(editForm.price) ? null : Number(editForm.price),
    unit: editForm.unit.trim() || null,
    amount: Number(editForm.amount) || 0,
    tax: Number(editForm.tax) || 0,
    total: Number(editForm.total) || 0,
    confirm: editForm.confirm
  }

  saving.value = true
  try {
    const result = await FinanceService.updateInvoice(editing.value.id, dto)
    if (result.errors?.length) {
      message.error(result.errors.join('；'))
      return
    }
    if (result.warnings?.length && !editForm.confirm) message.warning(result.warnings.join('；'))
    message.success(editForm.confirm ? '发票已人工确认，进入待报销状态' : '发票已保存')
    editModal.value = false
    await load()
  } catch (e: any) {
    message.error(e?.detail || e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

function revertToReview(invoice: FinanceInvoice) {
  dialog.warning({
    title: '回退到待确认',
    content: `发票「${invoice.number || invoice.id}」会回到待确认状态，需要重新人工确认后才能用于报销。`,
    positiveText: '确认回退',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        const result = await FinanceService.updateInvoice(invoice.id, {
          type: invoice.type,
          number: invoice.number,
          issueDate: invoice.issueDate,
          buyer: invoice.buyer,
          seller: invoice.seller,
          item: invoice.item,
          qty: invoice.qty,
          price: invoice.price,
          unit: invoice.unit,
          amount: invoice.amount,
          tax: invoice.tax,
          total: invoice.total,
          confirm: false
        })
        if (result.errors?.length) {
          message.error(result.errors.join('；'))
          return false
        }
        message.success('发票已回退到待确认')
        await load()
      } catch (e: any) {
        message.error(e?.detail || e?.message || '回退失败')
        return false
      }
    }
  })
}

// ===== 删除 =====
function batchRemoveInvoices() {
  const rows = items.value.filter(item => checkedKeys.value.includes(item.id))
  if (!rows.length) {
    message.warning('请先选择发票')
    return
  }

  dialog.warning({
    title: '批量删除发票',
    content: `确定删除选中的 ${rows.length} 张发票吗？删除为软删除，原件仍保留在服务器。`,
    positiveText: '删除',
    negativeText: '取消',
    onPositiveClick: async () => {
      let success = 0
      let failed = 0
      for (const invoice of rows) {
        try {
          await FinanceService.deleteInvoice(invoice.id)
          success++
        } catch {
          failed++
        }
      }
      checkedKeys.value = []
      await load()
      if (failed > 0) message.warning(`已删除 ${success} 张，${failed} 张删除失败`)
      else message.success(`已删除 ${success} 张发票`)
    }
  })
}

function remove(invoice: FinanceInvoice) {
  dialog.warning({
    title: '删除发票',
    content: `确定删除发票「${invoice.number || invoice.id}」吗？删除为软删除，原件仍保留在服务器。`,
    positiveText: '删除',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await FinanceService.deleteInvoice(invoice.id)
        message.success('发票已删除')
        await load()
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    }
  })
}

onMounted(() => {
  
  layoutStore.setPageHeader('财务管理')
  layoutStore.setShowPageActions(false)
  void load()
})

onBeforeUnmount(() => layoutStore.clearPageHeader())
</script>

<template>
  <div class="finance-page">
    <FinanceTabs/>

    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between mb-4">
      <div>
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">发票库</h2>
        
      </div>
      <div class="flex items-center gap-2">
        <button class="finance-btn primary" :disabled="uploading" @click="triggerUpload">
          <Icon icon="upload" class="w-4 h-4"/>{{ uploading ? uploadText : '上传发票' }}
        </button>
        <input ref="uploadInput" type="file" multiple class="hidden" :accept="accept" @change="onFileChange"/>
      </div>
    </div>

    <div v-if="meta && !meta.ocrConfigured" class="finance-alert info mb-4">
      <Icon icon="circle-alert" class="w-5 h-5 shrink-0"/>
      <span>当前未配置 OCR 服务：图片与扫描版 PDF 会识别失败并转手工录入；docx 与带文本层的 PDF 仍可自动解析。可在服务器设置 Finance__OcrEndpoint 接入 OCR。</span>
    </div>

    <div class="finance-card mb-4">
      <div class="flex flex-col lg:flex-row lg:items-center gap-3">
        <div class="flex gap-1 rounded-full bg-black/5 dark:bg-white/10 p-1 w-fit overflow-x-auto">
          <button v-for="tab in statusTabs" :key="tab.value"
                  class="px-4 py-1.5 rounded-full text-sm whitespace-nowrap transition-all"
                  :class="statusFilter === tab.value
                    ? 'bg-white dark:bg-[#2C2C2E] text-gray-900 dark:text-white shadow-sm font-medium'
                    : 'text-gray-500 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200'"
                  @click="setStatus(tab.value)">{{ tab.label }}</button>
        </div>
        <div class="flex-1 relative min-w-[180px]">
          <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
            <Icon icon="search" class="text-gray-400 w-4 h-4"/>
          </div>
          <input v-model="keyword" type="text" placeholder="搜索发票号、销售方、项目..."
                 class="finance-input pl-9 pr-9" @keyup.enter="page = 1; load()"/>
          <button v-if="keyword" type="button" title="清空搜索"
                  class="absolute right-2 top-1/2 -translate-y-1/2 p-1 rounded-full text-gray-400 hover:text-gray-700 dark:hover:text-gray-200"
                  @click="keyword = ''; page = 1; load()">
            <Icon icon="circle-x" class="w-4 h-4"/>
          </button>
        </div>
        <button class="finance-btn" @click="page = 1; load()"><Icon icon="search" class="w-4 h-4"/>搜索</button>
        <button class="finance-btn" @click="page = 1; load()"><Icon icon="refresh-cw" class="w-4 h-4"/>刷新</button>
      </div>
    </div>

    <div v-if="checkedKeys.length" class="finance-card mb-4 flex items-center justify-between">
      <span class="text-sm text-gray-600 dark:text-gray-300">已选择 {{ checkedKeys.length }} 张发票</span>
      <div class="flex gap-2">
        <button class="finance-btn" @click="checkedKeys = []">取消选择</button>
        <button class="finance-btn danger" @click="batchRemoveInvoices">批量删除</button>
      </div>
    </div>

    <div class="finance-card">
      <div v-if="loadError" class="finance-empty">
        <Icon icon="circle-alert" class="w-8 h-8 text-red-500"/>
        <p>{{ loadError }}</p>
        <button class="finance-btn primary" @click="load">重新加载</button>
      </div>
      <div v-else class="overflow-x-auto">
        <n-data-table :columns="columns" :data="items" :loading="loading" :pagination="pagination"
                      :bordered="false" :row-key="(row: FinanceInvoice) => row.id"
                      :row-props="invoiceRowProps"
                      v-model:checked-row-keys="checkedKeys" remote
                      class="finance-table min-w-[1055px]">
          <template #empty>
            <div class="finance-empty">
              <Icon icon="file-text" class="w-10 h-10"/>
              <span>还没有发票，上传后会自动识别</span>
              <button class="finance-btn primary" @click="triggerUpload">上传第一张发票</button>
            </div>
          </template>
        </n-data-table>
      </div>
    </div>

    <!-- 编辑 / 校对发票 -->
    <n-modal v-model:show="editModal" preset="card" style="max-width: 760px" :bordered="false"
             :title="editReadonly ? '发票详情' : editForm.confirm ? '人工确认发票' : '人工录入发票'">
      <fieldset v-if="editing" :disabled="editReadonly" class="space-y-4 border-0 p-0 m-0">
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label class="finance-label">发票类型</label>
            <input v-model="editForm.type" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'type') }" :title="confidenceTitle(editing, 'type')"/>
          </div>
          <div>
            <label class="finance-label">开票日期</label>
            <input v-model="editForm.issueDate" type="date" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'issueDate') }" :title="confidenceTitle(editing, 'issueDate')"/>
          </div>
          <div>
            <label class="finance-label">发票号码</label>
            <input v-model="editForm.number" maxlength="20" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'number') }" :title="confidenceTitle(editing, 'number')"/>
          </div>
          <div>
            <label class="finance-label">购买方</label>
            <input v-model="editForm.buyer" maxlength="100" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'buyer') }" :title="confidenceTitle(editing, 'buyer')"/>
          </div>
          <div>
            <label class="finance-label">销售方</label>
            <input v-model="editForm.seller" maxlength="100" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'seller') }" :title="confidenceTitle(editing, 'seller')"/>
          </div>
          <div class="sm:col-span-2">
            <label class="finance-label">项目名称</label>
            <input v-model="editForm.item" maxlength="100" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'item') }" :title="confidenceTitle(editing, 'item')"/>
          </div>
          <div class="grid grid-cols-3 gap-2 sm:col-span-2">
            <div>
              <label class="finance-label">数量</label>
              <input v-model.number="editForm.qty" type="number" step="0.0001" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'qty') }"/>
            </div>
            <div>
              <label class="finance-label">单价（元）</label>
              <input v-model.number="editForm.price" type="number" step="0.01" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'price') }"/>
            </div>
            <div>
              <label class="finance-label">单位</label>
              <input v-model="editForm.unit" maxlength="10" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'unit') }"/>
            </div>
          </div>
          <div>
            <label class="finance-label">金额（不含税）</label>
            <input v-model.number="editForm.amount" type="number" step="0.01" min="0" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'amount') }"/>
          </div>
          <div>
            <label class="finance-label">税额</label>
            <input v-model.number="editForm.tax" type="number" step="0.01" min="0" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'tax') }"/>
          </div>
          <div class="sm:col-span-2">
            <label class="finance-label">价税合计</label>
            <input v-model.number="editForm.total" type="number" step="0.01" min="0" class="finance-input" :class="{ 'finance-low-confidence': isLow(editing, 'total') }"/>
          </div>
        </div>

        <div v-if="editing.recognitionError" class="finance-alert danger">
          <Icon icon="triangle-alert" class="w-5 h-5 shrink-0"/>
          <span>{{ editing.recognitionError }}</span>
        </div>

        <label class="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-200 select-none">
          <input v-model="editForm.confirm" type="checkbox" class="rounded"/>
          人工确认后进入“待报销”状态（可用于生成表单与报销）
        </label>
        <p class="finance-muted">标黄字段表示识别置信度低于 {{ ((meta?.lowConfidenceThreshold ?? 0.8) * 100).toFixed(0) }}%，请人工核对。</p>
      </fieldset>
      <template #footer>
        <div class="flex justify-between items-center gap-2">
          <span class="finance-muted">上传时间：{{ editing ? formatTime(editing.createdAt) : '' }}</span>
          <div class="flex gap-2">
            <button class="finance-btn" @click="editModal = false">{{ editReadonly ? '关闭' : '取消' }}</button>
            <button v-if="!editReadonly" class="finance-btn primary" :disabled="saving" @click="saveEdit">{{ saving ? '保存中…' : '保存' }}</button>
          </div>
        </div>
      </template>
    </n-modal>
  </div>
</template>
