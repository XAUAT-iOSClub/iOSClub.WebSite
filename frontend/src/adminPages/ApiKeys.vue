<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { NButton, NModal, useDialog, useMessage } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import ApiKeyForm from '@/components/ApiKeys/ApiKeyForm.vue'
import ApiKeyList from '@/components/ApiKeys/ApiKeyList.vue'
import { apiKeyService, type ApiKeyCredentials, type ApiKeySummary, type CreateApiKey } from '@/services/ApiKeyService'
import { useLayoutStore } from '@/stores/LayoutStore'
import { url } from '@/services/Url'

const keys = ref<ApiKeySummary[]>([])
const loading = ref(false)
const loadFailed = ref(false)
const saving = ref(false)
const revoking = ref<string | null>(null)
const credentials = ref<ApiKeyCredentials | null>(null)
const showCreateDialog = ref(false)
const layout = useLayoutStore()
const dialog = useDialog()
const message = useMessage()
const resourceUrl = new URL(`${url}/Resource`, window.location.origin).href

async function load() {
  loading.value = true
  loadFailed.value = false
  try {
    keys.value = await apiKeyService.list()
  } catch {
    loadFailed.value = true
  } finally {
    loading.value = false
  }
}

async function create(request: CreateApiKey) {
  if (saving.value) return
  saving.value = true
  try {
    credentials.value = await apiKeyService.create(request)
    showCreateDialog.value = false
    await load()
  } catch {
    // apiRequest displays the server error.
  } finally {
    saving.value = false
  }
}

function confirmRevoke(key: ApiKeySummary) {
  dialog.warning({
    title: '撤销 API Key',
    content: `撤销「${key.name}」后，使用该密钥的服务将无法继续访问资源接口。`,
    positiveText: '撤销',
    negativeText: '取消',
    async onPositiveClick() {
      revoking.value = key.id
      try {
        await apiKeyService.revoke(key.id)
        keys.value = keys.value.filter(item => item.id !== key.id)
      } catch {
        return false
      } finally {
        revoking.value = null
      }
    },
  })
}

async function copyCredentials() {
  if (!credentials.value) return
  try {
    await navigator.clipboard.writeText(`${credentials.value.id}:${credentials.value.secret}`)
    message.success('已复制 Basic 凭据')
  } catch {
    message.error('复制失败，请手动选中凭据复制')
  }
}

onMounted(() => {
  layout.setPageHeader('API Key 管理', '为外部服务授予社团资源接口的访问权限')
  void load()
})
</script>

<template>
  <div class="mx-auto grid max-w-6xl gap-5 px-4 py-5 sm:px-6 text-[#1d1d1f] dark:text-[#f5f5f7]">
    <section aria-labelledby="key-list-heading" class="settings-card">
      <header class="flex flex-wrap items-center justify-between gap-3 border-b border-black/8 p-5 dark:border-white/10">
        <div class="flex items-center gap-3">
          <span class="settings-icon bg-indigo-50 text-indigo-700 dark:bg-indigo-400/15 dark:text-indigo-300">
            <Icon icon="key" :size="20" />
          </span>
          <h2 id="key-list-heading" class="text-lg font-semibold tracking-tight">已创建的密钥</h2>
          <span
            v-if="!loading && !loadFailed"
            class="rounded-full bg-black/5 px-2.5 py-1 text-xs font-medium tabular-nums dark:bg-white/10"
          >{{ keys.length }} 个</span>
        </div>
        <NButton round color="#0071e3" text-color="#fff" aria-haspopup="dialog" @click="showCreateDialog = true">
          <template #icon><Icon icon="plus" /></template>
          创建密钥
        </NButton>
      </header>
      <div v-if="loadFailed" class="flex flex-wrap items-center justify-between gap-3 p-5">
        <p role="alert" class="text-sm text-red-700 dark:text-red-300">无法读取密钥列表。</p>
        <NButton round size="small" @click="load">重试</NButton>
      </div>
      <ApiKeyList v-else :keys="keys" :loading="loading" :revoking="revoking" @revoke="confirmRevoke" />
    </section>

    <section aria-labelledby="api-access-heading" class="settings-card p-5">
      <div class="flex items-center gap-3">
        <span class="settings-icon bg-sky-50 text-sky-700 dark:bg-sky-400/15 dark:text-sky-300">
          <Icon icon="lock" :size="19" />
        </span>
        <h2 id="api-access-heading" class="text-base font-semibold tracking-tight">访问资源接口</h2>
      </div>
      <p class="mt-3 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
        使用 HTTPS 和 HTTP Basic 认证。只读密钥可查询资源，读写密钥还可新增、编辑和删除资源。
      </p>
      <code class="mt-3 block select-all break-all rounded-xl bg-[#f5f5f7] px-4 py-3 text-xs leading-relaxed dark:bg-black/25">curl -u 'KEY_ID:SECRET' '{{ resourceUrl }}'</code>
    </section>

    <NModal
      v-model:show="showCreateDialog"
      preset="dialog"
      title="创建密钥"
      aria-label="创建密钥"
      class="w-[calc(100%-2rem)] max-w-lg rounded-3xl!"
      :show-icon="false"
      :mask-closable="false"
      :close-on-esc="!saving"
      :closable="!saving"
    >
      <ApiKeyForm :saving="saving" @create="create" @cancel="showCreateDialog = false" />
    </NModal>

    <NModal
      :show="credentials !== null" preset="card" title="保存 API Key"
      class="w-[calc(100%-2rem)] max-w-lg rounded-3xl!"
      :mask-closable="false" :close-on-esc="false" :closable="false"
    >
      <div v-if="credentials" class="grid gap-4">
        <p class="text-sm text-gray-600 dark:text-gray-400">密钥仅在创建时显示。请保存后再关闭窗口。</p>
        <code class="select-all break-all rounded-xl bg-[#f5f5f7] p-4 text-sm dark:bg-black/25">{{ credentials.id }}:{{ credentials.secret }}</code>
        <div class="flex flex-wrap justify-end gap-2">
          <NButton round @click="copyCredentials">
            <template #icon><Icon icon="copy" /></template>
            复制 Basic 凭据
          </NButton>
          <NButton round color="#0071e3" text-color="#fff" @click="credentials = null">完成</NButton>
        </div>
      </div>
    </NModal>
  </div>
</template>

<style scoped>
@reference "../style.css";

.settings-card {
  @apply min-w-0 rounded-3xl border border-black/8 bg-white shadow-sm dark:border-white/10 dark:bg-[#1c1c1e];
}

.settings-icon {
  @apply grid size-10 shrink-0 place-items-center rounded-xl;
}
</style>
