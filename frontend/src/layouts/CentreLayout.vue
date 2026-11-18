<script setup lang="ts">
import { computed, h, onMounted, onBeforeUnmount, shallowRef, useId } from 'vue'
import { NConfigProvider, NDialogProvider, NMessageProvider, NDropdown } from 'naive-ui'
import type { GlobalThemeOverrides, DropdownProps } from 'naive-ui'
import { storeToRefs } from 'pinia'
import { useRoute } from 'vue-router'
import Icon from '@/components/Icon.vue'
import Sidebar from '@/components/Sidebar.vue'
import { useThemeStore } from '@/stores/theme'
import { useLayoutStore } from '@/stores/LayoutStore'
import '@/styles/admin.css'

const themeMenuOpen = shallowRef(false)
const themeMenuId = useId()
const route = useRoute()
const themeStore = useThemeStore()
const layoutStore = useLayoutStore()
const { isDark, userPreference } = storeToRefs(themeStore)
const { isMobile, pageTitle, pageSubtitle, showPageActions } = storeToRefs(layoutStore)
const title = computed(() => pageTitle.value || String(route.meta.title || 'iMember').split(' - ')[0])
const themeOptions = computed(() => [
  { key: 'light', label: '浅色', icon: () => h(Icon, { icon: 'sun', size: 17 }) },
  { key: 'dark', label: '深色', icon: () => h(Icon, { icon: 'moon-star', size: 17 }) },
  { key: 'system', label: '跟随系统', icon: () => h(Icon, { icon: 'monitor', size: 17 }) },
])
const themeNodeProps: NonNullable<DropdownProps['nodeProps']> = option => ({
  role: 'menuitemradio',
  'aria-checked': option.key === userPreference.value ? 'true' : 'false',
})
function selectTheme(key: string) {
  if (key === 'light' || key === 'dark' || key === 'system') themeStore.setThemePreference(key)
}
const overrides = computed<GlobalThemeOverrides>(() => ({
  common: {
    primaryColor: isDark.value ? '#75b6ff' : '#0066cc',
    primaryColorHover: isDark.value ? '#9bcbff' : '#0077ed',
    primaryColorPressed: isDark.value ? '#5599e5' : '#0055aa',
    primaryColorSuppl: '#0066cc',
    bodyColor: isDark.value ? '#161618' : '#f5f5f7',
    modalColor: isDark.value ? '#222225' : '#ffffff',
    popoverColor: isDark.value ? '#2c2c30' : '#ffffff',
    tableColor: isDark.value ? '#222225' : '#ffffff',
    textColorBase: isDark.value ? '#f5f5f7' : '#1d1d1f',
    borderRadius: '10px', fontSize: '14px',
    fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
  },
  DataTable: {
    tdColor: isDark.value ? '#222225' : '#ffffff',
    thColor: isDark.value ? '#28282c' : '#f9f9fb',
    tdColorHover: isDark.value ? '#2c2c30' : '#f5f7fa',
    borderColor: isDark.value ? '#37373c' : '#ececef',
    borderRadius: '14px',
  },
  Button: { fontWeight: '500', borderRadiusMedium: '10px' },
}))
function resize() { layoutStore.handleResize() }
onMounted(() => {
  resize()
  window.addEventListener('resize', resize)
})
onBeforeUnmount(() => window.removeEventListener('resize', resize))
</script>

<template>
  <NConfigProvider :theme="themeStore.theme" :theme-overrides="overrides">
    <NDialogProvider>
      <NMessageProvider>
        <div class="admin-shell admin-theme">
          <a class="admin-skip-link" href="#admin-main">跳转到主要内容</a>
          <Sidebar />
          <div class="admin-workspace">
            <header class="admin-header">
              <button v-if="isMobile" class="admin-icon-button" aria-label="打开导航" :aria-expanded="layoutStore.showSidebar" aria-controls="admin-navigation" @click="layoutStore.toggleSidebar()"><Icon icon="panel-left" :size="20" /></button>
              <div class="admin-header-title">
                <h1 class="truncate">{{ title }}</h1>
                <p v-if="pageSubtitle" class="truncate">{{ pageSubtitle }}</p>
              </div>
              <div v-if="showPageActions" class="admin-actions admin-header-actions">
                <component :is="layoutStore.actionsComponent" v-if="layoutStore.actionsComponent" />
                <div v-else v-html="layoutStore.pageActionsContent" />
              </div>
              <NDropdown v-model:show="themeMenuOpen" trigger="click" :options="themeOptions" :value="userPreference"
                :menu-props="() => ({ role: 'menu', id: themeMenuId, 'aria-label': '外观', class: 'admin-theme-menu' })"
                :node-props="themeNodeProps"
                @select="selectTheme">
                <button class="admin-icon-button" aria-label="选择外观" aria-haspopup="menu" :aria-expanded="themeMenuOpen" :aria-controls="themeMenuId"><Icon :icon="userPreference === 'system' ? 'monitor' : isDark ? 'moon-star' : 'sun'" :size="19" /></button>
              </NDropdown>
            </header>
            <div v-if="showPageActions && isMobile" class="admin-actions admin-mobile-actions">
              <component :is="layoutStore.actionsComponent" v-if="layoutStore.actionsComponent" />
              <div v-else v-html="layoutStore.pageActionsContent" />
            </div>
            <main id="admin-main" class="admin-main" tabindex="-1"><router-view /></main>
          </div>
        </div>
      </NMessageProvider>
    </NDialogProvider>
  </NConfigProvider>
</template>
