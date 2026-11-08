<script setup lang="ts">
import { computed, h, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NDataTable, NModal, useDialog, useMessage } from 'naive-ui'
import type { DataTableColumns } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import FinanceTabs from './components/FinanceTabs.vue'
import { FinanceService } from '../../services/FinanceService'
import { useLayoutStore } from '../../stores/LayoutStore'
import type {
  FinanceInvoice,
  FinanceLog,
  FinanceMaterialDetail,
  FinanceMaterialUpsertDto,
  FinanceMeta
} from '../../models'

const route = useRoute()
const router = useRouter()
const message = useMessage()
const dialog = useDialog()
const layoutStore = useLayoutStore()

const material = ref<FinanceMaterialDetail | null>(null)
const meta = ref<FinanceMeta | null>(null)
const logs = ref<FinanceLog[]>([])
const loading = ref(true)
const loadError = ref('')
const saving = ref(false)
const generating = ref<'' | 'apply' | 'reimburse'>('')
const confirming = ref(false)
const revoking = ref(false)

const showConfirm = ref(false)
const showRevoke = ref(false)
const revokeReason = ref('')

const showPool = ref(false)
const poolLoading = ref(false)
const poolItems = ref<FinanceInvoice[]>([])
const poolChecked = ref<Array<string | number>>([])
const poolKeyword = ref('')

const form = reactive({
  activityName: '',
  activityDate: '',
  activityType: '',
  location: '',
  headcount: null as number | null,
  owner: '',
  phone: '',
  summary: '',
  activityBudget: 0,
  remark: ''
})

const materialId = computed(() => String(route.params.id ?? ''))
const editable = computed(() => material.value?.status === 'pending')
const canGenerate = computed(() =>
    editable.value && (material.value?.confirmedInvoiceCount ?? 0) > 0 && (material.value?.totalAmount ?? 0) > 0)
const canConfirm = computed(() =>
    canGenerate.value &&
    (material.value?.unconfirmedAmount ?? 0) <= 0 &&
    (material.value?.activityName?.trim().length ?? 0) >= 2)

const formRows = computed(() => [
  { kind: 'reimburse' as const, label: '经费报销单', has: material.value?.hasReimburseForm ?? false },
  { kind: 'apply' as const, label: '社团经费申请表', has: material.value?.hasApplyForm ?? false }
])

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

const statusClass = (status: string) =>
    status === 'reimbursed' ? 'reimbursed' : status === 'deleted' ? 'deleted' : 'pending'

function syncForm() {
  if (!material.value) return
  form.activityName = material.value.activityName ?? ''
  form.activityDate = material.value.activityDate ? formatDate(material.value.activityDate) : ''
  form.activityType = material.value.activityType ?? ''
  form.location = material.value.location ?? ''
  form.headcount = material.value.headcount ?? null
  form.owner = material.value.owner ?? ''
  form.phone = material.value.phone ?? ''
  form.summary = material.value.summary ?? ''
  form.activityBudget = material.value.activityBudget ?? 0
  form.remark = material.value.remark ?? ''
}

async function load() {
  if (!materialId.value) return
  loading.value = true
  loadError.value = ''
  try {
    if (!meta.value) meta.value = await FinanceService.meta()
    const [detail, logList] = await Promise.all([
      FinanceService.material(materialId.value),
      FinanceService.materialLogs(materialId.value)
    ])
    material.value = detail
    logs.value = logList
    syncForm()
  } catch (e: any) {
    loadError.value = e?.message || '材料加载失败'
  } finally {
    loading.value = false
  }
}

async function loadLogs() {
  try {
    logs.value = await FinanceService.materialLogs(materialId.value)
  } catch {
    // 日志失败不影响主流程
  }
}

function buildDto(): FinanceMaterialUpsertDto {
  return {
    activityName: form.activityName.trim() || null,
    activityDate: form.activityDate ? new Date(`${form.activityDate}T00:00:00`).toISOString() : null,
    activityType: form.activityType || null,
    location: form.location.trim() || null,
    headcount: form.headcount === null || Number.isNaN(form.headcount) ? null : Number(form.headcount),
    owner: form.owner.trim() || null,
    phone: form.phone.trim() || null,
    summary: form.summary.trim() || null,
    activityBudget: Number(form.activityBudget) || 0,
    remark: form.remark.trim() || null
  }
}

