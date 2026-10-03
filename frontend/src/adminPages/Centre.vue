<template>
  <div
      class="min-h-screen bg-[#F5F5F7] dark:bg-black text-slate-900 dark:text-slate-100 transition-colors duration-300 selection:bg-blue-500/30">
    <!-- 顶部导航/欢迎区 -->
    <header
        class="sticky top-0 z-20 px-6 py-4 sm:px-8 lg:px-12 glass-effect border-b border-slate-200/50 dark:border-neutral-800/50 flex justify-between items-center">
      <div>
        <h1 class="text-2xl font-bold tracking-tight -mb-1">控制中心</h1>
        <p class="text-xs font-medium text-slate-500 dark:text-slate-400 mt-1">
          {{ welcomeMessage }}，{{ userInfo.name }}
        </p>
      </div>
      <div class="flex items-center gap-4">

      </div>
    </header>

    <main class="max-w-[1600px] mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">

      <!-- 顶部主要信息区：Bento Grid 布局 -->
      <div class="grid grid-cols-1 md:grid-cols-12 gap-6">

        <!-- 个人卡片 (占比较大) -->
        <div class="md:col-span-12 lg:col-span-4 ios-card relative overflow-hidden group">
          <div
              class="absolute inset-0 bg-gradient-to-br from-blue-50 via-transparent to-purple-50 dark:from-blue-900/20 dark:via-transparent dark:to-purple-900/20 opacity-50"></div>
          <div class="relative z-10 flex flex-col h-full p-6 justify-between">
            <div class="flex items-start justify-between">
              <div class="flex flex-col">
                <span class="text-sm font-semibold text-slate-500 dark:text-slate-400 uppercase tracking-wider">{{
                    getRoleText(userInfo.role)
                  }}</span>
                <h2 class="text-3xl font-bold mt-1">{{ userInfo.name }}</h2>
                <p class="text-xs text-slate-400 mt-1 font-mono">ID: {{ userInfo.id }}</p>
              </div>
              <div class="bg-white/80 dark:bg-neutral-800/80 backdrop-blur-md p-1 rounded-full shadow-sm">
                <Icon icon="solar:verified-check-bold" class="w-6 h-6 text-blue-500"/>
              </div>
            </div>

            <div class="mt-8">
              <button
                  @click="goToPersonalData"
                  class="w-full py-2.5 rounded-xl bg-slate-900 dark:bg-white text-white dark:text-black font-medium text-sm hover:scale-[1.02] transition-transform active:scale-95 shadow-lg shadow-slate-200 dark:shadow-none flex items-center justify-center gap-2"
              >
                <Icon icon="solar:pen-new-square-linear" class="w-4 h-4"/>
                编辑 iMember 资料
              </button>
            </div>
          </div>
        </div>

        <!-- 关键指标 -->
        <div v-if="userInfo.isAdmin"
             class="md:col-span-12 lg:col-span-8 h-full">
          <!-- 成员概览 -->
          <div class="ios-card p-5 flex flex-col justify-between hover:scale-[1.02] transition-all cursor-pointer h-full"
               @click="router.push('/Centre/MemberData')">
            <div class="flex items-center justify-between mb-2">
              <div class="p-2 bg-indigo-100 dark:bg-indigo-500/20 rounded-lg text-indigo-600 dark:text-indigo-400">
                <Icon icon="solar:users-group-rounded-bold-duotone" class="w-6 h-6"/>
              </div>
              <span
                  class="text-xs font-medium text-green-500 bg-green-100 dark:bg-green-900/30 px-2 py-0.5 rounded-full">+2.4%</span>
            </div>
            <div>
              <div class="text-sm text-slate-500 dark:text-slate-400 font-medium">总成员</div>
              <div class="text-2xl font-bold mt-0.5">{{ statistics.members }}</div>
            </div>
          </div>

        </div>
      </div>

      <!-- 工具栏 (横向滚动) -->
      <section>
        <div class="flex items-center justify-between mb-4 px-2">
          <h3 class="text-lg font-bold text-slate-800 dark:text-slate-200 flex items-center gap-2">
            <Icon icon="solar:widget-5-bold-duotone" class="text-blue-500"/>
            iTools
          </h3>
        </div>
        <div class="ios-card p-1 overflow-hidden">
          <div v-if="loading.tools" class="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-4 p-4">
            <div v-for="n in 6" :key="n" class="h-24 bg-slate-100 dark:bg-neutral-800 animate-pulse rounded-xl"></div>
          </div>
          <div v-else class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 xl:grid-cols-6 gap-3 p-3">
            <div
                v-for="(tool, index) in tools" :key="index"
                @click="openTool(tool.url)"
                class="group flex flex-col items-center justify-center p-4 rounded-xl transition-all cursor-pointer"
            >
              <div
                  class="w-14 h-14 rounded-2xl flex items-center justify-center mb-3 shadow-sm bg-white dark:bg-neutral-800 border border-slate-100 dark:border-neutral-700 group-hover:scale-110 transition-transform duration-300">
                <template v-if="tool.icon && !tool.icon.startsWith('http')">
                  <IconFont :type="tool.icon" className="w-8 h-8 text-slate-700 dark:text-slate-200"/>
                </template>
                <img v-else :src="fixImageUrl(tool)" class="w-8 h-8 object-contain rounded" :alt="tool.name"/>
              </div>
              <span
                  class="text-xs font-medium text-center line-clamp-1 text-slate-600 dark:text-slate-300 group-hover:text-blue-600 dark:group-hover:text-blue-400">{{
                  tool.name
                }}</span>
            </div>
          </div>
        </div>
      </section>

      <!-- 社团资源 / 部门管理 -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">

          <!-- 资源快捷入口 -->
          <div
              v-if="userInfo.isAdmin"
              @click="goToResources"
              class="ios-card p-6 relative overflow-hidden group cursor-pointer hover:ring-2 ring-blue-500/30 transition-all"
          >
            <div
                class="absolute -right-5 -top-5 w-32 h-32 bg-linear-to-br from-purple-400/20 to-blue-400/20 blur-3xl rounded-full transition-transform group-hover:scale-150"></div>
            <div class="relative z-10 h-full flex flex-col justify-between">
              <div class="flex items-center gap-3">
                <div class="p-2.5 bg-purple-100 dark:bg-purple-500/20 rounded-xl text-purple-600 dark:text-purple-300">
                  <Icon icon="solar:cloud-storage-bold-duotone" class="w-7 h-7"/>
                </div>
                <div>
                  <h3 class="font-bold text-lg">社团资源</h3>
                  <p class="text-xs text-slate-500 dark:text-slate-400">共 {{ statistics.resources }} 个资源</p>
                </div>
              </div>
              <div class="flex items-center text-sm font-medium text-purple-600 dark:text-purple-400 mt-4">
                浏览所有资源
                <Icon icon="solar:alt-arrow-right-linear" class="ml-1 group-hover:translate-x-1 transition-transform"/>
              </div>
            </div>
          </div>

          <!-- 部门入口 -->
          <div
              v-if="userInfo.isAdmin"
              @click="goToDepartment"
              class="ios-card p-6 relative overflow-hidden group cursor-pointer hover:ring-2 ring-blue-500/30 transition-all"
          >
            <!-- 背景装饰 -->
            <div
                class="absolute inset-0 bg-[url('https://assets.codepen.io/1462889/pattern-1.svg')] opacity-[0.03] dark:opacity-[0.07]"></div>

            <div class="relative z-10 flex items-center justify-between h-full">
              <div class="flex items-center gap-4">
                <div class="flex -space-x-3">
                  <!-- 模拟头像堆叠 -->
                  <div
                      class="w-10 h-10 rounded-full border-2 border-white dark:border-neutral-900 bg-blue-100 flex items-center justify-center text-xs font-bold">
                    S
                  </div>
                  <div
                      class="w-10 h-10 rounded-full border-2 border-white dark:border-neutral-900 bg-green-100 flex items-center justify-center text-xs font-bold">
                    H
                  </div>
                  <div
                      class="w-10 h-10 rounded-full border-2 border-white dark:border-neutral-900 bg-pink-100 flex items-center justify-center text-xs font-bold">
                    M
                  </div>
                  <div
                      class="w-10 h-10 rounded-full border-2 border-white dark:border-neutral-900 bg-slate-100 dark:bg-neutral-800 flex items-center justify-center text-xs text-slate-500">
                    +{{ statistics.departments }}
                  </div>
                </div>
                <div>
                  <h3 class="font-bold text-lg">部门与组织</h3>
                  <p class="text-xs text-slate-500 dark:text-slate-400">管理架构与权限</p>
                </div>
              </div>
              <div
                  class="h-10 w-10 rounded-full bg-slate-100 dark:bg-neutral-800 flex items-center justify-center group-hover:bg-blue-500 group-hover:text-white transition-colors">
                <Icon icon="solar:settings-minimalistic-bold-duotone" class="w-6 h-6"/>
              </div>
            </div>
          </div>
      </div>

    </main>

    <!-- 全局背景装饰 -->
    <div class="fixed top-0 left-0 w-full h-full pointer-events-none -z-10 overflow-hidden">
      <div class="absolute top-[-10%] right-[-5%] w-[500px] h-[500px] bg-blue-400/10 rounded-full blur-[100px]"></div>
      <div
          class="absolute bottom-[-10%] left-[-5%] w-[600px] h-[600px] bg-purple-400/10 rounded-full blur-[120px]"></div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {ref, onMounted, computed} from 'vue'
