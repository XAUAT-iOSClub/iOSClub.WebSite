<template>
  <div
      class="apple-page min-h-screen bg-[#f5f5f7] dark:bg-black text-slate-900 dark:text-slate-100 transition-colors duration-500">
    <main class="max-w-[1440px] mx-auto px-6 py-10">

      <!-- 顶部统计与图表区 (Grid布局) -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">

        <!-- 左侧：关键指标卡片 (2/3 宽度) -->
        <div class="lg:col-span-3 xl:col-span-1 grid grid-cols-2 sm:grid-cols-2 gap-4">
          <!-- 总日志 -->
          <div class="apple-card relative overflow-hidden group p-6 flex flex-col justify-between h-40">
            <div
                class="absolute -right-4 -top-4 opacity-10 group-hover:opacity-20 transition-opacity transform rotate-12">
              <Icon icon="files" width="120"/>
            </div>
            <div>
              <p class="text-sm font-medium text-slate-500 dark:text-slate-400 uppercase tracking-wide">总日志数</p>
              <h3 class="text-4xl font-semibold mt-2 tracking-tight">{{
                  statisticsLoading ? '...' : totalLogsCount
                }}</h3>
            </div>
            <div class="flex items-center gap-2 mt-4">
              <span class="flex h-2 w-2 rounded-full bg-blue-500"></span>
              <span class="text-xs text-slate-400">系统活跃</span>
            </div>
          </div>

          <!-- 错误日志 -->
          <div
              class="apple-card relative overflow-hidden group p-6 flex flex-col justify-between h-40 bg-red-50/50 dark:bg-red-900/10 border-red-100 dark:border-red-900/30">
            <div
                class="absolute -right-4 -top-4 opacity-10 text-red-500 group-hover:opacity-20 transition-opacity transform rotate-12">
              <Icon icon="triangle-alert" width="120"/>
            </div>
            <div>
              <p class="text-sm font-medium text-red-600/80 dark:text-red-400 uppercase tracking-wide">错误</p>
              <h3 class="text-4xl font-semibold mt-2 text-red-600 dark:text-red-400 tracking-tight">{{
                  errorLogsCount
                }}</h3>
            </div>
            <div class="flex items-center gap-2 mt-4">
              <span class="flex h-2 w-2 rounded-full bg-red-500 animate-pulse"></span>
              <span class="text-xs text-red-600/70 dark:text-red-400/70">需要关注</span>
            </div>
          </div>

          <!-- 警告日志 -->
          <div class="apple-card p-6 flex flex-col justify-center h-32">
            <div class="flex justify-between items-center mb-2">
                 <span
                     class="flex h-8 w-8 items-center justify-center rounded-full bg-yellow-100 dark:bg-yellow-900/30 text-yellow-600 dark:text-yellow-400">
                    <Icon icon="bell" width="18"/>
                 </span>
              <span class="text-xs font-bold bg-slate-100 dark:bg-slate-800 px-2 py-1 rounded-full">警告</span>
            </div>
            <div class="text-2xl font-bold text-yellow-600 dark:text-yellow-400">{{ warningLogsCount }}</div>
            <div class="text-xs text-slate-400 mt-1">发现潜在问题</div>
          </div>

          <!-- 信息日志 -->
          <div class="apple-card p-6 flex flex-col justify-center h-32">
            <div class="flex justify-between items-center mb-2">
                 <span
                     class="flex h-8 w-8 items-center justify-center rounded-full bg-emerald-100 dark:bg-emerald-900/30 text-emerald-600 dark:text-emerald-400">
                    <Icon icon="info" width="18"/>
                 </span>
              <span class="text-xs font-bold bg-slate-100 dark:bg-slate-800 px-2 py-1 rounded-full">信息</span>
            </div>
            <div class="text-2xl font-bold text-emerald-600 dark:text-emerald-400">{{ infoLogsCount }}</div>
            <div class="text-xs text-slate-400 mt-1">正常运行</div>
          </div>
        </div>

        <!-- 右侧：ECharts 趋势图 (占据剩余空间) -->
        <div class="lg:col-span-3 xl:col-span-2 apple-card p-6 min-h-[340px] flex flex-col">
          <h2 class="text-lg font-semibold mb-4 flex items-center gap-2">
            <Icon icon="trending-up" class="text-blue-500"/>
            日志活动趋势
          </h2>
          <div class="flex-1 w-full relative">
            <div ref="chartDom" class="w-full h-full absolute inset-0"></div>
            <!-- Absolute positioning for responsive resizing -->
          </div>
        </div>
      </div>

      <!-- 控制栏与表格区域 -->
      <div class="apple-card p-0 overflow-hidden flex flex-col min-h-[600px]">

        <!-- 顶部工具栏 -->
        <div
            class="p-5 border-b border-slate-100 dark:border-slate-800/50 bg-white/50 dark:bg-gray-800/50 backdrop-blur-sm sticky top-0 z-10">
          <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4">

            <!-- 搜索与筛选左侧 -->
            <div class="flex flex-1 flex-col sm:flex-row gap-3">
              <!-- 搜索框 - iOS 风格 -->
              <div class="relative flex-1 max-w-md group">
                <Icon icon="search"
                      class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 group-focus-within:text-blue-500 transition-colors"
                      width="18"/>
                <!-- 使用原生 input 配合样式实现更纯粹的 Tailwind 4 控制 -->
                <input
                    v-model="searchKeyword"
                    @keyup.enter="applyFilters"
                    type="text"
                    placeholder="搜索日志..."
                    class="w-full bg-slate-100 dark:bg-slate-900/80 border-none rounded-lg py-2.5 pl-10 pr-4 text-sm focus:ring-2 focus:ring-blue-500/50 outline-none transition-all"
                />
                <button
                    v-if="searchKeyword"
                    @click="searchKeyword = ''; applyFilters()"
                    class="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 dark:hover:text-slate-200">
                  <Icon icon="circle-x" width="16"/>
                </button>
              </div>

              <!-- 下拉筛选 NaiveUI -->
              <div class="w-full sm:w-48">
                <n-select
                    v-model:value="selectedLevel"
                    :options="logLevelOptions"
                    placeholder="所有级别"
                    clearable
                    size="medium"
                    class="apple-select"
                    @update:value="applyFilters"
                />
              </div>
            </div>

            <!-- 右侧操作 -->
            <div class="flex items-center gap-2 self-end lg:self-auto">
              <!-- 时间范围选择 -->
              <div class="hidden md:flex items-center bg-slate-100 dark:bg-slate-900/80 rounded-lg p-1">
                <button
                    v-for="range in quickTimeRanges.slice(0, 4)"
                    :key="range.value"
                    @click="applyQuickTimeRange(range.value)"
                    :class="['px-3 py-1.5 text-xs font-medium rounded-md transition-all',
                      selectedQuickTimeRange === range.value
                      ? 'bg-white dark:bg-slate-700 shadow-sm text-slate-900 dark:text-white'
                      : 'text-slate-500 hover:text-slate-700 dark:hover:text-slate-300']"
                >
                  {{ range.label }}
                </button>
              </div>
              <n-button secondary circle class="ml-2" @click="refreshLogs">
                <template #icon>
                  <Icon icon="rotate-ccw"/>
                </template>
              </n-button>
              <n-button secondary circle type="error" @click="showCleanupDialog = true">
                <template #icon>
                  <Icon icon="trash"/>
                </template>
              </n-button>
            </div>
          </div>
        </div>

        <!-- 表格内容区 -->
        <div class="flex-1 overflow-x-auto relative custom-scrollbar">
          <table class="w-full text-left border-collapse">
            <thead
                class="bg-slate-50/80 dark:bg-slate-800/30 backdrop-blur sticky top-0 z-9 text-xs uppercase tracking-wider text-slate-500 font-semibold">
            <tr>
              <th class="px-6 py-4 w-48">时间戳</th>
              <th class="px-6 py-4 w-32 text-center">级别</th>
              <th class="px-6 py-4">消息</th>
              <th class="px-6 py-4 w-24 text-right">操作</th>
            </tr>
            </thead>
            <tbody class="divide-y divide-slate-100 dark:divide-slate-800">
            <template v-if="loading">
              <tr v-for="i in 8" :key="i" class="animate-pulse">
                <td class="px-6 py-4">
                  <div class="h-4 bg-slate-200 dark:bg-slate-700 rounded w-32"></div>
                </td>
                <td class="px-6 py-4">
                  <div class="h-6 bg-slate-200 dark:bg-slate-700 rounded-full w-16 mx-auto"></div>
                </td>
                <td class="px-6 py-4">
                  <div class="h-4 bg-slate-200 dark:bg-slate-700 rounded w-3/4"></div>
                </td>
                <td class="px-6 py-4"></td>
              </tr>
            </template>

            <template v-else-if="paginatedLogs.length > 0">
              <tr
                  v-for="log in paginatedLogs"
                  :key="`${log.timestamp}-${log.level}`"
                  class="group hover:bg-blue-50/50 dark:hover:bg-blue-900/10 transition-colors cursor-pointer"
                  @click="showLogDetails(log)"
              >
                <td class="px-6 py-4 text-sm text-slate-500 dark:text-slate-400 font-mono whitespace-nowrap">
                  {{ formatDateTime(log.timestamp) }}
                </td>
                <td class="px-6 py-4 text-center">
                     <span
                         :class="[
                        'px-3 py-1 text-[11px] font-bold rounded-full border inline-flex items-center gap-1',
                        getBadgeStyle(log.level)
                      ]">
                      {{ log.level }}
                     </span>
                </td>
                <td class="px-6 py-4">
                  <div
                      class="text-sm text-slate-700 dark:text-slate-200 font-medium line-clamp-1 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">
                    {{ log.message || '无消息内容' }}
                  </div>
                  <div v-if="log.exception"
                       class="text-xs text-red-500 mt-1 line-clamp-1 font-mono bg-red-50 dark:bg-red-900/20 px-2 py-0.5 rounded inline-block">
                    异常: {{ log.exception.substring(0, 50) }}...
                  </div>
                </td>
                <td class="px-6 py-4 text-right opacity-0 group-hover:opacity-100 transition-opacity">
                  <button
                      class="text-slate-400 hover:text-blue-500 p-1 rounded-full hover:bg-slate-200 dark:hover:bg-slate-700 transition-all"
                      @click.stop="showLogDetails(log)">
                    <Icon icon="info" width="20"/>
                  </button>
                </td>
              </tr>
            </template>

            <tr v-else>
              <td colspan="4" class="px-6 py-20 text-center">
                <div class="flex flex-col items-center justify-center text-slate-400">
                  <Icon icon="package" width="64" class="mb-4 opacity-50"/>
                  <p class="text-lg">未找到日志</p>
                  <p class="text-sm opacity-70">尝试调整筛选条件</p>
                </div>
              </td>
            </tr>
            </tbody>
          </table>
        </div>

        <!-- 底部页码 -->
        <div
            class="p-4 border-t border-slate-100 dark:border-slate-800 bg-slate-50/50 dark:bg-slate-900/30 flex justify-between items-center">
           <span class="text-xs text-slate-500">
              {{ totalLogs }} 条记录总数
           </span>
          <n-pagination
              v-model:page="currentPage"
              v-model:page-size="pageSize"
              :item-count="totalLogs"
              :page-sizes="[10, 25, 50, 100]"
              show-size-picker
              simple
              @update:page="handlePageChange"
              @update:page-size="handlePageSizeChange"
              class="apple-pagination"
          />
        </div>
      </div>

    </main>

    <!-- 详情弹窗：使用 macOS 风格的 Modal -->
    <n-modal
        v-model:show="showLogDetailsDialog"
        preset="card"
        :bordered="false"
        class="!w-[90vw] !max-w-[700px] !rounded-2xl apple-modal overflow-hidden"
        :mask-closable="true"
    >
      <template #header>
        <div class="flex items-center gap-3 pt-2 pl-2">
          <div
              class="h-10 w-10 rounded-full bg-slate-100 dark:bg-slate-700 flex items-center justify-center text-slate-500">
            <Icon icon="file-text" width="24"/>
          </div>
          <div>
            <h3 class="text-lg font-semibold">日志详情</h3>
            <p class="text-xs text-slate-400 font-mono">{{
                selectedLog ? formatDateTime(selectedLog.timestamp) : ''
              }}</p>
          </div>
        </div>
      </template>

      <div v-if="selectedLog" class="py-2 space-y-6">
        <!-- 级别与基本信息 -->
        <div class="flex flex-wrap gap-3">
          <div class="bg-slate-50 dark:bg-slate-800 rounded-lg px-4 py-2 flex-1 min-w-[120px]">
            <div class="text-xs text-slate-400 uppercase mb-1">Level</div>
            <span :class="['px-2 py-0.5 rounded text-xs font-bold inline-block', getBadgeStyle(selectedLog.level)]">
                 {{ selectedLog.level }}
              </span>
          </div>
          <!-- 其他属性占位 -->
        </div>

        <div>
          <h4 class="text-sm font-medium text-slate-500 mb-2 flex items-center gap-2">
            <Icon icon="message-square-text" class="text-blue-500" width="16"/>
            消息
          </h4>
          <div
              class="bg-slate-50 dark:bg-slate-900/80 p-4 rounded-xl text-sm leading-relaxed text-slate-800 dark:text-slate-200 border border-slate-200 dark:border-slate-700 select-text whitespace-pre-wrap">
            {{ selectedLog.message }}
          </div>
        </div>

        <div v-if="selectedLog.exception">
          <h4 class="text-sm font-medium text-red-500 mb-2 flex items-center gap-2">
            <Icon icon="circle-alert" width="16"/>
            异常追踪
          </h4>
          <div
              class="bg-red-50/50 dark:bg-red-900/10 p-4 rounded-xl border border-red-100 dark:border-red-900/30 overflow-x-auto">
            <pre class="text-xs font-mono text-red-700 dark:text-red-300 whitespace-pre">{{
                selectedLog.exception
              }}</pre>
          </div>
        </div>

        <div v-if="selectedLog.properties && Object.keys(selectedLog.properties).length > 0">
          <h4 class="text-sm font-medium text-slate-500 mb-2">Properties (JSON)</h4>
          <div class="bg-slate-800 text-slate-200 p-4 rounded-xl overflow-auto max-h-40 custom-scrollbar">
            <pre class="text-xs font-mono">{{ JSON.stringify(selectedLog.properties, null, 2) }}</pre>
          </div>
        </div>
      </div>
    </n-modal>

    <!-- 清理弹窗 -->
    <n-modal v-model:show="showCleanupDialog" preset="dialog" title="清理日志" positive-text="清理" negative-text="取消"
             @positive-click="confirmCleanup" @negative-click="showCleanupDialog = false"
             class="!rounded-2xl apple-modal"
             :show-icon="false"
    >
      <div class="pt-4 pb-2">
        <div
            class="flex items-center gap-4 mb-4 bg-yellow-50 dark:bg-yellow-900/20 p-3 rounded-lg text-yellow-700 dark:text-yellow-400 text-sm">
          <Icon icon="circle-alert" width="24" class="shrink-0"/>
          此操作不可撤销。仅保留最近几天的日志。
        </div>
        <label class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
          保留期限 (天)
        </label>
        <n-input-number v-model:value="cleanupDays" :min="1" :max="365" class="w-full text-center" size="large"/>
      </div>
    </n-modal>

  </div>
