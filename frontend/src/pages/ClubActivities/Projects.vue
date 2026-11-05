<template>
  <!-- 页面容器：模仿 macOS 桌面背景风格 -->
  <div class="page-container min-h-[calc(100vh-64px)] transition-colors duration-500 font-sans">

    <!-- 顶部导航/标题区域 -->
    <header class="pt-20 pb-12 px-6 max-w-7xl mx-auto text-center sm:text-left">
      <div class="flex flex-col sm:flex-row items-center sm:items-end gap-4 sm:gap-6 animate-fade-in-down">
        <!-- 仿 iOS App 图标风格的 Logo -->
        <div
            class="relative w-24 h-24 rounded-[22px] overflow-hidden shadow-2xl bg-white dark:bg-neutral-800 flex items-center justify-center border border-gray-100 dark:border-white/10">
          <img :src="appleLogo" alt="Logo" class="w-16 h-16 object-contain"/>
        </div>

        <div class="space-y-1 text-center sm:text-left">
          <h1 class="text-4xl sm:text-5xl font-semibold bg-linear-to-r from-orange-500 via-pink-500 to-pink-600 bg-clip-text text-transparent animate-gradient">
            iOS Club 社团项目
          </h1>
          <p class="text-lg text-gray-500 dark:text-gray-400 font-medium tracking-wide">
            创造世界的新方式
          </p>
        </div>
      </div>
    </header>

    <!-- 内容区域：iCloud 风格 Bento Grid -->
    <main class="max-w-7xl mx-auto px-6 pb-20">
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">

        <div
            v-for="(item, index) in projects"
            :key="index"
            class="apple-card group relative p-6 cursor-pointer overflow-hidden transition-all duration-300"
            :style="{ animationDelay: `${index * 100}ms` }"
            @click="openUrl(item.url)"
        >
          <!-- 卡片背景装饰 (光晕) -->
          <div
              class="absolute -right-10 -top-10 w-32 h-32 bg-gray-200 dark:bg-white/5 rounded-full blur-3xl transition-opacity opacity-0 group-hover:opacity-100 dark:group-hover:opacity-30"></div>

          <!-- 头部：图标与标题 -->
          <div class="relative z-10 flex items-center justify-between mb-4">
            <div
                class="w-12 h-12 rounded-2xl bg-gray-100 dark:bg-white/10 flex items-center justify-center text-2xl text-gray-900 dark:text-white transition-transform group-hover:scale-110 duration-300">
              <Icon :icon="item.icon"/>
            </div>

            <!-- 外部链接箭头的隐喻 -->
            <div
                class="w-8 h-8 rounded-full bg-transparent flex items-center justify-center text-gray-400 group-hover:text-blue-500 group-hover:bg-blue-50/50 dark:group-hover:bg-blue-500/20 transition-all">
              <Icon icon="arrow-up-right"
                    class="text-xl transform group-hover:translate-x-0.5 group-hover:-translate-y-0.5 transition-transform"/>
            </div>
          </div>

          <!-- 内容 -->
          <div class="relative z-10">
            <h3 class="text-xl font-bold text-gray-900 dark:text-white mb-2 leading-tight">
              {{ item.title }}
            </h3>
            <p class="text-sm text-gray-500 dark:text-gray-400 leading-relaxed line-clamp-3">
              {{ item.content }}
            </p>
          </div>

          <!-- 底部标签 (模拟 App Store 类别) -->
          <div class="relative z-10 mt-6 pt-4 border-t border-gray-100 dark:border-white/5">
            <span
                class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800 dark:bg-white/10 dark:text-gray-300">
              {{ item.tag }}
            </span>
          </div>
        </div>

      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import {onMounted} from 'vue';
import Icon from '@/components/Icon.vue'
import appleLogo from '/assets/Centre/AppleLogo.png'; // 保持原有资源引用

// 定义项目接口
interface ProjectItem {
  title: string;
  content: string;
  url: string;
  icon: string; // Lucide 图标名称
  tag: string;  // 主要技术栈标签
}

