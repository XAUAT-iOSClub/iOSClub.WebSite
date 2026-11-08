<script setup lang="ts">
import { computed, h, onBeforeUnmount, onMounted, ref } from 'vue'
import { NDataTable, NDrawer, NDrawerContent, useMessage } from 'naive-ui'
import type { DataTableColumns } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import FinanceTabs from './components/FinanceTabs.vue'
import { FinanceService } from '../../services/FinanceService'
import { useLayoutStore } from '../../stores/LayoutStore'
import type { FinanceLog } from '../../models'

const message = useMessage()
const layoutStore = useLayoutStore()

const items = ref<FinanceLog[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)
const loadError = ref('')
const exporting = ref(false)

const actionFilter = ref('')
const operatorFilter = ref('')
const targetFilter = ref('')
const resultFilter = ref('')
const from = ref('')
const to = ref('')

const showDetail = ref(false)
const selected = ref<FinanceLog | null>(null)

const actionOptions = [
  { label: '上传发票', value: '上传发票' },
  { label: '识别发票', value: '识别发票' },
  { label: '人工录入发票', value: '校对发票' },
  { label: '人工确认发票', value: '确认发票' },
  { label: '删除发票', value: '删除发票' },
  { label: '新建材料', value: '新建材料' },
  { label: '编辑材料', value: '编辑材料' },
  { label: '加入材料', value: '归集发票' },
  { label: '移出材料', value: '解除归集' },
  { label: '生成表单', value: '生成表单' },
  { label: '确认已报销', value: '确认已报销' },
  { label: '撤销报销', value: '撤销报销' },
  { label: '删除材料', value: '删除材料' },
  { label: '恢复材料', value: '恢复材料' },
  { label: '导出材料', value: '导出材料' },
  { label: '修改年度经费', value: '修改年度经费' },
  { label: '越权访问尝试', value: '越权访问尝试' }
]

const actionLabels: Record<string, string> = {
  '校对发票': '人工录入发票',
  '确认发票': '人工确认发票',
  '归集发票': '加入材料',
  '解除归集': '移出材料',
  '批量导出': '批量导出（历史）'
}

const actionLabel = (action?: string) => action ? (actionLabels[action] || action) : '—'

const targetLabels: Record<string, string> = {
  material: '材料',
  invoice: '发票',
  account: '经费',
  template: '模板',
  flow: '流水'
}

