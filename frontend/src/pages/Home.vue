<template>
  <div class="page-wrapper transition-colors duration-500">
    <!-- 背景装饰 (Mesh Gradient) -->
    <div class="fixed inset-0 z-0 pointer-events-none overflow-hidden">
      <div class="background-blob blob-1"></div>
      <div class="background-blob blob-2"></div>
      <div class="background-blob blob-3"></div>
    </div>

    <!-- Hero Section -->
    <section class="relative z-10 flex flex-col items-center justify-center min-h-[calc(100vh-64px)] px-6 pt-20 pb-10">
      <div class="container w-full mx-auto grid grid-cols-1 lg:grid-cols-2 gap-12 lg:gap-20 items-center">

        <!-- Hero Text Content -->
        <div class="order-2 lg:order-1 text-center lg:text-left space-y-8">
          <div class="space-y-2">
            <h1 class="text-4xl md:text-6xl lg:text-7xl font-bold tracking-tight leading-tight">
              <span class="gradient-text">iOS Club of XAUAT</span>
            </h1>
            <p class="text-4xl md:text-5xl font-bold text-slate-900 dark:text-white tracking-tight">
              在此，创造未来。
            </p>
          </div>

          <div class="flex flex-col gap-4 max-w-2xl mx-auto lg:mx-0">
            <p class="text-lg md:text-xl text-slate-600 dark:text-slate-300 leading-relaxed font-medium">
              <span class="opacity-75 font-normal">Stay hungry, stay foolish.</span>
            </p>
          </div>

          <!-- Action Buttons (Apple Style Pills) -->
          <div class="flex flex-col sm:flex-row gap-4 justify-center lg:justify-start pt-4">
            <button @click="scrollToAbout" class="apple-btn-primary">
              <span>了解更多</span>
              <Icon icon="arrow-down" class="ml-2 text-lg"/>
            </button>

            <button @click="router.push('/login')" class="apple-btn-secondary">
              <span>加入我们</span>
              <Icon icon="user-plus" class="ml-2 text-lg"/>
            </button>
          </div>
        </div>

        <!-- Hero Visual (Logo Card) -->
        <div class="order-1 lg:order-2 flex justify-center lg:justify-end relative">
          <div
              class="relative w-72 h-72 md:w-96 md:h-96 group"
              @mouseenter="logoHovered = true"
              @mouseleave="logoHovered = false"
          >
            <!-- Floating Background Layers -->
            <div
                class="absolute inset-0 bg-gradient-to-br from-blue-500 to-blue-600 rounded-[3rem] transform transition-all duration-700 ease-out"
                :class="logoHovered ? 'rotate-12 scale-105' : 'rotate-6'"
                style="opacity: 0.15"
            ></div>
            <div
                class="absolute inset-0 bg-gradient-to-br from-purple-500 to-purple-600 rounded-[3rem] transform transition-all duration-700 ease-out"
                :class="logoHovered ? '-rotate-12 scale-105' : '-rotate-6'"
                style="opacity: 0.15"
            ></div>

            <!-- Main Logo Card -->
            <div
                class="relative w-full h-full bg-white/90 dark:bg-white/10 backdrop-blur-3xl rounded-[3rem] flex items-center justify-center shadow-2xl border border-gray-200/50 dark:border-white/10 transition-all duration-700"
                :class="logoHovered ? 'scale-105' : 'scale-100'">
              <img
                  src="/assets/iOS_Club_LOGO.png"
                  alt="iOS Club Logo"
                  class="w-3/5 h-3/5 object-contain transition-transform duration-700"
                  :class="logoHovered ? 'scale-110' : 'scale-100'"
              />
            </div>
          </div>
        </div>
      </div>
    </section>

    <!-- Bento Grid Section (About) -->
    <section id="about" class="relative z-10 py-24 px-6">
      <div class="container mx-auto">
        <div class="flex flex-col items-center text-center mb-16">
          <h2 class="ml-3 text-3xl md:text-5xl font-bold text-slate-900 dark:text-white mb-6">
            不仅是社团，<br/>更是你的<span class="text-blue-600 dark:text-blue-400">创意实验室</span>。
          </h2>
          <p class="text-lg text-slate-500 dark:text-slate-400 max-w-2xl">
            探索 iOS Club 的核心价值，发现属于你的无限可能。
          </p>
        </div>

        <!-- Bento Grid Layout -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <div
              v-for="(card, index) in cards"
              :key="index"
              class="apple-bento-card group"
              :class="{
                'md:col-span-2 lg:col-span-2': index === 0 || index === 4,
                'md:col-span-1 lg:col-span-1': index === 1 || index === 2 || index === 3 || index === 5,
              }"
              @click="card.url ? router.push(card.url) : null"
          >
            <div class="relative z-10 h-full flex flex-col justify-between p-6">
              <!-- Decorative Icon in Bottom Right -->
              <div class="absolute bottom-6 right-6 opacity-10 group-hover:opacity-15 transition-opacity duration-300">
                <Icon :icon="card.iconName" class="text-6xl sm:text-7xl md:text-8xl lg:text-9xl transition-all"
                      :style="{ color: card.color }"/>
              </div>

              <div>
                <h3 class="text-2xl font-bold text-slate-900 dark:text-white mb-3 pr-4">
                  {{ card.title }}
                </h3>
                <p class="text-base md:text-lg text-slate-600 dark:text-slate-300 leading-relaxed opacity-90">
                  {{ card.content }}
                </p>
              </div>

              <div
                  class="mt-8 flex items-center text-base font-semibold transition-all duration-300 group-hover:translate-x-1"
                  :style="{ color: card.color }">
                <span v-if="card.url">了解更多</span>
                <Icon v-if="card.url" icon="arrow-right" class="ml-2"/>
              </div>
            </div>

            <!-- Hover Gradient Overlay -->
            <div
                class="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-500 pointer-events-none"
                :style="{ background: `radial-gradient(800px circle at top right, ${card.color}15, transparent 40%)` }">
            </div>
          </div>
        </div>

        <div class="mt-16 text-center">
          <button @click="router.push('/About')" class="apple-text-link text-lg group">
            深入了解我们的故事
            <Icon icon="chevron-right"
                  class="inline-block ml-1 group-hover:translate-x-1 transition-transform"/>
          </button>
        </div>
      </div>
    </section>

    <!-- Minimal Footer -->
    <footer
        class="relative z-10 border-t border-slate-200 dark:border-white/10 bg-white/50 dark:bg-black/20 backdrop-blur-xl">
      <div class="max-w-7xl mx-auto px-6 py-12 flex flex-col md:flex-row justify-between items-center gap-6">
        <p class="text-[10px] md:text-xs text-slate-500 dark:text-slate-400 font-medium">
          Copyright © 2023 - {{ new Date().getFullYear() }} iOS Club of XAUAT. All rights reserved.
        </p>

         <div class="flex items-center gap-6 text-xs font-medium text-slate-600 dark:text-slate-300">
          <a href="https://cn.xauat.edu.cn/" target="_blank"
             class="hover:text-blue-600 dark:hover:text-blue-400 transition-colors">学校官网</a>
          <router-link to="/PrivacyPolicy"
             class="hover:text-blue-600 dark:hover:text-blue-400 transition-colors">隐私政策</router-link>
          <router-link to="/HelpCenter"
             class="hover:text-blue-600 dark:hover:text-blue-400 transition-colors">帮助中心</router-link>
          <a href="https://beian.miit.gov.cn/" target="_blank"
             class="hover:text-blue-600 dark:hover:text-blue-400 transition-colors">陕ICP备2024031872号</a>
          <a href="https://gitee.com/XAUATiOSClub" target="_blank"
             class="hover:text-blue-600 dark:hover:text-blue-400 transition-colors flex items-center gap-1">
             <Icon icon="brand-gitee"/>
             Gitee
          </a>
        </div>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import {ref} from 'vue'
