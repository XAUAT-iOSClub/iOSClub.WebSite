<script setup lang="ts">
import {computed, h, onBeforeUnmount, onMounted, ref} from 'vue'
import {
  NButton,
  NDataTable,
  NDatePicker,
  NInput,
  NModal,
  NPagination,
  NSelect,
  useMessage
} from 'naive-ui'
import type {DataTableColumns} from 'naive-ui'
import Icon from '@/components/Icon.vue'
import {useLayoutStore} from '../stores/LayoutStore'
import {
  OperationLogService,
  type OperationLogItem,
  type OperationLogOptions
} from '../services/OperationLogService'

const message = useMessage()
const layoutStore = useLayoutStore()

const loading = ref(false)
const items = ref<OperationLogItem[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(20)

const moduleFilter = ref<string | null>(null)
const operatorFilter = ref<string | null>(null)
const resultFilter = ref<string | null>(null)
const keyword = ref('')
const timeRange = ref<[number, number] | null>(null)

const options = ref<OperationLogOptions>({modules: [], operators: []})
const showDetail = ref(false)
const selected = ref<OperationLogItem | null>(null)

const identityLabels: Record<string, string> = {
  Founder: '创始人',
  President: '社长',
  Minister: '部长',
  Department: '部员',
  Member: '成员'
}

const moduleOptions = computed(() =>
  options.value.modules.map(value => ({label: value, value}))
)

const operatorOptions = computed(() =>
  options.value.operators.map(value => ({
    label: value.name ? `${value.name}（${value.userId}）` : value.userId,
    value: value.userId
  }))
)

const resultOptions = [
  {label: '成功', value: 'true'},
  {label: '失败', value: 'false'}
]

const formatDate = (value: string) => {
  const date = new Date(value)
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString('zh-CN', {hour12: false})
}

const prettyChangeContent = (value?: string | null) => {
  if (!value) return '无变更内容'

  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}

const loadOptions = async () => {
  try {
    options.value = await OperationLogService.getOptions()
  } catch (error: any) {
    message.error(error.message || '加载筛选选项失败')
  }
}

const load = async () => {
  loading.value = true
  try {
    const page = await OperationLogService.getLogs({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      module: moduleFilter.value,
      operatorId: operatorFilter.value,
      keyword: keyword.value.trim() || null,
      success: resultFilter.value === null ? null : resultFilter.value === 'true',
      startTime: timeRange.value ? new Date(timeRange.value[0]).toISOString() : null,
      endTime: timeRange.value ? new Date(timeRange.value[1]).toISOString() : null
    })
    items.value = page.items
    total.value = page.totalCount
  } catch (error: any) {
    message.error(error.message || '加载操作日志失败')
  } finally {
    loading.value = false
  }
}

const applyFilters = () => {
  pageIndex.value = 1
  void load()
}

const resetFilters = () => {
  moduleFilter.value = null
  operatorFilter.value = null
  resultFilter.value = null
  keyword.value = ''
  timeRange.value = null
  applyFilters()
}

const changePage = (page: number) => {
  pageIndex.value = page
  void load()
}

const changePageSize = (size: number) => {
  pageSize.value = size
  pageIndex.value = 1
  void load()
}

const openDetail = (row: OperationLogItem) => {
  selected.value = row
  showDetail.value = true
}

const columns: DataTableColumns<OperationLogItem> = [
  {
    title: '时间',
    key: 'createdAt',
    width: 175,
    render: row => h('span', {class: 'text-xs text-gray-500 dark:text-gray-400'},
        formatDate(row.createdAt))
  },
  {
    title: '操作人',
    key: 'operatorName',
    width: 180,
    render: row => h('div', {class: 'flex flex-col leading-tight'}, [
      h('span', {class: 'font-medium text-gray-900 dark:text-gray-100'},
          row.operatorName || row.operatorId || '未知'),
      h('span', {class: 'text-xs text-gray-400 dark:text-gray-500'},
          identityLabels[row.operatorRole || ''] || row.operatorRole || '')
    ])
  },
  {
    title: '模块 / 操作',
    key: 'module',
    width: 190,
    render: row => h('div', {class: 'flex flex-col leading-tight'}, [
      h('span', {class: 'text-xs text-gray-400 dark:text-gray-500'}, row.module),
      h('span', {class: 'font-medium text-gray-900 dark:text-gray-100'}, row.action)
    ])
  },
  {
    title: '摘要',
    key: 'summary',
    minWidth: 260,
    render: row => h('span', {class: 'text-sm text-gray-700 dark:text-gray-300'},
        row.summary || row.target || row.path || '-')
  },
  {
    title: '结果',
    key: 'success',
    width: 90,
    render: row => h('span', {
      class: row.success
        ? 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-50 text-emerald-600 dark:bg-emerald-500/15 dark:text-emerald-300'
        : 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-red-50 text-red-600 dark:bg-red-500/15 dark:text-red-300'
    }, row.success ? '成功' : '失败')
  },
  {
    title: 'IP',
    key: 'ipAddress',
    width: 135,
    render: row => h('span', {class: 'font-mono text-xs text-gray-500 dark:text-gray-400'},
        row.ipAddress || '-')
  },
  {
    title: '',
    key: 'actions',
    width: 80,
    align: 'right',
    render: row => h('button', {
      class: 'text-xs font-medium text-blue-600 hover:text-blue-700 dark:text-blue-400 dark:hover:text-blue-300',
      onClick: () => openDetail(row)
    }, '详情')
  }
]

const rowKey = (row: OperationLogItem) => row.id

onMounted(async () => {
  layoutStore.setPageHeader('操作日志', '查看后台写操作与脱敏后的变更内容')
  await Promise.all([loadOptions(), load()])
})

onBeforeUnmount(() => {
  layoutStore.clearPageHeader()
})
</script>

<template>
  <div class="apple-container min-h-screen max-sm:p-0 p-6 md:p-8 transition-colors duration-300">
    <div class="p-4 space-y-6">

      <!-- 筛选栏 -->
      <div class="apple-sub-card p-4 space-y-3">
        <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-3">
          <n-select
              v-model:value="moduleFilter"
              :options="moduleOptions"
              placeholder="全部模块"
              clearable
              @update:value="applyFilters"
          />
          <n-select
              v-model:value="operatorFilter"
              :options="operatorOptions"
              placeholder="全部操作人"
              clearable
              filterable
              @update:value="applyFilters"
          />
          <n-select
              v-model:value="resultFilter"
              :options="resultOptions"
              placeholder="全部结果"
              clearable
              @update:value="applyFilters"
          />
          <n-date-picker
              v-model:value="timeRange"
              type="datetimerange"
              clearable
              placeholder="选择时间范围"
              @update:value="applyFilters"
          />
        </div>
        <div class="flex flex-col sm:flex-row sm:items-center gap-3">
          <n-input
              v-model:value="keyword"
              placeholder="搜索操作人、摘要、路径、变更内容..."
              clearable
              @keyup.enter="applyFilters"
          />
          <div class="flex items-center gap-2 shrink-0">
            <n-button type="primary" @click="applyFilters">查询</n-button>
            <n-button @click="resetFilters">重置</n-button>
            <n-button quaternary @click="load">
              <template #icon>
                <Icon icon="refresh-cw"/>
              </template>
              刷新
            </n-button>
          </div>
        </div>
      </div>

      <!-- 日志表 -->
      <div class="apple-sub-card overflow-hidden">
        <div class="overflow-x-auto">
          <n-data-table
              :columns="columns"
              :data="items"
              :loading="loading"
              :bordered="false"
              :row-key="rowKey"
              :pagination="false"
              class="apple-table"
          />
        </div>
        <div class="flex justify-end p-4 border-t border-slate-100 dark:border-slate-800/60">
          <n-pagination
              :page="pageIndex"
              :page-size="pageSize"
              :item-count="total"
              :page-sizes="[10, 20, 50, 100]"
              show-size-picker
              @update:page="changePage"
              @update:page-size="changePageSize"
          />
        </div>
      </div>
    </div>

    <!-- 详情 -->
    <n-modal
        v-model:show="showDetail"
        preset="card"
        class="apple-modal"
        style="max-width: 820px"
        title="操作详情"
        :bordered="false"
        size="huge"
    >
      <div v-if="selected" class="space-y-5 text-sm">
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <div class="text-xs text-gray-400 mb-1">操作人</div>
            <div class="text-gray-900 dark:text-gray-100">
              {{ selected.operatorName || selected.operatorId || '未知' }}
              <span class="text-gray-400">（{{ identityLabels[selected.operatorRole || ''] || selected.operatorRole || '-' }}）</span>
            </div>
          </div>
          <div>
            <div class="text-xs text-gray-400 mb-1">时间</div>
            <div class="text-gray-900 dark:text-gray-100">{{ formatDate(selected.createdAt) }}</div>
          </div>
          <div>
            <div class="text-xs text-gray-400 mb-1">模块 / 操作</div>
            <div class="text-gray-900 dark:text-gray-100">{{ selected.module }} / {{ selected.action }}</div>
          </div>
          <div>
            <div class="text-xs text-gray-400 mb-1">结果</div>
            <div :class="selected.success ? 'text-emerald-600 dark:text-emerald-400' : 'text-red-600 dark:text-red-400'">
              {{ selected.success ? '成功' : '失败' }}（HTTP {{ selected.httpStatus }}，{{ selected.durationMs }} ms）
            </div>
          </div>
          <div>
            <div class="text-xs text-gray-400 mb-1">接口</div>
            <div class="font-mono text-xs text-gray-600 dark:text-gray-300 break-all">
              {{ selected.method }} {{ selected.path }}
            </div>
          </div>
          <div>
            <div class="text-xs text-gray-400 mb-1">IP / TraceId</div>
            <div class="font-mono text-xs text-gray-600 dark:text-gray-300 break-all">
              {{ selected.ipAddress || '-' }} / {{ selected.traceId || '-' }}
            </div>
          </div>
        </div>

        <div v-if="selected.message">
          <div class="text-xs text-gray-400 mb-1">响应消息</div>
          <div class="text-gray-700 dark:text-gray-300">{{ selected.message }}</div>
        </div>

        <div v-if="selected.errorMessage">
          <div class="text-xs text-gray-400 mb-1">异常信息</div>
          <div class="text-red-600 dark:text-red-400 break-all">{{ selected.errorMessage }}</div>
        </div>

        <div>
          <div class="text-xs text-gray-400 mb-1">变更内容（已脱敏）</div>
          <pre class="max-h-80 overflow-auto whitespace-pre-wrap break-all rounded-xl bg-gray-50 dark:bg-white/5 p-4 text-xs text-gray-700 dark:text-gray-300">{{ prettyChangeContent(selected.changeContent) }}</pre>
        </div>

        <div v-if="selected.userAgent">
          <div class="text-xs text-gray-400 mb-1">User-Agent</div>
          <div class="text-xs text-gray-500 dark:text-gray-400 break-all">{{ selected.userAgent }}</div>
        </div>
      </div>
    </n-modal>
  </div>
</template>
