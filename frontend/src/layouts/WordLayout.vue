<template>
  <!-- App Container: 高度改为 h-[calc(100vh-64px)] -->
  <div
      class="flex h-[calc(100vh-64px)] w-full overflow-hidden bg-[#f2f2f7] text-slate-900 dark:bg-black dark:text-slate-100 transition-colors duration-300">

    <!-- Sidebar (Desktop) -->
    <!-- 左侧菜单: 磨砂质感，调整了宽度和头部设计 -->
    <aside
        v-if="!isMobile"
        class="flex w-[260px] flex-col border-r border-black/5 bg-zinc-50/80 backdrop-blur-xl dark:border-white/10 dark:bg-[#1e1e1e]/80 transition-all"
    >
      <!-- Sidebar Header -->
      <!-- 这里的样式更接近 macOS 侧边栏标题 -->
      <div class="flex h-14 shrink-0 items-center px-5 pt-2">
        <span class="text-sm font-bold text-zinc-500 dark:text-zinc-400 select-none">
          社团文章
        </span>
      </div>

      <!-- Sidebar Menu -->
      <n-scrollbar class="flex-1 px-3 pb-4">
        <!-- MenuContent 组件容器 -->
        <div class="space-y-1">
          <MenuContent @menu-item-click="handleMenuClick"/>
        </div>
      </n-scrollbar>
    </aside>

    <!-- Main Content Area "Paper" -->
    <main class="relative flex flex-1 flex-col overflow-hidden focus:outline-none">

      <!-- 主内容区域: 桌面端悬浮圆角，移动端全屏 -->
      <div
          class="flex flex-1 flex-col overflow-hidden bg-white shadow-sm transition-all dark:bg-[#1c1c1e] md:border md:border-black/5 md:dark:border-white/10">

        <!-- Header / Toolbar -->
        <header
            class="z-20 flex h-14 shrink-0 items-center justify-between border-b border-zinc-100 bg-white/80 px-4 backdrop-blur-md dark:border-white/5 dark:bg-[#1c1c1e]/80 transition-colors">

          <!-- 左侧: 面包屑 & 移动端菜单开关 -->
          <div class="flex items-center gap-2 overflow-hidden">
            <button
                v-if="isMobile"
                @click="mobileMenuOpen = true"
                class="rounded-full p-2 text-zinc-500 hover:bg-zinc-100 active:scale-95 dark:text-zinc-400 dark:hover:bg-white/10 transition-all"
            >
              <Icon icon="panel-left" class="h-5 w-5"/>
            </button>

            <!-- 面包屑导航 -->
            <nav class="flex items-center text-sm">
              <div class="flex items-center gap-1 text-zinc-500 dark:text-zinc-400">
                <!-- 根节点中文 -->
                <span class="cursor-default hover:text-zinc-800 dark:hover:text-zinc-200 transition-colors">文档</span>
                <template v-for="(crumb, index) in breadcrumbs" :key="index">
                  <Icon icon="chevron-right" class="h-3.5 w-3.5 text-zinc-300 dark:text-zinc-600"/>
                  <div class="flex items-center gap-1.5">
                    <!-- 最后一个节点高亮 -->
                    <Icon
                        :icon="index === breadcrumbs.length - 1 ? 'file-text' : 'folder'"
                        class="h-3.5 w-3.5"
                        :class="index === breadcrumbs.length - 1 ? 'text-blue-500 dark:text-blue-400' : ''"
                    />
                    <span
                        class="truncate max-w-[120px]"
                        :class="index === breadcrumbs.length - 1 ? 'font-bold text-slate-800 dark:text-slate-100' : ''"
                    >
                      {{ crumb }}
                    </span>
                  </div>
                </template>
              </div>
            </nav>
          </div>

          <!-- 右侧: 操作按钮 -->
          <div class="flex items-center gap-1">
            <button
                @click="handleSearchClick"
                class="group flex items-center justify-center rounded-md p-2 transition-colors hover:bg-zinc-100 dark:hover:bg-white/10"
                title="搜索 (Cmd+K)"
            >
              <Icon icon="search"
                    class="h-4.5 w-4.5 text-zinc-400 transition-colors group-hover:text-zinc-600 dark:group-hover:text-zinc-200"/>
            </button>

            <div class="h-4 w-[1px] bg-zinc-200 dark:bg-white/10 mx-1"></div>

            <n-dropdown
                trigger="click"
                placement="bottom-end"
                :options="dropdownOptions"
                @select="handleDropdownSelect"
                class-name="ios-dropdown"
            >
              <button
                  class="group flex items-center justify-center rounded-md p-2 transition-colors hover:bg-zinc-100 dark:hover:bg-white/10">
                <Icon icon="ellipsis"
                      class="h-4.5 w-4.5 text-zinc-400 transition-colors group-hover:text-zinc-600 dark:group-hover:text-zinc-200"/>
              </button>
            </n-dropdown>
          </div>
        </header>

        <!-- 路由视图容器 -->
        <div class="relative flex-1 overflow-y-auto scroll-smooth">
          <div class="min-h-full p-4 md:p-8 max-w-5xl mx-auto">
            <router-view v-slot="{ Component }">
              <transition name="fade-blur" mode="out-in">
                <component :is="Component"/>
              </transition>
            </router-view>
          </div>
        </div>

      </div>
    </main>

    <!-- 移动端抽屉 (iOS Sheet 风格) -->
    <n-drawer
        v-model:show="mobileMenuOpen"
        placement="bottom"
        height="85vh"
        class="!bg-zinc-50 dark:!bg-[#1c1c1e] rounded-t-[20px]"
        :trap-focus="false"
        :block-scroll="true"
    >
      <n-drawer-content body-content-style="padding: 0; display: flex; flex-direction: column;">
        <!-- 拖拽手柄 -->
        <div class="flex w-full justify-center pt-3 pb-1 cursor-grab active:cursor-grabbing">
          <div class="h-1.5 w-12 rounded-full bg-zinc-300 dark:bg-zinc-600/50"></div>
        </div>

        <div class="flex-1 px-6 py-6 overflow-y-auto">
          <h2 class="mb-6 text-2xl font-bold tracking-tight text-slate-900 dark:text-white">导航</h2>
          <MenuContent @menu-item-click="handleMenuClick"/>
        </div>

        <div
            class="shrink-0 px-6 pb-8 pt-4 border-t border-zinc-200 dark:border-white/5 bg-zinc-50/50 dark:bg-[#1c1c1e]/50 backdrop-blur-sm">
          <button
              @click="mobileMenuOpen = false"
              class="w-full py-3.5 rounded-xl bg-white dark:bg-zinc-800 font-medium text-slate-900 dark:text-white shadow-sm border border-zinc-200 dark:border-zinc-700 active:scale-98 transition-transform"
          >
            关闭
          </button>
        </div>
      </n-drawer-content>
    </n-drawer>

    <!-- 搜索弹窗 (Spotlight 风格) -->
    <n-modal
        v-model:show="searchModalVisible"
        :show-icon="false"
        :show-header="false"
        transform-origin="center"
        class="backdrop-blur-sm bg-black/10 dark:bg-black/70 box-border"
    >
      <div
          class="spotlight-container w-[90vw] max-w-[600px] overflow-hidden rounded-2xl bg-white shadow-xl dark:bg-[#1c1c1e]">
        <!-- 搜索输入框 -->
        <div class="flex items-center gap-3 border-b border-zinc-200/50 px-4 py-4 dark:border-white/10">
          <Icon icon="search" class="h-5 w-5 text-zinc-500"/>
          <n-input
              v-model:value="searchKeyword"
              placeholder="搜索文章..."
              size="large"
              autofocus
              :bordered="false"
              @keyup.enter="handleSearch"
              class="flex-1 bg-white text-lg !p-0 dark:bg-[#2c2c2e] search-input"
          >
            <template #suffix>
              <div v-if="isSearching" class="flex items-center">
                <n-spin size="small"/>
              </div>
              <button v-else-if="searchKeyword" @click="searchKeyword = ''"
                      class="text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200">
                <Icon icon="circle-x" class="w-4 h-4"/>
              </button>
            </template>
          </n-input>
          <button
              class="hidden text-xs font-medium text-zinc-500 sm:block rounded border border-zinc-300 px-1.5 py-0.5 dark:border-zinc-600 dark:bg-zinc-700/50"
              @click="searchModalVisible = false"
          >
            ESC
          </button>
        </div>

        <!-- 结果区域 -->
        <div class="max-h-[60vh] overflow-y-auto p-2">
          <!-- 初始状态 -->
          <div v-if="!searchKeyword && !searchResults.length" class="py-12 text-center">
            <div
                class="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-xl bg-zinc-100 dark:bg-zinc-800">
              <Icon icon="search" class="h-6 w-6 text-zinc-400"/>
            </div>
            <p class="text-sm text-zinc-500 dark:text-zinc-400">输入关键词开始搜索文档...</p>
          </div>

          <!-- 错误提示 -->
          <div v-else-if="searchError" class="py-8 text-center text-red-500">
            <p>{{ searchError }}</p>
          </div>

          <!-- 搜索结果 -->
          <div v-else-if="searchResults.length > 0" class="flex flex-col gap-1">
            <div
                v-for="article in searchResults"
                :key="article.path"
                @click="handleResultClick(article.path)"
                class="group flex cursor-pointer flex-col gap-1 rounded-lg p-3 transition-colors hover:bg-blue-500 hover:text-white dark:hover:bg-blue-600"
            >
              <div class="flex items-center justify-between">
                <h4 class="font-medium text-slate-900 group-hover:text-white dark:text-slate-100 text-sm truncate"
                    v-html="article.highlightedTitle || article.title"></h4>
                <span class="text-xs text-zinc-500 group-hover:text-blue-100 dark:text-zinc-400 ml-2 flex-shrink-0">{{
                    new Date(article.lastWriteTime).toLocaleDateString()
                  }}</span>
              </div>
              <p class="line-clamp-2 text-xs text-zinc-500 group-hover:text-blue-100 dark:text-zinc-400 mt-1"
                 v-html="article.highlightedContent || ''"></p>
            </div>
          </div>

          <!-- 无结果 -->
          <div v-else-if="searchKeyword && !isSearching" class="py-8 text-center">
            <div class="flex justify-center mb-3">
              <Icon icon="file-x" class="h-6 w-6 text-zinc-400" />
            </div>
            <p class="text-zinc-500 dark:text-zinc-400">未找到与 "{{ searchKeyword }}" 相关的文章</p>
          </div>
        </div>

        <!-- 底部提示 -->
        <div
            class="border-t border-zinc-200 bg-zinc-50 px-4 py-2 text-[10px] text-zinc-500 dark:border-white/10 dark:bg-zinc-800/50 dark:text-zinc-400 flex justify-between items-center">
          <div class="flex items-center">
            <Icon icon="info" class="h-3 w-3 mr-1" />
            <span>本地搜索支持</span>
          </div>
          <div class="flex items-center">
            <Icon icon="corner-down-left" class="h-3 w-3 mr-1" />
            <span class="font-mono">回车键确认搜索</span>
          </div>
        </div>
      </div>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import {computed, onBeforeUnmount, onMounted, ref} from 'vue'
