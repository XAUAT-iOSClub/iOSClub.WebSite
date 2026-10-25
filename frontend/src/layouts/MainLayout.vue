<template>
  <div class="app-container min-h-screen flex flex-col transition-colors duration-500 ease-out">

    <!--
      Header: Apple 风格导航栏
      - sticky 定位
      - 强毛玻璃效果 (backdrop-blur-2xl)
      - 底部极细边框
    -->
    <header
        class="sticky top-0 z-50 w-full transition-all duration-300 border-b text-lg"
        :class="[
        isScrolled
          ? 'bg-white/75 dark:bg-[#161617]/75 border-gray-200/50 dark:border-white/10 backdrop-blur-2xl'
          : 'bg-white/0 dark:bg-black/0 border-transparent backdrop-blur-sm'
      ]"
    >
      <div class="px-4 sm:px-6 h-14 flex items-center justify-between">

        <!-- Logo Area -->
        <router-link to="/" class="flex items-center gap-2 group z-50">
          <img
              src="/assets/iOS_Club_LOGO.png"
              alt="iOS Club"
              class="w-7 h-7 opacity-90 group-hover:opacity-100 transition-opacity rounded-sm"
          />
          <span class="font-semibold text-xl tracking-tight text-gray-900 dark:text-[#f5f5f7]">
            iOS Club of XAUAT
          </span>
        </router-link>

        <!-- Desktop Navigation (Hidden on Mobile) -->
        <nav class="hidden md:flex items-center space-x-1">

          <!-- Nav Item: 社团简介 (Dropdown) -->
          <div class="relative group">
            <button class="nav-link">
              关于我们
              <Icon icon="chevron-down" class="text-gray-400" width="16"/>
            </button>
            <!-- Custom Dropdown Menu -->
            <div
                class="absolute left-0 mt-2 w-48 bg-white dark:bg-[#1c1c1e] rounded-xl shadow-lg border border-gray-200 dark:border-white/20 z-50 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-200 ease-out">
              <div class="py-2">
                <button
                    v-for="item in aboutUsOptions"
                    :key="item.key"
                    @click="handleSelect(item.key)"
                    class="block w-full text-left px-4 py-2 text-sm font-medium transition-colors duration-150"
                    :class="[
                      'text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-white/10'
                    ]"
                >
                  {{ item.label }}
                </button>
              </div>
            </div>
          </div>

          <!-- Nav Item: 社团动态 (Dropdown) -->
          <div class="relative group">
            <button class="nav-link">
              社团动态
              <Icon icon="chevron-down" class="text-gray-400" width="16"/>
            </button>
            <!-- Custom Dropdown Menu -->
            <div
                class="absolute left-0 mt-2 w-48 bg-white dark:bg-[#1c1c1e] rounded-xl shadow-lg border border-gray-200 dark:border-white/20 z-50 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-200 ease-out">
              <div class="py-2">
                <button
                    v-for="item in communityOptions"
                    :key="item.key"
                    @click="handleSelect(item.key)"
                    class="block w-full text-left px-4 py-2 text-sm font-medium transition-colors duration-150"
                    :class="[
                      'text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-white/10'
                    ]"
                >
                  {{ item.label }}
                </button>
              </div>
            </div>
          </div>

          <!-- Divider -->
          <div class="h-4 w-px bg-gray-300 dark:bg-white/20 mx-3"></div>

          <!-- Theme Toggle Dropdown -->
          <div class="relative group">
            <button
                class="icon-btn"
                title="切换主题"
            >
              <Icon :icon="isDark ? 'moon-star' : 'sun'" morph class="w-4 h-4"/>
            </button>
            <!-- Custom Dropdown Menu -->
            <div
                class="absolute right-0 mt-2 w-48 bg-white dark:bg-[#1c1c1e] rounded-xl shadow-lg border border-gray-200 dark:border-white/20 z-50 opacity-0 invisible group-hover:opacity-100 group-hover:visible transition-all duration-200 ease-out">
              <div class="py-2">
                <button
                    v-for="option in themeOptions"
                    :key="option.key"
                    @click="handleThemeSelect(option.key)"
                    class=" w-full text-left px-4 py-2 text-sm font-medium transition-colors duration-150 flex items-center gap-2"
                    :class="[
                      userPreference === option.key
                        ? 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300'
                        : 'text-gray-700 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-white/10'
                    ]"
                >
                  <Icon :icon="option.icon" class="w-4 h-4"/>
                  <span>{{ option.label }}</span>
                </button>
              </div>
            </div>
          </div>

          <!-- Auth Button (Apple ID Style) -->
          <button
              v-if="!isCentreRoute"
              class="auth-btn ml-3"
              @click="() => router.push('/login')"
          >
            登录
          </button>
          <button
              v-else
              class="auth-btn ml-3"
              @click="toCentre"
          >
            个人中心
          </button>
        </nav>

        <!-- Mobile Menu Toggle -->
        <button
            class="md:hidden z-50 p-2 -mr-2 text-gray-800 dark:text-white"
            @click="drawerVisible = !drawerVisible"
        >
          <Icon
              :icon="drawerVisible ? 'x' : 'menu'"
              morph
              width="24"
          />
        </button>
      </div>
    </header>

    <!-- Mobile Dropdown Menu (Full Screen Overlay style) -->
    <transition name="mobile-menu">
      <div
          v-if="drawerVisible"
          class="fixed inset-0 z-40 bg-[#fbfbfd] dark:bg-black pt-14 px-4 md:hidden flex flex-col"
      >
        <div class="flex flex-col space-y-3 animate-fade-in">
          <!-- Mobile Links Group -->
          <div class="space-y-1">
            <div class="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-1 px-2">关于</div>
            <router-link
                v-for="item in aboutUsOptions"
                :key="item.key"
                :to="item.key"
                class="mobile-link"
                @click="drawerVisible = false"
            >
              {{ item.label }}
            </router-link>
          </div>

          <div class="space-y-1">
            <div class="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-1 px-2">探索</div>
            <router-link
                v-for="item in communityOptions"
                :key="item.key"
                :to="item.key"
                class="mobile-link"
                @click="drawerVisible = false"
            >
              {{ item.label }}
            </router-link>
          </div>

          <!-- Mobile Auth & Theme -->
          <div class="pt-3 mt-auto border-t border-gray-200 dark:border-white/10">
            <button
                v-if="!isCentreRoute"
                class="w-full py-2 bg-[#0071e3] text-white font-medium rounded-lg mb-3 active:scale-95 transition-transform"
                @click="() => { router.push('/login'); drawerVisible = false }"
            >
              登录 / 注册
            </button>
            <button
                v-else
                class="w-full py-2 bg-[#0071e3] text-white font-medium rounded-lg mb-3 active:scale-95 transition-transform"
                @click="logout"
            >
              退出登录
            </button>

            <!-- Theme Selection -->
            <div class="w-full space-y-1">
              <div class="text-xs font-semibold text-gray-400 uppercase tracking-wider mb-1 px-2">主题</div>
              <div class="flex space-x-1">
                <button
                    v-for="option in themeOptions"
                    :key="option.key"
                    @click="handleThemeSelect(option.key)"
                    class="flex-1 py-1.5 px-2 rounded-lg font-medium transition-all duration-200 flex items-center justify-center gap-1.5 text-xs"
                    :class="[
                      userPreference === option.key
                        ? 'bg-[#0071e3] text-white'
                        : 'bg-gray-100 dark:bg-[#1c1c1e] text-gray-900 dark:text-white opacity-70 hover:opacity-90'
                    ]"
                >
                  <Icon :icon="option.icon" class="w-3 h-3"/>
                  <span>{{ option.label }}</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </transition>

    <!-- Main Content -->
    <main class="flex-1 w-full bg-[#fbfbfd] dark:bg-[#1c1c1e]/70">
      <router-view>
      </router-view>
    </main>

  </div>
