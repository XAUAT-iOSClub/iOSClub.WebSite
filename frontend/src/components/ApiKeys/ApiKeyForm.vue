<script setup lang="ts">
import { ref } from 'vue'
import { NButton, NSwitch, useMessage } from 'naive-ui'
import { apiKeyPermissions, type ApiKeyPermission, type CreateApiKey } from '@/services/ApiKeyService'

defineProps<{ saving: boolean }>()
const emit = defineEmits<{ create: [request: CreateApiKey], cancel: [] }>()
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
  <form class="grid gap-5 pt-2" @submit.prevent="submit">
    <label class="grid gap-2 text-sm font-medium">
      名称
      <input
        v-model="name"
        class="settings-input"
        maxlength="100"
        placeholder="例如资源同步服务"
        required
      >
    </label>

    <fieldset>
      <legend class="mb-2 text-sm font-medium">权限等级</legend>
      <div class="grid grid-cols-2 gap-1 rounded-xl bg-[#f0f0f3] p-1 dark:bg-white/8">
        <label v-for="option in apiKeyPermissions" :key="option.value" class="relative cursor-pointer">
          <input v-model="permission" type="radio" name="permission" :value="option.value" class="peer sr-only">
          <span
            class="block rounded-lg px-2 py-2 text-center text-sm text-gray-600 peer-checked:bg-white peer-checked:font-semibold peer-checked:text-[#1d1d1f] peer-checked:shadow-sm peer-focus-visible:ring-2 peer-focus-visible:ring-blue-500 dark:text-gray-300 dark:peer-checked:bg-[#48484a] dark:peer-checked:text-white"
          >{{ option.label }}</span>
        </label>
      </div>
    </fieldset>

    <div class="grid gap-3 border-t border-black/8 pt-4 dark:border-white/10">
      <div class="flex min-h-8 items-center justify-between gap-3">
        <span id="key-expiry-switch-label" class="text-sm font-medium">永不过期</span>
        <NSwitch
          v-model:value="neverExpires"
          aria-labelledby="key-expiry-switch-label"
          :theme-overrides="{ railColorActive: '#0071e3' }"
        />
      </div>
      <label v-if="!neverExpires" class="grid gap-2 text-sm font-medium">
        过期时间（本地时区）
        <input v-model="expiry" class="settings-input dark:scheme-dark" type="datetime-local" required>
      </label>
    </div>

    <div class="flex flex-wrap justify-end gap-2">
      <NButton attr-type="button" round :disabled="saving" @click="emit('cancel')">取消</NButton>
      <NButton attr-type="submit" round color="#0071e3" text-color="#fff" :loading="saving" :disabled="!name.trim()">
        创建密钥
      </NButton>
    </div>
  </form>
</template>

<style scoped>
@reference "../../style.css";

.settings-input {
  @apply min-h-11 w-full min-w-0 rounded-xl border border-black/10 bg-[#f5f5f7] px-3 py-2 text-base font-normal outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/25 dark:border-white/10 dark:bg-white/6;
}
</style>
