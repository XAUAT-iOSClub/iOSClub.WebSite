<template>
  <div class="apple-container min-h-screen max-sm:p-0 p-6 md:p-8 transition-colors duration-300">
    <input ref="importFileInput" type="file" accept=".xlsx,.xls,.csv,.json" class="hidden" @change="handleImportFile"/>
    <div class="p-4">
      <n-tabs
          type="segment"
          animated
          class="apple-tabs"
          @update:value="handleTabChange"
      >
        <!-- 总览 Tab -->
        <n-tab-pane name="overview" tab="总览面板">
          <div class="space-y-8 mt-6 animate-fade-in">

            <!-- 顶部统计卡片组 -->
            <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
              <!-- 领导层概览 -->
              <div class="apple-sub-card p-6 flex flex-col justify-between h-full">
                <div class="flex items-center justify-between mb-4">
                  <div class="flex items-center gap-2 text-gray-500 dark:text-gray-400">
                    <Icon icon="ion:ribbon-outline" class="text-xl"/>
                    <span class="text-sm font-medium">领导核心</span>
                  </div>
                  <div class="flex gap-2">
                    <button v-if="!loading" @click="() => openAddMember(null)" class="apple-icon-btn text-blue-500">
                      <Icon icon="ion:add-circle" width="24"/>
                    </button>
                  </div>
                </div>

                <div v-if="loading" class="animate-pulse space-y-2">
                  <div class="h-8 bg-gray-200 dark:bg-gray-700 rounded-md w-3/4"></div>
                </div>
                <div v-else class="flex flex-wrap gap-2 content-start">
                  <div v-for="member in ministers" :key="member.userId"
                       class="apple-chip group">
                    <span class="font-medium">{{ member.userName }}</span>
                    <button @click.stop="() => deleteMember(member, ministers)"
                            class="ml-1 opacity-0 group-hover:opacity-100 transition-opacity text-red-500">
                      <Icon icon="ion:close-circle"/>
                    </button>
                  </div>
                  <div v-if="ministers.length === 0" class="text-gray-400 italic text-sm">暂无领导成员</div>
                </div>
              </div>

              <!-- 成员概览 -->
              <div class="apple-sub-card p-6 flex flex-col justify-between h-full">
                <div class="flex items-center justify-between mb-4">
                  <div class="flex items-center gap-2 text-gray-500 dark:text-gray-400">
                    <Icon icon="ion:people-outline" class="text-xl"/>
                    <span class="text-sm font-medium">成员总数</span>
                  </div>
                  <n-dropdown
                      trigger="click"
                      :options="exportOptions"
                      @select="handleOverviewExportSelect"
                  >
                    <button class="apple-icon-btn text-blue-500" title="导出数据">
                      <Icon icon="ion:cloud-download-outline" width="24"/>
                    </button>
                  </n-dropdown>
                </div>
                <div class="text-4xl font-bold text-gray-900 dark:text-white tracking-tight">
                  {{ loading ? '-' : members.length }}
                  <span class="text-lg font-normal text-gray-400 ml-1">人</span>
                </div>
              </div>

            </div>

            <!-- 数据图表区 -->
            <section>
              <h3 class="section-title mb-4">数据透视</h3>
              <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div v-for="(chartId, index) in ['collegeChart', 'genderChart']"
                     :key="chartId"
                     class="apple-sub-card p-4 h-[350px] flex flex-col">
                    <span class="text-sm font-medium text-gray-500 dark:text-gray-400 mb-2 ml-2">
                      {{ ['学院分布', '男女比例'][index] }}
                    </span>
                  <div class="flex-1 rounded-xl overflow-hidden relative">
                    <div :id="chartId" class="w-full h-full"></div>
                    <!-- Loading State for Charts -->
                    <div v-if="loading"
                         class="absolute inset-0 z-10 flex items-center justify-center bg-white/50 dark:bg-black/50 backdrop-blur-sm">
                      <Icon icon="ion:load-c" class="animate-spin text-3xl text-blue-500"/>
                    </div>
                  </div>
                </div>
              </div>
            </section>

            <!-- 成员列表表格 -->
            <section class="apple-sub-card overflow-hidden">
              <div class="p-4 border-b border-gray-100 dark:border-white/10 flex justify-between items-center">
                <h3 class="font-semibold text-lg">全体成员名单</h3>
              </div>
              <n-data-table
                  :columns="memberColumns"
                  :data="members"
                  :pagination="pagination"
                  :bordered="false"
                  :loading="loading"
                  class="apple-table mb-4"
              />
            </section>

          </div>
        </n-tab-pane>

        <!-- 动态部门 Tab -->
        <n-tab-pane
            v-for="department in departments"
            :key="department.id"
            :name="department.id || ''"
            :tab="department.name"
        >
          <div class="space-y-8 mt-6 animate-fade-in" v-if="!loading">

            <!-- 部门头部信息 -->
            <div class="apple-sub-card p-8 relative overflow-hidden">
              <!-- 装饰背景 -->
              <div
                  class="absolute -right-10 -top-10 w-64 h-64 bg-blue-500/10 rounded-full blur-3xl pointer-events-none"></div>

              <div class="relative z-10">
                <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-6">
                  <div>
                    <h2 class="text-3xl font-bold text-gray-900 dark:text-white tracking-tight">{{
                        department.name
                      }}</h2>
                  </div>
                  <div class="flex flex-wrap items-center gap-2">
                    <button @click="() => openImportModal(department)" class="apple-btn secondary">
                      <Icon icon="ion:cloud-upload-outline" class="mr-1"/>
                      导入名单
                    </button>
                    <n-dropdown
                        trigger="click"
                        :options="exportOptions"
                        @select="(key) => handleExportSelect(department, key)"
                    >
                      <button class="apple-btn secondary">
                        <Icon icon="ion:download-outline" class="mr-1"/>
                        导出
                        <Icon icon="ion:chevron-down" class="ml-1 text-xs"/>
                      </button>
                    </n-dropdown>
                    <button @click="() => openHistory(department)" class="apple-btn secondary">
                      <Icon icon="ion:time-outline" class="mr-1"/>
                      导入历史
                    </button>
                    <button @click="() => openDepartment(department)" class="apple-btn secondary">
                      <Icon icon="ion:settings-outline" class="mr-1"/>
                      设置
                    </button>
                    <button @click="() => deleteDepartment(department)" class="apple-btn danger">
                      <Icon icon="ion:trash-outline" class="mr-1"/>
                      删除
                    </button>
                  </div>
                </div>
                <p class="text-gray-600 dark:text-gray-300 max-w-3xl leading-relaxed text-lg">
                  {{ department.description }}
                </p>
              </div>
            </div>

            <!-- 部门内容双栏布局 -->
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">

              <!-- 左侧：部长与信息 -->
              <div class="space-y-6">
                <div class="apple-sub-card p-6">
                  <div class="flex justify-between items-center mb-4">
                    <h3 class="font-semibold text-lg">管理团队</h3>
                    <div class="flex gap-1">
                      <button @click="() => openAddMember(department, 'Minister')"
                              class="apple-icon-btn text-blue-500">
                        <Icon icon="ion:add"/>
                      </button>
                      <button @click="() => deleteAll(department.ministers)" class="apple-icon-btn text-red-500">
                        <Icon icon="ion:trash-bin-outline"/>
                      </button>
                    </div>
                  </div>

                  <div class="flex flex-wrap gap-2">
                    <div v-for="member in department.ministers" :key="member.userId"
                         class="apple-chip large blue group">
                      <Icon icon="ion:shield-checkmark" class="mr-1 text-blue-600 dark:text-blue-300 opacity-70"/>
                      <span>{{ member.name }}</span>
                      <button @click="() => deleteMember(member, department.ministers)"
                              class="ml-1 hover:text-red-600 transition-colors">
                        <Icon icon="ion:close"/>
                      </button>
                    </div>
                    <div v-if="!department.ministers?.length" class="text-sm text-gray-400 py-2">
                      暂未指派部长
                    </div>
                  </div>
                </div>

              </div>

              <!-- 右侧：成员列表 -->
              <div class="lg:col-span-2 apple-sub-card overflow-hidden flex flex-col">
                <div
                    class="p-4 border-b border-gray-100 dark:border-white/10 flex justify-between items-center bg-gray-50/50 dark:bg-white/5">
                  <div class="flex items-center gap-2">
                    <h3 class="font-semibold">部门成员</h3>
                    <span
                        class="bg-gray-200 dark:bg-gray-700 text-gray-600 dark:text-gray-300 text-xs px-2 py-0.5 rounded-full">{{
                        department.members?.length || 0
                      }}</span>
                  </div>
                  <div class="flex gap-2">
                    <button @click="() => openAddMember(department, 'Department')" class="apple-btn-sm primary">
                      添加成员
                    </button>
                    <button @click="() => deleteAll(department.members)" class="apple-btn-sm danger">清空</button>
                  </div>
                </div>
                <div class="flex-1 overflow-auto">
                  <n-data-table
                      :columns="staffColumns"
                      :data="department.members"
                      :pagination="pagination"
                      :bordered="false"
                      class="apple-table"
                  />
                </div>
              </div>
            </div>

          </div>
          <!-- Loading Skeleton for specific tabs -->
          <div v-else class="p-12 flex justify-center">
            <Icon icon="ion:load-c" class="animate-spin text-4xl text-gray-300"/>
          </div>
        </n-tab-pane>
      </n-tabs>
    </div>
  </div>

  <!-- 模态框组件 - 样式重写 -->
  <!-- 添加成员 -->
  <n-modal
      v-model:show="showAddMemberModal"
      preset="card"
      class="apple-modal"
      style="max-width: 500px"
      :title="`添加${addMemberType === 'minister' ? '部长' : '成员'}`"
      :bordered="false"
      size="huge"
  >
    <div class="space-y-6">
      <div class="relative">
        <div class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
          <Icon icon="ion:search" class="text-gray-400"/>
        </div>
        <input
            v-model="searchKeyword"
            @keyup.enter="searchMembers"
            type="text"
            placeholder="搜索姓名或学号..."
            class="w-full pl-10 pr-4 py-3 bg-gray-100 dark:bg-white/10 rounded-xl focus:outline-none focus:ring-2 focus:ring-blue-500 transition-all"
        />
      </div>

      <div class="min-h-[200px]">
        <n-data-table
            v-if="searchResults.length > 0"
            :columns="searchColumns"
            :data="searchResults"
            class="apple-table"
            :bordered="false"
            :pagination="{pageSize: 5}"
        />
        <div v-else class="h-full flex flex-col items-center justify-center text-gray-400 gap-2 py-8">
          <Icon icon="ion:search-outline" width="48" class="opacity-20"/>
          <span>{{ searchKeyword ? '未找到匹配成员' : '输入关键词开始搜索' }}</span>
        </div>
      </div>
    </div>
  </n-modal>

  <!-- 编辑部门 -->
  <n-modal
      v-model:show="showDepartmentModal"
      preset="card"
      class="apple-modal"
      style="max-width: 500px"
      :title="editingDepartment ? '编辑部门' : '新建部门'"
      :bordered="false"
  >
    <n-form :model="departmentForm" :rules="departmentRules" ref="departmentFormRef" class="space-y-4">
      <n-form-item label="部门名称" path="name">
        <n-input v-model:value="departmentForm.name" placeholder="例如：技术部" class="apple-input-Override"/>
      </n-form-item>
      <n-form-item label="职能描述" path="description">
        <n-input
            v-model:value="departmentForm.description"
            placeholder="描述该部门的主要职责..."
            type="textarea"
            :autosize="{ minRows: 4, maxRows: 6 }"
            class="apple-input-Override"
        />
      </n-form-item>
    </n-form>
    <template #footer>
      <div class="flex justify-end gap-3">
        <button @click="showDepartmentModal = false" class="apple-btn secondary">取消</button>
        <button @click="saveDepartment" class="apple-btn primary">完成</button>
      </div>
    </template>
  </n-modal>

  <!-- 更改部门 -->
  <n-modal
      v-model:show="showChangeDepartmentModalRef"
      preset="card"
      class="apple-modal"
      style="max-width: 500px"
      title="人事调动"
      :bordered="false"
  >
    <div v-if="selectedStaff" class="space-y-6">
      <div class="bg-gray-50 dark:bg-white/5 p-4 rounded-xl flex items-center gap-4">
        <div
            class="w-12 h-12 rounded-full bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center text-blue-600 font-bold text-xl">
          {{ selectedStaff.name.charAt(0) }}
        </div>
        <div>
          <div class="font-medium text-gray-900 dark:text-white">{{ selectedStaff.name }}</div>
          <div class="text-sm text-gray-500">{{ selectedStaff.userId }} | {{
              selectedStaff.departmentName || '无部门'
            }}
          </div>
        </div>
      </div>

      <div>
        <label class="block text-sm font-medium mb-2 text-gray-600 dark:text-gray-400">调动至</label>
        <n-select v-model:value="targetDepartment" :options="departmentOptions" placeholder="选择新部门"/>
      </div>
    </div>
    <template #footer>
      <div class="flex justify-end gap-3">
        <button @click="showChangeDepartmentModalRef = false" class="apple-btn secondary">取消</button>
        <button
            @click="handleChangeDepartment"
            class="apple-btn primary"
            :disabled="!targetDepartment || targetDepartment === selectedStaff?.departmentName"
        >确认调动
        </button>
      </div>
    </template>
  </n-modal>

  <!-- 导入部门名单（选择文件 → 预检查，同一个弹窗内完成） -->
  <n-modal
      v-model:show="showImportModal"
      preset="card"
      class="apple-modal"
      style="max-width: 560px"
      :title="importStep === 'select' ? '导入部门名单' : '导入检查'"
      :bordered="false"
  >
    <div v-if="importStep === 'select'" class="space-y-5">
      <p class="text-sm text-gray-500 dark:text-gray-400">
        支持 XLSX / CSV / JSON，表头需包含「姓名 / 学号 / 职位」。
      </p>
      <div class="flex flex-wrap gap-3">
        <button class="apple-btn secondary" @click="triggerImportFileInput">
          <Icon icon="ion:document-outline" class="mr-1"/>
          选择文件
        </button>
        <button class="apple-btn secondary" @click="downloadImportTemplate">
          <Icon icon="ion:download-outline" class="mr-1"/>
          下载导入模板
        </button>
      </div>
      <p v-if="importFileName" class="text-sm text-gray-600 dark:text-gray-300">已选择：{{ importFileName }}</p>
      <p v-if="importError" class="text-sm text-red-500">{{ importError }}</p>
      <div class="flex items-start gap-2 p-3 rounded-xl bg-amber-50 dark:bg-amber-500/10 text-amber-700 dark:text-amber-400 text-sm">
        <Icon icon="ion:warning-outline" class="mt-0.5 shrink-0"/>
        <span>导入将覆盖当前部门成员名单，原名单会自动备份。</span>
      </div>
    </div>

    <div v-else class="space-y-5">
      <div class="flex items-center justify-between text-sm">
        <span class="text-gray-500 dark:text-gray-400">原成员：{{ importPreview.beforeCount }} 人</span>
        <span class="font-medium">新成员：{{ importPreview.afterCount }} 人</span>
      </div>
      <div class="rounded-xl border border-gray-100 dark:border-white/10 divide-y divide-gray-100 dark:divide-white/10">
        <div v-for="row in importPreview.rows" :key="row.identity"
             class="flex items-center justify-between px-4 py-2 text-sm">
          <span>{{ row.label }}</span>
          <span class="font-mono">{{ row.before }} → {{ row.after }}</span>
        </div>
      </div>
      <p class="text-xs text-gray-500 dark:text-gray-400">
        数据库关联：已关联 {{ importLinkSummary.linked }} 人，新建 {{ importLinkSummary.created }} 人
        <span v-if="importLinkSummary.conflict" class="text-red-500">（姓名冲突 {{ importLinkSummary.conflict }} 人）</span>
      </p>
      <ul class="space-y-1 text-sm">
        <li v-for="check in importChecks" :key="check.text" class="flex items-center gap-2"
            :class="check.ok ? 'text-green-600 dark:text-green-400' : 'text-red-500'">
          <Icon :icon="check.ok ? 'ion:checkmark-circle' : 'ion:close-circle'"/>
          {{ check.text }}
        </li>
      </ul>
      <p v-if="importFileName" class="text-xs text-gray-400">来源文件：{{ importFileName }}</p>
    </div>

    <template #footer>
      <div class="flex justify-end gap-3">
        <button v-if="importStep === 'select'" class="apple-btn secondary" @click="closeImportModal">取消</button>
        <button v-else class="apple-btn secondary" @click="backToSelectStep">返回</button>
        <button v-if="importStep === 'preview'"
                class="apple-btn primary disabled:opacity-50 disabled:cursor-not-allowed"
                :disabled="!importCanSubmit || importSubmitting"
                @click="confirmImport">
          {{ importSubmitting ? '导入中...' : '确认并覆盖' }}
        </button>
      </div>
    </template>
  </n-modal>

  <!-- 导入历史 -->
  <n-modal
      v-model:show="showHistoryModal"
      preset="card"
      class="apple-modal"
      style="max-width: 600px"
      title="版本记录"
      :bordered="false"
  >
    <div v-if="historyLoading" class="py-10 flex justify-center">
      <Icon icon="ion:load-c" class="animate-spin text-3xl text-gray-300"/>
    </div>
    <div v-else-if="importHistory.length === 0" class="py-10 text-center text-gray-400 text-sm">
      暂无导入记录
    </div>
    <div v-else class="space-y-3 max-h-[60vh] overflow-auto">
      <div v-for="record in importHistory" :key="record.id"
           class="flex items-center justify-between gap-3 p-3 rounded-xl bg-gray-50 dark:bg-white/5">
        <div class="min-w-0">
          <div class="text-sm font-medium">{{ formatImportTime(record.importedAt) }}</div>
          <div class="text-xs text-gray-500 dark:text-gray-400 truncate">
            {{ record.operatorName || record.operatorId }} · {{ record.memberCount }} 人
            <span v-if="record.fileName"> · {{ record.fileName }}</span>
          </div>
        </div>
        <div class="flex items-center gap-2 shrink-0">
          <button v-if="record.canRollback" class="apple-btn-sm primary" @click="rollbackVersion(record)">
            <Icon icon="ion:time-outline" class="mr-1"/>
            回滚
          </button>
          <button class="apple-btn-sm secondary" @click="downloadHistoryBackup(record)">
            <Icon icon="ion:download-outline" class="mr-1"/>
            下载备份
          </button>
        </div>
      </div>
    </div>
    <template #footer>
      <div class="flex justify-end">
        <button class="apple-btn secondary" @click="showHistoryModal = false">关闭</button>
      </div>
    </template>
  </n-modal>