async function saveInfo() {
  if (!material.value) return
  if (form.activityName.trim() && (form.activityName.trim().length < 2 || form.activityName.trim().length > 50)) {
    message.warning('活动名称需为 2-50 个字符')
    return
  }
  if (!Number.isFinite(Number(form.activityBudget)) || Number(form.activityBudget) < 0) {
    message.warning('活动经费需为不小于 0 的数字')
    return
  }

  saving.value = true
  try {
    material.value = await FinanceService.updateMaterial(material.value.id, buildDto())
    syncForm()
    message.success('材料信息已保存')
    await loadLogs()
  } catch (e: any) {
    message.error(e?.detail || e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

const invoiceColumns: DataTableColumns<FinanceInvoice> = [
  {
    title: '发票号', key: 'number', width: 150,
    render: row => h('span', { class: 'font-medium text-gray-900 dark:text-gray-100' }, row.number || '—')
  },
  { title: '开票日期', key: 'issueDate', width: 105, render: row => formatDate(row.issueDate) },
  { title: '销售方', key: 'seller', minWidth: 150, render: row => row.seller || '—' },
  { title: '项目', key: 'item', minWidth: 120, render: row => row.item || '—' },
  { title: '数量', key: 'qty', width: 70, render: row => row.qty ?? '—' },
  { title: '单价', key: 'price', width: 95, align: 'right', render: row => row.price == null ? '—' : formatMoney(row.price) },
  { title: '金额', key: 'total', width: 110, align: 'right', render: row => formatMoney(row.total) },
  {
    title: '状态', key: 'status', width: 95,
    render: row => h('span', { class: `finance-tag ${row.status === 'failed' ? 'failed' : 'pending'}` },
        row.status === 'failed' ? '识别失败' : row.confirmed ? '待报销' : '待确认')
  },
  {
    title: '操作', key: 'actions', width: 80,
    render: row => editable.value
        ? h('button', {
            class: 'text-xs px-2.5 py-1 rounded-lg bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/20 dark:text-red-300',
            onClick: () => removeInvoice(row)
          }, '移除')
        : h('span', { class: 'text-xs text-gray-400' }, '—')
  }
]

const poolColumns: DataTableColumns<FinanceInvoice> = [
  { type: 'selection', width: 40 },
  { title: '发票号', key: 'number', width: 140, render: row => row.number || '未识别' },
  { title: '销售方', key: 'seller', minWidth: 160, render: row => row.seller || '—' },
  { title: '项目', key: 'item', minWidth: 120, render: row => row.item || '—' },
  { title: '价税合计', key: 'total', width: 120, align: 'right', render: row => formatMoney(row.total) },
  { title: '状态', key: 'statusLabel', width: 90, render: row => row.status === 'failed' ? '识别失败' : row.confirmed ? '待报销' : '待确认' }
]

function removeInvoice(invoice: FinanceInvoice) {
  dialog.warning({
    title: '移出材料',
    content: `将发票「${invoice.number || invoice.id}」移出当前材料？移出后发票回到待报销发票池，材料合计同步减少。`,
    positiveText: '移出材料',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        material.value = await FinanceService.assignInvoices(materialId.value, [invoice.id], 'remove')
        message.success('已移出材料')
        await loadLogs()
      } catch (e: any) {
        message.error(e?.message || '解除失败')
      }
    }
  })
}

async function openPool() {
  showPool.value = true
  poolChecked.value = []
  poolKeyword.value = ''
  await loadPool()
}

async function loadPool() {
  poolLoading.value = true
  try {
    poolItems.value = await FinanceService.assignableInvoices(poolKeyword.value.trim(), 100)
  } catch (e: any) {
    message.error(e?.message || '待报销发票加载失败')
  } finally {
    poolLoading.value = false
  }
}

async function addSelectedInvoices() {
  if (!poolChecked.value.length) {
    message.warning('请选择待报销发票')
    return
  }
  try {
    material.value = await FinanceService.assignInvoices(materialId.value, poolChecked.value.map(String), 'assign')
    showPool.value = false
    message.success('已加入材料')
    await loadLogs()
  } catch (e: any) {
    message.error(e?.message || '加入材料失败')
  }
}

function doGenerate(kind: 'apply' | 'reimburse') {
  if (!canGenerate.value) {
    message.warning('至少需要 1 张待报销状态的发票，且合计大于 0')
    return
  }

  const run = async () => {
    generating.value = kind
    try {
      material.value = await FinanceService.generate(materialId.value, kind)
      message.success(`${kind === 'apply' ? '经费申请表' : '经费报销单'}已生成`)
      await loadLogs()
    } catch (e: any) {
      message.error(e?.message || '生成失败')
    } finally {
      generating.value = ''
    }
  }

  const hasForm = kind === 'apply' ? material.value?.hasApplyForm : material.value?.hasReimburseForm
  if (hasForm) {
    dialog.warning({
      title: '重新生成表单',
      content: '重新生成会覆盖当前下载文件，是否继续？',
      positiveText: '重新生成',
      negativeText: '取消',
      onPositiveClick: run
    })
  } else {
    void run()
  }
}

async function download(kind: 'apply' | 'reimburse', format: 'docx' | 'pdf') {
  try {
    await FinanceService.exportMaterial(materialId.value, kind, format)
    message.success('已开始下载')
  } catch (e: any) {
    message.error(e?.message || '导出失败')
  }
}

function confirmReimbursement() {
  if (!canConfirm.value) {
    message.warning('确认前需填写活动名称、所有发票均为待报销状态且合计大于 0')
    return
  }
  showConfirm.value = true
}

async function doConfirm() {
  confirming.value = true
  try {
    material.value = await FinanceService.confirm(materialId.value)
    showConfirm.value = false
    message.success(`已确认报销，本次扣减 ${formatMoney(material.value.estimatedDeduction)}`)
    await loadLogs()
  } catch (e: any) {
    message.error(e?.message || '确认失败')
  } finally {
    confirming.value = false
  }
}

function openRevoke() {
  revokeReason.value = ''
  showRevoke.value = true
}

async function doRevoke() {
  const reason = revokeReason.value.trim()
  if (reason.length < 2) {
    message.warning('请填写至少 2 个字符的撤销原因')
    return
  }
  revoking.value = true
  try {
    material.value = await FinanceService.revoke(materialId.value, reason)
    showRevoke.value = false
    message.success('已撤销报销，材料回到待报销')
    await loadLogs()
  } catch (e: any) {
    message.error(e?.message || '撤销失败')
  } finally {
    revoking.value = false
  }
}

function removeMaterial() {
  if (!material.value) return
  dialog.warning({
    title: '删除报销材料',
    content: `确定删除材料 ${material.value.id} 吗？删除为软删除，30 天内可在“已删除”列表恢复。`,
    positiveText: '删除',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await FinanceService.deleteMaterial(materialId.value)
        message.success('材料已删除')
        await router.push('/Centre/Finance/Materials')
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    }
  })
}