</template>

<script setup lang="ts">
import {ref, computed, onMounted, onBeforeUnmount, nextTick} from 'vue'
import {
  NSelect, NButton, NModal, NPagination, NInputNumber, useMessage
} from 'naive-ui'
import Icon from '@/components/Icon.vue'
import {LogsService, type LogEntry, type LogDistribution} from '../services/LogsService'
import {useLayoutStore} from '../stores/LayoutStore'
import * as echarts from 'echarts'

// ---------------------------------------------------------
// State
// ---------------------------------------------------------
const logs = ref<LogEntry[]>([])
const loading = ref<boolean>(false)
const statisticsLoading = ref<boolean>(false)

// 筛选与分页
const searchKeyword = ref<string>('')
const selectedLevel = ref<string | null>(null)
const selectedQuickTimeRange = ref<string | null>('today') // 默认选中 'today'
const currentPage = ref<number>(1)
const pageSize = ref<number>(20)
const totalLogs = ref<number>(0)
const totalPages = ref<number>(1)

// 详情与模态框
const selectedLog = ref<LogEntry | null>(null)
const showLogDetailsDialog = ref<boolean>(false)
const showCleanupDialog = ref<boolean>(false)
const cleanupDays = ref<number>(7)

// 统计数据
const totalLogsCount = ref<number>(0)
const errorLogsCount = ref<number>(0)
const warningLogsCount = ref<number>(0)
const infoLogsCount = ref<number>(0)