import {useRouter, useRoute} from 'vue-router'
import {
  NDrawer,
  NDrawerContent,
  NScrollbar,
  NModal,
  NInput,
  NSpin,
  NDropdown,
  useMessage
} from 'naive-ui'
import Icon from '@/components/Icon.vue'
import MenuContent from '../components/MenuContent.vue'
import {ArticleService} from '../services/ArticleService'
import {useAuthorizationStore} from '../stores/Authorization'
import type {ArticleSearchResult} from '../models'

// --- 配置与状态 ---
const router = useRouter()
const route = useRoute()
const message = useMessage()

const mobileMenuOpen = ref<boolean>(false)
const windowWidth = ref<number>(typeof window !== 'undefined' ? window.innerWidth : 1024)
const MOBILE_BREAKPOINT = 768
const isMobile = computed<boolean>(() => windowWidth.value < MOBILE_BREAKPOINT)

// --- 鉴权 ---
const authStore = useAuthorizationStore()
const isLoggedIn = computed(() => authStore.isAuthenticated)
const isAdmin = computed(() => authStore.isAdmin())

// --- 下拉菜单逻辑 ---
type DropdownOption = {
  label: string;
  key: string;
  icon?: () => any;
}

// 汉化下拉菜单
const dropdownOptions = computed<DropdownOption[]>(() => {
  const options = [
    {label: '分享此页面', key: 'share'}
  ]
  if (isLoggedIn.value && isAdmin.value) {
    options.push({label: '编辑内容', key: 'edit'})
  }
  return options
})