const formatTime = (value?: string | null) => {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}:${String(d.getSeconds()).padStart(2, '0')}`
}

const pretty = (value?: string | null) => {
  if (!value) return '—'
  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}

const logColumns: DataTableColumns<FinanceLog> = [
  { title: '时间', key: 'createdAt', width: 165, render: row => formatTime(row.createdAt) },
  {
    title: '操作人', key: 'operatorName', width: 140,
    render: row => h('div', { class: 'flex flex-col' }, [
      h('span', { class: 'font-medium text-gray-900 dark:text-gray-100' }, row.operatorName || row.operatorId || '—'),
      h('span', { class: 'text-[11px] text-gray-400' }, row.roleLabel || row.role || '')
    ])
  },
  { title: '动作', key: 'action', width: 130, render: row => actionLabel(row.action) },
  {
    title: '对象', key: 'targetId', minWidth: 150,
    render: row => row.targetId
        ? `${targetLabels[row.targetType || ''] || row.targetType || ''} ${row.targetId}`
        : '—'
  },
  {
    title: '结果', key: 'result', width: 80,
    render: row => h('span', { class: `finance-tag ${row.result === 'success' ? 'reimbursed' : 'failed'}` },
        row.result === 'success' ? '成功' : '失败')
  },
  { title: '说明', key: 'message', minWidth: 220, render: row => row.message || '—' }
]

const pagination = computed(() => ({
  page: page.value,
  pageSize: pageSize.value,
  itemCount: total.value,
  showSizePicker: true,
  pageSizes: [20, 50, 100],
  prefix: (info: { itemCount?: number }) => `共 ${info.itemCount ?? 0} 条日志`,
  onUpdatePage: (value: number) => { page.value = value; void load() },
  onUpdatePageSize: (value: number) => { pageSize.value = value; page.value = 1; void load() }
}))

function filters() {
  return {
    action: actionFilter.value || undefined,
    operatorName: operatorFilter.value.trim() || undefined,
    targetId: targetFilter.value.trim() || undefined,
    result: resultFilter.value || undefined,
    from: from.value ? new Date(`${from.value}T00:00:00`).toISOString() : undefined,
    to: to.value ? new Date(`${to.value}T23:59:59`).toISOString() : undefined
  }
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const data = await FinanceService.logs({ ...filters(), page: page.value, pageSize: pageSize.value })
    items.value = data.items
    total.value = data.total
  } catch (e: any) {
    loadError.value = e?.message || '操作日志加载失败'
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  actionFilter.value = ''
  operatorFilter.value = ''
  targetFilter.value = ''
  resultFilter.value = ''
  from.value = ''
  to.value = ''
  page.value = 1
  void load()
}

async function exportCsv() {
  if (exporting.value) return
  exporting.value = true
  try {
    await FinanceService.exportLogs(filters() as Record<string, string | undefined>)
    message.success('日志导出已开始')
  } catch (e: any) {
    message.error(e?.message || '导出失败')
  } finally {
    exporting.value = false
  }
}

function openDetail(row: FinanceLog) {
  selected.value = row
  showDetail.value = true
}

const rowProps = (row: FinanceLog) => ({
  style: 'cursor: pointer',
  onClick: () => openDetail(row)
})

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
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">操作日志</h2>
        
      </div>
      <div class="flex gap-2">
        <button class="finance-btn" @click="load"><Icon icon="refresh-cw" class="w-4 h-4"/>刷新</button>
        <button class="finance-btn primary" :disabled="exporting" @click="exportCsv">
          <Icon icon="download" class="w-4 h-4"/>{{ exporting ? '导出中…' : '导出 CSV' }}
        </button>
      </div>
    </div>

    <div class="finance-card mb-4">
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-7 gap-3">
        <div>
          <label class="finance-label">动作</label>
          <select v-model="actionFilter" class="finance-select" @change="page = 1; load()">
            <option value="">全部</option>
            <option v-for="action in actionOptions" :key="action.value" :value="action.value">{{ action.label }}</option>
          </select>
        </div>
        <div>
          <label class="finance-label">操作人</label>
          <input v-model="operatorFilter" class="finance-input" placeholder="姓名 / 账号" @keyup.enter="page = 1; load()"/>
        </div>
        <div>
          <label class="finance-label">对象编号</label>
          <input v-model="targetFilter" class="finance-input" placeholder="材料 / 发票编号" @keyup.enter="page = 1; load()"/>
        </div>
        <div>
          <label class="finance-label">结果</label>
          <select v-model="resultFilter" class="finance-select" @change="page = 1; load()">
            <option value="">全部</option>
            <option value="success">成功</option>
            <option value="failure">失败</option>
          </select>
        </div>
        <div>
          <label class="finance-label">开始日期</label>
          <input v-model="from" type="date" class="finance-input"/>
        </div>
        <div>
          <label class="finance-label">结束日期</label>
          <input v-model="to" type="date" class="finance-input"/>
        </div>
        <div class="flex items-end gap-2">
          <button class="finance-btn primary" @click="page = 1; load()">查询</button>
          <button class="finance-btn" @click="resetFilters">重置</button>
        </div>
      </div>
    </div>

    <div class="finance-card">
      <div v-if="loadError" class="finance-empty">
        <Icon icon="circle-alert" class="w-8 h-8 text-red-500"/>
        <p>{{ loadError }}</p>
        <button class="finance-btn primary" @click="load">重新加载</button>
      </div>
      <n-data-table v-else :columns="logColumns" :data="items" :loading="loading" :pagination="pagination"
                    :bordered="false" :row-key="(row: FinanceLog) => row.id" :row-props="rowProps"
                    remote class="finance-table">
        <template #empty>
          <div class="finance-empty">
            <Icon icon="scroll-text" class="w-10 h-10"/>
            <span>当前条件下没有操作日志</span>
          </div>
        </template>
      </n-data-table>
    </div>

    <n-drawer v-model:show="showDetail" :width="560" placement="right">
      <n-drawer-content v-if="selected" title="日志详情" closable>
        <div class="space-y-4">
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div><span class="finance-muted block">时间</span>{{ formatTime(selected.createdAt) }}</div>
            <div><span class="finance-muted block">结果</span>{{ selected.result === 'success' ? '成功' : '失败' }}</div>
            <div><span class="finance-muted block">操作人</span>{{ selected.operatorName || '—' }}</div>
            <div><span class="finance-muted block">角色</span>{{ selected.roleLabel || selected.role || '—' }}</div>
            <div><span class="finance-muted block">账号</span>{{ selected.operatorId || '—' }}</div>
            <div class="col-span-2"><span class="finance-muted block">动作</span>{{ actionLabel(selected.action) }}</div>
            <div class="col-span-2"><span class="finance-muted block">对象</span>
              {{ targetLabels[selected.targetType || ''] || selected.targetType || '—' }} {{ selected.targetId || '' }}
            </div>
            <div class="col-span-2"><span class="finance-muted block">说明</span>{{ selected.message || '—' }}</div>
            <div class="col-span-2"><span class="finance-muted block">User-Agent</span>
              <span class="text-xs break-all">{{ selected.userAgent || '—' }}</span>
            </div>
          </div>

          <div>
            <div class="finance-label">变更前</div>
            <pre class="text-xs bg-black/[.04] dark:bg-white/[.06] rounded-xl p-3 overflow-auto max-h-56 whitespace-pre-wrap break-all">{{ pretty(selected.before) }}</pre>
          </div>
          <div>
            <div class="finance-label">变更后</div>
            <pre class="text-xs bg-black/[.04] dark:bg-white/[.06] rounded-xl p-3 overflow-auto max-h-56 whitespace-pre-wrap break-all">{{ pretty(selected.after) }}</pre>
          </div>
        </div>
      </n-drawer-content>
    </n-drawer>
  </div>
</template>