</template>

<script setup lang="ts">
import {ref, onMounted, onBeforeUnmount, h, computed, nextTick, watch, defineComponent} from 'vue'
import {
  useMessage,
  useDialog,
  NTabs,
  NTabPane,
  NSelect,
  NInput,
  NDataTable,
  NModal,
  NForm,
  NFormItem,
  NDropdown,
} from 'naive-ui'
import type {DataTableColumns} from 'naive-ui'
import {Icon} from '@iconify/vue'
import {DepartmentService} from '../services/DepartmentService'
import {StaffService} from '../services/StaffService'
import type {
  Department,
  DepartmentImportHistory,
  DepartmentImportMember,
  DepartmentModel,
  MemberVO,
  StudentVO,
  StaffModel
} from '../models'
import * as echarts from 'echarts'
import * as XLSX from 'xlsx'
import {MemberQueryService} from "../services/MemberQueryService";
import {useLayoutStore} from '../stores/LayoutStore';

const message = useMessage()
const dialog = useDialog()
const layoutStore = useLayoutStore()

// --- 导入/导出：身份与排序 ---
const IDENTITY_LABELS: Record<string, string> = {
  President: '社长/团支书',
  Minister: '部长',
  Department: '部员',
  Founder: '创始人'
}
const IMPORT_IDENTITIES = ['President', 'Minister', 'Department']
const ROLE_ORDER: Record<string, number> = {President: 0, Minister: 1, Department: 2, Founder: 3}

