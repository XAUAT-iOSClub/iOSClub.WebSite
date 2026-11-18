<script setup lang="ts">
import AdminSurface from '@/components/admin/AdminSurface.vue'
import {computed, onBeforeUnmount, onMounted, ref} from 'vue'
import {
  NButton,
  NDatePicker,
  NInput,
  NPagination,
  NSelect,
  NSpin,
  useMessage
} from 'naive-ui'
import OperationLogDetail from '@/components/OperationLogs/OperationLogDetail.vue'
import Icon from '@/components/Icon.vue'
import {useLayoutStore} from '../stores/LayoutStore'
import {
  OperationLogService,
  type OperationLogItem,
  type OperationLogOptions
} from '../services/OperationLogService'

const message = useMessage()
const layoutStore = useLayoutStore()

let latestRequest = 0
const loading = ref(false)
const loadFailed = ref(false)
const items = ref<OperationLogItem[]>([])
const total = ref(0)
const pageIndex = ref(1)
const pageSize = ref(20)

const moduleFilter = ref<string | null>(null)
const operatorFilter = ref<string | null>(null)
const resultFilter = ref<string | null>(null)
const keyword = ref('')
const startTime = ref<number | null>(null)
const endTime = ref<number | null>(null)

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

const controlTheme = { borderRadius: '12px' }
const selectTheme = { peers: { InternalSelection: controlTheme } }
const dateTheme = { panelBorderRadius: '16px', peers: { Input: controlTheme } }
const paginationTheme = {
  itemBorderRadius: '8px',
  itemBorderActive: '1px solid transparent',
  itemColorActive: 'rgb(0 113 227 / 15%)',
  itemColorActiveHover: 'rgb(0 113 227 / 20%)',
  itemTextColorActive: 'var(--log-page-accent)',
  itemTextColorHover: 'var(--log-page-accent)',
  itemTextColorPressed: 'var(--log-page-accent)'
}

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

const formatRole = (role?: string | null) => identityLabels[role || ''] || role || ''

const loadOptions = async () => {
  try {
    options.value = await OperationLogService.getOptions()
  } catch (error: any) {
    message.error(error.message || '加载筛选选项失败')
  }
}

