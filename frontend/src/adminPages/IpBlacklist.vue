<template>
  <div class="ip-blacklist-container min-h-screen transition-colors duration-300">
    <main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <div class="mb-8 px-2">
        <h2 class="text-3xl font-bold tracking-tight text-slate-900 dark:text-white text-effect">
          概览
        </h2>
      </div>

      <!-- 统计概览卡片 -->
      <div v-if="!statsLoading" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 mb-8">
        <!-- 精确IP数量 -->
        <div class="apple-card group relative overflow-hidden p-6">
          <div class="flex items-start justify-between">
            <div class="flex flex-col">
              <span class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">
                精确 IP
              </span>
              <span class="text-3xl font-bold tracking-tight text-gray-900 dark:text-white font-display">
                {{ stats?.totalIps || 0 }}
              </span>
            </div>
            <div
                class="w-10 h-10 rounded-xl bg-red-100 dark:bg-red-900/40 flex items-center justify-center shadow-sm transition-transform group-hover:scale-110">
              <Icon icon="network" class="w-6 h-6 text-red-600 dark:text-red-400"/>
            </div>
          </div>
        </div>

        <!-- CIDR范围数量 -->
        <div class="apple-card group relative overflow-hidden p-6">
          <div class="flex items-start justify-between">
            <div class="flex flex-col">
              <span class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">
                CIDR 范围
              </span>
              <span class="text-3xl font-bold tracking-tight text-gray-900 dark:text-white font-display">
                {{ stats?.totalCidrRanges || 0 }}
              </span>
            </div>
            <div
                class="w-10 h-10 rounded-xl bg-orange-100 dark:bg-orange-900/40 flex items-center justify-center shadow-sm transition-transform group-hover:scale-110">
              <Icon icon="network" class="w-6 h-6 text-orange-600 dark:text-orange-400"/>
            </div>
          </div>
        </div>

        <!-- 拦截次数 -->
        <div class="apple-card group relative overflow-hidden p-6">
          <div class="flex items-start justify-between">
            <div class="flex flex-col">
              <span class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-1">
                拦截次数
              </span>
              <span class="text-3xl font-bold tracking-tight text-gray-900 dark:text-white font-display">
                {{ formatNumber(stats?.blacklistHits || 0) }}
              </span>
            </div>
            <div
                class="w-10 h-10 rounded-xl bg-blue-100 dark:bg-blue-900/40 flex items-center justify-center shadow-sm transition-transform group-hover:scale-110">
              <Icon icon="shield-check" class="w-6 h-6 text-blue-600 dark:text-blue-400"/>
            </div>
          </div>
        </div>
      </div>

      <!-- 缓存性能图表 -->
      <div class="apple-card p-6 mb-8">
        <div v-if="!statsLoading && stats" class="flex items-center justify-between mb-6">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-xl bg-purple-100 dark:bg-purple-900/40 flex items-center justify-center">
              <Icon icon="chart-line" class="w-5 h-5 text-purple-600 dark:text-purple-400"/>
            </div>
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white">缓存性能</h3>
          </div>
          <span class="text-sm text-gray-500 dark:text-gray-400">
            命中率: {{ cacheHitRate }}%
          </span>
        </div>
        <div ref="cacheChartRef" class="h-64"></div>
      </div>

      <!-- 操作区域 -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
        <!-- 添加IP -->
        <div class="apple-card p-6">
          <div class="flex items-center gap-3 mb-4">
            <div class="w-8 h-8 rounded-xl bg-green-100 dark:bg-green-900/40 flex items-center justify-center">
              <Icon icon="circle-plus" class="w-5 h-5 text-green-600 dark:text-green-400"/>
            </div>
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white">添加黑名单</h3>
          </div>

          <n-form ref="addFormRef" :model="addForm" :rules="addRules">
            <n-form-item path="ip" label="IP 地址或 CIDR">
              <n-input
                  v-model:value="addForm.ip"
                  placeholder="例如：192.168.1.100 或 192.168.1.0/24"
                  :input-props="{ class: 'apple-input' }"
              />
            </n-form-item>
            <n-form-item path="reason" label="封禁原因">
              <n-input
                  v-model:value="addForm.reason"
                  type="textarea"
                  placeholder="请输入封禁原因..."
                  :autosize="{ minRows: 2, maxRows: 4 }"
              />
            </n-form-item>
            <n-button
                type="primary"
                :loading="addLoading"
                @click="handleAdd"
                class="w-full apple-button"
            >
              <template #icon>
                <Icon icon="shield-plus"/>
              </template>
              添加到黑名单
            </n-button>
          </n-form>
        </div>

        <!-- 移除IP -->
        <div class="apple-card p-6">
          <div class="flex items-center gap-3 mb-4">
            <div class="w-8 h-8 rounded-xl bg-red-100 dark:bg-red-900/40 flex items-center justify-center">
              <Icon icon="circle-minus" class="w-5 h-5 text-red-600 dark:text-red-400"/>
            </div>
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white">移除黑名单</h3>
          </div>

          <n-form ref="removeFormRef" :model="removeForm" :rules="removeRules">
            <n-form-item path="ip" label="IP 地址或 CIDR">
              <n-input
                  v-model:value="removeForm.ip"
                  placeholder="例如：192.168.1.100 或 192.168.1.0/24"
              />
            </n-form-item>
            <n-form-item path="reason" label="移除原因">
              <n-input
                  v-model:value="removeForm.reason"
                  type="textarea"
                  placeholder="请输入移除原因..."
                  :autosize="{ minRows: 2, maxRows: 4 }"
              />
            </n-form-item>
            <n-button
                type="error"
                :loading="removeLoading"
                @click="handleRemove"
                class="w-full apple-button"
            >
              <template #icon>
                <Icon icon="shield-minus"/>
              </template>
              从黑名单移除
            </n-button>
          </n-form>
        </div>
      </div>

      <!-- IP检查 -->
      <div class="apple-card p-6 mb-8">
        <div class="flex items-center gap-3 mb-4">
          <div class="w-8 h-8 rounded-xl bg-blue-100 dark:bg-blue-900/40 flex items-center justify-center">
            <Icon icon="search" class="w-5 h-5 text-blue-600 dark:text-blue-400"/>
          </div>
          <h3 class="text-lg font-semibold text-gray-900 dark:text-white">检查 IP</h3>
        </div>

        <div class="flex gap-3">
          <n-input
              v-model:value="checkIp"
              placeholder="输入IP地址进行检查..."
              class="flex-1"
              @keydown.enter="handleCheck"
          />
          <n-button
              type="info"
              :loading="checkLoading"
              @click="handleCheck"
              class="apple-button"
          >
            <template #icon>
              <Icon icon="circle-check"/>
            </template>
            检查
          </n-button>
        </div>

        <!-- 检查结果 -->
        <div v-if="checkResult" class="mt-4 p-4 rounded-xl transition-colors"
             :class="checkResult.isBlacklisted
               ? 'bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800'
               : 'bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800'">
          <div class="flex items-center gap-3">
            <Icon
                :icon="checkResult.isBlacklisted ? 'shield-alert' : 'shield-check'"
                morph
                class="w-6 h-6"
                :class="checkResult.isBlacklisted ? 'text-red-600 dark:text-red-400' : 'text-green-600 dark:text-green-400'"
            />
            <div>
              <p class="font-semibold"
                 :class="checkResult.isBlacklisted ? 'text-red-900 dark:text-red-100' : 'text-green-900 dark:text-green-100'">
                {{ checkResult.isBlacklisted ? '已在黑名单中' : '不在黑名单中' }}
              </p>
              <p class="text-sm mt-1"
                 :class="checkResult.isBlacklisted ? 'text-red-700 dark:text-red-300' : 'text-green-700 dark:text-green-300'">
                检查时间: {{ formatDateTime(checkResult.checkTime) }}
              </p>
            </div>
          </div>
        </div>
      </div>

      <!-- 操作按钮组 -->
      <div class="flex flex-wrap gap-3">
        <n-button
            type="warning"
            :loading="refreshLoading"
            @click="handleRefresh"
            class="apple-button"
        >
          <template #icon>
            <Icon icon="refresh-cw"/>
          </template>
          刷新缓存
        </n-button>

        <n-button
            type="info"
            @click="loadStats"
            class="apple-button"
        >
          <template #icon>
            <Icon icon="rotate-cw"/>
          </template>
          重新加载统计
        </n-button>
      </div>

    </main>
  </div>