// ECharts 实例
const chartDom = ref<HTMLElement | null>(null)
let chartInstance: echarts.ECharts | null = null

// 日志分布数据状态
const logDistributionData = ref<LogDistribution[]>([])
const chartLoading = ref<boolean>(false)

// Store & Hooks
const message = useMessage()
const layoutStore = useLayoutStore()

// ---------------------------------------------------------
// Constants & Options
// ---------------------------------------------------------
const logLevelOptions = [
  {label: '信息', value: 'Information'},
  {label: '警告', value: 'Warning'},
  {label: '错误', value: 'Error'},
  {label: '严重', value: 'Critical'},
  {label: '调试', value: 'Debug'},
  {label: '跟踪', value: 'Trace'}
]

const quickTimeRanges = [
  {label: '今天', value: 'today'},
  {label: '昨天', value: 'yesterday'},
  {label: '最近7天', value: '7days'},
  {label: '30天', value: '30days'},
]

// ---------------------------------------------------------
// UI Helpers
// ---------------------------------------------------------
// 根据日志级别返回样式类
const getBadgeStyle = (level: string) => {
  switch (level) {
    case 'Information':
      return 'bg-emerald-100 text-emerald-700 border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-400 dark:border-emerald-900/50'
    case 'Warning':
      return 'bg-yellow-100 text-yellow-700 border-yellow-200 dark:bg-yellow-900/30 dark:text-yellow-400 dark:border-yellow-900/50'
    case 'Error':
    case 'Critical':
      return 'bg-red-100 text-red-700 border-red-200 dark:bg-red-900/30 dark:text-red-400 dark:border-red-900/50'
    default:
      return 'bg-slate-100 text-slate-600 border-slate-200 dark:bg-slate-800 dark:text-slate-400 dark:border-slate-700'
  }
}

