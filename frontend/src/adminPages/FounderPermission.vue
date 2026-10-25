<script setup lang="ts">
import {computed, h, onBeforeUnmount, onMounted, reactive, ref, watch} from 'vue'
import {NDataTable, NModal, NSelect, useMessage} from 'naive-ui'
import type {DataTableColumns} from 'naive-ui'
import Icon from '@/components/Icon.vue'
import {useAuthorizationStore} from '../stores/Authorization'
import {useLayoutStore} from '../stores/LayoutStore'
import {FounderPermissionService} from '../services/FounderPermissionService'
import {DepartmentService} from '../services/DepartmentService'
import type {StaffModel, DepartmentModel} from '../models'

const message = useMessage()
const layoutStore = useLayoutStore()
const auth = useAuthorizationStore()

// --- 数据状态 ---
const currentId = auth.getAuthorizationInfo?.userId || localStorage.getItem('UserId') || ''
const members = ref<StaffModel[]>([])
const departments = ref<DepartmentModel[]>([])
const loading = ref(false)
const query = ref('')

// --- 分页状态 ---
const currentPage = ref(1)
const pageSize = ref(10)

// --- 编辑弹窗状态 ---
const showModal = ref(false)
const saving = ref(false)
const selected = ref<StaffModel | null>(null)
const form = reactive({identity: 'Member', departmentName: null as string | null})

// --- 选项 ---
const identityOptions = [
  {label: '成员 (Member)', value: 'Member'},
  {label: '部员 (Department)', value: 'Department'},
  {label: '部长 (Minister)', value: 'Minister'},
  {label: '社长 (President)', value: 'President'},
  {label: '创始人 (Founder)', value: 'Founder'}
]

const identityLabels: Record<string, string> = {
  Member: '成员',
  Department: '部员',
  Minister: '部长',
  President: '社长',
  Founder: '创始人'
}

const identityStyles: Record<string, string> = {
  Founder: 'bg-purple-100 text-purple-700 dark:bg-purple-500/20 dark:text-purple-300',
  President: 'bg-blue-100 text-blue-700 dark:bg-blue-500/20 dark:text-blue-300',
  Minister: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300',
  Department: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300',
  Member: 'bg-gray-100 text-gray-600 dark:bg-white/10 dark:text-gray-300'
}

const departmentOptions = computed(() => departments.value.map(d => ({label: d.name, value: d.name})))

const needsDept = computed(() => form.identity === 'Minister' || form.identity === 'Department')

// --- 搜索与分页 ---
const filtered = computed(() => {
  const keyword = query.value.trim().toLowerCase()
  if (!keyword) return members.value
  return members.value.filter(m => `${m.name}${m.userId}`.toLowerCase().includes(keyword))
})

const pagination = computed(() => ({
  page: currentPage.value,
  pageSize: pageSize.value,
  showSizePicker: true,
  pageSizes: [10, 20, 30, 50],
  itemCount: filtered.value.length,
  prefix: (info: {itemCount?: number}) => `共 ${info.itemCount ?? 0} 人`,
  onUpdatePage: (page: number) => {
    currentPage.value = page
  },
  onUpdatePageSize: (size: number) => {
    pageSize.value = size
    currentPage.value = 1
  }
}))

// 搜索条件变化后回到第一页，避免停留在越界页码上
watch(query, () => {
  currentPage.value = 1
})

// --- 渲染辅助 ---
const chip = (text: string, className: string) =>
  h('span', {
    class: `inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${className}`
  }, text)

const AppleButton = (props: {disabled?: boolean, onClick: () => void, text: string}) => {
  const baseClass = 'inline-flex items-center justify-center font-medium transition-all rounded-lg px-2.5 py-1 text-xs'
  const colorClass = props.disabled
    ? 'bg-gray-100 text-gray-300 dark:bg-white/5 dark:text-gray-600 cursor-not-allowed'
    : 'bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-white/10 dark:text-gray-200 dark:hover:bg-white/20 active:scale-95'

  return h('button', {
    class: `${baseClass} ${colorClass}`,
    disabled: props.disabled,
    onClick: (e: Event) => {
      e.stopPropagation()
      props.onClick()
    }
  }, props.text)
}

const rowKey = (row: StaffModel) => row.userId

// --- 表格列 ---
const columns: DataTableColumns<StaffModel> = [
  {
    title: '姓名', key: 'name', width: 200,
    render: (row) => h('div', {class: 'flex items-center gap-2'}, [
      h(Icon, {icon: 'circle-user', class: 'text-lg text-gray-400 shrink-0'}),
      h('span', {class: 'font-medium text-gray-900 dark:text-gray-100'}, row.name),
      row.userId === currentId
        ? chip('我', 'bg-blue-50 text-blue-600 dark:bg-blue-500/15 dark:text-blue-300')
        : null
    ])
  },
  {title: '学号', key: 'userId', width: 160, className: 'text-gray-500 font-mono text-xs'},
  {
    title: '所属部门', key: 'departmentName', width: 180,
    render: (row) => row.departmentName
      ? chip(row.departmentName, 'bg-gray-100 text-gray-700 dark:bg-white/10 dark:text-gray-300')
      : h('span', {class: 'text-gray-400 text-sm'}, '未分配')
  },
  {
    title: '当前身份', key: 'identity', width: 140,
    render: (row) => chip(
      identityLabels[row.identity] || row.identity,
      identityStyles[row.identity] || identityStyles.Member
    )
  },
  {
    title: '操作', key: 'actions', width: 120, align: 'right',
    render: (row) => AppleButton({
      text: '调整权限',
      disabled: row.userId === currentId,
      onClick: () => edit(row)
    })
  }
]