import {useRouter} from 'vue-router'
import Icon from '@/components/Icon.vue'

const router = useRouter()
const logoHovered = ref(false)

interface Card {
  iconName: string
  title: string
  content: string
  color: string
  bgColor?: string // Derived light background
  url?: string
}

// 辅助函数：生成淡色背景
const getBgColor = (hex: string) => {
  // 这里简单演示，实际可以使用 polished 库或者 CSS 变量
  return hex + '1A' // 10% opacity
}

// Data adapted with updated Icons (Lucide, Apple look)
const cards: Card[] = [
  {
    iconName: 'graduation-cap',
    title: '我们是谁?',
    content: '由 Apple 公司资金支持，受西建大学管中心指导的顶尖科技社团。在这里，创意不再是空想。',
    color: '#0071E3', // Apple Blue
    bgColor: getBgColor('#0071E3'),
    url: '/About'
  },
  {
    iconName: 'users-round',
    title: '结伴同行',
    content: '无论你是代码大神还是小白，这里都有你的位置。',
    color: '#FF9500', // Apple Orange
    bgColor: getBgColor('#FF9500'),
  },
  {
    iconName: 'monitor-smartphone',
    title: '不止 iOS',
    content: '跨越专业界限，涵盖软件开发、硬件开发、产品策划与前沿数码体验。',
    color: '#34C759', // Apple Green
    bgColor: getBgColor('#34C759'),
  },
  {
    iconName: 'calendar-heart',
    title: '精彩活动',
    content: '定期的 WWDC 观影、技术沙龙、Swift 编程挑战赛以及 Apple 官方专家讲座。',
    color: '#FF375F', // Apple Pink
    bgColor: getBgColor('#FF375F'),
    url: '/Event'
  },
  {
    iconName: 'square-code',
    title: '项目孵化',
    content: '寻找志同道合的 iMember，从 0 到 1 ，构建新的世界。',
    color: '#AF52DE', // Apple Purple
    bgColor: getBgColor('#AF52DE'),
  },
  {
    iconName: 'rocket',
    title: '构建社区',
    content: '致力于打造西建大最具影响力的科技社区，连接全校每一位极客。',
    color: '#5E5CE6', // Apple Indigo
    bgColor: getBgColor('#5E5CE6'),
  }
]

