<script setup lang="ts">
import { computed } from 'vue'
import { NDrawer, NDrawerContent } from 'naive-ui'
import { useRoute, useRouter } from 'vue-router'
import { useAuthorizationStore } from '../stores/Authorization'
import { useLayoutStore } from '../stores/LayoutStore'
import Icon from '@/components/Icon.vue'

const route = useRoute()
const router = useRouter()
const authorizationStore = useAuthorizationStore()
const layoutStore = useLayoutStore()

// 财务管理是一个模块入口，进入发票库/报销材料等子页面时仍保持高亮。
const isMenuActive = (item: { path: string }) =>
    item.path !== '/Centre'
        ? route.path === item.path || route.path.startsWith(item.path + '/')
        : route.path === item.path

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
  // 财务模块：Founder / President / Minister 同权，Department / Member 无入口。
  {name: '财务管理', path: '/Centre/Finance', icon: 'wallet', requiresRole: 'Minister'},
  {name: '成员管理', path: '/Centre/MemberData', icon: 'users', requiresRole: 'Minister'},
  {name: '权限管理', path: '/Centre/FounderPermission', icon: 'shield-check', requiresRole: 'Founder'},
  {name: '数据分析', path: '/Centre/Admin', icon: 'settings', requiresRole: 'Minister'},
  {name: 'IP黑名单', path: '/Centre/IpBlacklist', icon: 'lock-keyhole', requiresRole: 'Minister'},
  {name: 'OAuth2 接入', path: '/Centre/Client', icon: 'key', requiresRole: 'Minister'},
  {name: 'API Key 管理', path: '/Centre/ApiKeys', icon: 'key', requiresRole: 'President'},
  {name: '系统日志', path: '/Centre/Logs', icon: 'scroll-text', requiresRole: 'Minister'},
  // 操作日志仅 Founder / President 可见；requiresRole 是最低层级过滤。
  {name: '操作日志', path: '/Centre/OperationLogs', icon: 'list', requiresRole: 'President'},
]

const filteredMenuItems = computed(() => {
  const userRole = authorizationStore.getRole
  const hierarchy: Record<string, number> = { 'Member': 1, 'Department': 2, 'Minister': 3, 'President': 4, 'Founder': 5 }
  const userLevel = hierarchy[userRole || ''] || 0

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

const collapsed = computed(() => !layoutStore.isMobile && layoutStore.isSidebarCollapsed)
const menuGroups = computed(() => [
  { label: '个人', items: filteredMenuItems.value.filter(item => ['/Centre', '/Centre/PersonalData'].includes(item.path)) },
  { label: '社团管理', items: filteredMenuItems.value.filter(item => ['/Centre/Department', '/Centre/Resources', '/Centre/Article', '/Centre/Activity', '/Centre/Finance', '/Centre/MemberData'].includes(item.path)) },
  { label: '系统管理', items: filteredMenuItems.value.filter(item => !['/Centre', '/Centre/PersonalData', '/Centre/Department', '/Centre/Resources', '/Centre/Article', '/Centre/Activity', '/Centre/Finance', '/Centre/MemberData'].includes(item.path)) },
].filter(group => group.items.length))
</script>

<template>
  <component :is="layoutStore.isMobile ? NDrawer : 'aside'"
    v-bind="layoutStore.isMobile ? { show: layoutStore.showSidebar, width: 280, placement: 'left', onUpdateShow: (value: boolean) => layoutStore.showSidebar = value } : {}"
    class="admin-sidebar admin-theme" :class="{ 'is-collapsed': collapsed }">
    <component :is="layoutStore.isMobile ? NDrawerContent : 'div'" class="admin-sidebar-inner" :native-scrollbar="false" :body-content-style="{ padding: '0', height: '100%', display: 'flex', flexDirection: 'column' }">
      <div class="admin-sidebar-brand">
        <router-link to="/" class="flex items-center gap-2.5 min-w-0" aria-label="返回 iOS Club 官网"><img src="/assets/iOS_Club_LOGO.png" alt="" /><span v-if="!collapsed">iMember</span></router-link>
        <button v-if="layoutStore.isMobile" class="admin-icon-button ml-auto" aria-label="关闭导航" @click="closeSidebar"><Icon icon="x" :size="18" /></button>
      </div>
      <nav id="admin-navigation" aria-label="后台导航">
        <template v-for="group in menuGroups" :key="group.label">
          <p v-if="!collapsed" class="admin-nav-heading">{{ group.label }}</p>
          <router-link v-for="item in group.items" :key="item.path" :to="item.path" class="admin-nav-link" :class="{ 'is-active': isMenuActive(item) }" :aria-current="isMenuActive(item) ? 'page' : undefined" :aria-label="item.name" :title="collapsed ? item.name : undefined" @click="closeSidebar">
            <Icon :icon="item.icon" :size="18" class="shrink-0" /><span v-if="!collapsed">{{ item.name }}</span>
          </router-link>
        </template>
      </nav>
      <div class="admin-sidebar-footer">
        <button v-if="!layoutStore.isMobile" class="admin-nav-link" :aria-label="collapsed ? '展开侧栏' : '折叠侧栏'" @click="layoutStore.toggleSidebarCollapse()"><Icon :icon="collapsed ? 'panel-left-open' : 'panel-left-close'" :size="18" /><span v-if="!collapsed">折叠侧栏</span></button>
        <button class="admin-nav-link" aria-label="退出登录" @click="logout"><Icon icon="log-out" :size="18" /><span v-if="!collapsed">退出登录</span></button>
      </div>
    </component>
  </component>
</template>

<style scoped>
.admin-sidebar-inner { display: flex; flex-direction: column; height: 100%; min-height: 0; }
</style>
