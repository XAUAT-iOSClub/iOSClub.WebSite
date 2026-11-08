<script setup lang="ts">
import { computed, h, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { NDataTable, NModal, useDialog, useMessage } from 'naive-ui'
import type { DataTableColumns } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import FinanceTabs from './components/FinanceTabs.vue'
import { FinanceService } from '../../services/FinanceService'
import { useLayoutStore } from '../../stores/LayoutStore'
import type { FinanceMaterial, FinanceMaterialUpsertDto, FinanceMeta } from '../../models'

const router = useRouter()
const message = useMessage()
const dialog = useDialog()
const layoutStore = useLayoutStore()

const meta = ref<FinanceMeta | null>(null)
const items = ref<FinanceMaterial[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)
const loadError = ref('')
const statusFilter = ref('')
const keyword = ref('')
const yearFilter = ref('')
const checkedKeys = ref<Array<string | number>>([])

const createModal = ref(false)
const saving = ref(false)
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

const statusTabs = [
  { label: '全部', value: '' },
  { label: '待报销', value: 'pending' },
  { label: '已报销', value: 'reimbursed' },
  { label: '已删除', value: 'deleted' }
]

const yearOptions = computed(() => meta.value?.years ?? [])

const formatMoney = (value: number | undefined | null) =>
    `¥${Number(value ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

const formatDate = (value?: string | null) => {
  if (!value) return '—'
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

const statusClass = (status: string) =>
    status === 'reimbursed' ? 'reimbursed' : status === 'deleted' ? 'deleted' : 'pending'

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

const columns: DataTableColumns<FinanceMaterial> = [
  { type: 'selection', width: 40 },
  {
    title: '材料编号', key: 'id', width: 125,
    render: row => h(RouterLink, {
      to: `/Centre/Finance/Materials/${row.id}`,
      class: 'font-medium text-[#007AFF] hover:underline'
    }, () => row.id)
  },
  {
    title: '活动名称', key: 'activityName', minWidth: 140,
    render: row => h('span', {
      class: 'font-medium text-gray-900 dark:text-gray-100 cursor-pointer hover:text-[#007AFF]',
      onClick: () => goDetail(row)
    }, row.activityName || '未命名活动')
  },
  { title: '活动时间', key: 'activityDate', width: 95, render: row => formatDate(row.activityDate) },
  { title: '发票', key: 'invoiceCount', width: 88, render: row => `${row.confirmedInvoiceCount}/${row.invoiceCount} 待报销` },
  {
    title: '合计', key: 'totalAmount', width: 105, align: 'right',
    render: row => h('span', { class: 'font-medium text-gray-900 dark:text-gray-100' }, formatMoney(row.totalAmount))
  },
  { title: '活动经费', key: 'activityBudget', width: 95, align: 'right', render: row => formatMoney(row.activityBudget) },
  {
    title: '状态', key: 'status', width: 85,
    render: row => chip(row.statusLabel, statusClass(row.status))
  },
  {
    title: '操作', key: 'actions', width: 130,
    render: row => {
      const buttons = [
        actionBtn('详情', 'bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-white/10 dark:text-gray-200', () => goDetail(row))
      ]
      if (row.status === 'deleted') {
        buttons.push(actionBtn('恢复', 'bg-emerald-50 text-emerald-600 hover:bg-emerald-100 dark:bg-emerald-500/20 dark:text-emerald-300', () => restore(row)))
      } else if (row.status !== 'reimbursed') {
        buttons.push(actionBtn('删除', 'bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/20 dark:text-red-300', () => remove(row)))
      }
      return h('div', { class: 'flex flex-wrap items-center' }, buttons)
    }
  }
]

const pagination = computed(() => ({
  page: page.value,
  pageSize: pageSize.value,
  itemCount: total.value,
  showSizePicker: true,
  pageSizes: [10, 20, 50],
  prefix: (info: { itemCount?: number }) => `共 ${info.itemCount ?? 0} 份材料`,
  onUpdatePage: (value: number) => { page.value = value; void load() },
  onUpdatePageSize: (value: number) => { pageSize.value = value; page.value = 1; void load() }
}))

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    if (!meta.value) meta.value = await FinanceService.meta()
    const data = await FinanceService.materials({
      status: statusFilter.value || undefined,
      keyword: keyword.value.trim() || undefined,
      academicYear: yearFilter.value || undefined,
      includeDeleted: statusFilter.value === 'deleted',
      page: page.value,
      pageSize: pageSize.value
    })
    items.value = data.items
    total.value = data.total
    checkedKeys.value = []
  } catch (e: any) {
    loadError.value = e?.message || '材料列表加载失败'
  } finally {
    loading.value = false
  }
}

function setStatus(value: string) {
  statusFilter.value = value
  page.value = 1
  void load()
}

function goDetail(row: FinanceMaterial) {
  void router.push(`/Centre/Finance/Materials/${row.id}`)
}

function materialRowProps(row: FinanceMaterial) {
  return {
    style: 'cursor: pointer',
    onClick: (event: MouseEvent) => {
      const target = event.target as HTMLElement | null
      if (target?.closest('button, a, .n-checkbox, .n-checkbox-box')) return
      goDetail(row)
    }
  }
}

function openCreate() {
  const today = new Date()
  const iso = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`
  Object.assign(form, {
    activityName: '',
    activityDate: iso,
    activityType: '',
    location: '',
    headcount: null,
    owner: '',
    phone: '',
    summary: '',
    activityBudget: 0,
    remark: ''
  })
  createModal.value = true
}