// --- 导入/导出：状态 ---
const showImportModal = ref(false)
const importStep = ref<'select' | 'preview'>('select')
const importTarget = ref<Department | null>(null)
const importFileName = ref('')
const importMembers = ref<DepartmentImportMember[]>([])
const importError = ref('')
const importSubmitting = ref(false)
const importFileInput = ref<HTMLInputElement>()
// 数据库现有成员的 学号 -> 姓名，用于判断导入成员是"关联已有人"还是"新建"
const existingProfileMap = ref<Map<string, string>>(new Map())
const showHistoryModal = ref(false)
const historyLoading = ref(false)
const importHistory = ref<DepartmentImportHistory[]>([])

// --- 数据状态 ---
const ministers = ref<MemberVO[]>([])
const members = ref<MemberVO[]>([])
const departments = ref<Department[]>([])
const staffs = ref<MemberVO[]>([])
const loading = ref(true) // 默认 loading true

const showChangeDepartmentModalRef = ref(false)
const selectedStaff = ref<StaffModel | null>(null)
const targetDepartment = ref('')

const showAddMemberModal = ref(false)
const showDepartmentModal = ref(false)
const searchKeyword = ref('')
const searchResults = ref<StudentVO[]>([])
const addMemberType = ref('member')
const departmentFormRef = ref<InstanceType<typeof NForm> | null>(null)

