<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import Icon from '@/components/Icon.vue'
import appleLogo from '/assets/Centre/AppleLogo.png'
import PageStart from "../../components/PageStart.vue"; // 确保路径正确

// --- Types ---
interface EventItem {
  title: string
  content: string
  url: string
  icon?: string // 可选：为每个活动添加图标
  bgGradient?: string // 可选：为卡片添加微弱的背景光
}

// --- State ---
const isMobile = ref<boolean>(false)
const hoveredIndex = ref<number | null>(null)

const events = ref<EventItem[]>([
  {
    title: '产品体验',
    content: 'iOS Club 与多家科技企业深度合作。在这里，我们不仅是观察者，更是体验者。第一时间上手最新的 Apple 设备与 Vision Pro 应用。',
    url: '/Article/VisionPro',
    icon: 'smartphone',
    bgGradient: 'from-blue-500/10 via-purple-500/5 to-transparent'
  },
  {
    title: '特别发布会',
    content: '见证科技界的「春晚」。iOS Club 组织线下观影活动，在大屏幕前同步见证未来的到来。未来已来，你来不来？',
    url: '/Article/PressConference',
    icon: 'presentation',
    bgGradient: 'from-orange-500/10 via-red-500/5 to-transparent'
  },
  {
    title: 'Today at iOS Club',
    content: '超越课堂，开启沙龙。从基础学习到项目开发，在分享与讨论中带你打破信息差，掌握最前沿的开发知识。',
    url: '/Article/TodayAtiOSClub',
    icon: 'graduation-cap',
    bgGradient: 'from-green-500/10 via-teal-500/5 to-transparent'
  },
  {
    title: 'Time to Code',
    content: '不仅仅是学习，用技术治理新世界。同时连接彼此，畅享属于开发者的欢乐时光与创意碰撞。',
    url: '/Article/TimeToCode',
    icon: 'ticket',
    bgGradient: 'from-pink-500/10 via-rose-500/5 to-transparent'
  }
])

// --- Logic ---
const checkScreenSize = () => {
  isMobile.value = window.innerWidth <= 768
}

const handleCardClick = (url: string) => {
  if (url) window.open(url, '_blank')
}

// 这一块通常不需要手动 ref 绑定，Tailwind 4 @media 查询更高效，
// 但为了脚本逻辑一致保持监听
onMounted(() => {
  checkScreenSize()
  window.addEventListener('resize', checkScreenSize)
})

onUnmounted(() => {
  window.removeEventListener('resize', checkScreenSize)
})
</script>

<template>
  <!--
    全局容器
    使用了原生 CSS 变量来控制背景，以获得更平滑的过渡
  -->
  <div class="app-container min-h-screen w-full transition-colors duration-500">

    <!-- 动态背景光斑 (Ambient Mesh) -->
    <div class="fixed inset-0 overflow-hidden pointer-events-none -z-10">
      <div class="absolute top-[-10%] left-[-10%] w-[40vw] h-[40vw] bg-purple-400/20 dark:bg-purple-900/20 rounded-full blur-[120px] animate-blob"></div>
      <div class="absolute bottom-[-10%] right-[-10%] w-[35vw] h-[35vw] bg-blue-400/20 dark:bg-blue-900/20 rounded-full blur-[120px] animate-blob animation-delay-2000"></div>
    </div>

    <div class="container mx-auto px-6 py-12 max-w-6xl relative z-10">

      <!-- Header Section: 模仿 Apple 官网排版，居中且克制 -->
      <PageStart
          :title="isMobile ? 'iOS Club 活动' : 'iOS Club 社团活动'"
          subtitle="Think Different"
          :img="appleLogo"
          gradient-class="bg-gradient-to-r from-purple-600 to-pink-600"
      />

      <!-- Grid Section: 模仿 iCloud / App Store 卡片 -->
      <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div
            v-for="(item, index) in events"
            :key="index"
            class="ios-card group relative overflow-hidden p-8 flex flex-col justify-between h-auto md:h-[320px] cursor-pointer"
            :class="{'hover-trans': true}"
            @click="handleCardClick(item.url)"
            @mouseenter="hoveredIndex = index"
            @mouseleave="hoveredIndex = null"
            :style="{ animationDelay: `${index * 100}ms` }"
        >
          <!-- 卡片背景渐变 (用于 Hover 效果增强) -->
          <div class="absolute inset-0 bg-gradient-to-br opacity-0 group-hover:opacity-100 transition-opacity duration-500 ease-out pointer-events-none"
               :class="item.bgGradient || 'from-gray-100/50 to-transparent'">
          </div>

          <!-- 内容区域 -->
          <div class="relative z-10 space-y-4">
            <!-- Icon Header -->
            <div class="flex items-center justify-between">
              <div class="p-3 rounded-2xl bg-gray-100/50 dark:bg-white/10 backdrop-blur-md w-fit">
                <Icon :icon="item.icon || 'sparkles'" class="w-8 h-8 text-purple-600 dark:text-purple-400" />
              </div>
              <!-- 箭头跟随动画 -->
              <div class="opacity-0 -translate-x-4 group-hover:opacity-100 group-hover:translate-x-0 transition-all duration-300 ease-out">
                <Icon icon="circle-arrow-right" class="w-8 h-8 text-gray-400 dark:text-gray-500 hover:text-purple-500 transition-colors" />
              </div>
            </div>

            <h2 class="text-3xl font-semibold tracking-tight text-card-title transition-colors">
              {{ item.title }}
            </h2>

            <p class="text-base md:text-lg leading-relaxed text-card-desc line-clamp-3">
              {{ item.content }}
            </p>
          </div>

          <!-- 底部链接文字 -->
          <div class="relative z-10 mt-4 pt-4 border-t border-gray-200/50 dark:border-white/10 flex items-center text-sm font-medium text-purple-600 dark:text-purple-400 group-hover:text-purple-700 dark:group-hover:text-purple-300 transition-colors">
            <span>进一步了解</span>
            <Icon icon="chevron-right" class="w-4 h-4 ml-1 transform group-hover:translate-x-1 transition-transform" />
          </div>
        </div>
      </div>
    </div>

  </div>
