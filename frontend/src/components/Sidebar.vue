<template>
  <!-- 移动端遮罩 -->
  <Transition name="fade">
    <div
        v-if="layoutStore.isMobile && layoutStore.showSidebar"
        class="fixed inset-0 bg-black/30 backdrop-blur-sm z-40"
        @click="layoutStore.toggleSidebar()"
    ></div>
  </Transition>

  <!-- 侧边栏主体 -->
  <aside
      class="sidebar-glass border-r border-gray-200/50 dark:border-white/10 fixed top-0 left-0 h-screen z-50 flex flex-col transition-all duration-500 cubic-apple"
      :class="[
        layoutStore.isSidebarCollapsed ? 'w-[80px]' : 'w-[280px]',
        layoutStore.isMobile && !layoutStore.showSidebar ? '-translate-x-full' : 'translate-x-0'
      ]"
  >
    <!-- 顶部 Logo 区域 -->
    <div class="h-18 flex items-center px-6 transition-all duration-300"
         :class="layoutStore.isSidebarCollapsed ? 'justify-center' : 'justify-between'">

      <router-link to="/" class="flex items-center gap-3 group overflow-hidden">
        <div class="relative shrink-0 rounded-xl shadow-sm overflow-hidden" :class="layoutStore.isSidebarCollapsed ? 'w-7 h-7'  : 'w-9 h-9'">
          <img
              src="/assets/iOS_Club_LOGO.png"
              alt="Logo"
              class="w-full h-full object-cover bg-white"
              @error="handleImageError"
          />
        </div>
        <span
            v-show="!layoutStore.isSidebarCollapsed"
            class="font-semibold text-lg tracking-tight text-gray-900 dark:text-white whitespace-nowrap opacity-100 transition-opacity delay-100"
        >
          iMember
        </span>
      </router-link>

      <!-- 桌面端折叠/展开按钮：始终使用同一个元素，折叠时形变并移动位置 -->
      <button
          v-if="!layoutStore.isMobile"
          @click="layoutStore.toggleSidebarCollapse()"
          :aria-label="layoutStore.isSidebarCollapsed ? '展开侧边栏' : '折叠侧边栏'"
          class="absolute z-10 p-1.5 rounded-md text-gray-400 hover:text-gray-600 hover:bg-gray-200/50 dark:hover:bg-white/10 transition-all duration-500"
          :class="layoutStore.isSidebarCollapsed ? 'top-20 left-1/2 -translate-x-1/2' : 'top-4 right-4'"
      >
        <Icon
            :icon="layoutStore.isSidebarCollapsed ? 'panel-left-open' : 'panel-left-close'"
            morph
            width="20"
        />
      </button>
    </div>

    <!-- 导航菜单 -->
    <nav
        class="flex-1 overflow-y-auto px-3 space-y-1 custom-scrollbar"
        :class="layoutStore.isSidebarCollapsed ? 'pt-16 pb-4' : 'py-4'"
    >
      <template v-for="item in filteredMenuItems" :key="item.name">
        <router-link
            :to="item.path"
            class="nav-item flex items-center py-2.5 transition-all duration-200 group select-none"
            :class="[
              $route.path === item.path
                ? 'bg-[#007AFF] text-white shadow-md shadow-blue-500/20'
                : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-white/5',
              layoutStore.isSidebarCollapsed ? 'justify-center rounded-2xl px-0 mx-1' : 'px-3.5 rounded-xl'
            ]"
            @click="closeSidebar"
        >
          <Icon
              :icon="item.icon"
              class="shrink-0 transition-transform duration-300"
              :class="[
               layoutStore.isSidebarCollapsed ? 'w-6 h-6' : 'w-5 h-5 mr-3',
               $route.path === item.path ? 'text-white' : 'text-gray-500 dark:text-gray-400 group-hover:text-gray-900 dark:group-hover:text-gray-200'
            ]"
          />

          <span
              v-if="!layoutStore.isSidebarCollapsed"
              class="text-[15px] font-medium whitespace-nowrap"
              :style="{
                color: $route.path === item.path ? '#ffffff' : 'var(--text-color)'
              }"
          >
            {{ item.name }}
          </span>
        </router-link>
      </template>
    </nav>

    <!-- 底部区域 -->
    <div class="p-4 border-t border-gray-200/50 dark:border-white/5">
      <button
          class="w-full flex items-center py-2.5 text-gray-500 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20 dark:text-gray-400 rounded-xl transition-colors"
          :class="layoutStore.isSidebarCollapsed ? 'justify-center px-0' : 'px-3'"
          @click="logout"
      >
        <Icon icon="log-out" class="shrink-0 w-5 h-5" :class="{'mr-3': !layoutStore.isSidebarCollapsed}" />
        <span v-if="!layoutStore.isSidebarCollapsed" class="text-[15px] font-medium">退出登录</span>
      </button>
    </div>
  </aside>

  <!-- 占位与布局控制 -->
  <div
      class="hidden md:block transition-all duration-500 cubic-apple"
      :style="{ width: layoutStore.showSidebar ? (layoutStore.isSidebarCollapsed ? '80px' : '280px') : '0' }"
  ></div>