const formatDateTime = (dateString: string): string => {
  const date = new Date(dateString)
  return date.toLocaleString('zh-CN', {
    month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit',
    hour12: false
  })
}

// ---------------------------------------------------------
// Logic Actions
// ---------------------------------------------------------

const applyQuickTimeRange = (range: string) => {
  if (selectedQuickTimeRange.value === range) return
  selectedQuickTimeRange.value = range
  applyFilters()
}

// 将 computed 简化为直接返回
const paginatedLogs = computed(() => logs.value)

const getLogs = async (): Promise<void> => {
  if (loading.value) return
  loading.value = true

  try {
    // 解析时间参数，适配后端接口逻辑
    let timeRangeParam: string | undefined = selectedQuickTimeRange.value || undefined

    // 简单处理: 如果是 days 才去提取数字，否则传 original string
    if (timeRangeParam && timeRangeParam.endsWith('days') && !timeRangeParam.startsWith('today')) {
      const daysMatch = timeRangeParam.match(/^(\d+)days$/)
      if (daysMatch) {
        timeRangeParam = daysMatch[1]
      }
    }

    const response = await LogsService.getRecentLogs(
        currentPage.value,
        pageSize.value,
        searchKeyword.value,
        selectedLevel.value || undefined,
        timeRangeParam
    )
    logs.value = response.data
    totalLogs.value = response.totalCount
    totalPages.value = response.totalPages

    // 数据更新后刷新图表
    void updateChart()
  } catch (error) {
    console.error('Failed to fetch logs:', error)
    message.error('加载日志失败')
  } finally {
    loading.value = false
  }
}