const scrollToAbout = (): void => {
  const element = document.getElementById('about')
  if (element) {
    const headerOffset = 80
    const elementPosition = element.getBoundingClientRect().top
    const offsetPosition = elementPosition + window.scrollY - headerOffset
    window.scrollTo({top: offsetPosition, behavior: 'smooth'})
  }
}
</script>

<style scoped>
/*
  Native CSS Styling as requested
  Using CSS Variables for easy dark mode handling
*/

.page-wrapper {
  background-color: #F5F5F7; /* Apple light gray background */
  min-height: 100vh;
  color: #1D1D1F;
}

/* Dark Mode Overrides */
.dark .page-wrapper {
  background-color: transparent;
  color: #F5F5F7;
}

/* Apple Card Style (Glassmorphism Base) */
.apple-card {
  background: rgba(255, 255, 255, 0.7);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border-radius: 40px;
  border: 1px solid rgba(255, 255, 255, 0.5);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.05);
}

.dark .apple-card {
  background: rgba(28, 28, 30, 0.6);
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
}

/* Bento Box Card */
.apple-bento-card {
  position: relative;
  background: #FFFFFF;
  border-radius: 28px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
  border: 1px solid rgba(0, 0, 0, 0.05);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  min-height: 240px;
}

.apple-bento-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
  border-color: rgba(0, 0, 0, 0.08);
}

.dark .apple-bento-card {
  background: #1C1C1E;
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: none;
}

.dark .apple-bento-card:hover {
  background: #2C2C2E;
  border-color: rgba(255, 255, 255, 0.15);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.2);
}

/* Buttons */
.apple-btn-primary {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 14px 32px;
  border-radius: 9999px;
  background-color: #0071E3;
  color: white;
  font-weight: 600;
  font-size: 1.05rem;
  transition: all 0.3s ease;
  border: none;
  cursor: pointer;
}

.apple-btn-primary:hover {
  background-color: #0077ED;
  transform: scale(1.02);
  box-shadow: 0 8px 20px rgba(0, 113, 227, 0.3);
}

.apple-btn-secondary {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 14px 32px;
  border-radius: 9999px;
  background-color: rgba(0, 0, 0, 0.05);
  color: #1D1D1F;
  font-weight: 600;
  font-size: 1.05rem;
  transition: all 0.3s ease;
  border: none;
  cursor: pointer;
}

.dark .apple-btn-secondary {
  background-color: rgba(255, 255, 255, 0.1);
  color: white;
}

.apple-btn-secondary:hover {
  background-color: rgba(0, 0, 0, 0.1);
}

.dark .apple-btn-secondary:hover {
  background-color: rgba(255, 255, 255, 0.2);
}

/* Text Link */
.apple-text-link {
  color: #06c;
  font-weight: 500;
  cursor: pointer;
  transition: opacity 0.2s;
}

.apple-text-link:hover {
  text-decoration: underline;
  opacity: 0.8;
}

.dark .apple-text-link {
  color: #2997ff;
}

/* Background Blobs (Mesh Gradient simulation) */
.background-blob {
  position: absolute;
  border-radius: 50%;
  filter: blur(80px);
  opacity: 0.4;
  animation: blobFloat 10s infinite alternate;
}

.blob-1 {
  top: -10%;
  left: -10%;
  width: 50vw;
  height: 50vw;
  background: radial-gradient(circle, #E0F2FE 0%, transparent 70%);
}

.dark .blob-1 {
  background: radial-gradient(circle, #1e3a8a40 0%, transparent 70%);
}

.blob-2 {
  bottom: 10%;
  right: -10%;
  width: 40vw;
  height: 40vw;
  background: radial-gradient(circle, #F5F3FF 0%, transparent 70%);
  animation-delay: -2s;
}

.dark .blob-2 {
  background: radial-gradient(circle, #4c1d9540 0%, transparent 70%);
}

.blob-3 {
  top: 40%;
  left: 30%;
  width: 30vw;
  height: 30vw;
  background: radial-gradient(circle, #FCE7F3 0%, transparent 70%);
  animation-delay: -5s;
}

.dark .blob-3 {
  background: radial-gradient(circle, #83184340 0%, transparent 70%);
}

@keyframes blobFloat {
  0% {
    transform: translate(0, 0) scale(1);
  }
  100% {
    transform: translate(20px, -20px) scale(1.1);
  }
}

/* Gradient Text - Kept from original */
.gradient-text {
  background: linear-gradient(
      -64deg,
      #f9bf65, #ffab6b, #ff9977, #fc8986,
      #ef7e95, #e47da6, #d37fb5, #bf83c1,
      #ab8dcf, #9597d8, #7fa0dc, #6ca7da
  );
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  background-size: 200% 200%;
  animation: gradientFlow 8s ease infinite;
}

@keyframes gradientFlow {
  0%, 100% {
    background-position: 0 50%;
  }
  50% {
    background-position: 100% 50%;
  }
}

/* Custom Scrollbar */
::-webkit-scrollbar {
  width: 0; /* Hide scrollbar for cleaner look on macOS */
  background: transparent;
}
</style>