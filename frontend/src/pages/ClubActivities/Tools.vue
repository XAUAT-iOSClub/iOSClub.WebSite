<template>
  <!--
    背景设计：
    - 浅色模式：经典的 Apple 浅灰色背景 (#F5F5F7)
    - 深色模式：纯黑或极深灰 (#000000)
  -->
  <div class="min-h-screen bg-[#F5F5F7] dark:bg-black transition-colors duration-500 ease-apple">

    <!-- 顶部模糊光斑效果 (类似 macOS 壁纸) -->
    <div class="fixed top-0 left-0 w-full h-full overflow-hidden pointer-events-none -z-1">
      <div
          class="absolute top-[-20%] right-[-10%] w-[600px] h-[600px] bg-blue-400/20 dark:bg-blue-600/10 rounded-full blur-[120px] mix-blend-multiply dark:mix-blend-normal animate-blob"></div>
      <div
          class="absolute bottom-[-20%] left-[-10%] w-[500px] h-[500px] bg-indigo-400/20 dark:bg-indigo-600/10 rounded-full blur-[100px] mix-blend-multiply dark:mix-blend-normal animate-blob animation-delay-2000"></div>
    </div>

    <div class="container mx-auto px-4 sm:px-6 max-w-6xl pt-8 sm:pt-12 pb-20 relative z-10">

      <!-- 头部组件 -->
      <PageStart
          :title="isMobile ? 'iOS Club' : 'iOS Club 工具箱'"
          subtitle="高效 · 简约 · 创造"
          :img="toolsImage"
          class="mb-10"
          gradient-class="bg-gradient-to-r from-blue-600 to-green-500"
      />

      <!--
        网格布局：
        - 模仿 iCloud 网页版的应用网格
        - 自适应：手机单列/双列，平板三列，桌面四列
      -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-5 sm:gap-6">
        <a
            v-for="(link, index) in models"
            :key="link.key || index"
            :href="link.url"
            target="_blank"
            class="apple-card group relative flex items-center p-4 sm:p-5 cursor-pointer"
            :style="{ animationDelay: `${index * 50}ms` }"
        >
          <!-- 卡片背景与高光 (CSS处理) -->
          <div class="card-bg absolute -z-10 inset-0 rounded-[20px] transition-all duration-300"></div>

          <!-- 图标区域：仿 iOS App 图标容器 -->
          <div
              class="relative flex-shrink-0 w-[52px] h-[52px] sm:w-[60px] sm:h-[60px] mr-4 flex items-center justify-center bg-white dark:bg-[#2c2c2e] rounded-[14px] shadow-sm border border-black/5 dark:border-white/10 overflow-hidden group-hover:scale-105 transition-transform duration-300 ease-apple">

            <!-- 情况1: IconFont 图标 (如果 link.icon 是字符串且不含http) -->
            <template v-if="link.icon && !link.icon.startsWith('http') && !link.icon.includes('/')">
              <IconFont
                  className="w-7 h-7 sm:w-8 sm:h-8 dark:text-gray-100"
                  :type="link.icon"/>
            </template>

            <!-- 情况2: 图片 URL -->
            <template v-else>
              <img
                  :src="fixImageUrl(link)"
                  class="w-full h-full object-cover"
                  :alt="link.name"
                  @error="(e) => handleImageError(e)"
                  loading="lazy"
              />
            </template>
          </div>

          <!-- 文字内容 -->
          <div class="flex-1 py-1">
            <div class="flex items-center justify-between">
              <h3 class="text-[17px] font-medium text-gray-900 dark:text-white leading-tight truncate group-hover:text-[#0066CC] dark:group-hover:text-[#0A84FF] transition-colors">
                {{ link.name }}
              </h3>

              <!-- 悬停时出现的箭头 -->
              <Icon
                  icon="arrow-up-right"
                  class="w-4 h-4 text-gray-400 opacity-0 -translate-x-2 group-hover:opacity-100 group-hover:translate-x-0 transition-all duration-300"
              />
            </div>
            <p class="mt-1 text-[13px] sm:text-[14px] text-gray-500 dark:text-gray-400 line-clamp-1 font-normal">
              {{ link.description || '前往使用' }}
            </p>
          </div>
        </a>

        <!-- 空状态 -->
        <div v-if="models.length === 0"
             class="col-span-full flex flex-col items-center justify-center py-20 animate-fade-in">
          <div class="w-20 h-20 bg-gray-100 dark:bg-[#1c1c1e] rounded-full flex items-center justify-center mb-4">
            <Icon icon="folder-open" class="w-10 h-10 text-gray-400"/>
          </div>
          <span class="text-gray-500 font-medium">暂无工具</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {ref, onMounted, computed} from 'vue';
import Icon from '@/components/Icon.vue' // 替代 n-icon
import PageStart from "../../components/PageStart.vue";
import {ToolService} from "../../services/ToolService";
import type {LinkModel} from "../../models";
import toolsImage from '/assets/Centre/AppleLogo.png';
import IconFont from "../../components/IconFont.vue";
// @ts-ignore
import '//at.alicdn.com/t/c/font_4612528_md4hjwjgcb.js';

// 状态定义
const models = ref<LinkModel[]>([]);
const windowWidth = ref(window.innerWidth);

// 计算属性：判断是否移动端
const isMobile = computed(() => windowWidth.value < 640);

// 监听窗口大小 (简单的防抖可以在这里添加，如果需要)
window.addEventListener('resize', () => {
  windowWidth.value = window.innerWidth;
});

/**
 * 处理图片URL
 * 如果是相对路径或缺少协议，尝试修复
 */
const fixImageUrl = (tool: LinkModel): string => {
  if (tool.icon && (tool.icon.startsWith('http') || tool.icon.startsWith('/'))) {
    // 简单的 http 修复逻辑，如果原逻辑有特定需求可保留
    return tool.icon.replace(/([^:])(\/\/)/g, '$1/');
  }

  if (tool.url) {
    try {
      // 提取域名获取 Favicon
      const domain = new URL(tool.url).hostname;
      return `https://${domain}/favicon.ico`;
    } catch (e) {
      return toolsImage;
    }
  }

  return toolsImage;
};

/**
 * 图片加载错误处理
 */
const handleImageError = (event: Event) => {
  const img = event.target as HTMLImageElement;
  if (img.src.includes(toolsImage)) return; // 防止死循环
  img.src = toolsImage; // 回退默认图
};

// 数据获取
onMounted(async () => {
  try {
    const res = await ToolService.getTools();
    // 简单的数据校验
    models.value = Array.isArray(res?.links) ? res.links : [];
  } catch (error) {
    console.error("Failed to load tools:", error);
  }
});
</script>

<style scoped>
/*<script src=""></script>
   原生 CSS 区域
   遵循 .dark .class 规范
*/

/* 苹果风格的缓动曲线 */
.ease-apple {
  transition-timing-function: cubic-bezier(0.25, 0.1, 0.25, 1.0);
}

/* 卡片基础样式 (Glassmorphism Lite) */
.apple-card {
  /* 初始状态：滑入动画 */
  opacity: 0;
  animation: fade-up 0.6s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}

/* 卡片背景层：分离背景以便做毛玻璃或特定阴影，不影响内容 */
.card-bg {
  background-color: #ffffff;
  /* 浅色模式下的投影：极其细腻 */
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04), 0 4px 12px rgba(0, 0, 0, 0.02);
  border: 1px solid rgba(0, 0, 0, 0.03);
}

/* Hover 状态：提升 */
.apple-card:hover .card-bg {
  /* 加深投影 */
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.06), 0 2px 6px rgba(0, 0, 0, 0.04);
  transform: translateY(-2px);
}

