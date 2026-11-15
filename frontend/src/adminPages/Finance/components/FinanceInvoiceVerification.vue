<script setup lang="ts">
import { onBeforeUnmount, shallowRef, useTemplateRef } from 'vue'
import type { FinanceInvoice } from '../../../models'
import { FinanceService } from '../../../services/FinanceService'

const props = defineProps<{
  invoice: FinanceInvoice
  uploadAllowed: boolean
  uploadHint: string
  uploadedAt: string
  disabled: boolean
}>()

const emit = defineEmits<{
  uploaded: [invoice: FinanceInvoice]
  busy: [value: boolean]
}>()

const fileInput = useTemplateRef<HTMLInputElement>('fileInput')
const error = shallowRef('')
const busy = shallowRef(false)
const previewUrl = shallowRef('')
let active = true

function closePreview() {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value)
  previewUrl.value = ''
}

onBeforeUnmount(() => {
  active = false
  closePreview()
})

async function run(action: () => Promise<void>, fallback: string) {
  if (busy.value || props.disabled) return
  busy.value = true
  emit('busy', true)
  error.value = ''
  try {
    await action()
  } catch (cause: unknown) {
    error.value = cause instanceof Error ? cause.message : fallback
  } finally {
    busy.value = false
    emit('busy', false)
  }
}

function upload(event: Event) {
  if (!(event.target instanceof HTMLInputElement)) return
  const file = event.target.files?.[0]
  event.target.value = ''
  if (!file || !props.uploadAllowed) return
  if (!/\.pdf$/i.test(file.name)) {
    error.value = '请选择税局查验结果 PDF。'
    return
  }

  void run(async () => {
    const invoice = await FinanceService.uploadInvoiceVerification(props.invoice.id, file)
    closePreview()
    emit('uploaded', invoice)
  }, '查验单上传失败')
}

function togglePreview() {
  if (previewUrl.value) {
    closePreview()
    return
  }
  void run(async () => {
    const href = await FinanceService.invoiceVerificationObjectUrl(props.invoice.id)
    if (active) previewUrl.value = href
    else URL.revokeObjectURL(href)
  }, '查验单预览失败')
}

function download() {
  void run(() => FinanceService.downloadInvoiceVerification(props.invoice.id), '查验单下载失败')
}
</script>

<template>
  <section aria-label="税局查验单" class="mt-4 space-y-2 border-t border-black/5 pt-4 dark:border-white/10">
    <div class="flex flex-wrap items-center gap-2">
      <span class="text-sm font-medium">税局查验单</span>
      <span class="finance-muted">
        {{ invoice.hasVerificationFile ? (uploadedAt ? `已归档 · ${uploadedAt}` : '已归档') : '未归档' }}
      </span>
    </div>
    <p v-if="uploadAllowed" class="finance-muted">在税局平台查验后，将结果另存为 PDF 并上传。</p>
    <p v-else-if="uploadHint" class="finance-muted">{{ uploadHint }}</p>
    <div class="flex flex-wrap items-center gap-2">
      <a class="finance-btn" href="https://inv-veri.chinatax.gov.cn/fpcygzfw/national-invoice-check"
         target="_blank" rel="noopener noreferrer">前往税局查验</a>
      <button v-if="uploadAllowed" type="button" class="finance-btn" :disabled="busy || disabled"
              @click="fileInput?.click()">{{ invoice.hasVerificationFile ? '替换查验单' : '上传查验单' }}</button>
      <button v-if="invoice.hasVerificationFile" type="button" class="finance-btn" :disabled="busy || disabled"
              @click="togglePreview">{{ previewUrl ? '关闭预览' : '预览查验单' }}</button>
      <button v-if="invoice.hasVerificationFile" type="button" class="finance-btn" :disabled="busy || disabled"
              @click="download">下载查验单</button>
      <input ref="fileInput" type="file" accept=".pdf,application/pdf" class="hidden" @change="upload"/>
    </div>
    <p v-if="busy" class="finance-muted" role="status">正在处理查验单…</p>
    <p v-if="error" class="finance-alert danger" role="alert">{{ error }}</p>
    <iframe v-if="previewUrl && invoice.hasVerificationFile" :src="previewUrl" title="税局查验单预览"
            class="h-96 w-full rounded-lg border border-black/5 dark:border-white/10"/>
  </section>
</template>