// 项目数据
const projects: ProjectItem[] = [
  {
    title: "建大百科小程序",
    content: "可以在微信小程序等平台浏览建大百科",
    url: "https://gitee.com/XAUATiOSClub/uniapp-ioswiki",
    icon: "square-code",
    tag: "UniApp + 微信小程序"
  },
  {
    title: "iOS Club 官网",
    content: "社团的官方门户，展示社团风采与最新动态，基于 Vue 3 + ASP.NET Core 框架构建的现代化 Web 应用。",
    url: "https://gitee.com/XAUATiOSClub/iOSClub.WebSite",
    icon: "globe",
    tag: "Vue 3 + ASP.NET Core"
  },
  {
    title: "LetCoding 平台",
    content: "集成了在线编辑器、编译器与 OJ 系统的全能代码平台。FastApi + Vue 的完美结合。",
    url: "https://gitee.com/XAUATiOSClub/LetCoding",
    icon: "code",
    tag: "Vue + FastAPI"
  },
  {
    title: "文档生成中心",
    content: "自动化文档生成解决方案。利用 WPF 和 ASP.NET WebAPI 强大的后端处理能力。",
    url: "https://gitee.com/XAUATiOSClub/DocumentMaking",
    icon: "files",
    tag: ".NET / Avalonia"
  },
  {
    title: "滑稽账本",
    content: "基于 .NET MAUI 开发的跨平台记账应用，让财务管理变得轻松有趣。",
    url: "https://gitee.com/XAUATiOSClub/huaji-ledger",
    icon: "wallet",
    tag: ".NET MAUI"
  },
  {
    title: "建大导航",
    content: "专为西建大学子设计的校园导航页服务",
    url: "https://gitee.com/luckyfishisdashen/university_link_search",
    icon: "map-pin-search",
    tag: "Vue 3 + ASP.NET Core"
  }
];

const openUrl = (url: string) => {
  window.open(url, '_blank');
};

// 简单的进场动画挂载检查（可选）
onMounted(() => {
  document.body.classList.add('loaded');
});
</script>

<style scoped>
/*
  Page Background
  这里定义页面的基础背景色
*/
.page-container {
  background-color: #F5F5F7; /* Apple 浅灰背景 */
}

/*
  Apple Style Card
  核心设计：模仿 iOS Widget 或 Setting 卡片样式
*/
.apple-card {
  outline: none;
  background-color: rgba(255, 255, 255, 0.7); /* 晶透白 */
  border-radius: 24px; /* iOS 风格的大圆角 */
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04),
  0 4px 16px rgba(0, 0, 0, 0.02); /* 极简阴影 */
  border: 1px solid rgba(255, 255, 255, 0.4);
  backdrop-filter: blur(20px); /* 核心毛玻璃 */
  -webkit-backdrop-filter: blur(20px);

  /* 进场动画基础样式 */
  opacity: 0;
  transform: translateY(20px);
  animation: slide-up 0.8s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}

.apple-card:hover {
  transform: scale(1.02); /* 弹性缩放 */
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.08),
  0 4px 8px rgba(0, 0, 0, 0.02);
  background-color: rgba(255, 255, 255, 0.9);
}

/*
  Dark Mode Overrides
  原生 CSS 适配暗色模式
*/
.dark .page-container {
  background-color: #000000; /* 深邃黑 */
}

.dark .apple-card {
  background-color: rgba(28, 28, 30, 0.6); /* iOS 深灰色 #1C1C1E */
  border: 1px solid rgba(255, 255, 255, 0.08); /* 微弱的白色描边 */
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.4);
}

.dark .apple-card:hover {
  background-color: rgba(44, 44, 46, 0.8); /* 悬浮变亮 */
  border-color: rgba(255, 255, 255, 0.15);
}

/*
  Animations
  使用类似 iOS 的贝塞尔曲线
*/
@keyframes slide-up {
  from {
    opacity: 0;
    transform: translateY(40px) scale(0.96);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

.animate-fade-in-down {
  animation: fade-in-down 0.8s cubic-bezier(0.16, 1, 0.3, 1) forwards;
}

@keyframes fade-in-down {
  from {
    opacity: 0;
    transform: translateY(-20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
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