</template>

<script setup lang="ts">
import {ref, onMounted, computed, watch} from 'vue';
import {useMessage, NInput, NButton, NForm, NFormItem} from 'naive-ui';
import Icon from '@/components/Icon.vue'
import * as echarts from 'echarts'
import {IpBlacklistService, type BlacklistStats, type IpCheckResult} from '../services/IpBlacklistService';
import {useThemeStore} from '../stores/theme';
import {useLayoutStore} from "../stores/LayoutStore";

const message = useMessage();
const layoutStore = useLayoutStore();
const useDark = useThemeStore();
const isDark = computed(() => useDark.isDark);

// 数据
const stats = ref<BlacklistStats | null>(null);
const statsLoading = ref(true);
const cacheChartRef = ref<HTMLElement>();
let cacheChart: echarts.ECharts | null = null;

// 表单
const addForm = ref({ip: '', reason: ''});
const removeForm = ref({ip: '', reason: ''});
const addFormRef = ref();
const removeFormRef = ref();
const addLoading = ref(false);
const removeLoading = ref(false);

// IP检查
const checkIp = ref('');
const checkResult = ref<IpCheckResult | null>(null);
const checkLoading = ref(false);

// 刷新缓存
const refreshLoading = ref(false);

// 表单验证规则
const addRules = {
  ip: {
    required: true,
    message: '请输入IP地址或CIDR范围',
    trigger: 'blur'
  }
};

