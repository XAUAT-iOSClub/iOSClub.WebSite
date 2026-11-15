<script setup lang="ts">
import { ref } from 'vue'
import { NButton, NCard, NInput, NSelect, NSwitch, useMessage } from 'naive-ui'
import { apiKeyPermissions, type ApiKeyPermission, type CreateApiKey } from '@/services/ApiKeyService'

defineProps<{ saving: boolean }>()
const emit = defineEmits<{ create: [request: CreateApiKey] }>()
const name = ref('')
const permission = ref<ApiKeyPermission>(1)
const neverExpires = ref(false)
const expiry = ref('')
const message = useMessage()

function submit() {
  const expiration = neverExpires.value ? null : new Date(expiry.value)
  if (expiration && !(expiration.getTime() > Date.now())) {
    message.error('请选择晚于当前时间的过期日期')
    return
  }

  emit('create', {
    name: name.value.trim(),
    permission: permission.value,
    expiresAt: expiration?.toISOString() ?? null,
  })
}
</script>

<template>
  <NCard title="创建 API Key">
    <form class="grid gap-4 md:grid-cols-2" @submit.prevent="submit">
      <label class="grid gap-2">
        <span>名称</span>
        <NInput v-model:value="name" :maxlength="100" placeholder="例如资源同步服务" />
      </label>
      <label class="grid gap-2">
        <span>权限等级</span>
        <NSelect v-model:value="permission" :options="[...apiKeyPermissions]" />
      </label>
      <label class="flex items-center gap-3 md:col-span-2">
        <NSwitch v-model:value="neverExpires" />
        <span>永不过期</span>
      </label>
      <label v-if="!neverExpires" class="grid gap-2">
        <span>过期日期与时间（本地时区）</span>
        <input
          v-model="expiry"
          class="w-full rounded border border-current/20 bg-transparent px-3 py-2"
          type="datetime-local"
          required
        >
      </label>
      <div class="md:col-span-2">
        <NButton attr-type="submit" type="primary" :loading="saving" :disabled="!name.trim()">创建密钥</NButton>
      </div>
    </form>
  </NCard>
</template>
