<script setup lang="ts">
import AdminState from '@/components/admin/AdminState.vue'
import AdminSurface from '@/components/admin/AdminSurface.vue'
import AdminModal from '@/components/admin/AdminModal.vue'
import { computed, h, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { NDataTable, NPagination, useMessage } from 'naive-ui'
import type { DataTableColumns } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import { ActivityService } from '../../services/ActivityService'
import { useLayoutStore } from '../../stores/LayoutStore'
import type { ActivityModel, ActivityCreateDto } from '../../models'

const router = useRouter()
const message = useMessage()
const layoutStore = useLayoutStore()

const activities = ref<ActivityModel[]>([])
const loading = ref(false)
const statusFilter = ref('')
const keyword = ref('')
const currentPage = ref(1)
const pageSize = ref(10)

const showModal = ref(false)
const editing = ref<ActivityModel | null>(null)
const submitting = ref(false)
const form = ref({ name: '', startTime: '', endTime: '', location: '', description: '' })

const statusTabs = [
  { label: '全部', value: '' },
  { label: '待开始', value: 'Upcoming' },
  { label: '进行中', value: 'Ongoing' },
  { label: '已结束', value: 'Finished' }
]
const statusLabel = (s: string) => ({ Upcoming: '待开始', Ongoing: '进行中', Finished: '已结束' }[s] || s)
const statusClass = (s: string) =>
  s === 'Upcoming' ? 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-300'
  : s === 'Ongoing' ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300'
  : 'bg-gray-100 text-gray-600 dark:bg-white/10 dark:text-gray-300'

const fmt = (iso: string) => {
  if (!iso) return ''
  const d = new Date(iso)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}
const toLocalInput = (iso: string) => {
  const d = new Date(iso); const p = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`
}

const rowKey = (row: ActivityModel) => row.id

const chip = (text: string, className: string) =>
  h('span', { class: `inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${className}` }, text)

const actionBtn = (text: string, cls: string, onClick: () => void) =>
  h('button', {
    class: `inline-flex items-center font-medium transition-all rounded-lg px-2.5 py-1 text-xs mr-2 ${cls}`,
    onClick: (e: Event) => { e.stopPropagation(); onClick() }
  }, text)

const pagination = computed(() => ({
  page: currentPage.value,
  pageSize: pageSize.value,
  showSizePicker: true,
  pageSizes: [10, 20, 30, 50],
  itemCount: activities.value.length,
  prefix: (info: { itemCount?: number }) => `共 ${info.itemCount ?? 0} 个活动`,
  onUpdatePage: (p: number) => { currentPage.value = p },
  onUpdatePageSize: (s: number) => { pageSize.value = s; currentPage.value = 1 }
}))
const visibleActivities = computed(() => activities.value.slice((currentPage.value - 1) * pageSize.value, currentPage.value * pageSize.value))

const columns: DataTableColumns<ActivityModel> = [
  {
    title: '活动名称', key: 'name', minWidth: 180,
    render: (row) => h('span', {
      class: 'font-medium text-gray-900 dark:text-gray-100 cursor-pointer hover:text-[#007AFF]',
      onClick: () => goDetail(row)
    }, row.name)
  },
  { title: '开始时间', key: 'startTime', width: 150, render: (row) => fmt(row.startTime) },
  { title: '结束时间', key: 'endTime', width: 150, render: (row) => fmt(row.endTime) },
  { title: '地点', key: 'location', width: 130 },
  { title: '状态', key: 'status', width: 100, render: (row) => chip(statusLabel(row.status), statusClass(row.status)) },
  {
    title: '人数', key: 'participantCount', width: 80,
    render: (row) => row.status === 'Upcoming' ? '待定' : String(row.participantCount)
  },
  {
    title: '操作', key: 'actions', width: 190,
    render: (row) => h('div', { class: 'flex items-center' }, [
      actionBtn('详情', 'bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-white/10 dark:text-gray-200', () => goDetail(row)),
      actionBtn('编辑', 'bg-blue-50 text-blue-600 hover:bg-blue-100 dark:bg-blue-500/20 dark:text-blue-300', () => openEdit(row)),
      actionBtn('删除', 'bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/20 dark:text-red-300', () => deleteActivity(row))
    ])
  }
]

async function load() {
  loading.value = true
  try {
    activities.value = await ActivityService.list(statusFilter.value || undefined, keyword.value.trim() || undefined)
    currentPage.value = 1
  } catch (e: any) {
    message.error(e.message || '加载失败')
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editing.value = null
  form.value = { name: '', startTime: '', endTime: '', location: '', description: '' }
  showModal.value = true
}
function openEdit(a: ActivityModel) {
  editing.value = a
  form.value = { name: a.name, startTime: toLocalInput(a.startTime), endTime: toLocalInput(a.endTime), location: a.location, description: a.description || '' }
  showModal.value = true
}
async function submit() {
  if (!form.value.name.trim() || !form.value.startTime || !form.value.endTime || !form.value.location.trim()) {
    message.warning('请填写活动名称、开始时间、结束时间和地点')
    return
  }
  submitting.value = true
  try {
    const dto: ActivityCreateDto = {
      name: form.value.name.trim(),
      startTime: new Date(form.value.startTime).toISOString(),
      endTime: new Date(form.value.endTime).toISOString(),
      location: form.value.location.trim(),
      description: form.value.description.trim() || null
    }
    if (editing.value) { await ActivityService.update(editing.value.id, dto); message.success('更新成功') }
    else { await ActivityService.create(dto); message.success('创建成功') }
    showModal.value = false
    await load()
  } catch (e: any) {
    message.error(e.message || '保存失败')
  } finally {
    submitting.value = false
  }
}
async function deleteActivity(a: ActivityModel) {
  if (!window.confirm(`确定删除活动「${a.name}」及其所有参与者数据吗？\n删除前会自动下载参与者备份。`)) return
  try {
    await ActivityService.exportExcel(a.id, true)   // 先备份
    await ActivityService.remove(a.id)              // 再删除
    message.success('已删除（备份已下载）')
    await load()
  } catch (e: any) {
    message.error(e.message || '删除失败')
  }
}
function goDetail(a: ActivityModel) { router.push(`/Centre/Activity/${a.id}`) }

onMounted(() => {
  layoutStore.setPageHeader('活动记录', '创建与管理社团活动')
  layoutStore.setShowPageActions(false)
  load()
})
onBeforeUnmount(() => { layoutStore.clearPageHeader() })
</script>

<template>
  <div class="admin-page apple-container min-h-screen max-sm:p-0 p-6 md:p-8 transition-colors duration-300">
    <div class="p-4 space-y-6">

      <!-- 工具栏 -->
      <AdminSurface class="apple-sub-card p-4 flex flex-col lg:flex-row lg:items-center gap-4">
        <!-- 状态筛选 -->
        <div class="activity-filters flex gap-1 rounded-full bg-black/5 dark:bg-white/10 p-1 w-fit">
          <button v-for="tab in statusTabs" :key="tab.value"
              class="px-4 py-1.5 rounded-full text-sm transition-all"
              :class="statusFilter === tab.value
                ? 'bg-white dark:bg-[#2C2C2E] text-gray-900 dark:text-white shadow-sm font-medium'
                : 'text-gray-500 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200'"
              @click="statusFilter = tab.value; load()">{{ tab.label }}</button>
        </div>

        <!-- 搜索 -->
        <div class="flex-1 relative">
          <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
            <Icon icon="search" class="text-gray-400" />
          </div>
          <input v-model="keyword" type="text" placeholder="搜索活动名称..."
              class="w-full pl-10 pr-4 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
              @keyup.enter="load" />
        </div>

        <div class="flex items-center gap-3 shrink-0">
          <button @click="load" class="apple-btn secondary"><Icon icon="refresh-cw" class="mr-1" />刷新</button>
          <button @click="openCreate" class="apple-btn primary"><Icon icon="circle-plus" class="mr-1" />新建活动</button>
        </div>
      </AdminSurface>

      <!-- 表格 -->
      <AdminSurface class="apple-sub-card overflow-hidden max-sm:hidden">
        <div class="overflow-x-auto">
          <n-data-table :columns="columns" :data="activities" :pagination="pagination"
              :bordered="false" :loading="loading" :row-key="rowKey" class="apple-table" />
        </div>
      </AdminSurface>

      <div class="activity-mobile-list sm:hidden" aria-live="polite">
        <AdminState v-if="loading" kind="loading" title="正在加载活动…" />
        <AdminState v-else-if="!activities.length" title="暂无活动记录" />
        <template v-else>
          <AdminSurface as="article" v-for="activity in visibleActivities" :key="activity.id" class="activity-card apple-sub-card">
            <div class="activity-card-heading">
              <h3>{{ activity.name }}</h3>
              <span class="activity-status" :class="statusClass(activity.status)">{{ statusLabel(activity.status) }}</span>
            </div>
            <dl class="activity-details">
              <div><dt>开始</dt><dd>{{ fmt(activity.startTime) }}</dd></div>
              <div><dt>结束</dt><dd>{{ fmt(activity.endTime) }}</dd></div>
              <div><dt>地点</dt><dd>{{ activity.location }}</dd></div>
              <div><dt>人数</dt><dd>{{ activity.status === 'Upcoming' ? '待定' : activity.participantCount }}</dd></div>
            </dl>
            <div class="activity-actions">
              <button class="apple-btn secondary" @click="goDetail(activity)">详情</button>
              <button class="apple-btn secondary" @click="openEdit(activity)">编辑</button>
              <button class="apple-btn activity-delete" @click="deleteActivity(activity)">删除</button>
            </div>
          </AdminSurface>
          <div class="activity-pagination">
            <span>共 {{ activities.length }} 个活动</span>
            <n-pagination v-model:page="currentPage" :page-size="pageSize" :item-count="activities.length" simple />
          </div>
        </template>
      </div>
    </div>

    <!-- 新建/编辑弹窗 -->
    <AdminModal v-model:show="showModal"  class="apple-modal" style="--admin-modal-width: 520px; "
        :title="editing ? '编辑活动' : '新建活动'" :bordered="false" size="huge">
      <div class="space-y-4">
        <div>
          <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">活动名称 <span class="text-red-500">*</span></label>
          <input v-model="form.name" maxlength="50" placeholder="1-50 个字符"
              class="w-full px-3 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div>
            <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">开始时间 <span class="text-red-500">*</span></label>
            <input v-model="form.startTime" type="datetime-local"
                class="w-full px-3 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">结束时间 <span class="text-red-500">*</span></label>
            <input v-model="form.endTime" type="datetime-local"
                class="w-full px-3 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500" />
          </div>
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">活动地点 <span class="text-red-500">*</span></label>
          <input v-model="form.location" maxlength="15" placeholder="1-15 个字符"
              class="w-full px-3 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500" />
        </div>
        <div>
          <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">活动描述（选填）</label>
          <textarea v-model="form.description" rows="3" maxlength="500" placeholder="最多 500 个字符"
              class="w-full px-3 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"></textarea>
        </div>
      </div>
      <template #footer>
        <div class="flex justify-end gap-3">
          <button @click="showModal = false" class="apple-btn secondary">取消</button>
          <button @click="submit" :disabled="submitting" class="apple-btn primary disabled:opacity-50">{{ submitting ? '保存中…' : '保存' }}</button>
        </div>
      </template>
    </AdminModal>
  </div>
</template>

<style scoped>
/* Apple Style Base —— 与 FounderPermission.vue / Department.vue 保持一致 */
.apple-container { background-color: #F1F4F9; }
.apple-sub-card {

  border: 1px solid rgba(0, 0, 0, 0.02);

  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.apple-btn {
  padding: 8px 16px; border-radius: 9999px; font-weight: 500; font-size: 14px;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1); display: inline-flex; align-items: center;
}
.apple-btn:active { transform: scale(0.96); }
.apple-btn.primary { background-color: #007AFF; color: white; box-shadow: 0 2px 6px rgba(0, 122, 255, 0.3); }
.apple-btn.secondary { background-color: rgba(0, 0, 0, 0.05); color: #1d1d1f; }
:deep(.apple-table .n-data-table-tr:last-child .n-data-table-td) { border-bottom: none; }

.dark .apple-container { background-color: #000000; }
.dark .apple-sub-card {  border-color: rgba(255, 255, 255, 0.05);  }

.dark .apple-btn.secondary { background-color: rgba(255, 255, 255, 0.1); color: #F5F5F7; }
.dark :deep(.apple-table .n-data-table-th) { border-bottom: 1px solid #38383A; color: #98989D; }
.dark :deep(.apple-table .n-data-table-td) { border-bottom: 1px solid #2C2C2E; color: #D1D1D6; }

@media (max-width: 639px) {
  .activity-filters { width: 100%; }
  .activity-filters button { flex: 1; padding-inline: 4px; min-height: 40px; white-space: nowrap; }
  .activity-mobile-list { display: grid; gap: 12px; }
  .activity-card { padding: 16px; }
  .activity-card-heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; }
  .activity-card-heading h3 { min-width: 0; font-size: 16px; font-weight: 600; overflow-wrap: anywhere; }
  .activity-status { flex-shrink: 0; padding: 4px 10px; border-radius: 999px; font-size: 12px; font-weight: 500; }
  .activity-details { display: grid; gap: 8px; margin-top: 14px; font-size: 13px; }
  .activity-details div { display: flex; gap: 12px; }
  .activity-details dt { flex-shrink: 0; color: #8e8e93; }
  .activity-details dd { min-width: 0; overflow-wrap: anywhere; }
  .activity-actions { display: flex; gap: 8px; margin-top: 16px; padding-top: 12px; border-top: 1px solid rgba(128, 128, 128, .15); }
  .activity-actions .apple-btn { justify-content: center; min-height: 40px; }
  .activity-delete { color: #dc2626; background: rgba(239, 68, 68, .08); }
  .activity-empty { padding: 36px 16px; color: #8e8e93; text-align: center; }
  .activity-pagination { display: flex; align-items: center; justify-content: space-between; gap: 12px; color: #8e8e93; font-size: 13px; }
}
</style>