/* 点击按下状态：微缩 */
.apple-card:active .card-bg {
  transform: scale(0.98);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
}

/* 暗黑模式下的卡片样式 */
.dark .card-bg {
  /* 深灰背景，类似 iOS设置页面的二级背景 */
  background-color: #1C1C1E;
  border: 1px solid rgba(255, 255, 255, 0.08); /* 微妙的亮边框增加对比度 */
  box-shadow: none;
}

.dark .apple-card:hover .card-bg {
  background-color: #2C2C2E; /* Hover 变亮一点 */
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.3);
}

/* 动画关键帧 */
@keyframes fade-up {
  from {
    opacity: 0;
    transform: translateY(20px) scale(0.96);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

/* 头部背景光斑动画 */
@keyframes blob {
  0% {
    transform: translate(0px, 0px) scale(1);
  }
  33% {
    transform: translate(30px, -50px) scale(1.1);
  }
  66% {
    transform: translate(-20px, 20px) scale(0.9);
  }
  100% {
    transform: translate(0px, 0px) scale(1);
  }
}

.animate-blob {
  animation: blob 10s infinite;
}

.animation-delay-2000 {
  animation-delay: 2s;
}

/* 简单的淡入 */
.animate-fade-in {
  animation: fade-in 0.5s ease-out forwards;
}

@keyframes fade-in {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}
</style>