const refreshLogs = async () => await getLogs()

const applyFilters = async () => {
  currentPage.value = 1
  await getLogs()
}

const handlePageChange = async (page: number) => {
  currentPage.value = page
  await getLogs()
}

const handlePageSizeChange = async (size: number) => {
  pageSize.value = size
  currentPage.value = 1
  await getLogs()
}

const showLogDetails = (log: LogEntry) => {
  selectedLog.value = log
  showLogDetailsDialog.value = true
}

const loadStatistics = async () => {
  statisticsLoading.value = true
  try {
    const stats = await LogsService.getLogStatistics()
    totalLogsCount.value = stats.totalCount
    errorLogsCount.value = stats.levelCounts['Error'] || 0
    warningLogsCount.value = stats.levelCounts['Warning'] || 0
    infoLogsCount.value = stats.levelCounts['Information'] || 0

    // 统计数据加载后也尝试初始化图表
    void updateChart()
  } catch (error) {
    console.error(error)
  } finally {
    statisticsLoading.value = false
  }
}

const confirmCleanup = async () => {
  const msgRef = message.loading('清理日志中...')
  try {
    const result = await LogsService.cleanupOldLogs(cleanupDays.value)
    msgRef.destroy()
    message.success(result.message || '日志清理成功')
    showCleanupDialog.value = false
    await getLogs()
    await loadStatistics()
  } catch (error: any) {
    msgRef.destroy()
    message.error(error.message || '清理失败')
  }
}