</template>

<script setup lang="ts">
import {ref, onMounted, onUnmounted, computed} from 'vue'
import {useRouter} from 'vue-router'
import {useAuthorizationStore} from '../stores/Authorization'
import Icon from '@/components/Icon.vue'
import {useThemeStore} from '../stores/theme'
import {storeToRefs} from 'pinia'

const themeStore = useThemeStore()
const {isDark, userPreference} = storeToRefs(themeStore)
const {setThemePreference} = themeStore

// 主题选项配置
const themeOptions = [
  {key: 'light', label: '浅色', icon: 'sun'},
  {key: 'dark', label: '深色', icon: 'moon-star'},
  {key: 'system', label: '跟随系统', icon: 'monitor'}
]

// 主题选择处理函数
const handleThemeSelect = (key: string) => {
  setThemePreference(key as 'light' | 'dark' | 'system')
}

const router = useRouter()
const authorizationStore = useAuthorizationStore()
const drawerVisible = ref(false)
const isScrolled = ref(false)

const isCentreRoute = computed(() => authorizationStore.isAuthenticated)

const handleScroll = () => {
  // Apple style: Trigger background change immediately after scroll starts
  isScrolled.value = window.scrollY > 5
}

const toCentre = () => {
  router.push('/Centre')
}

const logout = () => {
  authorizationStore.logout()
  drawerVisible.value = false
  if (isCentreRoute.value) {
    router.push('/')
  }
}

