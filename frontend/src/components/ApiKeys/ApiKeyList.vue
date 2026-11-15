<script setup lang="ts">
import { NButton, NCard, NEmpty, NSpin, NTag } from 'naive-ui'
import { apiKeyPermissions, type ApiKeySummary } from '@/services/ApiKeyService'

defineProps<{ keys: ApiKeySummary[], loading: boolean, revoking: string | null }>()
const emit = defineEmits<{ revoke: [key: ApiKeySummary] }>()
const formatDate = new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' })
</script>

<template>
  <NCard title="已创建的密钥">
    <NSpin :show="loading">
      <NEmpty v-if="!keys.length" description="暂无 API Key" />
      <ul v-else class="divide-y divide-current/10">
        <li v-for="key in keys" :key="key.id" class="flex flex-wrap items-center justify-between gap-3 py-3">
          <div class="min-w-0">
            <div class="flex flex-wrap items-center gap-2">
              <span class="break-all font-medium">{{ key.name }}</span>
              <NTag size="small">{{ apiKeyPermissions.find(option => option.value === key.permission)?.label }}</NTag>
              <NTag v-if="key.expiresAt && Date.parse(key.expiresAt) <= Date.now()" size="small" type="warning">已过期</NTag>
            </div>
            <p class="mt-1 text-sm opacity-70">
              {{ key.expiresAt ? `${formatDate.format(new Date(key.expiresAt))} 过期` : '永不过期' }}
              · {{ formatDate.format(new Date(key.createdAt)) }} 创建
            </p>
            <code class="break-all text-xs opacity-70">{{ key.id }}</code>
          </div>
          <NButton
            size="small" type="error" secondary
            :loading="revoking === key.id" :disabled="revoking !== null"
            @click="emit('revoke', key)"
          >撤销</NButton>
        </li>
      </ul>
    </NSpin>
  </NCard>
</template>
