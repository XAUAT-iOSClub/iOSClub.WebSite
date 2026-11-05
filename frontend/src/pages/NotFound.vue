<template>
  <!-- 全屏容器：处理背景与居中布局 -->
  <div class="relative flex min-h-[calc(100vh-64px)] w-full flex-col items-center justify-center overflow-hidden transition-colors duration-500 bg-[#F5F5F7] dark:bg-[#000000]">
    
    <!-- 装饰性背景光晕 (仿 macOS 壁纸氛围) -->
    <div class="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-blue-400/20 dark:bg-blue-500/10 rounded-full blur-[120px] pointer-events-none"></div>
    <div class="absolute top-1/3 left-1/3 -translate-x-1/2 -translate-y-1/2 w-[400px] h-[400px] bg-purple-400/20 dark:bg-purple-500/10 rounded-full blur-[100px] pointer-events-none"></div>

    <!-- 主要内容区域 -->
    <main class="z-10 flex flex-col items-center px-6 text-center max-w-md mx-auto animation-fade-in-up">
      
      <!-- 图标容器：仿 iCloud 图标风格 -->
      <div class="mb-8 relative group">
        <div class="absolute inset-0 bg-linear-to-br from-blue-100 to-purple-100 dark:from-blue-900/30 dark:to-purple-900/30 rounded-4xl blur-xl opacity-50 group-hover:opacity-70 transition-opacity duration-500"></div>
        <div class="relative flex items-center justify-center w-32 h-32 bg-white/80 dark:bg-white/10 backdrop-blur-2xl border border-white/20 shadow-2xl rounded-4xl transition-transform duration-500 hover:scale-105">
          <Icon icon="triangle-alert" class="w-16 h-16 text-[#0071e3] dark:text-[#2997ff]" />
        </div>
      </div>

      <!-- 错误代码 -->
      <h1 class="text-8xl font-extrabold tracking-tighter text-transparent bg-clip-text bg-linear-to-b from-gray-900 to-gray-600 dark:from-white dark:to-gray-400 mb-2 font-sf">
        404
      </h1>

      <!-- 标题与描述 -->
      <h2 class="text-2xl font-semibold text-gray-900 dark:text-white mb-4 tracking-tight">
        页面未找到
      </h2>
      
      <p class="text-base text-gray-500 dark:text-gray-400 mb-10 leading-relaxed max-w-[300px]">
        您所寻找的页面可能已被移动、删除或暂时不可用。请检查网址或返回首页。
      </p>

      <!-- 操作按钮组 -->
      <div class="flex flex-col sm:flex-row gap-4 w-full sm:w-auto">
        <!-- 主按钮：主要样式 -->
        <n-button 
          type="primary" 
          size="large" 
          class="rounded-full! px-8! h-12! text-base! !font-medium shadow-lg shadow-blue-500/20 hover:shadow-blue-500/40 transition-shadow"
          :color="isDark ? '#0A84FF' : '#0071e3'"
          @click="goHome"
        >
          <template #icon>
            <Icon icon="house" />
          </template>
          返回首页
        </n-button>

        <!-- 辅助按钮：次要样式 -->
        <n-button 
          secondary
          size="large"
          class="!rounded-full !px-8 !h-12 !text-base !font-medium !bg-white/50 dark:!bg-white/5 !backdrop-blur-md !border-transparent hover:!bg-white hover:dark:!bg-white/10 transition-all"
          @click="goBack"
        >
          <template #icon>
             <Icon icon="arrow-left" />
          </template>
          返回上一页
        </n-button>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { NButton } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import { useOsTheme } from 'naive-ui'
import { computed } from 'vue'

// 路由控制
const router = useRouter()

// 简单的深色模式检测 (或者你可以接入你的全局 Pinia store)
const osTheme = useOsTheme()
const isDark = computed(() => osTheme.value === 'dark')

const goHome = () => {
  router.push('/')
}

const goBack = () => {
  router.back()
}
</script>

<style scoped>
/* 简单模拟 SF Pro 字体栈，如果在项目中已全局配置可移除 */
.font-sf {
  font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
}

/* 这里是一些自定义动画，Tailwind 4.0 可以直接在 CSS 中写，也可以配置在 theme 中 */
@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.animation-fade-in-up {
  animation: fadeInUp 0.8s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}
</style>