import {useRouter} from 'vue-router'
import {useMessage} from 'naive-ui'
import {Icon} from '@iconify/vue'
import {ToolService} from '../services/ToolService'
import {UserService} from '../services/UserService'
import {DataCentreService} from "../services/DataCentreService"
import {AuthService} from "../services/AuthService"
import {ApiError} from '../services/ApiService'
import {ErrorCode} from '../constants/ErrorCode'
import IconFont from "../components/IconFont.vue"
// @ts-ignore
import '//at.alicdn.com/t/c/font_4612528_md4hjwjgcb.js';

const router = useRouter()
const message = useMessage()

// --- Interfaces ---
interface UserInfo {
  name: string
  id: string
  role: string
  isAdmin: boolean
  gender: string
}

interface Tool {
  id?: number
  name: string
  icon?: string
  url: string
  description?: string
}

interface Statistics {
  members: number
  staffs: number
  resources: number
  departments: number
}

// --- State ---
const userInfo = ref<UserInfo>({
  name: '',
  id: '',
  role: '',
  isAdmin: false,
  gender: '男'
})

const tools = ref<Tool[]>([])
const statistics = ref<Statistics>({
  members: 0,
  staffs: 0,
  resources: 0,
  departments: 0
})

const loading = ref({
  tools: true,
  resources: true,
  departments: true,
  statistics: true
})