const departmentForm = ref({
  name: '',
  description: ''
})

const editingDepartment = ref<Department | null>(null)
const currentDepartment = ref<Department | null>(null)

const pagination = {pageSize: 8} // 调整每页数量适配卡片高度

// --- Helper Components for Render Functions (Tailwind Styled) ---
const AppleButton = (props: {
  type?: 'primary' | 'danger' | 'secondary',
  size?: 'small',
  onClick: () => void,
  text: string
}) => {
  const baseClass = "inline-flex items-center justify-center font-medium transition-all active:scale-95 rounded-lg"
  const sizeClass = props.size === 'small' ? 'px-2.5 py-1 text-xs' : 'px-4 py-2 text-sm'

  let colorClass: string
  if (props.type === 'danger') colorClass = 'bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/10 dark:text-red-400 dark:hover:bg-red-500/20'
  else if (props.type === 'primary') colorClass = 'bg-blue-600 text-white hover:bg-blue-700 shadow-sm shadow-blue-500/30'
  else colorClass = 'bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-white/10 dark:text-gray-200 dark:hover:bg-white/20'

  return h('button', {
    class: `${baseClass} ${sizeClass} ${colorClass}`,
    onClick: (e: Event) => {
      e.stopPropagation();
      props.onClick()
    }
  }, props.text)
}

// --- Table Columns Configuration ---
const memberColumns: DataTableColumns<MemberVO> = [
  {
    title: '姓名', key: 'userName', width: 100,
    render: (row) => h('span', {class: 'font-medium text-gray-900 dark:text-gray-100'}, row.userName)
  },
  {title: '学号', key: 'userId', width: 120, className: 'text-gray-500'},
  {title: '学院', key: 'academy', width: 150},
  {title: '性别', key: 'gender', width: 60},
  {title: '专业班级', key: 'className', width: 140},
  {title: '手机', key: 'phoneNum', width: 120},
  {
    title: '操作',
    key: 'actions',
    width: 80,
    render: (row) => AppleButton({type: 'danger', size: 'small', onClick: () => deleteMember(row), text: '移除'})
  }
]

const staffColumns: DataTableColumns<StaffModel> = [
  {
    title: '姓名', key: 'name', width: 100,
    render: (row) => h('div', {class: 'flex items-center gap-2'}, [
      h(Icon, {icon: 'ion:person-circle-outline', class: 'text-lg text-gray-400'}),
      h('span', {class: 'font-medium'}, row.name)
    ])
  },
  {title: '学号', key: 'userId', width: 120, className: 'text-gray-500 font-mono text-xs'},
  {
    title: '操作',
    key: 'actions',
    width: 140,
    render: (row) => h('div', {class: 'flex gap-2'}, [
      AppleButton({type: 'secondary', size: 'small', onClick: () => showChangeDepartmentModal(row), text: '调岗'}),
      AppleButton({type: 'danger', size: 'small', onClick: () => deleteStaff(row), text: '移除'})
    ])
  }
]

const searchColumns: DataTableColumns<any> = [
  {title: '姓名', key: 'userName', width: 100, render: (row) => h('b', row.userName)},
  {title: '学号', key: 'userId', width: 120},
  {title: '学院', key: 'academy', width: 150},
  {
    title: '操作',
    key: 'actions',
    width: 80,
    render: (row) => AppleButton({type: 'primary', size: 'small', onClick: () => addMember(row), text: '添加'})
  }
]

const departmentRules = {
  name: {required: true, message: '请输入部门名称', trigger: 'blur'},
  description: {required: true, message: '请输入部门简介', trigger: 'blur'}
}

const departmentOptions = computed(() => {
  return departments.value.map(dept => ({
    label: dept.name,
    value: dept.name
  }))
})

// --- Actions ---

const openDepartment = (department: Department | null = null) => {
  if (department) {
    editingDepartment.value = department
    departmentForm.value = {
      name: department.name || '',
      description: department.description || ''
    }
  } else {
    editingDepartment.value = null
    departmentForm.value = {name: '', description: ''}
  }
  showDepartmentModal.value = true
}

const showChangeDepartmentModal = (staff: StaffModel) => {
  selectedStaff.value = staff
  targetDepartment.value = staff.departmentName || ''
  showChangeDepartmentModalRef.value = true
}

const openAddMember = (department: Department | null = null, type = 'member') => {
  currentDepartment.value = department
  showAddMemberModal.value = true
  searchKeyword.value = ''
  searchResults.value = []
  addMemberType.value = type
}

// --- 导入 / 导出 / 导入历史 ---

const downloadBlob = (blob: Blob, filename: string) => {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  setTimeout(() => {
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
  }, 100)
}

