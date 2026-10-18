<script setup lang="ts">
import { computed, h, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { NDataTable, NModal, NSelect, NSwitch, useMessage } from 'naive-ui'
import type { DataTableColumns } from 'naive-ui'
import { Icon } from '@iconify/vue'
import QRCode from 'qrcode'
import { ActivityService } from '../../services/ActivityService'
import { InfoService } from '../../services/InfoService'
import { useLayoutStore } from '../../stores/LayoutStore'
import type { ActivityModel, ActivityStatusValue, ParticipantModel, ParticipantPage, ImportResult, ActivityOperationLog } from '../../models'

const route = useRoute()
const router = useRouter()
const message = useMessage()
const layoutStore = useLayoutStore()

const activityId = route.params.id as string
const activity = ref<ActivityModel | null>(null)
const academies = ref<string[]>([])
const academyOptions = computed(() => academies.value.map(a => ({ label: a, value: a })))
const logs = ref<ActivityOperationLog[]>([])
const showLogs = ref(false)
const qrDataUrl = ref('')
const qrZoom = ref(false)

const participants = ref<ParticipantPage>({ items: [], total: 0 })
const participantsLoading = ref(false)
const selectedIds = ref<(string | number)[]>([])
const search = ref('')
const academyFilter = ref<string | null>(null)
const page = ref(1)
const pageSize = 20

const selectedFile = ref<File | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)
function pickFile() { fileInput.value?.click() }
const importing = ref(false)
const importResult = ref<ImportResult | null>(null)
const previewOk = ref(false)

const showParticipantModal = ref(false)
const editingParticipant = ref<ParticipantModel | null>(null)
const participantSubmitting = ref(false)
const participantForm = ref({ name: '', studentId: '', academy: '', className: '' })

const statusOptions = [
  { label: '待开始', value: 'Upcoming' },
  { label: '进行中', value: 'Ongoing' },
  { label: '已结束', value: 'Finished' }
]
const statusLabel = (s: string) => ({ Upcoming: '待开始', Ongoing: '进行中', Finished: '已结束' }[s] || s)
const statusChangeImpacts: Record<string, string> = {
  'Upcoming->Ongoing': '确定开始活动？自助登记通道将开启，已有参与者数据会保留。',
  'Upcoming->Finished': '确定直接结束活动？活动将跳过进行中阶段，自助登记通道保持关闭，已有参与者数据会保留。',
  'Ongoing->Upcoming': '确定将活动回退到待开始？自助登记通道将关闭，已登记的参与者数据会保留。',
  'Ongoing->Finished': '确定结束活动？自助登记通道将关闭，结束后仍可添加/导出参与者数据。',
  'Finished->Upcoming': '确定将活动回退到待开始？已登记的参与者数据将保留，自助登记通道关闭。',
  'Finished->Ongoing': '确定重新开始活动？自助登记通道将重新开启，之前的参与者数据保留。'
}
const statusClass = (s: string) =>
  s === 'Upcoming' ? 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-300'
  : s === 'Ongoing' ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300'
  : 'bg-gray-100 text-gray-600 dark:bg-white/10 dark:text-gray-300'
const sourceLabel = (s: string) => ({ Manual: '代录', Self: '自助', Import: '导入' }[s] || s)
const fmt = (iso: string) => {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}
const registerUrl = computed(() => `${window.location.origin}/ActivityRegister/${activityId}`)
const statusSelectOptions = computed(() =>
  statusOptions.map(o => ({ ...o, disabled: o.value === activity.value?.status }))
)

const chip = (text: string, className: string) =>
  h('span', { class: `inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${className}` }, text)
const actionBtn = (text: string, cls: string, onClick: () => void) =>
  h('button', {
    class: `inline-flex items-center font-medium transition-all rounded-lg px-2.5 py-1 text-xs mr-2 ${cls}`,
    onClick: (e: Event) => { e.stopPropagation(); onClick() }
  }, text)

const rowKey = (row: ParticipantModel) => row.id
function onCheckedChange(keys: (string | number)[]) { selectedIds.value = keys }
const pagination = computed(() => ({
  page: page.value,
  pageSize,
  itemCount: participants.value.total,
  prefix: (info: { itemCount?: number }) => `共 ${info.itemCount ?? 0} 人`,
  onUpdatePage: (p: number) => loadParticipants(p)
}))

