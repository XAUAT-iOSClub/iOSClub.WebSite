<template>
  <!-- 外部容器：模拟 iOS/macOS 系统背景色 -->
  <div class="page-container min-h-[calc(100vh-64px)] transition-colors duration-500">

    <!-- 内容限制宽度的居中容器 -->
    <div class="mx-auto max-w-5xl px-6 py-12 sm:px-8 sm:py-20">

      <!-- 头部：复用 PageStart 或重构为简约标题 -->
      <PageStart
          title="iOS Club 历史"
          subtitle="历史是时代的见证"
          :img="historyImage"
          gradient-class="bg-gradient-to-r from-blue-600 to-green-500"
      />

      <!-- 核心 Grid 布局：仿 iCloud 仪表盘风格 -->
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-5 sm:gap-6">

        <div
            v-for="(item, index) in historyItems"
            :key="index"
            class="apple-card group relative overflow-hidden p-6 sm:p-8 cursor-pointer"
            :style="{ animationDelay: `${index * 100}ms` }"
            @click="handleCardClick(item)"
        >
          <!-- 卡片磨砂/渐变背景装饰 (仅在 Dark 模式微调) -->
          <div class="absolute top-0 right-0 -mt-4 -mr-4 w-24 h-24 rounded-full opacity-20 blur-2xl transition-colors duration-500"
               :class="item.bgClass"></div>

          <div class="relative z-10 flex flex-col h-full justify-between">

            <!-- 图标与标题区域 -->
            <div class="flex items-start space-x-4">
              <!-- 图标容器：仿 iOS App 图标风格 -->
              <div
                  class="flex-shrink-0 w-12 h-12 rounded-xl flex items-center justify-center text-white shadow-sm transition-transform duration-300 group-hover:scale-110"
                  :class="item.iconBgClass"
              >
                <Icon :icon="item.icon" class="w-6 h-6" />
              </div>

              <div class="flex-1 pt-1">
                <h3 class="text-xl font-semibold tracking-tight text-primary mb-1 group-hover:text-blue-500 transition-colors">
                  {{ item.title }}
                </h3>
                <p class="text-sm font-medium text-tertiary uppercase tracking-wider">
                  {{ item.yearLabel }}
                </p>
              </div>

              <!-- 右上角箭头 -->
              <div class="text-tertiary opacity-0 transform translate-x-[-10px] transition-all duration-300 group-hover:opacity-100 group-hover:translate-x-0">
                <Icon icon="arrow-right" class="w-6 h-6" />
              </div>
            </div>

            <!-- 描述文本 -->
            <div class="mt-6">
              <p class="text-base leading-relaxed text-secondary line-clamp-2">
                {{ item.content }}
              </p>
            </div>
          </div>
        </div>

      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import Icon from '@/components/Icon.vue'
import historyImage from '/assets/Centre/History.png'
import PageStart from "../../components/PageStart.vue"; // 保持原有引用

// 定义类型接口
interface HistoryItem {
  title: string;
  content: string;
  url: string;
  imageUrl?: string;
  icon: string;        // Lucide 图标名称
  iconBgClass: string; // Tailwind 背景色类名
  bgClass: string;     // 装饰背景色
  yearLabel: string;   // 添加一个年份标签增加设计感
}

const router = useRouter()

// 数据源：添加了设计所需的图标和颜色
const historyItems: HistoryItem[] = [
  {
    title: "总述",
    content: "我们社团从何而来，又要到哪里去，这是一个值得思考的问题。探索 iOS Club 的核心理念与发展脉络。",
    url: "/Article/History-Overview",
    icon: "library",
    iconBgClass: "bg-blue-500",
    bgClass: "bg-blue-500",
    yearLabel: "Overview"
  },
  {
    title: "创社时代",
    content: "社团的开始总是有着一段传奇故事。回溯那些充满激情与梦想的最初时刻，星星之火如何燎原。",
    url: "/Article/History-Founding",
    icon: "flag",
    iconBgClass: "bg-orange-500",
    bgClass: "bg-orange-500",
    yearLabel: "2019 - 2021"
  },
  {
    title: "邵韩之治",
    content: "解决完社团的初步建设，接下来就要进行社团的发展了。这是社团走向规范化、规模化的关键时期。",
    url: "/Article/History-Shao Han's Reign",
    icon: "hammer",
    iconBgClass: "bg-purple-500",
    bgClass: "bg-purple-500",
    yearLabel: "GOLDEN AGE"
  },
  {
    title: "活动室变迁",
    content: "沧海桑田，我们正在努力。每一次搬迁都见证了我们的成长，物理空间的变化折射出精神的传承。",
    url: "/Article/History-Room",
    icon: "map",
    iconBgClass: "bg-green-500",
    bgClass: "bg-green-500",
    yearLabel: "Locations"
  }
];