async function restoreMaterial() {
  try {
    material.value = await FinanceService.restoreMaterial(materialId.value)
    syncForm()
    message.success('材料已恢复')
    await loadLogs()
  } catch (e: any) {
    message.error(e?.message || '恢复失败')
  }
}

watch(() => route.params.id, () => { void load() })

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

    <div v-if="loading" class="space-y-4">
      <div class="finance-card h-24 animate-pulse"></div>
      <div class="finance-card h-64 animate-pulse"></div>
    </div>

    <div v-else-if="loadError" class="finance-card">
      <div class="finance-empty">
        <Icon icon="circle-alert" class="w-8 h-8 text-red-500"/>
        <p>{{ loadError }}</p>
        <div class="flex gap-2">
          <button class="finance-btn" @click="router.push('/Centre/Finance/Materials')">返回列表</button>
          <button class="finance-btn primary" @click="load">重新加载</button>
        </div>
      </div>
    </div>

    <template v-else-if="material">
      <div class="flex flex-wrap items-center gap-3 mb-4">
        <button class="finance-btn" @click="router.push('/Centre/Finance/Materials')">
          <Icon icon="arrow-left" class="w-4 h-4"/>返回
        </button>
        <div class="flex-1 min-w-[200px]">
          <div class="flex items-center gap-2 flex-wrap">
            <h2 class="text-xl font-semibold text-gray-900 dark:text-white">{{ material.activityName || '未命名活动' }}</h2>
            <span class="finance-tag" :class="statusClass(material.status)">{{ material.statusLabel }}</span>
          </div>
          <p class="finance-muted mt-1">{{ material.id }} · {{ material.academicYear }} 学年 · 创建于 {{ formatTime(material.createdAt) }}</p>
        </div>
        <button v-if="material.status === 'deleted'" class="finance-btn success" @click="restoreMaterial">
          <Icon icon="rotate-ccw" class="w-4 h-4"/>恢复材料
        </button>
        <template v-else-if="material.status === 'pending'">
          <button class="finance-btn success" :disabled="!canConfirm"
                  :title="canConfirm ? '确认后材料锁定并写入扣减流水' : '需填写活动名称、全部发票均为待报销状态且合计大于 0'"
                  @click="confirmReimbursement">
            <Icon icon="badge-check" class="w-4 h-4"/>确认已报销
          </button>
          <button class="finance-btn danger" @click="removeMaterial">
            <Icon icon="trash" class="w-4 h-4"/>删除
          </button>
        </template>
      </div>

      <div v-if="material.status === 'reimbursed'" class="finance-alert info mb-4">
        <Icon icon="badge-check" class="w-5 h-5 shrink-0"/>
        <span>材料已报销并锁定，字段不可编辑；如需修改请先撤销报销。确认时间：{{ formatTime(material.confirmedAt) }}</span>
      </div>
      <div v-if="material.status === 'deleted'" class="finance-alert danger mb-4">
        <Icon icon="triangle-alert" class="w-5 h-5 shrink-0"/>
        <span>材料已删除（可 30 天内恢复），不会参与任何经费统计。</span>
      </div>

      <div class="finance-kpi-grid mb-4">
        <div class="finance-kpi">
          <div class="finance-kpi-label">合计（待报销发票）</div>
          <div class="finance-kpi-value">{{ formatMoney(material.totalAmount) }}</div>
        </div>
        <div class="finance-kpi">
          <div class="finance-kpi-label">发票</div>
          <div class="finance-kpi-value">{{ material.confirmedInvoiceCount }}<span class="text-sm font-normal text-gray-400"> / {{ material.invoiceCount }}</span></div>
          <div class="finance-muted mt-1">待报销 / 全部</div>
        </div>
        <div class="finance-kpi">
          <div class="finance-kpi-label">活动经费</div>
          <div class="finance-kpi-value">{{ formatMoney(material.activityBudget) }}</div>
        </div>
        <div class="finance-kpi">
          <div class="finance-kpi-label">{{ material.status === 'reimbursed' ? '已扣减' : '预计扣减' }}</div>
          <div class="finance-kpi-value text-[#007AFF]">{{ formatMoney(material.estimatedDeduction) }}</div>
          <div class="finance-muted mt-1">max(0, 合计 − 活动经费)</div>
        </div>
      </div>

      <div v-if="material.unconfirmedAmount > 0" class="finance-alert warning mb-4">
        <Icon icon="triangle-alert" class="w-5 h-5 shrink-0"/>
        <span>还有 {{ formatMoney(material.unconfirmedAmount) }} 的发票还未人工确认。确认报销前需逐张人工确认。</span>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="xl:col-span-2 space-y-4">
          <!-- 活动信息 -->
          <div class="finance-card">
            <div class="flex items-center justify-between mb-4">
              <span class="finance-card-title">活动信息</span>
              <button v-if="editable" class="finance-btn primary" :disabled="saving" @click="saveInfo">
                {{ saving ? '保存中…' : '保存信息' }}
              </button>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div class="sm:col-span-2">
                <label class="finance-label">活动名称（确认报销前必填，2-50 字）</label>
                <input v-model="form.activityName" maxlength="50" class="finance-input" :disabled="!editable"/>
              </div>
              <div>
                <label class="finance-label">活动时间</label>
                <input v-model="form.activityDate" type="date" class="finance-input" :disabled="!editable"/>
              </div>
              <div>
                <label class="finance-label">活动性质</label>
                <select v-model="form.activityType" class="finance-select" :disabled="!editable">
                  <option value="">请选择</option>
                  <option v-for="type in (meta?.activityTypes ?? [])" :key="type" :value="type">{{ type }}</option>
                </select>
              </div>
              <div>
                <label class="finance-label">活动地点</label>
                <input v-model="form.location" maxlength="50" class="finance-input" :disabled="!editable"/>
              </div>
              <div>
                <label class="finance-label">参加人数</label>
                <input v-model.number="form.headcount" type="number" min="0" class="finance-input" :disabled="!editable"/>
              </div>
              <div>
                <label class="finance-label">负责人</label>
                <input v-model="form.owner" maxlength="20" class="finance-input" :disabled="!editable"/>
              </div>
              <div>
                <label class="finance-label">联系电话</label>
                <input v-model="form.phone" maxlength="14" class="finance-input" :disabled="!editable"/>
              </div>
              <div class="sm:col-span-2">
                <label class="finance-label">活动经费（元）</label>
                <input v-model.number="form.activityBudget" type="number" min="0" step="0.01" class="finance-input" :disabled="!editable"/>
              </div>
              <div class="sm:col-span-2">
                <label class="finance-label">活动简介（≤200 字）</label>
                <textarea v-model="form.summary" rows="2" maxlength="200" class="finance-textarea" :disabled="!editable"></textarea>
              </div>
              <div class="sm:col-span-2">
                <label class="finance-label">备注</label>
                <input v-model="form.remark" maxlength="200" class="finance-input" :disabled="!editable"/>
              </div>
            </div>
          </div>

          <!-- 发票明细 -->
          <div class="finance-card">
            <div class="flex items-center justify-between mb-3">
              <span class="finance-card-title">发票明细</span>
              <button v-if="editable" class="finance-btn primary" @click="openPool">
                <Icon icon="plus" class="w-4 h-4"/>选择待报销发票
              </button>
            </div>
            <n-data-table :columns="invoiceColumns" :data="material.invoices" :bordered="false"
                          :pagination="false" :row-key="(row: FinanceInvoice) => row.id" size="small" class="finance-table">
              <template #empty>
                <div class="finance-empty py-8">
                  <Icon icon="file-text" class="w-8 h-8"/>
                  <span>还没有发票，从待报销发票中选择</span>
                </div>
              </template>
            </n-data-table>
          </div>

          <!-- 表单 -->
          <div class="finance-card">
            <span class="finance-card-title">表单</span>
            <div class="mt-3 space-y-3">
              <div v-for="item in formRows" :key="item.kind"
                   class="flex flex-wrap items-center gap-3 p-3 rounded-xl bg-black/[.02] dark:bg-white/[.04]">
                <div class="flex-1 min-w-[160px]">
                  <div class="text-sm font-medium text-gray-900 dark:text-gray-100">{{ item.label }}</div>
                  <div class="finance-muted">
                    {{ item.has ? '已生成，可在详情内导出' : '尚未生成' }}
                  </div>
                </div>
                <button v-if="editable" class="finance-btn" :disabled="generating !== '' || !canGenerate" @click="doGenerate(item.kind)">
                  {{ generating === item.kind ? '生成中…' : item.has ? '重新生成' : '生成' }}
                </button>
                <button v-if="item.has" class="finance-btn" @click="download(item.kind, 'docx')">
                  <Icon icon="download" class="w-4 h-4"/>docx
                </button>
                <button v-if="item.has" class="finance-btn" :disabled="!meta?.pdfConfigured"
                        :title="meta?.pdfConfigured ? '' : '服务器未配置 PDF 转换器'" @click="download(item.kind, 'pdf')">
                  PDF
                </button>
              </div>
            </div>
            <p class="finance-muted mt-3">生成表单只填充单元格，不改版式；签字、盖章栏留空，线下完成。</p>
          </div>
        </div>

        <div class="space-y-4">
          <!-- 状态操作 -->
          <div class="finance-card" v-if="material.status !== 'pending'">
            <span class="finance-card-title">状态操作</span>
            <div class="mt-3 space-y-2">
              <template v-if="material.status === 'reimbursed'">
                <div class="finance-alert info">
                  <Icon icon="badge-check" class="w-5 h-5 shrink-0"/>
                  <span>已报销，参与经费扣减。要修改字段需先撤销。</span>
                </div>
                <button class="finance-btn danger w-full" @click="openRevoke">
                  <Icon icon="rotate-ccw" class="w-4 h-4"/>撤销报销
                </button>
                <p v-if="material.revokeReason" class="finance-muted">上次撤销原因：{{ material.revokeReason }}</p>
              </template>
              <template v-else>
                <div class="finance-alert danger">
                  <Icon icon="triangle-alert" class="w-5 h-5 shrink-0"/>
                  <span>材料已删除，恢复后才能继续操作。</span>
                </div>
                <button class="finance-btn success w-full" @click="restoreMaterial">恢复材料</button>
              </template>
            </div>
          </div>

          <!-- 变更日志 -->
          <div class="finance-card">
            <div class="flex items-center justify-between mb-3">
              <span class="finance-card-title">变更日志</span>
              <button class="text-xs text-[#007AFF]" @click="loadLogs">刷新</button>
            </div>
            <div v-if="!logs.length" class="finance-empty py-8">
              <Icon icon="scroll-text" class="w-8 h-8"/>
              <span>暂无日志</span>
            </div>
            <div v-else class="space-y-3 max-h-[420px] overflow-y-auto pr-1">
              <div v-for="log in logs" :key="log.id" class="flex gap-2.5">
                <div class="mt-1 w-1.5 h-1.5 rounded-full shrink-0"
                     :class="log.result === 'failure' ? 'bg-red-500' : 'bg-emerald-500'"></div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="text-xs font-medium text-gray-900 dark:text-gray-100">{{ log.action }}</span>
                    <span class="finance-muted">{{ log.operatorName || log.operatorId }}</span>
                  </div>
                  <div class="finance-muted">{{ formatTime(log.createdAt) }}</div>
                  <div v-if="log.message" class="text-[11px] text-gray-500 dark:text-gray-400 mt-0.5 break-words">{{ log.message }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- 确认报销 -->
    <n-modal v-model:show="showConfirm" preset="card" style="max-width: 500px" :bordered="false" title="确认已报销">
      <div v-if="material" class="space-y-3">
        <p class="text-sm text-gray-600 dark:text-gray-300">确认后材料将锁定并写入扣减流水，请再次核对：</p>
        <div class="rounded-xl bg-black/[.03] dark:bg-white/[.06] p-3 text-sm space-y-1.5">
          <div class="flex justify-between"><span class="finance-muted">活动名称</span><span>{{ material.activityName }}</span></div>
          <div class="flex justify-between"><span class="finance-muted">本次开销（待报销发票）</span><span>{{ formatMoney(material.totalAmount) }}</span></div>
          <div class="flex justify-between"><span class="finance-muted">活动经费</span><span>{{ formatMoney(material.activityBudget) }}</span></div>
          <div class="flex justify-between font-semibold"><span>本次扣减</span><span class="text-[#007AFF]">{{ formatMoney(material.estimatedDeduction) }}</span></div>
        </div>
        <p class="finance-muted">扣减 = max(0, 本次开销 − 活动经费)。确认后如需修改，只能撤销报销。</p>
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <button class="finance-btn" @click="showConfirm = false">取消</button>
          <button class="finance-btn success" :disabled="confirming" @click="doConfirm">
            {{ confirming ? '确认中…' : '确认已报销' }}
          </button>
        </div>
      </template>
    </n-modal>

    <!-- 撤销报销 -->
    <n-modal v-model:show="showRevoke" preset="card" style="max-width: 480px" :bordered="false" title="撤销报销">
      <div class="space-y-3">
        <p class="text-sm text-gray-600 dark:text-gray-300">撤销后材料回到待报销，原扣减流水作废并写入回滚流水。请填写原因：</p>
        <textarea v-model="revokeReason" rows="3" maxlength="200" class="finance-textarea"
                  placeholder="2-200 个字符，例如：金额填写有误"></textarea>
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <button class="finance-btn" @click="showRevoke = false">取消</button>
          <button class="finance-btn danger" :disabled="revoking" @click="doRevoke">{{ revoking ? '撤销中…' : '确认撤销' }}</button>
        </div>
      </template>
    </n-modal>

    <!-- 选择待报销发票 -->
    <n-modal v-model:show="showPool" preset="card" style="max-width: 900px" :bordered="false" title="选择待报销发票">
      <div class="space-y-3">
        <div class="flex gap-2">
          <input v-model="poolKeyword" type="text" class="finance-input" placeholder="搜索发票号、销售方、项目..."
                 @keyup.enter="loadPool"/>
          <button class="finance-btn" @click="loadPool"><Icon icon="search" class="w-4 h-4"/>搜索</button>
        </div>
        <n-data-table :columns="poolColumns" :data="poolItems" :loading="poolLoading" :bordered="false"
                      :pagination="false" :row-key="(row: FinanceInvoice) => row.id"
                      v-model:checked-row-keys="poolChecked" size="small" class="finance-table">
          <template #empty>
            <div class="finance-empty py-8">
              <Icon icon="file-text" class="w-8 h-8"/>
              <span>待报销发票池没有发票，请先到发票库上传</span>
            </div>
          </template>
        </n-data-table>
      </div>
      <template #footer>
        <div class="flex items-center justify-between">
          <span class="finance-muted">已选择 {{ poolChecked.length }} 张</span>
          <div class="flex gap-2">
            <button class="finance-btn" @click="showPool = false">取消</button>
            <button class="finance-btn primary" :disabled="!poolChecked.length" @click="addSelectedInvoices">加入材料</button>
          </div>
        </div>
      </template>
    </n-modal>
  </div>
</template>