const columns: DataTableColumns<ParticipantModel> = [
  { type: 'selection' },
  { title: '姓名', key: 'name', width: 130, render: (row) => h('span', { class: 'font-medium text-gray-900 dark:text-gray-100' }, row.name) },
  { title: '学号', key: 'studentId', width: 140 },
  { title: '学院', key: 'academy', minWidth: 180 },
  { title: '班级', key: 'className', width: 130 },
  { title: '来源', key: 'source', width: 90, render: (row) => chip(sourceLabel(row.source), 'bg-gray-100 text-gray-600 dark:bg-white/10 dark:text-gray-300') },
  {
    title: '操作', key: 'actions', width: 140,
    render: (row) => h('div', { class: 'flex items-center' }, [
      actionBtn('编辑', 'bg-blue-50 text-blue-600 hover:bg-blue-100 dark:bg-blue-500/20 dark:text-blue-300', () => openEdit(row)),
      actionBtn('删除', 'bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/20 dark:text-red-300', () => removeParticipant(row))
    ])
  }
]

async function loadActivity() {
  try { activity.value = await ActivityService.get(activityId) }
  catch (e: any) { message.error(e.message || '加载失败') }
}
async function loadLogs() {
  try { logs.value = await ActivityService.logs(activityId) } catch { /* ignore */ }
}
async function loadParticipants(p: number) {
  page.value = p
  participantsLoading.value = true
  try {
    participants.value = await ActivityService.participants(activityId, {
      search: search.value.trim() || undefined,
      academy: academyFilter.value || undefined,
      page: p, pageSize
    })
    selectedIds.value = []
  } catch (e: any) { message.error(e.message || '加载失败') }
  finally { participantsLoading.value = false }
}
async function loadAcademies() {
  try { academies.value = await InfoService.getAcademies() } catch { /* ignore */ }
}
async function renderQr() {
  try { qrDataUrl.value = await QRCode.toDataURL(registerUrl.value, { width: 260, margin: 2 }) } catch { /* ignore */ }
}

async function onStatusChange(v: string) {
  if (!activity.value || v === activity.value.status) return
  const impact = statusChangeImpacts[`${activity.value.status}->${v}`] || '参与者数据会保留。'
  if (!window.confirm(`确定把「${activity.value.name}」从「${statusLabel(activity.value.status)}」改为「${statusLabel(v)}」吗？\n\n${impact}`)) return
  try {
    await ActivityService.changeStatus(activityId, v as ActivityStatusValue)
    message.success('状态已更新')
    await loadActivity(); await loadLogs()
  } catch (e: any) { message.error(e.message || '操作失败') }
}
async function toggleSelfRegistration(v: boolean) {
  try {
    await ActivityService.setSelfRegistration(activityId, v)
    message.success(v ? '已开启自助登记' : '已关闭自助登记')
    await loadActivity(); await loadLogs()
  } catch (e: any) { message.error(e.message || '操作失败') }
}
async function exportExcel() {
  try { await ActivityService.exportExcel(activityId, false); message.success('导出成功'); await loadLogs() }
  catch (e: any) { message.error(e.message || '导出失败') }
}
async function downloadTemplate() {
  try { await ActivityService.downloadTemplate() } catch (e: any) { message.error(e.message || '下载失败') }
}
function copyRegisterUrl() { navigator.clipboard.writeText(registerUrl.value).then(() => message.success('链接已复制')) }

function onFileChange(e: Event) {
  const input = e.target as HTMLInputElement
  selectedFile.value = input.files && input.files[0] ? input.files[0] : null
  importResult.value = null
  previewOk.value = false
}
async function doImport(isPreview: boolean) {
  if (!selectedFile.value) { message.warning('请先选择文件'); return }
  importing.value = true
  try {
    const res = await ActivityService.importExcel(activityId, selectedFile.value, isPreview)
    importResult.value = res
    if (isPreview) { previewOk.value = true; message.success('校验完成，请确认导入') }
    else {
      message.success(`导入完成，成功 ${res.successCount} 条`)
      previewOk.value = false
      await loadParticipants(1); await loadActivity(); await loadLogs()
    }
  } catch (e: any) { message.error(e.message || '导入失败') }
  finally { importing.value = false }
}
async function downloadErrorReport() {
  if (!selectedFile.value) return
  try { await ActivityService.downloadErrorReport(activityId, selectedFile.value) }
  catch (e: any) { message.error(e.message || '下载失败') }
}