// ---------------------------------------------------------
// ECharts Logic
// ---------------------------------------------------------
const initChart = () => {
  if (!chartDom.value) return
  chartInstance = echarts.init(chartDom.value)
  void updateChart() // 使用void忽略Promise返回值，避免未处理的Promise警告
  window.addEventListener('resize', resizeChart)
}

const resizeChart = () => chartInstance?.resize()

const updateChart = async () => {
  if (!chartInstance) return
  chartLoading.value = true

  try {
    // 解析时间参数，适配后端接口逻辑
    let timeRangeParam: string | undefined = selectedQuickTimeRange.value || undefined

    // 简单处理: 如果是 days 才去提取数字，否则传 original string
    if (timeRangeParam && timeRangeParam.endsWith('days') && !timeRangeParam.startsWith('today')) {
      const daysMatch = timeRangeParam.match(/^(\d+)days$/)
      if (daysMatch) {
        timeRangeParam = daysMatch[1]
      }
    }

    // 调用API获取真实数据
    const distribution = await LogsService.getLogDistribution(timeRangeParam || 'today')
    logDistributionData.value = distribution

    // 准备图表数据
    const timePoints = distribution.map(item => item.timePoint)
    const dataInfo = distribution.map(item => item.infoCount)
    const dataError = distribution.map(item => item.errorCount)
    const dataWarning = distribution.map(item => item.warningCount)

    // 主题颜色设置
    const isDark = document.documentElement.classList.contains('dark')
    const textColor = isDark ? '#94a3b8' : '#64748b'
    const splitLineColor = isDark ? '#334155' : '#e2e8f0'

    const option = {
      backgroundColor: 'transparent',
      tooltip: {
        trigger: 'axis',
        backgroundColor: isDark ? 'rgba(30, 41, 59, 0.9)' : 'rgba(255, 255, 255, 0.9)',
        borderColor: isDark ? '#475569' : '#e2e8f0',
        textStyle: {color: textColor},
        borderRadius: 8,
        padding: 12
      },
      legend: {
        bottom: 0,
        itemWidth: 8,
        itemHeight: 8,
        icon: 'circle',
        textStyle: {color: textColor},
        data: ['信息', '错误', '警告']
      },
      grid: {
        left: 10, right: 20, top: 30, bottom: 40, containLabel: true
      },
      xAxis: {
        type: 'category',
        boundaryGap: false,
        data: timePoints.length > 0 ? timePoints : Array.from({length: 12}, (_, i) => `${i * 2}:00`),
        axisLine: {show: false},
        axisTick: {show: false},
        axisLabel: {color: textColor, fontSize: 11}
      },
      yAxis: {
        type: 'value',
        splitLine: {lineStyle: {color: splitLineColor, type: 'dashed'}},
        axisLabel: {show: false} // Hide Y labels for cleaner look
      },
      series: [
        {
          name: '信息',
          type: 'line',
          smooth: true,
          showSymbol: false,
          lineStyle: {width: 3, color: '#10b981'}, // Emerald
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              {offset: 0, color: 'rgba(16, 185, 129, 0.4)'},
              {offset: 1, color: 'rgba(16, 185, 129, 0.01)'}
            ])
          },
          data: dataInfo.length > 0 ? dataInfo : Array.from({length: 12}, () => 0)
        },
        {
          name: '错误',
          type: 'line',
          smooth: true,
          showSymbol: false,
          lineStyle: {width: 3, color: '#ef4444'}, // Red
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              {offset: 0, color: 'rgba(239, 68, 68, 0.4)'},
              {offset: 1, color: 'rgba(239, 68, 68, 0.01)'}
            ])
          },
          data: dataError.length > 0 ? dataError : Array.from({length: 12}, () => 0)
        },
        {
          name: '警告',
          type: 'line',
          smooth: true,
          showSymbol: false,
          lineStyle: {width: 3, color: '#f59e0b'}, // Amber
          areaStyle: {
            color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
              {offset: 0, color: 'rgba(245, 158, 11, 0.4)'},
              {offset: 1, color: 'rgba(245, 158, 11, 0.01)'}
            ])
          },
          data: dataWarning.length > 0 ? dataWarning : Array.from({length: 12}, () => 0)
        }
      ]
    }

    // @ts-ignore
    chartInstance.setOption(option)
  } catch (error) {
    console.error('Failed to update chart:', error)
    message.error('加载图表数据失败')
  } finally {
    chartLoading.value = false
  }
}

