<template>
  <!-- 背景容器：适配深色模式，增加苹果标志性的模糊背景感 -->
  <div class="min-h-[calc(100vh-64px)] flex items-center justify-center p-4 sm:p-6 bg-[#f5f5f7] dark:bg-[#000000] transition-colors duration-500">
    
    <!-- 主卡片：模拟 macOS 窗口/iOS 小组件风格 -->
    <div class="relative w-full max-w-md group perspective-1000">
      
      <!-- 核心内容区：毛玻璃 + 柔和阴影 -->
      <div class="
        relative overflow-hidden
        bg-white/80 dark:bg-[#1c1c1e]/90
        backdrop-blur-2xl 
        rounded-3xl
        shadow-[0_20px_40px_-12px_rgba(0,0,0,0.1)] dark:shadow-[0_20px_40px_-12px_rgba(0,0,0,0.5)]
        border border-white/20 dark:border-white/10
        transition-all duration-500 ease-out
        hover:shadow-[0_30px_60px_-12px_rgba(0,0,0,0.15)] dark:hover:shadow-[0_30px_60px_-12px_rgba(0,0,0,0.6)]
        hover:-translate-y-1
        z-10
      ">
        
        <!-- 头部区域：参考 iCloud 账户页眉 -->
        <div class="px-8 pt-10 pb-6 text-center">
          <div class="mx-auto w-24 h-24 mb-6 relative group/icon cursor-pointer" @click="handleTitleClick">
             <!-- 图标加一层微妙的光晕 -->
            <div class="absolute inset-0 bg-blue-500/20 rounded-app blur-xl opacity-0 group-hover/icon:opacity-100 transition-opacity duration-500"></div>
            <n-image
              src="/assets/iOS_Club_LOGO.png"
              preview-disabled
              class="w-full h-full rounded-app relative z-10 shadow-sm"
              object-fit="cover"
            />
          </div>
          
          <h1 
            class="text-2xl font-semibold text-[#1d1d1f] dark:text-[#f5f5f7] tracking-tight cursor-pointer hover:opacity-70 transition-opacity"
            @click="handleTitleClick"
          >
            iOS Club of XAUAT 2025
          </h1>
          <p class="text-[#86868b] dark:text-[#86868b] text-sm mt-2 font-medium">
            加入我们的开发者社区
          </p>
        </div>

        <!-- 二维码区域：类似 Wallet 应用的卡片感 -->
        <div class="mx-6 mb-6 p-4 bg-white dark:bg-[#2c2c2e] rounded-2xl shadow-inner border border-gray-100 dark:border-white/5 flex justify-center items-center">
          <img
            alt="二维码"
            src="/assets/other/qrcord.png"
            class="w-64 h-64 object-contain mix-blend-darken dark:mix-blend-normal rounded-lg"
          />
        </div>

        <!-- 操作区域：类似设置菜单的列表 -->
        <div class="px-6 pb-8 space-y-3">
          <!-- 群号信息条 -->
          <div class="
            flex items-center justify-between 
            p-4 
            bg-[#f5f5f7] dark:bg-[#2c2c2e] 
            rounded-xl 
            active:scale-[0.98] transition-transform
          ">
            <div class="flex items-center gap-3">
              <div class="w-8 h-8 rounded-full bg-[#007aff] flex items-center justify-center text-white">
                 <Icon icon="messages-square" width="18" />
              </div>
              <div class="flex flex-col">
                <span class="text-xs text-[#86868b] font-medium">QQ 群号</span>
                <span class="text-sm font-semibold text-[#1d1d1f] dark:text-white select-all">952954710</span>
              </div>
            </div>
             <n-button
                quaternary
                circle
                size="small"
                @click="handleCopyClick"
                class="text-[#007aff] hover:bg-[#007aff]/10"
            >
              <template #icon>
                <Icon icon="copy" />
              </template>
            </n-button>
          </div>

           <!-- 扫码提示 -->
          <div class="flex items-center justify-center gap-2 text-[#86868b] text-xs mt-4">
            <Icon icon="scan" />
            <span>使用 QQ 扫一扫加入群聊</span>
          </div>
        </div>

      </div>
      
      <!-- 底部光晕装饰 -->
      <div class="absolute -inset-4 bg-gradient-to-r from-blue-500/20 via-purple-500/20 to-pink-500/20 rounded-[3rem] blur-3xl -z-10 opacity-50 dark:opacity-30"></div>

    </div>
  </div>
</template>

<script setup lang="ts">
import { h, onMounted } from 'vue'
import { useMessage, NImage, NButton } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import { NavigateTo } from '../../lib/site'
import { ios } from '../../services/AuthService'

const message = useMessage()

const handleTitleClick = () => {
  NavigateTo(ios.url1, ios.url2)
}

const handleCopyClick = async () => {
  const textToCopy = '952954710'
  try {
    await navigator.clipboard.writeText(textToCopy)
    message.success('群号已复制', {
        icon: () => h(Icon, { icon: 'circle-check', class: 'text-[#007aff]' }),
        closable: true
    }) 
  } catch (error) {
    const result = copyTextFallback(textToCopy)
    if (result) {
        message.success('群号已复制')
    } else {
        message.error('复制失败，请手动选择')
    }
  }
}

const copyTextFallback = (text: string): boolean => {
  const textArea = document.createElement('textarea')
  textArea.value = text
  // 避免页面滚动
  textArea.style.position = 'fixed'
  textArea.style.left = '-9999px'
  textArea.style.top = '0'
  document.body.appendChild(textArea)
  textArea.focus()
  textArea.select()

  try {
    const successful = document.execCommand('copy')
    document.body.removeChild(textArea)
    return successful
  } catch (err) {
    document.body.removeChild(textArea)
    return false
  }
}

onMounted(() => {
  // 使用更柔和的初始提示
  console.log('Page Loaded: iOS Club QR Code')
})
</script>

<style scoped>
@reference 'tailwindcss';

/* Apple App Icon shape curve inspired border-radius */
.rounded-app {
  border-radius: 22.5%; 
}

/* 确保透视效果 */
.perspective-1000 {
  perspective: 1000px;
}

/* 字体优化，模仿 San Francisco 字体栈 */
div {
  font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}
</style>