// --- 数据加载 ---
const load = async () => {
  loading.value = true
  try {
    const [staffList, departmentList] = await Promise.all([
      FounderPermissionService.members(),
      DepartmentService.getAllDepartments()
    ])
    members.value = staffList
    departments.value = departmentList
  } catch (error: any) {
    message.error(error.message || '获取权限数据失败')
  } finally {
    loading.value = false
  }
}

// --- 编辑与保存 ---
const edit = (member: StaffModel) => {
  if (member.userId === currentId) return
  selected.value = member
  form.identity = member.identity
  form.departmentName = member.departmentName
  showModal.value = true
}

const save = async () => {
  if (!selected.value || saving.value) return
  if (needsDept.value && !form.departmentName) {
    message.warning('该身份必须指定部门')
    return
  }
  saving.value = true
  try {
    await FounderPermissionService.updateRole({userId: selected.value.userId, ...form})
    message.success('权限更新成功')
    showModal.value = false
    selected.value = null
    await load()
  } catch (error: any) {
    message.error(error.message || '权限更新失败')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  layoutStore.setPageHeader('权限管理', '仅 Founder 可调整成员身份')
  await load()
})

onBeforeUnmount(() => {
  layoutStore.clearPageHeader()
})
</script>

<template>
  <div class="apple-container min-h-screen max-sm:p-0 p-6 md:p-8 transition-colors duration-300">
    <div class="p-4 space-y-6">

      <!-- 工具栏：搜索 + 统计 -->
      <div class="apple-sub-card p-4 flex flex-col md:flex-row md:items-center gap-3">
        <div class="flex-1 relative">
          <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
            <Icon icon="search" class="text-gray-400"/>
          </div>
          <input
              v-model="query"
              type="text"
              placeholder="搜索姓名或学号..."
              class="w-full pl-10 pr-4 py-2.5 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
          />
        </div>
        <div class="flex items-center justify-between md:justify-end gap-4 text-sm text-gray-500 dark:text-gray-400">
          <span>共 <b class="text-gray-900 dark:text-white">{{ filtered.length }}</b> 名成员</span>
          <button @click="load" class="apple-btn secondary">
            <Icon icon="refresh-cw" class="mr-1"/>
            刷新
          </button>
        </div>
      </div>

      <!-- 成员权限表 -->
      <div class="apple-sub-card overflow-hidden">
        <div class="overflow-x-auto">
          <n-data-table
              :columns="columns"
              :data="filtered"
              :pagination="pagination"
              :bordered="false"
              :loading="loading"
              :row-key="rowKey"
              class="apple-table"
          />
        </div>
      </div>
    </div>

    <!-- 调整权限 -->
    <n-modal
        v-model:show="showModal"
        preset="card"
        class="apple-modal"
        style="max-width: 460px"
        title="调整权限"
        :bordered="false"
        size="huge"
    >
      <div v-if="selected" class="space-y-5">
        <div class="flex items-center gap-3 p-3 bg-gray-50 dark:bg-white/5 rounded-xl">
          <Icon icon="circle-user" class="text-3xl text-gray-400 shrink-0"/>
          <div class="min-w-0">
            <div class="font-semibold truncate">{{ selected.name }}</div>
            <div class="text-xs text-gray-500 font-mono">{{ selected.userId }}</div>
          </div>
        </div>

        <div>
          <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">身份</label>
          <n-select v-model:value="form.identity" :options="identityOptions"/>
        </div>

        <div v-if="needsDept">
          <label class="block text-sm font-medium text-gray-500 dark:text-gray-400 mb-1.5">
            所属部门 <span class="text-red-500">*</span>
          </label>
          <n-select
              v-model:value="form.departmentName"
              :options="departmentOptions"
              placeholder="请选择部门"
              filterable
              clearable
          />
        </div>
      </div>

      <template #footer>
        <div class="flex justify-end gap-3">
          <button @click="showModal = false" class="apple-btn secondary">取消</button>
          <button @click="save" :disabled="saving" class="apple-btn primary disabled:opacity-50">
            {{ saving ? '保存中…' : '保存' }}
          </button>
        </div>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
/* Apple Style Base —— 与 Department.vue / MemberData.vue 保持一致 */
.apple-container {
  background-color: #F1F4F9; /* iCloud light gray */
}

.apple-sub-card {
  background-color: #FFFFFF;
  border-radius: 24px;
  border: 1px solid rgba(0, 0, 0, 0.02);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.apple-sub-card:hover {
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.06);
}

/* Buttons */
.apple-btn {
  padding: 8px 16px;
  border-radius: 9999px;
  font-weight: 500;
  font-size: 14px;
  transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
  display: inline-flex;
  align-items: center;
}

.apple-btn:active {
  transform: scale(0.96);
}

.apple-btn.primary {
  background-color: #007AFF;
  color: white;
  box-shadow: 0 2px 6px rgba(0, 122, 255, 0.3);
}

.apple-btn.secondary {
  background-color: rgba(0, 0, 0, 0.05);
  color: #1d1d1f;
}

:deep(.apple-table .n-data-table-tr:last-child .n-data-table-td) {
  border-bottom: none;
}

/* DARK MODE */
.dark .apple-container {
  background-color: #000000;
}

.dark .apple-sub-card {
  background-color: #1C1C1E;
  border-color: rgba(255, 255, 255, 0.05);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
}

.dark .apple-sub-card:hover {
  background-color: #242426;
}

.dark .apple-btn.secondary {
  background-color: rgba(255, 255, 255, 0.1);
  color: #F5F5F7;
}

.dark :deep(.apple-table .n-data-table-th) {
  border-bottom: 1px solid #38383A;
  color: #98989D;
}

.dark :deep(.apple-table .n-data-table-td) {
  border-bottom: 1px solid #2C2C2E;
  color: #D1D1D6;
}
</style>
