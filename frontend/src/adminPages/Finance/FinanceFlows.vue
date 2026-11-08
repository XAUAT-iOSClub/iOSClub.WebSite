<script setup lang="ts">
import { computed, h, onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { NDataTable } from 'naive-ui'
import type { DataTableColumns } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import FinanceTabs from './components/FinanceTabs.vue'
import { FinanceService } from '../../services/FinanceService'
import { useLayoutStore } from '../../stores/LayoutStore'
import type { FinanceFlow, FinanceFundSummary, FinanceMeta } from '../../models'

const layoutStore = useLayoutStore()

const meta = ref<FinanceMeta | null>(null)
const summary = ref<FinanceFundSummary | null>(null)
const items = ref<FinanceFlow[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)
const loadError = ref('')
const yearFilter = ref('')
const typeFilter = ref('')
const keyword = ref('')
const from = ref('')
const to = ref('')

const formatMoney = (value: number | undefined | null) =>
    `¥${Number(value ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

const formatTime = (value?: string | null) => {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

const flowColumns: DataTableColumns<FinanceFlow> = [
  { title: '时间', key: 'createdAt', width: 150, render: row => formatTime(row.createdAt) },
  {
    title: '材料 / 活动', key: 'materialId', minWidth: 190,
    render: row => h(RouterLink, {
      to: `/Centre/Finance/Materials/${row.materialId}`,
      class: 'flex flex-col hover:text-[#007AFF]'
    }, () => [
      h('span', { class: 'font-medium text-gray-900 dark:text-gray-100' }, row.activityName || '未命名活动'),
      h('span', { class: 'text-[11px] text-gray-400' }, row.materialNo || row.materialId)
    ])
  },
  { title: '学年', key: 'academicYear', width: 100 },
  {
    title: '类型', key: 'type', width: 90,
    render: row => h('span', {
      class: `finance-tag ${row.type === 'rollback' ? 'deleted' : 'pending'}`,
      title: row.type === 'rollback'
          ? '回滚：撤销报销时把原扣减金额退回，同时原扣减流水标记为已作废'
          : '扣减：确认报销时从年度经费中扣除的金额'
    }, row.typeLabel)
  },
  {
    title: '金额', key: 'amount', width: 130, align: 'right',
    render: row => h('span', { class: row.type === 'rollback' ? 'text-emerald-600' : 'text-gray-900 dark:text-gray-100' },
        `${row.type === 'rollback' ? '+' : '-'}${formatMoney(row.amount)}`)
  },
  {
    title: '流水状态', key: 'status', width: 90,
    render: row => h('span', { class: `finance-tag ${row.status === 'voided' ? 'deleted' : 'reimbursed'}` },
        row.status === 'voided' ? '已作废' : '有效')
  },
  { title: '操作人', key: 'operatorName', width: 110, render: row => row.operatorName || '—' },
  { title: '原因 / 说明', key: 'reason', minWidth: 140, render: row => row.reason || '—' }
]

const pagination = computed(() => ({
  page: page.value,
  pageSize: pageSize.value,
  itemCount: total.value,
  showSizePicker: true,
  pageSizes: [10, 20, 50],
  prefix: (info: { itemCount?: number }) => `共 ${info.itemCount ?? 0} 条流水`,
  onUpdatePage: (value: number) => { page.value = value; void load() },
  onUpdatePageSize: (value: number) => { pageSize.value = value; page.value = 1; void load() }
}))

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    if (!meta.value) {
      meta.value = await FinanceService.meta()
      if (!yearFilter.value) yearFilter.value = meta.value.currentAcademicYear
    }
    const [data, fund] = await Promise.all([
      FinanceService.flows({
        academicYear: yearFilter.value || undefined,
        type: typeFilter.value || undefined,
        keyword: keyword.value.trim() || undefined,
        from: from.value ? new Date(`${from.value}T00:00:00`).toISOString() : undefined,
        to: to.value ? new Date(`${to.value}T23:59:59`).toISOString() : undefined,
        page: page.value,
        pageSize: pageSize.value
      }),
      FinanceService.summary(yearFilter.value || undefined)
    ])
    items.value = data.items
    total.value = data.total
    summary.value = fund
  } catch (e: any) {
    loadError.value = e?.message || '经费流水加载失败'
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  typeFilter.value = ''
  keyword.value = ''
  from.value = ''
  to.value = ''
  page.value = 1
  void load()
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
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">经费流水</h2>
        
      </div>
      <div class="flex gap-2">
        <select v-model="yearFilter" class="finance-select w-auto min-w-[140px] shrink-0" @change="page = 1; load()">
          <option v-for="year in (meta?.years ?? [])" :key="year" :value="year">{{ year }} 学年</option>
        </select>
        <button class="finance-btn" @click="load"><Icon icon="refresh-cw" class="w-4 h-4"/>刷新</button>
      </div>
    </div>

    <div v-if="summary" class="finance-alert info mb-4">
      <Icon icon="circle-alert" class="w-5 h-5 shrink-0"/>
      <span>
        <b>扣减</b>：确认报销时从年度经费中扣掉的金额。
        <b>回滚</b>：撤销报销时退回原扣减金额，同时原扣减流水会被标记为“已作废”。
      </span>
    </div>

    <div v-if="summary" class="finance-kpi-grid mb-4">
      <div class="finance-kpi">
        <div class="finance-kpi-label">年度经费</div>
        <div class="finance-kpi-value">{{ formatMoney(summary.budgetAmount) }}</div>
      </div>
      <div class="finance-kpi">
        <div class="finance-kpi-label">已扣减</div>
        <div class="finance-kpi-value">{{ formatMoney(summary.usedAmount) }}</div>
      </div>
      <div class="finance-kpi">
        <div class="finance-kpi-label">在途金额</div>
        <div class="finance-kpi-value text-blue-600 dark:text-blue-400">{{ formatMoney(summary.inTransitAmount) }}</div>
      </div>
      <div class="finance-kpi">
        <div class="finance-kpi-label">剩余经费</div>
        <div class="finance-kpi-value" :class="summary.warningLevel === 'danger' ? 'danger' : summary.warningLevel === 'warning' ? 'warning' : ''">
          {{ formatMoney(summary.remainingAmount) }}
        </div>
      </div>
    </div>

    <div class="finance-card mb-4">
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
        <div>
          <label class="finance-label">类型</label>
          <select v-model="typeFilter" class="finance-select" @change="page = 1; load()">
            <option value="">全部</option>
            <option value="deduct">扣减</option>
            <option value="rollback">回滚</option>
          </select>
        </div>
        <div>
          <label class="finance-label">活动 / 材料编号</label>
          <input v-model="keyword" class="finance-input" placeholder="搜索..." @keyup.enter="page = 1; load()"/>
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
      <n-data-table v-else :columns="flowColumns" :data="items" :loading="loading" :pagination="pagination"
                    :bordered="false" :row-key="(row: FinanceFlow) => row.id" remote class="finance-table">
        <template #empty>
          <div class="finance-empty">
            <Icon icon="refresh-cw" class="w-10 h-10"/>
            <span>当前条件下没有经费流水</span>
          </div>
        </template>
      </n-data-table>
    </div>
  </div>
</template>