const removeRules = {
  ip: {
    required: true,
    message: '请输入IP地址或CIDR范围',
    trigger: 'blur'
  }
};

// 计算缓存命中率
const cacheHitRate = computed(() => {
  if (!stats.value) return '0.00';
  const total = stats.value.cacheHits + stats.value.cacheMisses;
  if (total === 0) return '0.00';
  return ((stats.value.cacheHits / total) * 100).toFixed(2);
});

// 格式化数字
const formatNumber = (num: number): string => {
  return num.toLocaleString('zh-CN');
};

// 格式化日期时间
const formatDateTime = (dateStr: string): string => {
  const date = new Date(dateStr);
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  });
};

// 加载统计数据
const loadStats = async () => {
  try {
    statsLoading.value = true;
    stats.value = await IpBlacklistService.getStats();

    // 渲染图表
    renderCacheChart();
  } catch (error: any) {
    console.error('加载统计数据失败:', error);
    message.error(error.message || '加载统计数据失败');
  } finally {
    statsLoading.value = false;
  }
};

// 渲染缓存性能图表
const renderCacheChart = () => {
  if (!cacheChartRef.value) return
  cacheChart = echarts.init(cacheChartRef.value)

  const option = {
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'item',
      formatter: '{a} <br/>{b}: {c} ({d}%)'
    },
    legend: {
      bottom: '0%',
      left: 'center',
      textStyle: {
        color: isDark.value ? '#9ca3af' : '#6b7280'
      }
    },
    series: [
      {
        name: '缓存统计',
        type: 'pie',
        radius: ['40%', '70%'],
        avoidLabelOverlap: false,
        itemStyle: {
          borderRadius: 10,
          borderColor: isDark.value ? '#1f2937' : '#ffffff',
          borderWidth: 2
        },
        label: {
          show: false,
          position: 'center'
        },
        emphasis: {
          label: {
            show: true,
            fontSize: 20,
            fontWeight: 'bold',
            color: isDark.value ? '#ffffff' : '#111827'
          }
        },
        labelLine: {
          show: false
        },
        data: [
          {
            value: stats.value!.cacheHits,
            name: '缓存命中',
            itemStyle: {color: '#10b981'}
          },
          {
            value: stats.value!.cacheMisses,
            name: '缓存未命中',
            itemStyle: {color: '#ef4444'}
          }
        ]
      }
    ]
  };

  cacheChart.setOption(option);
  console.log(option)
  console.log(stats.value)

  // 响应式调整
  window.addEventListener('resize', () => {
    cacheChart?.resize();
  });
};