</template>

<style scoped>
/*
  按照要求，在 scoped 中使用原生 CSS
  并使用 .dark .class 的方式处理暗黑模式
*/

/* 全局背景颜色控制 */
.app-container {
  background-color: #fbfbfd; /* Apple 浅色背景通常不是纯白，而是极浅的灰 */
}

.dark .app-container {
  background-color: #000000; /* Apple 深色模式背景通常是纯黑或极深灰 */
}

/* 文本颜色系统 */
.text-primary {
  color: #1d1d1f;
}
.dark .text-primary {
  color: #f5f5f7;
}

.text-secondary {
  color: #86868b;
}
.dark .text-secondary {
  color: #a1a1a6;
}

.text-card-title {
  color: #1d1d1f;
}
.dark .text-card-title {
  color: #f5f5f7;
}

.text-card-desc {
  color: #424245;
}
.dark .text-card-desc {
  color: #86868b;
}

/*
  iOS/macOS 风格卡片核心样式
  特点：大圆角、极其细腻的边框、模糊背景
*/
.ios-card {
  border-radius: 32px; /* Apple 风格的大圆角 */
  background-color: rgba(255, 255, 255, 0.8); /* 晶透白 */
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border: 1px solid rgba(0, 0, 0, 0.05); /* 极细微的边框 */
  box-shadow: 0 4px 24px -1px rgba(0, 0, 0, 0.05);
  transition: all 0.4s cubic-bezier(0.25, 0.8, 0.25, 1); /* iOS 风格的缓动 */
}

/* 深色模式下的卡片 */
.dark .ios-card {
  background-color: rgba(28, 28, 30, 0.6); /* 深灰色半透明 */
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: 0 4px 24px -1px rgba(0, 0, 0, 0.3);
}

/* 悬停效果 */
.ios-card:hover {
  transform: scale(1.02); /* 细微缩放 */
  box-shadow: 0 10px 40px -5px rgba(0, 0, 0, 0.1);
  background-color: rgba(255, 255, 255, 0.95);
}

.dark .ios-card:hover {
  box-shadow: 0 10px 40px -5px rgba(0, 0, 0, 0.5);
  background-color: rgba(28, 28, 30, 0.8);
}

/* 动画 Keyframes */
@keyframes blob {
  0% { transform: translate(0px, 0px) scale(1); }
  33% { transform: translate(30px, -50px) scale(1.1); }
  66% { transform: translate(-20px, 20px) scale(0.9); }
  100% { transform: translate(0px, 0px) scale(1); }
}

.animate-blob {
  animation: blob 7s infinite;
}

.animation-delay-2000 {
  animation-delay: 2s;
}

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
  animation: fadeInUp 0.8s ease-out forwards;
}
</style>