const sortByRole = <T extends { identity: string }>(list: T[]) =>
    [...list].sort((a, b) => (ROLE_ORDER[a.identity] ?? 9) - (ROLE_ORDER[b.identity] ?? 9))

const departmentRoster = (department: Department): StaffModel[] => [
  ...(department.ministers || []),
  ...(department.members || [])
]

// --- 导入预览 / 校验 ---
const importPreview = computed(() => {
  const before: Record<string, number> = {President: 0, Minister: 0, Department: 0}
  if (importTarget.value) {
    departmentRoster(importTarget.value).forEach(s => {
      if (before[s.identity] !== undefined) before[s.identity]++
    })
  }

  const after: Record<string, number> = {President: 0, Minister: 0, Department: 0}
  importMembers.value.forEach(m => {
    if (after[m.identity] !== undefined) after[m.identity]++
  })

  const rows = IMPORT_IDENTITIES.map(identity => ({
    identity,
    label: IDENTITY_LABELS[identity],
    before: before[identity],
    after: after[identity]
  }))

  return {
    beforeCount: Object.values(before).reduce((sum, value) => sum + value, 0),
    afterCount: importMembers.value.length,
    rows
  }
})

const importLinkSummary = computed(() => {
  let linked = 0, created = 0, conflict = 0
  for (const m of importMembers.value) {
    const dbName = existingProfileMap.value.get(m.userId)
    if (dbName === undefined) created++
    else if (dbName === (m.name || '').trim()) linked++
    else conflict++
  }
  return {linked, created, conflict}
})

const importChecks = computed(() => {
  const phonePattern = /^1[3-9]\d{9}$/
  const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  const invalidFormat = importMembers.value.filter(m => {
    if (!m.userId || !m.name) return true
    if (m.phoneNum && !phonePattern.test(m.phoneNum)) return true
    if (m.eMail && !emailPattern.test(m.eMail)) return true
    if (m.gender && m.gender !== '男' && m.gender !== '女') return true
    return false
  }).length

  const seen = new Set<string>()
  let duplicate = 0
  importMembers.value.forEach(m => {
    if (seen.has(m.userId)) duplicate++
    else seen.add(m.userId)
  })

  const invalidIdentity = importMembers.value.filter(m => !IMPORT_IDENTITIES.includes(m.identity)).length

  return [
    {ok: invalidFormat === 0, text: '数据格式正确'},
    {ok: duplicate === 0, text: '学号无重复'},
    {ok: invalidIdentity === 0, text: '职位合法'},
    {ok: importLinkSummary.value.conflict === 0, text: '学号与姓名匹配'}
  ]
})

const importCanSubmit = computed(() => importMembers.value.length > 0 && importChecks.value.every(c => c.ok))

// --- 文件解析 ---
const triggerImportFileInput = () => importFileInput.value?.click()

const normalizeIdentity = (raw: unknown): string => {
  const value = String(raw ?? '').trim()
  if (!value) return 'Department'
  if (['President', '社长', '团支书', '社长/团支书', '副社长', '秘书长'].includes(value)) return 'President'
  if (['Minister', '部长', '副部长'].includes(value)) return 'Minister'
  if (['Department', '部员', '成员', '普通成员'].includes(value)) return 'Department'
  return value
}

const pickField = (row: Record<string, unknown>, keys: string[]): unknown => {
  for (const key of keys) {
    const value = row[key]
    if (value !== undefined && value !== null && String(value).trim() !== '') return value
  }
  return ''
}

const mapRosterRow = (row: Record<string, unknown>): DepartmentImportMember | null => {
  const userId = String(pickField(row, ['学号', 'userId', 'UserId', 'id', 'ID', '编号'])).trim()
  const name = String(pickField(row, ['姓名', 'name', 'Name', 'userName', 'UserName', '名字'])).trim()
  const identity = normalizeIdentity(pickField(row, ['职位', '身份', 'identity', 'Identity', 'role', 'Role']))
  if (!userId && !name) return null
  return {
    userId,
    name,
    identity,
    academy: String(pickField(row, ['学院', 'academy', 'Academy'])).trim(),
    className: String(pickField(row, ['专业班级', '班级', '专业', 'className', 'ClassName'])).trim(),
    phoneNum: String(pickField(row, ['手机号', '手机', '电话', 'phoneNum', 'PhoneNum'])).trim(),
    politicalLandscape: String(pickField(row, ['政治面貌', '面貌', 'politicalLandscape', 'PoliticalLandscape'])).trim(),
    gender: String(pickField(row, ['性别', 'gender', 'Gender'])).trim(),
    eMail: String(pickField(row, ['邮箱', '电子邮箱', 'eMail', 'EMail', 'email', 'Email'])).trim() || null
  }
}

const parseRosterFile = async (file: File): Promise<DepartmentImportMember[]> => {
  if (file.name.toLowerCase().endsWith('.json')) {
    const text = await file.text()
    const data = JSON.parse(text)
    const rows: Record<string, unknown>[] = Array.isArray(data)
        ? data
        : (data?.members || data?.staffs || [])
    return rows.map(mapRosterRow).filter((m): m is DepartmentImportMember => m !== null)
  }

  const buffer = await file.arrayBuffer()
  const workbook = XLSX.read(buffer, {type: 'array'})
  const sheet = workbook.Sheets[workbook.SheetNames[0]]
  if (!sheet) return []
  const rows = XLSX.utils.sheet_to_json<Record<string, unknown>>(sheet, {defval: ''})
  return rows.map(mapRosterRow).filter((m): m is DepartmentImportMember => m !== null)
}

const handleImportFile = async (event: Event) => {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  target.value = ''
  if (!file) return

  importError.value = ''
  try {
    const members = await parseRosterFile(file)
    if (!members.length) {
      importMembers.value = []
      importError.value = '未从文件中解析到成员数据，请检查表头是否为「姓名 / 学号 / 职位」'
      return
    }
    importMembers.value = members
    importFileName.value = file.name
    importStep.value = 'preview'
  } catch (e: any) {
    importMembers.value = []
    importError.value = e?.message || '文件解析失败'
  }
}

// --- 导入弹窗 ---
const openImportModal = async (department: Department) => {
  importTarget.value = department
  importStep.value = 'select'
  importFileName.value = ''
  importMembers.value = []
  importError.value = ''
  existingProfileMap.value = new Map()
  showImportModal.value = true

  // 拉取数据库现有成员，用于判断导入项是"关联已有人"还是"新建"
  try {
    const profiles = await StaffService.getAllStaff()
    existingProfileMap.value = new Map(profiles.map(p => [p.userId, (p.userName || '').trim()]))
  } catch {
    existingProfileMap.value = new Map()
  }
}