const handleSelect = (key: string) => {
  router.push(key)
  drawerVisible.value = false
}

onMounted(() => {
  window.addEventListener('scroll', handleScroll)
})

onUnmounted(() => {
  window.removeEventListener('scroll', handleScroll)
})

// Data structure optimized for clean template rendering
const aboutUsOptions = [
  {label: '社团简介', key: '/About'},
  {label: '结构架构', key: '/Structure'},
  {label: '合作组织', key: '/OtherOrg'},
  {label: '竞赛资源', key: '/Article/Competitions'},
  {label: '发展历史', key: '/History'}
]

const communityOptions = [
  {label: '近期活动', key: '/Event'},
  {label: '技术博客', key: '/Blog'},
  {label: 'iOS App', key: '/Tools'},
  {label: '开源项目', key: '/Projects'}
]
</script>

<style scoped>
/* Native CSS Configuration */

/* Background Colors */
.app-container {
  /* fallback */
  background-color: #fbfbfd;
  color: #1d1d1f;
}

/* Dark Mode Override */
.dark .app-container {
  background-color: #1c1c1e;
  color: #f5f5f7;
}

/*
   Apple-style Navigation Link
   Using Tailwind @apply is avoided as requested for styling blocks
*/
.nav-link {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 12px;
  font-size: 15px;
  font-weight: 400;
  color: #424245;
  border-radius: 9999px; /* Pill shape */
  transition: all 0.2s ease;
  letter-spacing: -0.01em;
}

.dark .nav-link {
  color: #e8e8ed;
}

.nav-link:hover {
  color: #1d1d1f;
  background-color: rgba(0, 0, 0, 0.04);
}

.dark .nav-link:hover {
  color: #ffffff;
  background-color: rgba(255, 255, 255, 0.1);
}

/* Icon Button */
.icon-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  color: #424245;
  transition: background-color 0.2s;
}

.dark .icon-btn {
  color: #e8e8ed;
}

.icon-btn:hover {
  background-color: rgba(0, 0, 0, 0.05);
}

.dark .icon-btn:hover {
  background-color: rgba(255, 255, 255, 0.1);
}

/* Apple Primary Button (Blue) */
.auth-btn {
  padding: 4px 16px;
  background-color: #0071e3; /* Apple Blue */
  color: white;
  font-size: 14px;
  font-weight: 500;
  border-radius: 9999px;
  transition: background-color 0.2s, transform 0.1s;
}

.auth-btn:hover {
  background-color: #0077ed;
}

.auth-btn:active {
  transform: scale(0.98);
}

/* Mobile Menu Link */
.mobile-link {
  display: block;
  padding: 6px 10px;
  font-size: 15px; /* iOS Human Interface Guidelines standard size */
  font-weight: 400;
  color: #1d1d1f;
  border-radius: 8px;
  margin-bottom: 4px;
  transition: all 0.2s ease;
  letter-spacing: -0.01em;
}

.mobile-link:hover {
  background: #f5f5f7;
  color: #0071e3;
}

.dark .mobile-link {
  color: #f5f5f7;
  background: #1c1c1e; /* Apple dark gray card color */
}

.dark .mobile-link:hover {
  background: #2c2c2e;
  color: #0077ed;
}

/* Mobile Menu Transition */
.mobile-menu-enter-active,
.mobile-menu-leave-active {
  transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1), opacity 0.3s ease;
}

.mobile-menu-enter-from,
.mobile-menu-leave-to {
  transform: translateY(-100%);
  opacity: 0;
}

/* Route Transitions */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

/* Animation helper for mobile content */
.animate-fade-in {
  animation: fadeIn 0.4s ease-out forwards;
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
</style>