function openAdd() {
  editingParticipant.value = null
  participantForm.value = { name: '', studentId: '', academy: academies.value[0] || '', className: '' }
  showParticipantModal.value = true
}
function openEdit(p: ParticipantModel) {
  editingParticipant.value = p
  participantForm.value = { name: p.name, studentId: p.studentId, academy: p.academy, className: p.className }
  showParticipantModal.value = true
}
async function submitParticipant() {
  if (!participantForm.value.name.trim() || !participantForm.value.studentId.trim() || !participantForm.value.academy || !participantForm.value.className.trim()) {
    message.warning('请填写完整的参与者信息（姓名/学号/学院/班级）')
    return
  }
  participantSubmitting.value = true
  try {
    const dto = {
      name: participantForm.value.name.trim(),
      studentId: participantForm.value.studentId.trim(),
      academy: participantForm.value.academy,
      className: participantForm.value.className.trim()
    }
    if (editingParticipant.value) { await ActivityService.updateParticipant(editingParticipant.value.id, dto); message.success('更新成功') }
    else { await ActivityService.addParticipant(activityId, dto); message.success('添加成功') }
    showParticipantModal.value = false
    await loadParticipants(page.value); await loadActivity(); await loadLogs()
  } catch (e: any) { message.error(e.message || '保存失败') }
  finally { participantSubmitting.value = false }
}
async function removeParticipant(p: ParticipantModel) {
  if (!window.confirm(`确定删除参与者「${p.name}」吗？`)) return
  try {
    await ActivityService.removeParticipant(p.id)
    message.success('删除成功')
    await loadParticipants(page.value); await loadActivity(); await loadLogs()
  } catch (e: any) { message.error(e.message || '删除失败') }
}
async function bulkDelete() {
  if (!selectedIds.value.length) return
  if (!window.confirm(`确定删除选中的 ${selectedIds.value.length} 名参与者吗？`)) return
  try {
    const res = await ActivityService.bulkDeleteParticipants(activityId, selectedIds.value.map(String))
    message.success(`已删除 ${res.deleted} 条`)
    await loadParticipants(page.value); await loadActivity(); await loadLogs()
  } catch (e: any) { message.error(e.message || '删除失败') }
}

onMounted(() => {
  layoutStore.setPageHeader('活动详情', '管理参与者与自助登记')
  layoutStore.setShowPageActions(false)
  loadActivity(); loadAcademies(); loadParticipants(1); loadLogs(); renderQr()
})
onBeforeUnmount(() => { layoutStore.clearPageHeader() })
</script>

