<script setup lang="ts">
import { computed, h, onBeforeUnmount, onMounted, ref } from 'vue'
import { NDataTable, NModal, useDialog, useMessage } from 'naive-ui'
import type { DataTableColumns } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import FinanceTabs from './components/FinanceTabs.vue'
import { FinanceService } from '../../services/FinanceService'
import { useLayoutStore } from '../../stores/LayoutStore'
import type { FinanceFlow, FinanceFundSummary, FinanceMeta } from '../../models'

const message = useMessage()
const dialog = useDialog()
const layoutStore = useLayoutStore()

const meta = ref<FinanceMeta | null>(null)
const summary = ref<FinanceFundSummary | null>(null)
const loading = ref(true)
const loadError = ref('')
const year = ref('')
const showBudget = ref(false)
const saving = ref(false)
const budgetForm = ref({ amount: 0, remark: '' })

const formatMoney = (value: number | undefined | null) =>
    `¥${Number(value ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

const formatTime = (value?: string | null) => {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

const warning = computed(() => summary.value?.warningLevel ?? 'normal')
const usage = computed(() => Math.min(100, Math.max(0, summary.value?.usagePercent ?? 0)))
const remainingClass = computed(() => warning.value === 'danger' ? 'danger' : warning.value === 'warning' ? 'warning' : '')

const yearOptions = computed(() => {
  const years = new Set<string>(meta.value?.years ?? [])
  if (year.value) years.add(year.value)
  if (summary.value?.academicYear) years.add(summary.value.academicYear)
  return Array.from(years).sort((a, b) => b.localeCompare(a)).map(y => ({ label: `${y} 学年`, value: y }))
})

const flowColumns: DataTableColumns<FinanceFlow> = [
  { title: '时间', key: 'createdAt', width: 150, render: row => formatTime(row.createdAt) },
  { title: '材料 / 活动', key: 'activityName', minWidth: 180, render: row => row.activityName || row.materialNo },
  {
    title: '类型', key: 'type', width: 90,
    render: row => h('span', { class: `finance-tag ${row.type === 'rollback' ? 'deleted' : 'pending'}` }, row.typeLabel)
  },
  {
    title: '金额', key: 'amount', width: 130, align: 'right',
    render: row => h('span', { class: row.type === 'rollback' ? 'text-emerald-600' : 'text-gray-900 dark:text-gray-100' },
        `${row.type === 'rollback' ? '+' : '-'}${formatMoney(row.amount)}`)
  },
  { title: '操作人', key: 'operatorName', width: 120, render: row => row.operatorName || '—' }
]

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    if (!meta.value) meta.value = await FinanceService.meta()
    if (!year.value) year.value = meta.value.currentAcademicYear
    summary.value = await FinanceService.summary(year.value)
  } catch (e: any) {
    loadError.value = e?.message || '经费看板加载失败'
  } finally {
    loading.value = false
  }
}

async function changeYear(value: string) {
  year.value = value
  await load()
}

function onYearChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value
  if (value) void changeYear(value)
}

function openBudget() {
  if (!summary.value?.editable) return
  budgetForm.value = { amount: summary.value.budgetAmount, remark: '' }
  showBudget.value = true
}

function saveBudget() {
  const amount = Number(budgetForm.value.amount)
  if (!Number.isFinite(amount) || amount < 0) {
    message.warning('年度经费需为不小于 0 的数字')
    return
  }

  dialog.warning({
    title: '确认修改年度经费',
    content: `将 ${year.value} 学年的年度经费设置为 ${formatMoney(amount)}。修改后会写入操作日志，且不可通过撤销恢复。`,
    positiveText: '确认修改',
    negativeText: '取消',
    onPositiveClick: async () => {
      saving.value = true
      try {
        await FinanceService.updateAccount(year.value, amount, budgetForm.value.remark.trim() || undefined)
        showBudget.value = false
        message.success('年度经费已保存')
        await load()
      } catch (e: any) {
        message.error(e?.message || '保存失败')
      } finally {
        saving.value = false
      }
    }
  })
}

onMounted(() => {
  
  layoutStore.setPageHeader('财务管理')
  layoutStore.setShowPageActions(false)
  load()
})

onBeforeUnmount(() => layoutStore.clearPageHeader())
</script>

<template>
  <div class="finance-page">
    <FinanceTabs/>

    <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between mb-5">
      <div>
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">经费看板</h2>
        
      </div>
      <div class="flex items-center gap-2">
        <select class="finance-select w-auto min-w-[150px] shrink-0" :value="year" @change="onYearChange">
          <option v-for="item in yearOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
        </select>
        <button class="finance-btn" @click="load"><Icon icon="refresh-cw" class="w-4 h-4"/>刷新</button>
        <button v-if="summary?.editable" class="finance-btn primary" @click="openBudget">
          <Icon icon="wallet" class="w-4 h-4"/>设置年度经费
        </button>
      </div>
    </div>

    <div class="finance-alert info mb-4">
      <Icon icon="calendar" class="w-5 h-5 shrink-0"/>
      <span>
        学年按 <b>9 月 1 日</b> 自动切换，历史学年只读、不自动结转；进入新学年后需要重新设置该学年的年度经费。
      </span>
    </div>

    <div v-if="loading" class="finance-kpi-grid">
      <div v-for="i in 4" :key="i" class="finance-kpi animate-pulse h-28"></div>
    </div>

    <div v-else-if="loadError" class="finance-card">
      <div class="finance-empty">
        <Icon icon="circle-alert" class="w-8 h-8 text-red-500"/>
        <p>{{ loadError }}</p>
        <button class="finance-btn primary" @click="load">重新加载</button>
      </div>
    </div>

    <template v-else-if="summary">
      <div v-if="warning !== 'normal'" class="finance-alert mb-4" :class="warning">
        <Icon icon="triangle-alert" class="w-5 h-5 shrink-0 mt-0.5"/>
        <span v-if="warning === 'danger'">剩余经费已为负数，请立即核对经费流水与报销材料。</span>
        <span v-else>剩余经费已低于年度经费的 20%，请控制后续支出。</span>
      </div>

      <div class="finance-kpi-grid">
        <div class="finance-kpi">
          <div class="finance-kpi-label">年度经费</div>
          <div class="finance-kpi-value">{{ formatMoney(summary.budgetAmount) }}</div>
          <div class="finance-muted mt-2">{{ summary.academicYear }} 学年</div>
        </div>
        <div class="finance-kpi">
          <div class="finance-kpi-label">已用金额</div>
          <div class="finance-kpi-value">{{ formatMoney(summary.usedAmount) }}</div>
          <div class="finance-muted mt-2">{{ summary.reimbursedCount }} 份已报销材料</div>
        </div>
        <div class="finance-kpi">
          <div class="finance-kpi-label">在途金额</div>
          <div class="finance-kpi-value text-blue-600 dark:text-blue-400">{{ formatMoney(summary.inTransitAmount) }}</div>
          <div class="finance-muted mt-2">{{ summary.pendingCount }} 份待报销材料，不参与扣减</div>
        </div>
        <div class="finance-kpi">
          <div class="finance-kpi-label">剩余经费</div>
          <div class="finance-kpi-value" :class="remainingClass">{{ formatMoney(summary.remainingAmount) }}</div>
          <div class="finance-muted mt-2">已使用 {{ summary.usagePercent.toFixed(2) }}%</div>
        </div>
      </div>

      <div class="finance-card mt-4">
        <div class="flex items-center justify-between mb-3">
          <span class="finance-card-title">预算执行进度</span>
          <span class="finance-muted">{{ summary.usagePercent.toFixed(2) }}%</span>
        </div>
        <div class="h-2.5 w-full rounded-full bg-black/5 dark:bg-white/10 overflow-hidden">
          <div class="h-full rounded-full transition-all duration-500"
               :class="warning === 'danger' ? 'bg-red-500' : warning === 'warning' ? 'bg-amber-500' : 'bg-[#007AFF]'"
               :style="{ width: `${usage}%` }"></div>
        </div>
        <div v-if="!summary.accountExists" class="finance-alert info mt-4">
          <Icon icon="circle-alert" class="w-5 h-5 shrink-0"/>
          <span>当前学年尚未设置年度经费，经费与扣减会按 0 计算。请点击右上角“设置年度经费”。</span>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-4 mt-4">
        <div class="finance-card lg:col-span-2">
          <div class="flex items-center justify-between mb-3">
            <span class="finance-card-title">最近经费流水</span>
            <router-link to="/Centre/Finance/Flows" class="text-xs text-[#007AFF] hover:underline">查看全部</router-link>
          </div>
          <n-data-table v-if="summary.recentFlows.length" :columns="flowColumns" :data="summary.recentFlows"
                        :bordered="false" :pagination="false" class="finance-table" size="small"/>
          <div v-else class="finance-empty py-10">
            <Icon icon="refresh-cw" class="w-8 h-8"/>
            <span>暂无经费流水，确认报销后会生成扣减记录</span>
          </div>
        </div>

        <div class="finance-card">
          <span class="finance-card-title">快捷入口</span>
          <div class="mt-3 space-y-2">
            <router-link to="/Centre/Finance/Invoices"
                         class="flex items-center gap-3 p-3 rounded-xl hover:bg-blue-50 dark:hover:bg-white/10 transition-colors">
              <span class="w-9 h-9 rounded-xl bg-blue-100 dark:bg-blue-500/20 flex items-center justify-center">
                <Icon icon="file-text" class="w-5 h-5 text-blue-600 dark:text-blue-400"/>
              </span>
              <span class="flex-1">
                <span class="block text-sm font-medium text-gray-900 dark:text-gray-100">发票库</span>
                <span class="finance-muted">发票上传、识别与报销材料</span>
              </span>
              <Icon icon="chevron-left" class="w-4 h-4 text-gray-300 rotate-180"/>
            </router-link>
            <router-link to="/Centre/Finance/Materials"
                         class="flex items-center gap-3 p-3 rounded-xl hover:bg-blue-50 dark:hover:bg-white/10 transition-colors">
              <span class="w-9 h-9 rounded-xl bg-emerald-100 dark:bg-emerald-500/20 flex items-center justify-center">
                <Icon icon="list" class="w-5 h-5 text-emerald-600 dark:text-emerald-400"/>
              </span>
              <span class="flex-1">
                <span class="block text-sm font-medium text-gray-900 dark:text-gray-100">报销材料</span>
                <span class="finance-muted">生成报销单与确认报销</span>
              </span>
              <Icon icon="chevron-left" class="w-4 h-4 text-gray-300 rotate-180"/>
            </router-link>
            <router-link to="/Centre/Finance/Logs"
                         class="flex items-center gap-3 p-3 rounded-xl hover:bg-blue-50 dark:hover:bg-white/10 transition-colors">
              <span class="w-9 h-9 rounded-xl bg-violet-100 dark:bg-violet-500/20 flex items-center justify-center">
                <Icon icon="scroll-text" class="w-5 h-5 text-violet-600 dark:text-violet-400"/>
              </span>
              <span class="flex-1">
                <span class="block text-sm font-medium text-gray-900 dark:text-gray-100">操作日志</span>
                <span class="finance-muted">追溯操作人、时间与变更</span>
              </span>
              <Icon icon="chevron-left" class="w-4 h-4 text-gray-300 rotate-180"/>
            </router-link>
          </div>
        </div>
      </div>
    </template>

    <n-modal v-model:show="showBudget" preset="card" style="max-width: 460px" :bordered="false"
             title="设置年度经费">
      <div class="space-y-4">
        <div>
          <label class="finance-label">学年</label>
          <input class="finance-input" :value="year" disabled/>
        </div>
        <div>
          <label class="finance-label">年度经费（元）</label>
          <input v-model.number="budgetForm.amount" type="number" min="0" step="0.01" class="finance-input"
                 placeholder="0.00"/>
          <p class="finance-muted mt-1">最多两位小数；历史学年只读，不自动结转。</p>
        </div>
        <div>
          <label class="finance-label">备注（选填）</label>
          <textarea v-model="budgetForm.remark" rows="2" maxlength="200" class="finance-textarea"
                    placeholder="如：2026-2027 学年社团经费"></textarea>
        </div>
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <button class="finance-btn" @click="showBudget = false">取消</button>
          <button class="finance-btn primary" :disabled="saving" @click="saveBudget">
            {{ saving ? '保存中…' : '保存' }}
          </button>
        </div>
      </template>
    </n-modal>
  </div>
</template>
