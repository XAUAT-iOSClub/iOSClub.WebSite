<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import Icon from '@/components/Icon.vue'
import { FinanceService } from '../../../services/FinanceService'

const props = defineProps<{
  invoiceId: string
  hasFile: boolean
  contentType?: string | null
  fileName?: string | null
}>()

const objectUrl = ref('')
const failed = ref(false)
const loading = ref(false)

const isImage = computed(() => {
  const type = (props.contentType || '').toLowerCase()
  if (type.startsWith('image/')) return true
  return /\.(jpe?g|png|webp|bmp)$/i.test(props.fileName || '')
})

function revoke() {
  if (objectUrl.value) {
    URL.revokeObjectURL(objectUrl.value)
    objectUrl.value = ''
  }
}

async function load() {
  revoke()
  failed.value = false
  if (!props.hasFile) return

  loading.value = true
  try {
    objectUrl.value = await FinanceService.invoiceObjectUrl(props.invoiceId)
  } catch {
    failed.value = true
  } finally {
    loading.value = false
  }
}

watch(() => [props.invoiceId, props.hasFile], load, { immediate: true })
onBeforeUnmount(revoke)
</script>

<template>
  <a v-if="objectUrl && isImage" :href="objectUrl" target="_blank" rel="noopener"
     class="block w-11 h-11 rounded-lg overflow-hidden border border-black/5 dark:border-white/10 bg-white dark:bg-white/5"
     title="查看发票原件">
    <img :src="objectUrl" alt="发票缩略图" class="w-full h-full object-cover"/>
  </a>
  <a v-else-if="objectUrl" :href="objectUrl" target="_blank" rel="noopener"
     class="w-11 h-11 rounded-lg flex items-center justify-center bg-blue-50 dark:bg-blue-500/15 text-blue-600 dark:text-blue-300"
     title="打开发票原件（非图片格式）">
    <Icon icon="file-text" class="w-5 h-5"/>
  </a>
  <div v-else class="w-11 h-11 rounded-lg flex items-center justify-center bg-gray-100 dark:bg-white/10"
       :class="{ 'animate-pulse': loading }">
    <Icon :icon="failed ? 'circle-alert' : 'file-text'" class="w-5 h-5 text-gray-400"/>
  </div>
</template>
