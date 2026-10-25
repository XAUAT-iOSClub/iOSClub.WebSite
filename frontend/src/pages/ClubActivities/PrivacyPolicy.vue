<script setup lang="ts">
import Icon from '@/components/Icon.vue'
import {NButton} from 'naive-ui'
import {useRouter} from "vue-router";

// --- 类型定义 ---
interface PolicySection {
  id: string
  title: string
  icon: string
  iconColor: string // Tailwind color class for bg
  content: string | string[]
}

// --- 数据配置 ---
const lastUpdated = '2026年1月8日'
const router = useRouter()

const sections: PolicySection[] = [
  {
    id: 'collection',
    title: '信息收集',
    icon: 'inbox',
    iconColor: 'bg-blue-500',
    content: [
      '我们收集的信息仅限于为您提供服务所必需的内容。这包括您的账户基本信息（如电子邮件地址、用户名）以及您在使用服务过程中产生的数据。'
    ]
  },
  {
    id: 'usage',
    title: '信息使用',
    icon: 'split',
    iconColor: 'bg-purple-500',
    content: [
      '我们使用收集的信息来运营、维护和改进我们的服务。您的数据帮助我们个性化您的体验，并在出现问题时提供客户支持。',
      '我们承诺不会将您的个人信息出售给第三方用于营销目的。'
    ]
  },
  {
    id: 'cookies',
    title: 'Jwt 与追踪',
    icon: 'circle-dot',
    iconColor: 'bg-orange-500',
    content: '我们使用 Jwt 和类似技术，以在各个会话中保持您的登录状态。'
  },
  {
    id: 'security',
    title: '数据安全',
    icon: 'shield-check',
    iconColor: 'bg-green-500',
    content: '我们采用行业标准的安全措施（如 SSL 加密）来保护您的数据免受未经授权的访问、披露、修改或销毁。'
  },
  {
    id: 'contact',
    title: '联系我们',
    icon: 'send',
    iconColor: 'bg-gray-500',
    content: '如果您对本隐私政策有任何疑问，或希望行使您的数据权利，请通过 iosclubxauat@163.com 与我们联系。'
  }
]

const next = () => {
  router.back()
}

const goToHome = () => {
  router.push('/')
}
</script>

<template>
  <div class="min-h-screen">

    <!-- 主内容区域 -->
    <main class="max-w-3xl mx-auto px-6 py-12 sm:py-20 animate-fade-in-up">

      <!-- 头部信息 -->
      <header class="text-center mb-16">
        <h1 class="text-4xl sm:text-5xl font-bold tracking-tight mb-4 text-black dark:text-white">
          隐私政策
        </h1>
        <p class="text-lg text-gray-500 dark:text-gray-400">
          致力于保护您的数据与隐私安全
        </p>
        <div
            class="mt-4 inline-block px-3 py-1 rounded-full bg-gray-200/50 dark:bg-white/10 text-xs font-medium text-gray-500 dark:text-gray-400">
          更新于 {{ lastUpdated }}
        </div>
      </header>

      <!-- 政策条款列表 -->
      <div class="space-y-6">
        <template v-for="(section, _) in sections" :key="section.id">

          <!-- 仿 iOS 设置项容器 (无 n-card) -->
          <div
              class="group relative overflow-hidden rounded-2xl bg-white dark:bg-[#1C1C1E] p-6 sm:p-8 shadow-[0_2px_8px_rgba(0,0,0,0.04)] dark:shadow-[0_0_0_1px_rgba(255,255,255,0.05)] transition-all duration-300 hover:shadow-lg dark:hover:shadow-[0_0_0_1px_rgba(255,255,255,0.1)]"
          >
            <div class="flex flex-col sm:flex-row gap-5">

              <!-- 图标区域 (iOS 风格彩色方块) -->
              <div class="shrink-0">
                <div
                    :class="`w-12 h-12 rounded-xl flex items-center justify-center text-white shadow-sm ${section.iconColor}`">
                  <Icon :icon="section.icon" class="text-2xl"/>
                </div>
              </div>

              <!-- 内容区域 -->
              <div class="flex-1">
                <h2 class="text-xl font-semibold mb-3 text-gray-900 dark:text-white flex items-center">
                  {{ section.title }}
                </h2>

                <div class="text-[15px] leading-7 text-gray-600 dark:text-gray-300 space-y-3">
                  <template v-if="Array.isArray(section.content)">
                    <p v-for="(para, pIndex) in section.content" :key="pIndex">
                      {{ para }}
                    </p>
                  </template>
                  <template v-else>
                    <p>{{ section.content }}</p>
                  </template>
                </div>
              </div>
            </div>
          </div>
        </template>
      </div>

      <!-- 底部行动呼吁 -->
      <div class="mt-16 text-center space-y-6">
        <p class="text-sm text-gray-400 dark:text-gray-500 max-w-lg mx-auto">
          继续使用我们的服务即表示您同意本隐私政策的条款。
        </p>
        <div class="flex justify-center gap-4">
          <NButton @click="goToHome" type="default" size="medium"
                   class="!px-8 !font-medium dark:!bg-[#2C2C2E] dark:!text-white border-0 !bg-white hover:!bg-gray-50">
            暂不
          </NButton>
          <NButton @click="next" type="info" size="medium" class="!px-8 !font-medium !text-white !bg-[#007AFF] hover:!bg-[#0062CC]">
            同意并继续
          </NButton>
        </div>
      </div>

    </main>

    <!-- 页脚 -->
    <footer class="py-8 border-t border-gray-200 dark:border-white/10 text-center">
      <p class="text-xs text-gray-400 dark:text-gray-600">
        &copy; 2025 iOS Club of XAUAT. All rights reserved.
      </p>
    </footer>

  </div>
</template>

<style scoped>
/* 定义一个简单的淡入上浮动画 */
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

.animate-fade-in-up {
  animation: fadeInUp 0.8s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}

/* 字体优化，针对 Apple 风格 */
.font-sans {
  font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", "Helvetica Neue", Helvetica, "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", Arial, sans-serif;
}
</style>