const handleDropdownSelect = (key: string) => {
  if (key === 'share') handleShareUrl()
  if (key === 'edit') handleEdit()
}

const handleShareUrl = async () => {
  try {
    if (navigator.share && isMobile.value) {
      await navigator.share({title: document.title, url: window.location.href})
    } else {
      await navigator.clipboard.writeText(`${document.title} - ${window.location.href}`)
      message.success('链接已复制到剪贴板')
    }
  } catch (err) {
    console.error('分享失败:', err)
  }
}

const handleEdit = () => {
  const path = route.path
  if (!path.startsWith('OtherOrg')) {
    const articlePath = path.replace('/Article', '')
    router.push(`/Centre/Article/edit${articlePath}`)
  }
}

// --- 导航与面包屑 ---
const breadcrumbs = computed<string[]>(() => {
  const path = route.path
  if (!path) return []
  return decodeURIComponent(path)
      .split('/')
      .filter(segment => segment && segment !== 'Article')
})

const handleMenuClick = (key: string) => {
  if (isMobile.value) mobileMenuOpen.value = false
  router.push(key)
}

// --- 搜索系统 ---
const searchModalVisible = ref(false)
const searchKeyword = ref('')
const searchResults = ref<ArticleSearchResult[]>([])
const isSearching = ref(false)
const searchError = ref('')