// --- Computed ---
const welcomeMessage = computed(() => {
  const hour = new Date().getHours()
  if (hour < 6) return '凌晨好'
  if (hour < 12) return '早上好'
  if (hour < 18) return '下午好'
  return '晚上好'
})

// --- Methods ---

const getRoleText = (role: string): string => {
  const map: Record<string, string> = {
    'Founder': '创始人',
    'President': '社长/团支书',
    'Minister': '部长/副部长',
    'Department': '部员',
    'Member': 'Member', // 英文看起来更简洁
  }
  return map[role] || role
}

const fixImageUrl = (tool: Tool) => {
  if (tool.icon) {
    return tool.icon.replace(/([^:])(\/\/)/g, '$1/')
  }
  try {
    const domain = tool.url.replace("https://", "").replace("http://", "").split('/')[0];
    return `https://${domain}/favicon.ico`;
  } catch {
    return ''
  }
}

// --- Actions ---
const goToPersonalData = () => router.push('/Centre/PersonalData')
const goToResources = () => router.push('/Centre/Resources')
const goToDepartment = () => router.push('/Centre/Department')
const openTool = (url: string) => window.open(url, '_blank')

// --- Data Fetching ---
// 这里保留原有逻辑，仅做 TS 类型适配和轻微简化
const fetchTools = async () => {
  try {
    loading.value.tools = true
    const res = await ToolService.getTools()
    tools.value = res.links || []
  } catch (error) {
    // Mock Data
    tools.value = [
      {id: 1, name: 'iLibrary', icon: 'solar:book-bookmark-linear', url: 'https://ilibrary.xauat.site'},
      {id: 2, name: '官网', icon: 'solar:global-linear', url: 'https://www.xauat.site'},
      {id: 3, name: 'AI Platform', icon: 'solar:magic-stick-3-linear', url: 'https://iosai.xauat.site'},
      {id: 4, name: '导航', icon: 'solar:map-point-linear', url: '#'},
      {id: 5, name: 'GitHub', icon: 'mdi:github', url: '#'},
    ]
  } finally {
    loading.value.tools = false
  }
}

