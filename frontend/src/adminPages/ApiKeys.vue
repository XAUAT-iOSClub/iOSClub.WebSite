<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { NAlert, NButton, NModal, useDialog, useMessage } from 'naive-ui'
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
const formVersion = ref(0)
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
    formVersion.value++
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
  <div class="mx-auto grid max-w-5xl gap-4">
    <NAlert type="info">
      使用 HTTPS 和 HTTP Basic 访问资源接口。只读密钥可查询资源，读写密钥还可新增、编辑和删除资源。
      <code class="mt-2 block break-all">curl -u 'KEY_ID:SECRET' '{{ resourceUrl }}'</code>
    </NAlert>
    <ApiKeyForm :key="formVersion" :saving="saving" @create="create" />
    <NAlert v-if="loadFailed" type="error">
      无法读取密钥列表。
      <NButton text type="error" @click="load">重试</NButton>
    </NAlert>
    <ApiKeyList v-else :keys="keys" :loading="loading" :revoking="revoking" @revoke="confirmRevoke" />
    <NModal
      :show="credentials !== null" preset="card" title="保存 API Key"
      class="w-[min(34rem,calc(100%-2rem))]"
      :mask-closable="false" :close-on-esc="false" :closable="false"
    >
      <div v-if="credentials" class="grid gap-3">
        <p>密钥仅在创建时显示。请保存后再关闭窗口。</p>
        <code class="select-all break-all rounded border border-current/15 p-3">{{ credentials.id }}:{{ credentials.secret }}</code>
        <div class="flex flex-wrap justify-end gap-2">
          <NButton @click="copyCredentials">复制 Basic 凭据</NButton>
          <NButton type="primary" @click="credentials = null">完成</NButton>
        </div>
      </div>
    </NModal>
  </div>
</template>