</template>

<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthorizationStore } from '../stores/Authorization'
import { useLayoutStore } from '../stores/LayoutStore'
import Icon from '@/components/Icon.vue'

const router = useRouter()
const authorizationStore = useAuthorizationStore()
const layoutStore = useLayoutStore()

const handleResize = () => {
  layoutStore.handleResize()
}

onMounted(() => {
  window.addEventListener('resize', handleResize)
  layoutStore.handleResize()
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
})

const closeSidebar = () => {
  if (layoutStore.isMobile) {
    layoutStore.showSidebar = false
  }
}

// 导航图标统一使用 Lucide
const menuItems = [
  {name: '概览', path: '/Centre', icon: 'house'},
  {name: '个人档案', path: '/Centre/PersonalData', icon: 'user'},
  {name: '社团部门', path: '/Centre/Department', icon: 'building-complex', requiresRole: 'Minister'},
  // Resource read API requires Department+, so Department members get the entry.
  {name: '社团资源', path: '/Centre/Resources', icon: 'library', requiresRole: 'Department'},
  // Article API allows Department+ (PRD 6.7 matrix).
  {name: '公告文章', path: '/Centre/Article', icon: 'newspaper', requiresRole: 'Department'},
  {name: '活动记录', path: '/Centre/Activity', icon: 'calendar', requiresRole: 'Minister'},
  {name: '成员管理', path: '/Centre/MemberData', icon: 'users', requiresRole: 'Minister'},
  {name: '权限管理', path: '/Centre/FounderPermission', icon: 'shield-check', requiresRole: 'Founder'},
  {name: '数据分析', path: '/Centre/Admin', icon: 'settings', requiresRole: 'Minister'},
  {name: 'IP黑名单', path: '/Centre/IpBlacklist', icon: 'lock-keyhole', requiresRole: 'Minister'},
  {name: 'OAuth2 接入', path: '/Centre/Client', icon: 'key', requiresRole: 'Minister'},
  {name: '系统日志', path: '/Centre/Logs', icon: 'scroll-text', requiresRole: 'Minister'},
  // 操作日志仅 Founder / President 可见；requiresRole 是最低层级过滤。
  {name: '操作日志', path: '/Centre/OperationLogs', icon: 'list', requiresRole: 'President'},
]

const filteredMenuItems = computed(() => {
  const userRole = authorizationStore.getRole
  const hierarchy: Record<string, number> = { 'Member': 1, 'Department': 2, 'Minister': 3, 'President': 4, 'Founder': 5 }
  const userLevel = hierarchy[userRole as string] || 0

  if (!userRole) return menuItems.filter(i => !i.requiresRole)

  return menuItems.filter(item => {
    if (!item.requiresRole) return true
    return userLevel >= (hierarchy[item.requiresRole] || 0)
  })
})

const logout = async () => {
  await authorizationStore.logout()
  await router.push('/')
}

const handleImageError = (e: Event) => {
  (e.target as HTMLImageElement).src = '/assets/default-logo.png'
}
</script>

<style scoped>
/* 贝塞尔曲线模拟苹果的动画手感 */
.cubic-apple {
  transition-timing-function: cubic-bezier(0.25, 1, 0.5, 1);
}

.sidebar-glass {
  background-color: rgba(249, 250, 251, 0.85);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
}

.dark .sidebar-glass {
  background-color: rgba(28, 28, 30, 0.85);
}

/* 滚动条样式 */
.custom-scrollbar::-webkit-scrollbar {
  width: 0; /* 隐藏滚动条，保持像 iOS 一样简洁，或者设置极细 */
  background: transparent;
}

.nav-item:active {
  transform: scale(0.98);
}

/* 淡入淡出 Vue Transition */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