<template>
  <div class="apple-container min-h-screen max-sm:p-0 p-6 md:p-8 transition-colors duration-300">
    <div class="p-4 space-y-6">

      <button class="apple-btn secondary" @click="router.push('/Centre/Activity')">
        <Icon icon="ion:chevron-back" class="mr-1" />返回活动列表
      </button>

      <!-- 活动信息 -->
      <div v-if="activity" class="apple-sub-card p-6">
        <div class="flex flex-col lg:flex-row lg:items-start justify-between gap-6">
          <div class="min-w-0">
            <div class="flex items-center gap-3 flex-wrap">
              <h2 class="text-2xl font-bold text-gray-900 dark:text-white">{{ activity.name }}</h2>
              <span :class="['inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium', statusClass(activity.status)]">{{ statusLabel(activity.status) }}</span>
            </div>
            <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">
              <Icon icon="ion:time-outline" class="inline -mt-0.5 mr-1" />{{ fmt(activity.startTime) }} ~ {{ fmt(activity.endTime) }}
              <span class="mx-2">·</span>
              <Icon icon="ion:location-outline" class="inline -mt-0.5 mr-1" />{{ activity.location }}
            </p>
            <p v-if="activity.description" class="mt-3 text-sm text-gray-600 dark:text-gray-300 whitespace-pre-line">{{ activity.description }}</p>
            <p class="mt-3 text-sm text-gray-500 dark:text-gray-400">已登记人数：<b class="text-gray-900 dark:text-white">{{ activity.status === 'Upcoming' ? '待定' : activity.participantCount }}</b></p>
          </div>

          <div class="flex flex-col gap-4 w-full lg:w-[260px] shrink-0">
            <div>
              <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">变更状态</label>
              <n-select :value="activity.status" :options="statusSelectOptions" @update:value="onStatusChange" />
            </div>
            <div class="flex items-center justify-between">
              <span class="text-sm text-gray-500 dark:text-gray-400">自助登记</span>
              <n-switch :value="activity.selfRegistrationEnabled" @update:value="toggleSelfRegistration" />
            </div>
            <button class="apple-btn primary" @click="exportExcel"><Icon icon="ion:download-outline" class="mr-1" />导出 Excel</button>
          </div>
        </div>
      </div>

      <!-- 二维码 -->
      <div v-if="activity" class="apple-sub-card p-6 flex flex-col sm:flex-row items-center gap-6">
        <img v-if="qrDataUrl" :src="qrDataUrl" alt="自助登记二维码"
            class="w-40 h-40 rounded-2xl border border-black/5 dark:border-white/10 bg-white p-2 cursor-zoom-in" @click="qrZoom = true" />
        <div class="flex-1 min-w-0 text-center sm:text-left">
          <h3 class="font-semibold text-gray-900 dark:text-white mb-1">自助登记二维码</h3>
          <p class="text-sm text-gray-500 dark:text-gray-400 break-all mb-4">{{ registerUrl }}</p>
          <div class="flex gap-3 justify-center sm:justify-start">
            <button class="apple-btn secondary" @click="copyRegisterUrl"><Icon icon="ion:copy-outline" class="mr-1" />复制链接</button>
            <a :href="qrDataUrl" :download="`${activity.name}_自助登记二维码.png`" class="apple-btn secondary"><Icon icon="ion:download-outline" class="mr-1" />下载二维码</a>
          </div>
        </div>
      </div>

      <div v-if="qrZoom" class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4" @click="qrZoom = false">
        <img :src="qrDataUrl" alt="二维码" class="w-[320px] h-[320px] rounded-3xl bg-white p-4" />
      </div>

      <!-- Excel 导入 -->
      <div class="apple-sub-card p-6">
        <h3 class="font-semibold text-gray-900 dark:text-white mb-4">Excel 导入参与者</h3>
        <div class="flex flex-wrap items-center gap-3">
          <button class="apple-btn secondary" @click="downloadTemplate"><Icon icon="ion:document-outline" class="mr-1" />下载模板</button>
          <input ref="fileInput" type="file" accept=".xlsx,.xls" class="hidden" @change="onFileChange" />
          <button class="apple-btn secondary" @click="pickFile"><Icon icon="ion:folder-open-outline" class="mr-1" />选择文件</button>
          <span class="text-sm text-gray-500 dark:text-gray-400 max-w-[240px] truncate">{{ selectedFile ? selectedFile.name : '未选择文件' }}</span>
          <button class="apple-btn secondary" :disabled="importing || !selectedFile" @click="doImport(true)">预览</button>
          <button class="apple-btn primary" :disabled="importing || !previewOk" @click="doImport(false)">确认导入</button>
          <button v-if="importResult && importResult.failCount > 0" class="apple-btn secondary" @click="downloadErrorReport">下载错误报告</button>
        </div>
        <div v-if="importResult" class="mt-4 text-sm">
          <p class="mb-1">共 <b>{{ importResult.totalRows }}</b> 行，可导入 <b class="text-emerald-600">{{ importResult.successCount }}</b> 条，失败 <b class="text-red-500">{{ importResult.failCount }}</b> 条</p>
          <ul v-if="importResult.errors?.length" class="max-h-40 overflow-y-auto text-xs text-red-500 space-y-1">
            <li v-for="(e, i) in importResult.errors" :key="i">{{ e }}</li>
          </ul>
        </div>
      </div>

      <!-- 参与者 -->
      <div class="apple-sub-card overflow-hidden">
        <div class="p-4 flex flex-col md:flex-row md:items-center gap-3 border-b border-black/5 dark:border-white/5">
          <div class="flex-1 relative">
            <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none"><Icon icon="ion:search" class="text-gray-400" /></div>
            <input v-model="search" type="text" placeholder="搜索姓名或学号..."
                class="w-full pl-10 pr-4 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500"
                @keyup.enter="loadParticipants(1)" />
          </div>
          <div class="w-full md:w-56">
            <n-select v-model:value="academyFilter" :options="academyOptions" placeholder="全部学院" clearable filterable @update:value="loadParticipants(1)" />
          </div>
          <div class="flex items-center gap-3 shrink-0">
            <span class="text-sm text-gray-500 dark:text-gray-400">共 <b class="text-gray-900 dark:text-white">{{ participants.total }}</b> 人</span>
            <button v-if="selectedIds.length" class="apple-btn secondary" @click="bulkDelete">批量删除（{{ selectedIds.length }}）</button>
            <button class="apple-btn primary" @click="openAdd"><Icon icon="ion:add-circle-outline" class="mr-1" />添加参与者</button>
          </div>
        </div>
        <div class="p-4 overflow-x-auto">
          <n-data-table :columns="columns" :data="participants.items" :pagination="pagination" :remote="true"
              :bordered="false" :loading="participantsLoading" :row-key="rowKey"
              :checked-row-keys="selectedIds" @update:checked-row-keys="onCheckedChange"
              class="apple-table min-w-[720px]" />
        </div>
      </div>

      <!-- 操作日志 -->
      <div class="apple-sub-card p-4">
        <button class="flex items-center text-sm text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-200" @click="showLogs = !showLogs">
          <Icon :icon="showLogs ? 'ion:chevron-down' : 'ion:chevron-forward'" class="mr-1" />操作日志（{{ logs.length }}）
        </button>
        <div v-if="showLogs" class="mt-4 overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="text-left text-gray-500 dark:text-gray-400">
              <tr><th class="py-2 pr-4 font-medium">时间</th><th class="py-2 pr-4 font-medium">操作</th><th class="py-2 pr-4 font-medium">内容</th><th class="py-2 font-medium">操作人</th></tr>
            </thead>
            <tbody>
              <tr v-if="logs.length === 0"><td colspan="4" class="py-4 text-center text-gray-400">暂无日志</td></tr>
              <tr v-for="l in logs" :key="l.id" class="border-t border-black/5 dark:border-white/5">
                <td class="py-2 pr-4 text-gray-500 dark:text-gray-400 whitespace-nowrap">{{ fmt(l.createdAt) }}</td>
                <td class="py-2 pr-4">{{ l.action }}</td>
                <td class="py-2 pr-4 text-gray-600 dark:text-gray-300">{{ l.detail }}</td>
                <td class="py-2 text-gray-500 dark:text-gray-400 font-mono text-xs">{{ l.operatorId }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- 添加/编辑参与者 -->
    <n-modal v-model:show="showParticipantModal" preset="card" class="apple-modal" style="max-width: 460px"
        :title="editingParticipant ? '编辑参与者' : '添加参与者'" :bordered="false" size="huge">
      <div class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">姓名 <span class="text-red-500">*</span></label>
          <input v-model="participantForm.name" maxlength="20" class="w-full px-3 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">学号（10 位）<span class="text-red-500"> *</span></label>
          <input v-model="participantForm.studentId" maxlength="10" class="w-full px-3 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">学院 <span class="text-red-500">*</span></label>
          <n-select v-model:value="participantForm.academy" :options="academyOptions" filterable />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">班级 <span class="text-red-500">*</span></label>
          <input v-model="participantForm.className" maxlength="30" class="w-full px-3 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
      </div>
      <template #footer>
        <div class="flex justify-end gap-3">
          <button @click="showParticipantModal = false" class="apple-btn secondary">取消</button>
          <button @click="submitParticipant" :disabled="participantSubmitting" class="apple-btn primary disabled:opacity-50">{{ participantSubmitting ? '保存中…' : '保存' }}</button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
.apple-container { background-color: #F1F4F9; }
.apple-sub-card {
  background-color: #FFFFFF;
  border-radius: 24px;
  border: 1px solid rgba(0, 0, 0, 0.02);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}
.apple-sub-card:hover { box-shadow: 0 6px 16px rgba(0, 0, 0, 0.06); }
.apple-btn {
  padding: 8px 16px; border-radius: 9999px; font-weight: 500; font-size: 14px;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1); display: inline-flex; align-items: center; width: fit-content;
}
.apple-btn:active { transform: scale(0.96); }
.apple-btn.primary { background-color: #007AFF; color: white; box-shadow: 0 2px 6px rgba(0, 122, 255, 0.3); }
.apple-btn.secondary { background-color: rgba(0, 0, 0, 0.05); color: #1d1d1f; }
:deep(.apple-table .n-data-table-tr:last-child .n-data-table-td) { border-bottom: none; }

.dark .apple-container { background-color: #000000; }
.dark .apple-sub-card { background-color: #1C1C1E; border-color: rgba(255, 255, 255, 0.05); box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2); }
.dark .apple-sub-card:hover { background-color: #242426; }
.dark .apple-btn.secondary { background-color: rgba(255, 255, 255, 0.1); color: #F5F5F7; }
.dark :deep(.apple-table .n-data-table-th) { border-bottom: 1px solid #38383A; color: #98989D; }
.dark :deep(.apple-table .n-data-table-td) { border-bottom: 1px solid #2C2C2E; color: #D1D1D6; }
</style>