const closeImportModal = () => {
  showImportModal.value = false
  importSubmitting.value = false
}

const backToSelectStep = () => {
  importStep.value = 'select'
  importMembers.value = []
  importError.value = ''
}

const confirmImport = async () => {
  if (!importTarget.value || !importCanSubmit.value) return
  importSubmitting.value = true
  try {
    const result = await DepartmentService.importDepartmentRoster(importTarget.value.name, {
      fileName: importFileName.value || undefined,
      members: importMembers.value
    })
    const deptName = importTarget.value.name
    const backupBlob = new Blob([JSON.stringify(result.backup, null, 2)], {type: 'application/json'})
    const backupName = `${deptName}-导入前备份-${new Date().toISOString().slice(0, 10)}.json`

    showImportModal.value = false
    await fetchData()

    message.success(
        () => h('div', {class: 'flex items-center gap-3'}, [
          h('span', `✓ ${deptName}名单导入成功，共 ${result.afterCount} 人`),
          h('button', {
            class: 'text-blue-600 dark:text-blue-400 underline underline-offset-2',
            onClick: () => downloadBlob(backupBlob, backupName)
          }, '下载导入前备份')
        ]),
        {duration: 8000}
    )
  } catch (e: any) {
    message.error(e?.message || '导入失败')
  } finally {
    importSubmitting.value = false
  }
}

// --- 模板下载 ---
const IMPORT_COLUMNS = ['姓名', '学号', '职位', '学院', '专业班级', '手机号', '政治面貌', '性别', '邮箱']

const downloadImportTemplate = () => {
  // 模板只保留表头，不预填任何具体信息。
  // 用 aoa_to_sheet 而不是 json_to_sheet：空数组时 json_to_sheet 不会写出表头。
  const worksheet = XLSX.utils.aoa_to_sheet([IMPORT_COLUMNS])
  worksheet['!cols'] = [
    {wch: 12}, {wch: 14}, {wch: 14}, {wch: 22}, {wch: 14},
    {wch: 14}, {wch: 12}, {wch: 8}, {wch: 24}
  ]
  const workbook = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(workbook, worksheet, '导入模板')
  XLSX.writeFile(workbook, '部门名单导入模板.xlsx')
}

// --- 导出 ---
const exportOptions = [
  {label: 'Excel (.xlsx)', key: 'xlsx'},
  {label: 'CSV (.csv)', key: 'csv'},
  {label: 'JSON (.json)', key: 'json'}
]

const handleExportSelect = (department: Department, key: string | number) => {
  void exportDepartment(department, key as 'xlsx' | 'csv' | 'json')
}

const writeExport = (rows: Record<string, unknown>[], base: string, format: 'xlsx' | 'csv' | 'json') => {
  if (format === 'json') {
    downloadBlob(new Blob([JSON.stringify(rows, null, 2)], {type: 'application/json'}), `${base}.json`)
  } else if (format === 'csv') {
    const worksheet = XLSX.utils.json_to_sheet(rows)
    const csv = '\uFEFF' + XLSX.utils.sheet_to_csv(worksheet)
    downloadBlob(new Blob([csv], {type: 'text/csv;charset=utf-8'}), `${base}.csv`)
  } else {
    const worksheet = XLSX.utils.json_to_sheet(rows)
    const workbook = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(workbook, worksheet, '成员名单')
    XLSX.writeFile(workbook, `${base}.xlsx`)
  }
  message.success('导出成功')
}

const memberToExportRow = (m: MemberVO) => ({
  姓名: m.userName,
  学号: m.userId,
  职位: IDENTITY_LABELS[m.identity] || m.identity,
  学院: m.academy || '',
  专业班级: m.className || '',
  手机号: m.phoneNum || '',
  政治面貌: m.politicalLandscape || '',
  性别: m.gender || '',
  邮箱: m.eMail || ''
})

const exportDepartment = async (department: Department, format: 'xlsx' | 'csv' | 'json') => {
  // 部门成员列表只有 name/userId/identity，学生档案字段需要从 /Staff/members 补齐
  let profiles: MemberVO[] = []
  try {
    profiles = await StaffService.getAllStaff()
  } catch {
    profiles = []
  }
  const profileMap = new Map(profiles.map(p => [p.userId, p]))

  const rows = departmentRoster(department).map(s => {
    const profile = profileMap.get(s.userId)
    return {
      姓名: profile?.userName || s.name,
      学号: s.userId,
      职位: IDENTITY_LABELS[s.identity] || s.identity,
      学院: profile?.academy || '',
      专业班级: profile?.className || '',
      手机号: profile?.phoneNum || '',
      政治面貌: profile?.politicalLandscape || '',
      性别: profile?.gender || '',
      邮箱: profile?.eMail || ''
    }
  })
  const stamp = new Date().toISOString().slice(0, 10)
  writeExport(rows, `${department.name}-名单-${stamp}`, format)
}

// --- 总览面板：导出全体成员 ---
const handleOverviewExportSelect = (key: string | number) => {
  const rows = members.value.map(memberToExportRow)
  const stamp = new Date().toISOString().slice(0, 10)
  writeExport(rows, `全体部员-${stamp}`, key as 'xlsx' | 'csv' | 'json')
}

// --- 导入历史 ---
const openHistory = async (department: Department) => {
  importTarget.value = department
  showHistoryModal.value = true
  historyLoading.value = true
  importHistory.value = []
  try {
    importHistory.value = await DepartmentService.getImportHistory(department.name)
  } catch (e: any) {
    message.error(e?.message || '获取导入历史失败')
  } finally {
    historyLoading.value = false
  }
}

const downloadHistoryBackup = async (record: DepartmentImportHistory) => {
  try {
    const blob = await DepartmentService.downloadImportBackup(record.id)
    downloadBlob(blob, `${record.departmentName}-备份-${record.id.slice(0, 8)}.json`)
    message.success('备份下载已开始')
  } catch (e: any) {
    message.error(e?.message || '下载失败')
  }
}

// 回滚到某个历史版本：先确认，成功后自动提供"回滚前备份"下载
const rollbackVersion = (record: DepartmentImportHistory) => {
  const department = importTarget.value
  if (!department || !record.canRollback) return

  dialog.warning({
    title: '回滚版本',
    content: `将把「${department.name}」名单回滚到 ${formatImportTime(record.importedAt)} 的版本（${record.memberCount} 人）。回滚前会自动备份当前名单。`,
    positiveText: '确认回滚',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        const result = await DepartmentService.rollbackDepartmentRoster(department.name, record.id)
        const backupBlob = new Blob([JSON.stringify(result.backup, null, 2)], {type: 'application/json'})
        const backupName = `${department.name}-回滚前备份-${new Date().toISOString().slice(0, 10)}.json`

        showHistoryModal.value = false
        await fetchData()

        message.success(
          () => h('div', {class: 'flex items-center gap-3'}, [
            h('span', `✓ ${department.name}已回滚到 ${result.afterCount} 人版本`),
            h('button', {
              class: 'text-blue-600 dark:text-blue-400 underline underline-offset-2',
              onClick: () => downloadBlob(backupBlob, backupName)
            }, '下载回滚前备份')
          ]),
          {duration: 8000}
        )
      } catch (e: any) {
        message.error(e?.message || '回滚失败')
      }
    }
  })
}

