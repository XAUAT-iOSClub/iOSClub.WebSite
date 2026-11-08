<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useDialog, useMessage } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import FinanceTabs from './components/FinanceTabs.vue'
import { FinanceService } from '../../services/FinanceService'
import { useLayoutStore } from '../../stores/LayoutStore'
import type { FinanceFormKind, FinanceTemplate } from '../../models'

const message = useMessage()
const dialog = useDialog()
const layoutStore = useLayoutStore()

const templates = ref<FinanceTemplate[]>([])
const loading = ref(true)
const loadError = ref('')
const uploadingKind = ref<'' | FinanceFormKind>('')
const uploadInput = ref<HTMLInputElement>()
const pendingKind = ref<FinanceFormKind>('reimburse')

const applyTemplates = computed(() => templates.value.filter(item => item.kind === 'apply'))
const reimburseTemplates = computed(() => templates.value.filter(item => item.kind === 'reimburse'))

const sections = computed(() => [
  { kind: 'reimburse' as const, title: '经费报销单', list: reimburseTemplates.value },
  { kind: 'apply' as const, title: '社团经费申请表', list: applyTemplates.value }
])


const formatTime = (value?: string | null) => {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    templates.value = await FinanceService.templates()
  } catch (e: any) {
    loadError.value = e?.message || '模板列表加载失败'
  } finally {
    loading.value = false
  }
}

function pickFile(kind: FinanceFormKind) {
  pendingKind.value = kind
  uploadInput.value?.click()
}

async function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  if (!file.name.toLowerCase().endsWith('.docx')) {
    message.warning('模板必须为 .docx 文件')
    return
  }
  if (file.size > 5 * 1024 * 1024) {
    message.warning('模板不能超过 5MB')
    return
  }

  uploadingKind.value = pendingKind.value
  try {
    await FinanceService.uploadTemplate(file, pendingKind.value)
    message.success('模板已上传并启用')
    await load()
  } catch (e: any) {
    message.error(e?.detail || e?.message || '上传失败')
  } finally {
    uploadingKind.value = ''
  }
}

async function enable(template: FinanceTemplate) {
  try {
    await FinanceService.enableTemplate(template.id)
    message.success(`已启用版本 v${template.version}`)
    await load()
  } catch (e: any) {
    message.error(e?.message || '启用失败')
  }
}

function remove(template: FinanceTemplate) {
  dialog.warning({
    title: '删除模板',
    content: `确定删除 ${template.kindLabel} v${template.version} 吗？删除后若没有其他启用版本，将改用系统内置版式。`,
    positiveText: '删除',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await FinanceService.deleteTemplate(template.id)
        message.success('模板已删除')
        await load()
      } catch (e: any) {
        message.error(e?.message || '删除失败')
      }
    }
  })
}

onMounted(() => {
  
  layoutStore.setPageHeader('财务管理')
  layoutStore.setShowPageActions(false)
  void load()
})

onBeforeUnmount(() => layoutStore.clearPageHeader())
</script>

<template>
  <div class="finance-page">
    <FinanceTabs/>

    <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between mb-4">
      <div>
        <h2 class="text-xl font-semibold text-gray-900 dark:text-white">表单模板</h2>
        
      </div>
      <button class="finance-btn" @click="load"><Icon icon="refresh-cw" class="w-4 h-4"/>刷新</button>
    </div>

    <input ref="uploadInput" type="file" accept=".docx" class="hidden" @change="onFileChange"/>

    <div v-if="loading" class="grid grid-cols-1 lg:grid-cols-2 gap-4">
      <div v-for="i in 2" :key="i" class="finance-card h-64 animate-pulse"></div>
    </div>

    <div v-else-if="loadError" class="finance-card">
      <div class="finance-empty">
        <Icon icon="circle-alert" class="w-8 h-8 text-red-500"/>
        <p>{{ loadError }}</p>
        <button class="finance-btn primary" @click="load">重新加载</button>
      </div>
    </div>

    <template v-else>
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div v-for="section in sections" :key="section.kind" class="finance-card h-full min-h-[240px] flex flex-col">
          <div class="flex items-start justify-between gap-3 mb-3">
            <div>
              <span class="finance-card-title">{{ section.title }}</span>
            </div>
            <button class="finance-btn primary" :disabled="uploadingKind !== ''" @click="pickFile(section.kind)">
              <Icon icon="upload" class="w-4 h-4"/>{{ uploadingKind === section.kind ? '上传中…' : '上传模板' }}
            </button>
          </div>

          <div v-if="!section.list.length" class="finance-empty flex-1 py-10">
            <Icon icon="settings" class="w-8 h-8"/>
            <span>尚未上传模板，当前使用系统内置版式</span>
          </div>

          <div v-else class="space-y-2 flex-1">
            <div v-for="template in section.list" :key="template.id"
                 class="flex flex-wrap items-center gap-3 p-3 rounded-xl bg-black/[.02] dark:bg-white/[.04]">
              <div class="flex-1 min-w-[160px]">
                <div class="flex items-center gap-2">
                  <span class="text-sm font-medium text-gray-900 dark:text-gray-100">版本 v{{ template.version }}</span>
                  <span class="finance-tag" :class="template.enabled ? 'reimbursed' : 'deleted'">
                    {{ template.enabled ? '启用中' : '未启用' }}
                  </span>
                </div>
                <div class="finance-muted truncate">{{ template.originalFileName || '—' }}</div>
                <div class="finance-muted">上传于 {{ formatTime(template.createdAt) }}</div>
              </div>
              <button v-if="!template.enabled" class="finance-btn success" @click="enable(template)">启用</button>
              <button class="finance-btn danger" @click="remove(template)">删除</button>
            </div>
          </div>
        </div>
      </div>

      <div class="finance-card mt-4">
        <span class="finance-card-title">模板填写说明</span>
        <p class="finance-muted mt-2">
          学校固定模板无需占位符：系统按“社团名称 / 活动主题 / 支出项目 / 合计（小写）/ 合计（大写）”等标签和表头定位单元格并填写，签字、盖章栏保持空白。
        </p>
        <p class="finance-muted mt-2">
          也兼容带占位符的模板；模板的字体、行高、列宽、合并单元格和边框保持不变。
        </p>
      </div>
    </template>
  </div>
</template>
