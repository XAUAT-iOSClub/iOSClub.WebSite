<script setup lang="ts">
import { NButton, NSpin } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import { apiKeyPermissions, type ApiKeySummary } from '@/services/ApiKeyService'

defineProps<{ keys: ApiKeySummary[], loading: boolean, revoking: string | null }>()
const emit = defineEmits<{ revoke: [key: ApiKeySummary] }>()
const formatDate = new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' })
</script>

<template>
  <div v-if="loading && !keys.length" role="status" class="grid min-h-52 place-content-center justify-items-center gap-3">
    <NSpin size="small" />
    <p class="text-sm text-gray-600 dark:text-gray-400">正在读取密钥</p>
  </div>
  <div v-else-if="!keys.length" class="grid min-h-52 place-content-center justify-items-center gap-3 px-5 py-8 text-center">
    <span class="grid size-12 place-items-center rounded-2xl bg-[#f5f5f7] text-gray-500 dark:bg-white/8 dark:text-gray-400">
      <Icon icon="key" :size="22" />
    </span>
    <p class="text-sm font-semibold">暂无 API Key</p>
    <p class="text-sm text-gray-600 dark:text-gray-400">创建密钥以授予外部服务访问权限。</p>
  </div>
  <NSpin v-else :show="loading">
    <ul class="divide-y divide-black/8 px-5 dark:divide-white/10">
      <li v-for="key in keys" :key="key.id" class="grid grid-cols-[minmax(0,1fr)_auto] items-start gap-3 py-4">
        <div class="grid min-w-0 gap-1.5">
          <div class="flex flex-wrap items-center gap-2">
            <span class="min-w-0 break-all text-sm font-semibold">{{ key.name }}</span>
            <span class="rounded-full bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-700 dark:bg-blue-400/12 dark:text-blue-300">
              {{ apiKeyPermissions.find(option => option.value === key.permission)?.label }}
            </span>
            <span
              v-if="key.expiresAt && Date.parse(key.expiresAt) <= Date.now()"
              class="rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-400/12 dark:text-amber-300"
            >已过期</span>
          </div>
          <p class="flex flex-wrap gap-x-3 gap-y-0.5 text-xs leading-relaxed text-gray-600 dark:text-gray-400">
            <span>{{ key.expiresAt ? `${formatDate.format(new Date(key.expiresAt))} 过期` : '永不过期' }}</span>
            <span>{{ formatDate.format(new Date(key.createdAt)) }} 创建</span>
          </p>
          <code class="select-all break-all text-xs text-gray-600 dark:text-gray-400">{{ key.id }}</code>
        </div>
        <NButton
          round size="small" type="error" secondary
          :loading="revoking === key.id" :disabled="revoking !== null"
          :aria-label="`撤销 ${key.name}`"
          @click="emit('revoke', key)"
        >撤销</NButton>
      </li>
    </ul>
  </NSpin>
</template>
