<script setup lang="ts">
import AdminModal from '@/components/admin/AdminModal.vue'
import { computed } from 'vue'

import type { OperationLogItem } from '@/services/OperationLogService'

const show = defineModel<boolean>('show', { required: true })
const props = defineProps<{ item: OperationLogItem | null, date: string, role: string }>()

const changeContent = computed(() => {
  const value = props.item?.changeContent
  if (!value) return '无变更内容'

  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
})
</script>

<template>
  <AdminModal
    v-model:show="show"

    title="操作详情"
    aria-label="操作详情"
    style="--admin-modal-width: 48rem"
    :bordered="false"
  >
    <div v-if="item" class="max-h-[70dvh] space-y-5 overflow-y-auto text-sm">
      <dl class="grid grid-cols-1 gap-px overflow-hidden rounded-2xl bg-black/8 sm:grid-cols-2 dark:bg-white/10">
        <div class="detail-cell">
          <dt>操作人</dt>
          <dd>{{ item.operatorName || item.operatorId || '未知' }}<span v-if="role" class="detail-secondary">（{{ role }}）</span></dd>
        </div>
        <div class="detail-cell">
          <dt>时间</dt>
          <dd><time :datetime="item.createdAt">{{ date }}</time></dd>
        </div>
        <div class="detail-cell">
          <dt>模块 / 操作</dt>
          <dd>{{ item.module }} / {{ item.action }}</dd>
        </div>
        <div class="detail-cell">
          <dt>结果</dt>
          <dd>
            <span :class="item.success ? 'text-emerald-700 dark:text-emerald-300' : 'text-red-700 dark:text-red-300'">
              {{ item.success ? '成功' : '失败' }}
            </span>
            <span class="detail-secondary"> · HTTP {{ item.httpStatus }} · {{ item.durationMs }} ms</span>
          </dd>
        </div>
        <div class="detail-cell">
          <dt>接口</dt>
          <dd class="break-all font-mono text-xs">{{ item.method }} {{ item.path }}</dd>
        </div>
        <div class="detail-cell">
          <dt>IP / TraceId</dt>
          <dd class="break-all font-mono text-xs">{{ item.ipAddress || '-' }} / {{ item.traceId || '-' }}</dd>
        </div>
      </dl>

      <div v-if="item.message">
        <h3 class="detail-heading">响应消息</h3>
        <p class="break-words">{{ item.message }}</p>
      </div>
      <div v-if="item.errorMessage">
        <h3 class="detail-heading">异常信息</h3>
        <p class="break-words text-red-700 dark:text-red-300">{{ item.errorMessage }}</p>
      </div>
      <div>
        <h3 class="detail-heading">变更内容（已脱敏）</h3>
        <pre class="max-h-64 overflow-auto rounded-2xl bg-[#f5f5f7] p-4 text-xs leading-relaxed whitespace-pre-wrap break-all dark:bg-white/6">{{ changeContent }}</pre>
      </div>
      <div v-if="item.userAgent">
        <h3 class="detail-heading">User-Agent</h3>
        <p class="detail-secondary break-all">{{ item.userAgent }}</p>
      </div>
    </div>
  </AdminModal>
</template>

<style scoped>
@reference "../../style.css";

.detail-cell {
  @apply grid content-start gap-1 bg-[#f5f5f7] px-4 py-3 break-words dark:bg-[#28282a];
}

.detail-cell dt,
.detail-secondary {
  @apply text-xs text-gray-600 dark:text-gray-400;
}

.detail-heading {
  @apply mb-2 text-sm font-semibold;
}
</style>