const formatImportTime = (value: string) => {
  try {
    return new Intl.DateTimeFormat('zh-CN', {dateStyle: 'medium', timeStyle: 'short'}).format(new Date(value))
  } catch {
    return value
  }
}

// --- CRUD Operations (Logic Preserved) ---

const deleteAll = async (list: any[] | undefined) => {
  try {
    if (list && Array.isArray(list)) {
      const listCopy = [...list]
      for (const member of listCopy) {
        await StaffService.deleteStaff(member.userId)
      }
      await fetchData()
      message.success('清空成功')
    }
  } catch (error: any) {
    console.error('Error:', error)
    message.error('操作失败')
  }
}

const deleteMember = async (member: any, list?: any[]) => {
  try {
    await StaffService.deleteStaff(member.userId)
    if (list && Array.isArray(list)) {
      const index = list.findIndex(m => m.userId === member.userId)
      if (index > -1) list.splice(index, 1)
    } else {
      const index = members.value.findIndex(m => m.userId === member.userId)
      if (index > -1) members.value.splice(index, 1)
    }
    message.success('已移除成员')
    await fetchData() // Refresh mostly for charts
  } catch (error: any) {
    message.error('删除失败')
  }
}

const deleteStaff = async (staff: any) => {
  const res = await StaffService.deleteStaff(staff.userId)
  if (!res) return message.error('操作失败')
  message.success('成员已删除')
  await fetchData()
}

const deleteDepartment = async (department: Department) => {
  try {
    await DepartmentService.deleteDepartment(department.name)
    await fetchData()
    message.success('部门已删除')
  } catch (error: any) {
    message.error('删除失败')
  }
}

const searchMembers = async () => {
  if (!searchKeyword.value) {
    searchResults.value = []
    return
  }
  try {
    searchResults.value = await MemberQueryService.search(searchKeyword.value, 'username')
  } catch (error) {
    message.error('搜索出错')
  }
}

const addMember = async (member: StudentVO) => {
  try {
    // StaffCreateDTO 只接受 userId / name / identity / departmentName。
    // 学院、班级、性别、电话、政治面貌属于学生信息（Students 表），不在员工请求里，
    // 之前传这些字段会被后端静默丢弃。
    const commonData = {
      userId: member.userId,
      name: member.userName,
    }

    if (currentDepartment.value) {
      await StaffService.createStaff({
        ...commonData,
        identity: addMemberType.value === 'Minister' ? 'Minister' : 'Department',
        departmentName: currentDepartment.value.name
      } as StaffModel);
      message.success(`已添加至 ${currentDepartment.value.name}`)
    } else {
      await StaffService.createStaff({
        ...commonData,
        identity: 'President',
        departmentName: null
      } as StaffModel);
      message.success(`已添加至领导层`)
    }
    await fetchData()
    showAddMemberModal.value = false
  } catch (error: any) {
    message.error('添加失败: ' + (error.message || '未知错误'))
  }
}

const saveDepartment = async () => {
  try {
    await departmentFormRef.value?.validate()
    const departmentData = {
      key: editingDepartment.value?.id.toString() || '',
      name: departmentForm.value.name,
      description: departmentForm.value.description
    } as DepartmentModel

    if (editingDepartment.value) {
      await DepartmentService.updateDepartment(departmentData)
      message.success('部门已更新')
    } else {
      await DepartmentService.createDepartment(departmentData)
      message.success('部门已创建')
    }
    await fetchData()
    showDepartmentModal.value = false
  } catch (error: any) {
    message.error('保存失败')
  }
}

const handleChangeDepartment = async () => {
  if (!selectedStaff.value || !targetDepartment.value) return
  try {
    await StaffService.changeDepartment(selectedStaff.value.userId, targetDepartment.value)
    showChangeDepartmentModalRef.value = false
    message.success('调岗成功')
    await fetchData()
  } catch (error: any) {
    message.error('调岗失败')
  }
}

// --- Data Fetching ---
const fetchData = async () => {
  loading.value = true
  try {
    const departmentsData = await DepartmentService.getAllDepartments()
    departments.value = departmentsData.map(dept => ({
      id: dept.key,
      name: dept.name,
      description: dept.description,
      ministers: sortByRole(dept.staffs?.filter((staff: any) => staff.identity === 'President' || staff.identity === 'Minister') || []),
      members: sortByRole(dept.staffs?.filter((staff: any) => staff.identity === 'Department') || []),
    } as Department))

    staffs.value = await StaffService.getAllStaff()
    ministers.value = staffs.value.filter(staff => staff.identity === 'President')
    members.value = sortByRole(staffs.value.filter(staff => staff.identity !== 'Founder'))

  } catch (error: any) {
    console.error(error)
    message.error('数据加载失败')
  } finally {
    loading.value = false
    // Trigger chart render after data is ready
    await nextTick(() => renderAllCharts())
  }
}

// --- Charts ---

const departmentData = computed(() => {
  if (!staffs.value.length) return []
  const map: Record<string, number> = {}
  departments.value.forEach(dept => {
    map[dept.name] = (dept.members?.length || 0) + (dept.ministers?.length || 0)
  })
  return Object.entries(map).map(([name, value]) => ({name, value}))
})

const collegeData = computed(() => {
  if (!staffs.value.length) return []
  const map: Record<string, number> = {}
  staffs.value.forEach(s => {
    const c = s.academy || '未知';
    map[c] = (map[c] || 0) + 1
  })
  return Object.entries(map).map(([name, value]) => ({name, value}))
})

const genderData = computed(() => {
  if (!staffs.value.length) return []
  const map = {'男': 0, '女': 0}
  staffs.value.forEach(s => {
    if (s.gender === '男') map['男']++; else if (s.gender === '女') map['女']++
  })
  return Object.entries(map).map(([name, value]) => ({name, value}))
})

const initChart = (id: string, options: any) => {
  const dom = document.getElementById(id)
  if (!dom) return
  const chart = echarts.init(dom as HTMLElement)
  chart.setOption(options)

  // Auto Resize
  const resizeHandler = () => chart.resize()
  window.addEventListener('resize', resizeHandler)
  // Store implementation to remove listener later if needed (skipped for this simple implementation)
}