// ---------------------------------------------------------
// Lifecycle
// ---------------------------------------------------------
onMounted(async () => {
  layoutStore.setPageHeader('系统日志', '监控系统事件和健康状态')

  await getLogs()
  await loadStatistics()

  await nextTick(() => {
    initChart()
  })
})

onBeforeUnmount(() => {
  layoutStore.clearPageHeader()
  if (chartInstance) {
    chartInstance.dispose()
  }
  window.removeEventListener('resize', resizeChart)
})
</script>

<style scoped>
/* 通用基础样式补充 (Tailwind 4 原生支持 css 变量，这里做一些特定的覆盖) */

/* Apple Card 风格 */
.apple-card {
  background: white;
  border-radius: 1.5rem; /* rounded-3xl equivalent or 2xl */
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.02), 0 2px 4px -1px rgba(0, 0, 0, 0.02);
  transition: all 0.3s ease;
  border: 1px solid rgba(226, 232, 240, 0.8);
}

.dark .apple-card {
  background: #1e1e1e; /* Dark gray like iOS Settings dark mode */
  border-color: rgba(255, 255, 255, 0.08);
  box-shadow: none;
}

.apple-card:hover {
  box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.05), 0 4px 6px -2px rgba(0, 0, 0, 0.025);
}

/* 覆盖 NaiveUI 默认圆角，使其更符合 Apple 风格 */
.n-button, .n-input, .n-input-wrapper {
  border-radius: 10px !important;
}


.apple-select .n-base-selection {
  border-radius: 8px !important;
}

/* 自定义滚动条 */
.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}

.custom-scrollbar::-webkit-scrollbar-track {
  background: transparent;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: rgba(156, 163, 175, 0.3);
  border-radius: 20px;
}

.custom-scrollbar:hover::-webkit-scrollbar-thumb {
  background-color: rgba(156, 163, 175, 0.5);
}

/* 文字选择颜色 */
::selection {
  background-color: rgba(59, 130, 246, 0.2);
  color: inherit;
}
</style>