// 监听暗黑模式变化，更新图表
watch(isDark, () => {
  if (stats.value) {
    renderCacheChart();
  }
});

// 添加IP
const handleAdd = async () => {
  try {
    await addFormRef.value?.validate();
    addLoading.value = true;

    await IpBlacklistService.addToBlacklist(addForm.value);
    message.success('添加成功');

    // 重置表单
    addForm.value = {ip: '', reason: ''};

    // 重新加载统计
    await loadStats();
  } catch (error: any) {
    if (error.message) {
      message.error(error.message);
    }
  } finally {
    addLoading.value = false;
  }
};

// 移除IP
const handleRemove = async () => {
  try {
    await removeFormRef.value?.validate();
    removeLoading.value = true;

    await IpBlacklistService.removeFromBlacklist(removeForm.value);
    message.success('移除成功');

    // 重置表单
    removeForm.value = {ip: '', reason: ''};

    // 重新加载统计
    await loadStats();
  } catch (error: any) {
    if (error.message) {
      message.error(error.message);
    }
  } finally {
    removeLoading.value = false;
  }
};

// 检查IP
const handleCheck = async () => {
  if (!checkIp.value.trim()) {
    message.warning('请输入IP地址');
    return;
  }

  try {
    checkLoading.value = true;
    checkResult.value = await IpBlacklistService.checkIp(checkIp.value);
  } catch (error: any) {
    message.error(error.message || '检查失败');
  } finally {
    checkLoading.value = false;
  }
};

// 刷新缓存
const handleRefresh = async () => {
  try {
    refreshLoading.value = true;
    await IpBlacklistService.refreshBlacklist();
    message.success('缓存刷新成功');

    // 重新加载统计
    await loadStats();
  } catch (error: any) {
    message.error(error.message || '刷新失败');
  } finally {
    refreshLoading.value = false;
  }
};

// 组件挂载时加载数据
onMounted(() => {
  loadStats();
  layoutStore.setPageHeader('IP 黑名单管理', '管理和监控IP访问黑名单，保护系统安全');
});
</script>

<style scoped>
@reference 'tailwindcss';

/* 苹果风格卡片 */
.apple-card {
  @apply bg-white rounded-2xl shadow-sm hover:shadow-md
  transition-all duration-300 border border-gray-100 ;
}

.dark .apple-card {
  @apply bg-gray-800 border-gray-700 ;
}

/* 苹果风格按钮 */
:deep(.apple-button) {
  @apply rounded-xl font-medium transition-all duration-200 shadow-sm;
}

:deep(.apple-button:hover) {
  @apply shadow-md;
}

/* 文字效果 */
.text-effect {
  @apply bg-clip-text;
}

/* 字体 */
.font-display {
  font-family: -apple-system, BlinkMacSystemFont, 'SF Pro Display', 'Segoe UI', sans-serif;
}

/* 图表容器 */
:deep(.echarts-container) {
  @apply rounded-xl overflow-hidden;
}

/* 输入框样式 */
:deep(.n-input) {
  @apply rounded-xl;
}

:deep(.n-input__input-el) {
  @apply px-4 py-3;
}

/* 响应式调整 */
@media (max-width: 640px) {
  .apple-card {
    @apply rounded-xl;
  }
}
</style>