const getCommonChartOptions = (data: any[], name: string) => {
  // Check if dark mode is likely active by checking body class or text color,
  // but here we'll just use neutral colors that work on both or slightly transparent.
  // For true adaptive ECharts, passing a theme is better.
  return {
    tooltip: {trigger: 'item', backgroundColor: 'rgba(255,255,255,0.95)', borderRadius: 8, textStyle: {color: '#333'}},
    legend: {bottom: 0, left: 'center', textStyle: {color: 'inherit'}, icon: 'circle'}, // Inherit css color doesn't always work in canvas, use transparent logic or simple gray
    series: [{
      name: name,
      type: 'pie',
      radius: ['40%', '65%'],
      center: ['50%', '45%'],
      itemStyle: {borderRadius: 8, borderColor: 'rgba(0,0,0,0)', borderWidth: 2},
      label: {show: false},
      data: data
    }]
  }
}

const renderAllCharts = () => {
  initChart('departmentChart', getCommonChartOptions(departmentData.value, '部门分布'))
  initChart('collegeChart', getCommonChartOptions(collegeData.value, '学院分布'))
  initChart('genderChart', getCommonChartOptions(genderData.value, '男女比例'))
}

watch([departmentData, collegeData, genderData], () => renderAllCharts())

const handleTabChange = (name: string) => {
  if (name === 'overview') nextTick(() => renderAllCharts())
}

// --- Lifecycle ---
onMounted(() => {
  fetchData()
  layoutStore.setPageHeader('社团中枢', '组织架构与人员管理')
  layoutStore.setShowPageActions(true)

  const ActionsComponent = defineComponent({
    setup() {
      return () => h('button', {
        class: 'px-4 py-2 rounded-full bg-blue-600 hover:bg-blue-700 text-white transition-transform active:scale-95 flex items-center text-sm font-medium shadow-lg shadow-blue-500/30',
        onClick: () => openDepartment()
      }, [h(Icon, {icon: 'ion:add', class: 'mr-1'}), '新建部门'])
    }
  })
  layoutStore.setActionsComponent(ActionsComponent);
})

onBeforeUnmount(() => {
  layoutStore.clearPageHeader()
})

</script>

<style scoped>
/* 原生 CSS 适配暗黑模式 */
/* Apple Style Base */
.apple-container {
  background-color: #F1F4F9; /* iCloud light gray */
}

.apple-card {
  background-color: rgba(255, 255, 255, 0.65);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border: 1px solid rgba(255, 255, 255, 0.4);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.03);
}

.apple-sub-card {
  background-color: #FFFFFF;
  border-radius: 24px; /* Apple uses larger border radiuses now */
  border: 1px solid rgba(0, 0, 0, 0.02); /* Very subtle border */
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.apple-sub-card:hover {
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.06);
}

/* Buttons */
.apple-btn {
  padding: 8px 16px;
  border-radius: 9999px; /* Pill shape */
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
  background-color: #007AFF; /* System Blue */
  color: white;
  box-shadow: 0 2px 6px rgba(0, 122, 255, 0.3);
}

.apple-btn.secondary {
  background-color: rgba(0, 0, 0, 0.05);
  color: #1d1d1f;
}

.apple-btn.danger {
  background-color: rgba(255, 59, 48, 0.1);
  color: #FF3B30;
}

.apple-btn-sm {
  padding: 4px 10px;
  font-size: 12px;
  border-radius: 8px;
  font-weight: 500;
  transition: opacity 0.2s;
}

.apple-btn-sm.secondary {
  background-color: #f5f5f7;
  color: #666;
}

.apple-btn-sm.primary {
  background-color: #eef6ff;
  color: #007AFF;
}

.apple-btn-sm.danger {
  background-color: #fff2f2;
  color: #FF3B30;
}

.apple-icon-btn {
  padding: 4px;
  border-radius: 50%;
  transition: background-color 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
}

.apple-icon-btn:hover {
  background-color: rgba(0, 0, 0, 0.05);
}

/* Chips */
.apple-chip {
  display: inline-flex;
  align-items: center;
  padding: 4px 12px;
  border-radius: 99px;
  font-size: 13px;
  background-color: #F5F5F7;
  color: #1D1D1F;
  transition: background-color 0.2s;
}

.apple-chip.large {
  padding: 6px 16px;
  font-size: 14px;
}

.apple-chip.blue {
  background-color: #F0F8FF;
  color: #007AFF;
}

/* Titles */
.section-title {
  font-size: 20px;
  font-weight: 600;
  color: #1d1d1f;
  letter-spacing: -0.01em;
}

:deep(.apple-table .n-data-table-tr:last-child .n-data-table-td) {
  border-bottom: none;
}

/* DARK MODE */
.dark .apple-container {
  background-color: #000000; /* Pure black for heavy contrast */
}

.dark .apple-card {
  background-color: rgba(28, 28, 30, 0.6);
  border-color: rgba(255, 255, 255, 0.1);
}

.dark .apple-sub-card {
  background-color: #1C1C1E; /* System Gray 6 Dark */
  border-color: rgba(255, 255, 255, 0.05);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
}

.dark .apple-sub-card:hover {
  background-color: #242426;
}

.dark .apple-item-card {
  background-color: #1C1C1E;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
}

.dark .apple-item-card:hover {
  background-color: #2C2C2E;
}

.dark .section-title {
  color: #F5F5F7;
}

.dark .apple-btn.secondary {
  background-color: rgba(255, 255, 255, 0.1);
  color: #F5F5F7;
}

.dark .apple-chip {
  background-color: #2C2C2E;
  color: #E5E5E7;
}

.dark .apple-chip.blue {
  background-color: rgba(10, 132, 255, 0.15);
  color: #64D2FF;
}

.dark .apple-icon-btn:hover {
  background-color: rgba(255, 255, 255, 0.1);
}

/* Table Dark */
.dark :deep(.apple-table .n-data-table-th) {
  border-bottom: 1px solid #38383A;
  color: #98989D;
}

.dark :deep(.apple-table .n-data-table-td) {
  border-bottom: 1px solid #2C2C2E;
  color: #D1D1D6;
}

/* Animation Utility */
.animate-fade-in {
  animation: fadeIn 0.4s cubic-bezier(0.4, 0, 0.2, 1) forwards;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

:deep(.n-tabs .n-tabs-capsule) {
  border-radius: 12px !important;
}

:deep(.n-tabs .n-tabs-rail) {
  border-radius: 16px !important;
}
</style>