async function submitCreate() {
  if (form.activityName.trim() && (form.activityName.trim().length < 2 || form.activityName.trim().length > 50)) {
    message.warning('活动名称需为 2-50 个字符，留空则稍后补填')
    return
  }
  if (!Number.isFinite(Number(form.activityBudget)) || Number(form.activityBudget) < 0) {
    message.warning('活动经费需为不小于 0 的数字')
    return
  }

  const dto: FinanceMaterialUpsertDto = {
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

  saving.value = true
  try {
    const created = await FinanceService.createMaterial(dto)
    createModal.value = false
    message.success(`材料 ${created.id} 已创建，请选择待报销发票`)
    await router.push(`/Centre/Finance/Materials/${created.id}`)
  } catch (e: any) {
    message.error(e?.detail || e?.message || '创建失败')
  } finally {
    saving.value = false
  }
}

function batchRemoveMaterials() {
  const selected = items.value.filter(item => checkedKeys.value.includes(item.id))
  const removable = selected.filter(item => item.status !== 'reimbursed' && item.status !== 'deleted')
  if (!removable.length) {
    message.warning(selected.length ? '已报销或已删除的材料不能批量删除' : '请先选择材料')
    return
  }

  const skipped = selected.length - removable.length
  dialog.warning({
    title: '批量删除材料',
    content: `确定删除选中的 ${removable.length} 份材料吗？${skipped ? `另有 ${skipped} 份已报销/已删除材料会跳过。` : ''}删除为软删除，30 天内可恢复。`,
    positiveText: '删除',
    negativeText: '取消',
    onPositiveClick: async () => {
      let success = 0
      let failed = 0
      for (const material of removable) {
        try {
          await FinanceService.deleteMaterial(material.id)
          success++
        } catch {
          failed++
        }
      }
      checkedKeys.value = []
      await load()
      if (failed > 0) message.warning(`已删除 ${success} 份，${failed} 份删除失败`)
      else message.success(`已删除 ${success} 份材料`)
    }
  })
}

function remove(row: FinanceMaterial) {
  if (row.status === 'reimbursed') {
    message.warning('已报销材料需先撤销后才能删除')
    return
  }
  dialog.warning({
    title: '删除报销材料',
    content: `确定删除材料 ${row.id}（${row.activityName || '未命名活动'}）吗？删除为软删除，30 天内可恢复。`,
    positiveText: '删除',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await FinanceService.deleteMaterial(row.id)
        message.success('材料已删除')
        await load()
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    }
  })
}