const fetchUserInfo = async () => {
  try {
    const userData = await UserService.getUserData()
    userInfo.value = {
      name: userData.userName,
      id: userData.userId,
      role: userData.identity || 'Member',
      isAdmin: ['Founder', 'President', 'Minister'].includes(userData.identity),
      gender: userData.gender || '男'
    }
  } catch (error: any) {
    // 只有真正的认证失败（401 / 令牌过期 / 无效令牌）才登出。
    // 业务错误（如 404 档案不存在、5xx）只提示，否则 Founder/Staff 没有 Student
    // 档案时会被误判为会话失效，登录成功后又被弹回登录页。
    const authErrorCodes: number[] = [ErrorCode.Unauthorized, ErrorCode.LoginExpired, ErrorCode.InvalidToken]
    const isAuthFailure = error instanceof ApiError
      && (error.code === 401 || authErrorCodes.includes(error.errorCode))

    if (isAuthFailure) {
      AuthService.clearTokens()
      router.push('/login')
      return
    }

    console.error('获取用户信息失败:', error)
    message.error(error?.message || '用户信息加载失败')
  }
}

const fetchStatistics = async () => {
  if (!userInfo.value.isAdmin) {
    return
  }
  try {
    loading.value.statistics = true
    statistics.value = await DataCentreService.getCentreData()
  } catch (e) {
    statistics.value = {members: 128, staffs: 42, resources: 24, departments: 4}
  } finally {
    loading.value.statistics = false
  }
}

onMounted(async () => {
  // 先获取用户信息（后续 fetchStatistics 依赖 isAdmin 判断）
  await fetchUserInfo()
  // 工具和统计数据并行加载
  await Promise.allSettled([
    fetchTools(),
    fetchStatistics(),
  ])
})

</script>

<style scoped>
/* 核心卡片样式：模仿 iOS 18 / macOS */
.ios-card {
  background-color: #ffffff;
  border-radius: 1.5rem;
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.05);
  border: 1px solid rgba(226, 232, 240, 0.6);
  transition: all 0.3s ease;
}

.dark .ios-card {
  background-color: #171717;
  border: 1px solid #262626;
}

/* 磨砂玻璃效果 */
.glass-effect {
  background-color: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(24px);
  -webkit-backdrop-filter: blur(24px);
  filter: saturate(1.5);
}

.dark .glass-effect {
  background-color: rgba(0, 0, 0, 0.7);
}

/* 自定义滚动条 */
.custom-scrollbar::-webkit-scrollbar {
  width: 4px;
}

.custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: #e2e8f0;
  border-radius: 9999px;
}

.dark .custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: #404040;
}

.custom-scrollbar::-webkit-scrollbar-track {
  background: transparent;
}
</style>