const load = async () => {
  if (startTime.value !== null && endTime.value !== null && startTime.value > endTime.value) {
    message.error('结束时间须晚于或等于开始时间')
    return
  }

  const request = ++latestRequest
  loading.value = true
  loadFailed.value = false
  try {
    const page = await OperationLogService.getLogs({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      module: moduleFilter.value,
      operatorId: operatorFilter.value,
      keyword: keyword.value.trim() || null,
      success: resultFilter.value === null ? null : resultFilter.value === 'true',
      startTime: startTime.value === null ? null : new Date(startTime.value).toISOString(),
      endTime: endTime.value === null ? null : new Date(endTime.value).toISOString()
    })
    if (request !== latestRequest) return
    items.value = page.items
    total.value = page.totalCount
  } catch (error: any) {
    if (request !== latestRequest) return
    loadFailed.value = true
    message.error(error.message || '加载操作日志失败')
  } finally {
    if (request === latestRequest) loading.value = false
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
  startTime.value = null
  endTime.value = null
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

onMounted(async () => {
  layoutStore.setPageHeader('操作日志', '查看后台写操作与脱敏后的变更内容')
  await Promise.all([loadOptions(), load()])
})

onBeforeUnmount(() => {
  layoutStore.clearPageHeader()
})
</script>

<template>
  <div class="admin-page mx-auto grid max-w-6xl gap-5 px-4 py-5 text-[#1d1d1f] sm:px-6 dark:text-[#f5f5f7] [--log-page-accent:#005bb9] dark:[--log-page-accent:#8bc4ff]">
    <AdminSurface as="section" aria-labelledby="log-filter-heading" class="settings-card">
      <h2 id="log-filter-heading" class="section-title px-5 pt-5">筛选日志</h2>
      <form class="grid gap-4 p-5" @submit.prevent="applyFilters">
        <div class="flex flex-wrap items-center gap-2">
          <NInput
            v-model:value="keyword"
            :input-props="{ 'aria-label': '搜索操作日志' }"
            placeholder="搜索操作人、摘要、路径或变更内容"
            class="min-w-0 basis-full sm:flex-1"
            round clearable
          >
            <template #prefix><Icon icon="search" /></template>
          </NInput>
          <NButton round color="#0071e3" text-color="#fff" attr-type="submit">查询</NButton>
          <NButton round secondary @click="resetFilters">重置</NButton>
        </div>
        <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-[minmax(0,1fr)_minmax(0,1.15fr)_minmax(0,0.85fr)_minmax(10.25rem,1.2fr)_minmax(10.25rem,1.2fr)]">
          <NSelect
            v-model:value="moduleFilter"
            :options="moduleOptions"
            placeholder="全部模块"
            :theme-overrides="selectTheme"
            aria-label="模块"
            clearable
            @update:value="applyFilters"
          />
          <NSelect
            v-model:value="operatorFilter"
            :options="operatorOptions"
            placeholder="全部操作人"
            :theme-overrides="selectTheme"
            aria-label="操作人"
            clearable filterable
            @update:value="applyFilters"
          />
          <NSelect
            v-model:value="resultFilter"
            :options="resultOptions"
            placeholder="全部结果"
            :theme-overrides="selectTheme"
            aria-label="结果"
            class="col-span-2 sm:col-span-1"
            clearable
            @update:value="applyFilters"
          />
          <NDatePicker
            v-model:value="startTime"
            :theme-overrides="dateTheme"
            type="datetime"
            placeholder="开始时间"
            aria-label="开始时间"
            class="col-span-2 min-w-0 sm:col-span-1"
            clearable
            @update:value="applyFilters"
          />
          <NDatePicker
            v-model:value="endTime"
            :theme-overrides="dateTheme"
            type="datetime"
            placeholder="结束时间"
            aria-label="结束时间"
            class="col-span-2 min-w-0 sm:col-span-1"
            clearable
            @update:value="applyFilters"
          />
        </div>
      </form>
    </AdminSurface>

    <AdminSurface as="section" aria-labelledby="log-list-heading" class="settings-card">
      <header class="flex flex-wrap items-center justify-between gap-3 border-b border-black/8 p-5 dark:border-white/10">
        <div class="flex items-center gap-3">
          <span class="grid size-10 shrink-0 place-items-center rounded-xl bg-blue-50 text-blue-700 dark:bg-blue-400/15 dark:text-blue-300">
            <Icon icon="scroll-text" :size="20" />
          </span>
          <h2 id="log-list-heading" class="section-title">日志记录</h2>
          <span v-if="!loading && !loadFailed" class="rounded-full bg-black/5 px-2.5 py-1 text-xs font-medium tabular-nums dark:bg-white/10">
            {{ total }} 条
          </span>
        </div>
        <NButton round secondary size="small" :disabled="loading" @click="load">
          <template #icon><Icon icon="refresh-cw" /></template>
          刷新
        </NButton>
      </header>

      <div v-if="loadFailed" role="alert" class="flex flex-wrap items-center justify-between gap-3 border-b border-black/8 px-5 py-4 text-sm text-red-700 dark:border-white/10 dark:text-red-300">
        无法加载操作日志。
        <NButton round size="small" @click="load">重试</NButton>
      </div>
      <div v-if="loading && !items.length" role="status" class="grid min-h-44 place-content-center justify-items-center gap-3">
        <NSpin size="small" />
        <p class="secondary-text">正在加载日志</p>
      </div>
      <div v-else-if="items.length === 0 && !loadFailed" class="grid min-h-44 place-content-center justify-items-center gap-3 p-5 text-center">
        <span class="grid size-12 place-items-center rounded-2xl bg-[#f5f5f7] text-gray-500 dark:bg-white/8 dark:text-gray-400">
          <Icon icon="inbox" :size="22" />
        </span>
        <p class="text-sm font-medium">没有符合条件的日志</p>
      </div>
      <NSpin v-else-if="items.length" :show="loading">
        <ul class="divide-y divide-black/8 dark:divide-white/10">
          <li v-for="item in items" :key="item.id">
            <button
              type="button"
              class="log-row w-full cursor-pointer gap-x-4 gap-y-2 px-5 py-4 text-left hover:bg-black/3 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-blue-500 dark:hover:bg-white/5"
              aria-haspopup="dialog"
              @click="openDetail(item)"
            >
              <span class="log-action min-w-0 break-words">
                <span class="block text-sm font-semibold">{{ item.action }}</span>
                <span class="secondary-text">{{ item.module }}</span>
                <span class="mt-1 block text-sm text-gray-700 dark:text-gray-300">{{ item.summary || item.target || item.path || '-' }}</span>
              </span>
              <span class="log-operator min-w-0 break-words">
                <span class="block text-sm font-medium">{{ item.operatorName || item.operatorId || '未知' }}</span>
                <span v-if="item.operatorRole" class="secondary-text">{{ formatRole(item.operatorRole) }}</span>
              </span>
              <span class="log-time min-w-0 break-words">
                <time :datetime="item.createdAt" class="secondary-text block tabular-nums">{{ formatDate(item.createdAt) }}</time>
                <span v-if="item.ipAddress" class="secondary-text font-mono">{{ item.ipAddress }}</span>
              </span>
              <span class="log-result justify-self-end">
                <span
                  class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap"
                  :class="item.success
                    ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-400/15 dark:text-emerald-300'
                    : 'bg-red-50 text-red-700 dark:bg-red-400/15 dark:text-red-300'"
                >
                  <Icon :icon="item.success ? 'circle-check' : 'circle-x'" />
                  {{ item.success ? '成功' : '失败' }}
                </span>
              </span>
              <Icon icon="chevron-right" class="log-chevron self-center text-gray-500 dark:text-gray-400" />
            </button>
          </li>
        </ul>
      </NSpin>

      <footer class="flex justify-end border-t border-black/8 px-5 py-4 dark:border-white/10">
        <NPagination
          :page="pageIndex"
          :page-size="pageSize"
          :item-count="total"
          :page-sizes="[10, 20, 50, 100]"
          :page-slot="5"
          :theme-overrides="paginationTheme"
          :disabled="loading"
          class="flex-wrap justify-end gap-y-2"
          size="small"
          show-size-picker
          @update:page="changePage"
          @update:page-size="changePageSize"
        />
      </footer>
    </AdminSurface>

    <OperationLogDetail
      v-model:show="showDetail"
      :item="selected"
      :date="selected ? formatDate(selected.createdAt) : ''"
      :role="formatRole(selected?.operatorRole)"
    />
  </div>
</template>

<style scoped>
@reference "../style.css";

.settings-card {
  @apply min-w-0 rounded-3xl border border-black/8 bg-white shadow-sm dark:border-white/10 dark:bg-[#1c1c1e];
}

.section-title {
  @apply text-lg font-semibold tracking-tight;
}

.secondary-text {
  @apply text-xs leading-relaxed text-gray-600 dark:text-gray-400;
}

.log-row {
  display: grid;
  grid-template:
    "action action result chevron" auto
    "operator time time chevron" auto
    / minmax(0, 1fr) minmax(0, 1fr) auto auto;
}

.log-action { grid-area: action; }
.log-operator { grid-area: operator; }
.log-time { grid-area: time; }
.log-result { grid-area: result; }
.log-chevron { grid-area: chevron; }

@media (min-width: 64rem) {
  .log-row {
    grid-template: "action operator time result chevron" auto
      / minmax(0, 2fr) minmax(0, 0.85fr) minmax(0, 1fr) auto auto;
    align-items: center;
  }
}
</style>