async function restore(row: FinanceMaterial) {
  try {
    await FinanceService.restoreMaterial(row.id)
    message.success('材料已恢复')
    await load()
  } catch (e: any) {
    message.error(e?.message || '恢复失败')
  }
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
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">报销材料</h2>
        
      </div>
      <div class="flex items-center gap-2">
        <button class="finance-btn primary" @click="openCreate">
          <Icon icon="plus" class="w-4 h-4"/>新建材料
        </button>
      </div>
    </div>

    <div class="finance-card mb-4">
      <div class="flex flex-col lg:flex-row lg:items-center gap-3">
        <div class="flex gap-1 rounded-full bg-black/5 dark:bg-white/10 p-1 w-fit shrink-0">
          <button v-for="tab in statusTabs" :key="tab.value"
                  class="px-4 py-1.5 rounded-full text-sm whitespace-nowrap transition-all"
                  :class="statusFilter === tab.value
                    ? 'bg-white dark:bg-[#2C2C2E] text-gray-900 dark:text-white shadow-sm font-medium'
                    : 'text-gray-500 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200'"
                  @click="setStatus(tab.value)">{{ tab.label }}</button>
        </div>
        <select v-model="yearFilter" class="finance-select w-auto min-w-[140px] shrink-0" @change="page = 1; load()">
          <option value="">全部学年</option>
          <option v-for="year in yearOptions" :key="year" :value="year">{{ year }} 学年</option>
        </select>
        <div class="flex-1 relative min-w-[180px]">
          <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
            <Icon icon="search" class="text-gray-400 w-4 h-4"/>
          </div>
          <input v-model="keyword" type="text" placeholder="搜索材料编号、活动名称或发票号..."
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
      <span class="text-sm text-gray-600 dark:text-gray-300">已选择 {{ checkedKeys.length }} 份材料</span>
      <div class="flex gap-2">
        <button class="finance-btn" @click="checkedKeys = []">取消选择</button>
        <button class="finance-btn danger" @click="batchRemoveMaterials">批量删除</button>
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
                      :bordered="false" :row-key="(row: FinanceMaterial) => row.id"
                      :row-props="materialRowProps"
                      v-model:checked-row-keys="checkedKeys" remote
                      class="finance-table min-w-[1050px]">
          <template #empty>
            <div class="finance-empty">
              <Icon icon="list" class="w-10 h-10"/>
              <span>还没有报销材料，新建一份开始选择发票</span>
              <button class="finance-btn primary" @click="openCreate">新建材料</button>
            </div>
          </template>
        </n-data-table>
      </div>
    </div>

    <n-modal v-model:show="createModal" preset="card" style="max-width: 640px" :bordered="false"
             title="新建报销材料">
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div class="sm:col-span-2">
          <label class="finance-label">活动名称（可稍后补填，确认报销前必填）</label>
          <input v-model="form.activityName" maxlength="50" class="finance-input" placeholder="2-50 个字符"/>
        </div>
        <div>
          <label class="finance-label">活动时间</label>
          <input v-model="form.activityDate" type="date" class="finance-input"/>
        </div>
        <div>
          <label class="finance-label">活动性质</label>
          <select v-model="form.activityType" class="finance-select">
            <option value="">请选择</option>
            <option v-for="type in (meta?.activityTypes ?? [])" :key="type" :value="type">{{ type }}</option>
          </select>
        </div>
        <div>
          <label class="finance-label">活动地点</label>
          <input v-model="form.location" maxlength="50" class="finance-input"/>
        </div>
        <div>
          <label class="finance-label">参加人数</label>
          <input v-model.number="form.headcount" type="number" min="0" class="finance-input"/>
        </div>
        <div>
          <label class="finance-label">负责人</label>
          <input v-model="form.owner" maxlength="20" class="finance-input"/>
        </div>
        <div>
          <label class="finance-label">联系电话</label>
          <input v-model="form.phone" maxlength="14" class="finance-input"/>
        </div>
        <div class="sm:col-span-2">
          <label class="finance-label">活动经费（元）</label>
          <input v-model.number="form.activityBudget" type="number" min="0" step="0.01" class="finance-input"/>
          
        </div>
        <div class="sm:col-span-2">
          <label class="finance-label">活动简介（选填，≤200 字）</label>
          <textarea v-model="form.summary" rows="2" maxlength="200" class="finance-textarea"></textarea>
        </div>
        <div class="sm:col-span-2">
          <label class="finance-label">备注（选填）</label>
          <input v-model="form.remark" maxlength="200" class="finance-input"/>
        </div>
      </div>
      <template #footer>
        <div class="flex justify-end gap-2">
          <button class="finance-btn" @click="createModal = false">取消</button>
          <button class="finance-btn primary" :disabled="saving" @click="submitCreate">{{ saving ? '创建中…' : '创建材料' }}</button>
        </div>
      </template>
    </n-modal>
  </div>
</template>