const handleSearchClick = () => {
  searchModalVisible.value = true
  setTimeout(() => {
    const input = document.querySelector('.spotlight-container input') as HTMLInputElement
    if (input) input.focus()
  }, 100)
}

/* 快捷键监听 (Cmd/Ctrl + K) */
const handleKeydown = (e: KeyboardEvent) => {
  if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
    e.preventDefault()
    handleSearchClick()
  }
}

const handleSearch = async () => {
  if (!searchKeyword.value.trim()) {
    searchResults.value = []
    return
  }
  isSearching.value = true
  searchError.value = ''
  try {
    searchResults.value = (await ArticleService.searchArticles(searchKeyword.value))
  } catch (error: any) {
    searchError.value = error.message || '搜索请求失败'
  } finally {
    isSearching.value = false
  }
}

const handleResultClick = (path: string) => {
  searchModalVisible.value = false
  router.push(`/Article/${path}`)
}

// 监听窗口调整
const handleResize = () => {
  windowWidth.value = window.innerWidth
  if (!isMobile.value) mobileMenuOpen.value = false
}

onMounted(() => {
  window.addEventListener('resize', handleResize)
  window.addEventListener('keydown', handleKeydown)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<style scoped>
/* 原生 CSS 用于自定义滚动条和必须的覆盖样式 */

/* 自定义滚动条 (Webkit) - 极简 & 不显眼 */
::-webkit-scrollbar {
  width: 6px;
  height: 6px;
}

::-webkit-scrollbar-track {
  background: transparent;
}

::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.1);
  border-radius: 100vh;
}

::-webkit-scrollbar-thumb:hover {
  background: rgba(0, 0, 0, 0.2);
}

/* 深色模式滚动条 */
.dark ::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.15);
}

.dark ::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.3);
}

/* 路由切换动画: 模糊+位移 */
.fade-blur-enter-active,
.fade-blur-leave-active {
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.fade-blur-enter-from {
  opacity: 0;
  filter: blur(4px);
  transform: translateY(10px);
}

.fade-blur-leave-to {
  opacity: 0;
  filter: blur(4px);
  transform: translateY(-10px);
}

/* Naive Input 样式覆盖: 用于 Spotlight 搜索框 */
:deep(.n-input.n-input--stateful) {
  --n-border: none !important;
  --n-border-hover: none !important;
  --n-border-focus: none !important;
  --n-box-shadow-focus: none !important;
}

:deep(.n-input .n-input__input-el) {
  height: 2.5rem;
}

:deep(.search-input .n-input__wrapper) {
  background-color: #ffffff !important;
}

:deep(.dark .search-input .n-input__wrapper) {
  background-color: #2c2c2e !important;
}
</style>