const handleCardClick = (item: HistoryItem) => {
  if (item.url) {
    router.push(item.url);
  }
};
</script>

<style scoped>
/* 定义动画 */
@keyframes fadeInDown {
  from {
    opacity: 0;
    transform: translateY(-20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes cardReveal {
  from {
    opacity: 0;
    transform: scale(0.96) translateY(20px);
  }
  to {
    opacity: 1;
    transform: scale(1) translateY(0);
  }
}

.animate-fade-in-down {
  animation: fadeInDown 0.8s cubic-bezier(0.2, 0.8, 0.2, 1) forwards;
}

/*
   原生 CSS 样式控制
   遵循 Apple 设计规范：平滑过渡、微妙阴影、圆角
*/

.page-container {
  background-color: #F5F5F7; /* Apple 浅灰色背景 */
}

/* 暗黑模式下的背景覆盖 */
.dark .page-container {
  background-color: #000000; /* 纯黑背景 */
}

/* 文本颜色变量化管理 */
.text-primary {
  color: #1d1d1f;
}
.text-secondary {
  color: #86868b;
}
.text-tertiary {
  color: #a1a1a6;
}

.dark .text-primary {
  color: #f5f5f7;
}
.dark .text-secondary {
  color: #a1a1a6;
}
.dark .text-tertiary {
  color: #6e6e73;
}

/* 自定义卡片样式 */
.apple-card {
  /* 基础结构 */
  background-color: #FFFFFF;
  border-radius: 24px; /* 典型的 iOS 大圆角 */
  border: 1px solid rgba(0, 0, 0, 0.04);
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.02), 0 2px 4px -1px rgba(0, 0, 0, 0.02);

  /* 动画 */
  transition: all 0.4s cubic-bezier(0.25, 0.8, 0.25, 1);
  animation: cardReveal 0.8s cubic-bezier(0.2, 0.8, 0.2, 1) backwards;
}

/* 卡片悬停态：模仿 iPadOS 指针悬停效果 / iOS App Store 点击反馈 */
.apple-card:hover {
  transform: scale(1.02);
  box-shadow: 0 20px 40px -10px rgba(0, 0, 0, 0.08);
}

/* 卡片暗黑模式 */
.dark .apple-card {
  background-color: #1C1C1E; /* iOS 深色二级背景 */
  border: 1px solid rgba(255, 255, 255, 0.08); /* 微妙的边框提升对比度 */
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.3);
}

.dark .apple-card:hover {
  background-color: #2C2C2E; /* Hover 稍微变亮 */
  box-shadow: 0 20px 40px -10px rgba(0, 0, 0, 0.5);
}
</style>

<style scoped>
.animate-fade-in {
  animation: fade-in 0.8s ease-out;
}

.animate-gradient {
  background-size: 200% 200%;
  animation: gradient 3s ease infinite;
}

@keyframes fade-in {
  from {
    opacity: 0;
    transform: translateY(-20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes slide-up {
  from {
    opacity: 0;
    transform: translateY(30px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes gradient {
  0%, 100% {
    background-position: 0 50%;
  }
  50% {
    background-position: 100% 50%;
  }
}

@keyframes float {
  0%, 100% {
    transform: translateY(0px);
  }
  50% {
    transform: translateY(-20px);
  }
}
</style>