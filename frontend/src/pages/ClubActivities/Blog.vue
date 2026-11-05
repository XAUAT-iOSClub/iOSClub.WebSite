<template>
  <meta name="referrer" content="never">

  <!-- 页面容器：Apple 风格的灰色背景 -->
  <div class="min-h-screen page-bg text-slate-900 transition-colors duration-500">

    <div class="container mx-auto px-4 sm:px-6 lg:px-8 max-w-7xl py-12">
      <!-- 头部区域：大标题风格 -->
      <div class="mb-12 animate-fade-in-down">
        <PageStart
            :title="isMobile ? 'iOS Club' : 'iOS Club 博客'"
            subtitle="以 Swift 之思，行未来之事"
            :img="articleHeaderImg"
            gradient-class="bg-gradient-to-r from-purple-600 to-pink-600"
        />
      </div>

      <!-- 主要内容区：Bento Grid 布局 -->
      <div class="flex flex-col gap-10">

        <!-- 文章板块 (App Store Today 风格) -->
        <section>
          <div class="flex items-center justify-between mb-6 px-2">
            <h2 class="text-2xl font-bold tracking-tight section-title">最新文章</h2>
          </div>

          <!-- 加载骨架屏 -->
          <div v-if="loading" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            <div v-for="i in 3" :key="i" class="h-96 rounded-3xl bg-gray-200 dark:bg-neutral-800 animate-pulse"></div>
          </div>

          <!-- 文章网格 -->
          <div v-else-if="rssArticles.length > 0" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            <article
                v-for="(article, index) in rssArticles"
                :key="index"
                class="apple-card group relative flex flex-col overflow-hidden cursor-pointer"
                :style="{ animationDelay: `${index * 100}ms` }"
                @click="openArticle(article.url)"
            >
              <!-- 图片区域 -->
              <div class="aspect-[4/3] w-full overflow-hidden bg-gray-100 dark:bg-neutral-800 relative">
                <img
                    v-if="article.image"
                    :src="article.image"
                    :alt="article.title"
                    class="h-full w-full object-cover transition-transform duration-700 ease-out group-hover:scale-105"
                    @error="handleImageError($event)"
                />
                <div v-else
                     class="h-full w-full flex items-center justify-center bg-gray-50 dark:bg-neutral-800 text-gray-300">
                  <Icon icon="brand-apple" class="text-6xl opacity-20"/>
                </div>
                <!-- 渐变遮罩 (提升文字可读性) -->
                <div class="absolute inset-0 bg-gradient-to-t from-black/10 to-transparent pointer-events-none"></div>
              </div>

              <!-- 内容区域 -->
              <div class="flex-1 p-6 flex flex-col justify-between bg-white/80 dark:bg-neutral-900/80 backdrop-blur-sm">
                <div>
                  <div class="text-xs font-semibold uppercase tracking-wider text-blue-500 mb-2">文章</div>
                  <h3 class="text-xl font-bold leading-tight text-primary line-clamp-2 mb-2">
                    {{ article.title }}
                  </h3>
                </div>

                <div
                    class="mt-4 flex items-center text-sm font-medium text-secondary group-hover:text-blue-500 transition-colors">
                  <span>阅读全文</span>
                  <Icon icon="chevron-right" class="ml-1 w-4 h-4"/>
                </div>
              </div>
            </article>
          </div>

          <!-- 空状态 -->
          <div v-else class="flex flex-col items-center justify-center py-20 text-center">
            <div class="w-20 h-20 rounded-full bg-gray-100 dark:bg-neutral-800 flex items-center justify-center mb-4">
              <Icon icon="newspaper" class="text-3xl text-gray-400"/>
            </div>
            <p class="text-lg text-gray-500 font-medium">暂无文章</p>
          </div>
        </section>

        <!-- 订阅源板块 (iOS Settings 列表风格) -->
        <section v-if="entries.length > 0">
          <h2 class="text-2xl font-bold tracking-tight mb-6 px-2 section-title">更新记录</h2>

          <div class="ios-list-container">
            <div
                v-for="(entry, index) in entries"
                :key="index"
                class="ios-list-item group"
                @click="openLink(entry.link[0]?.href)"
            >
              <div class="flex-1 py-4 pr-4 flex flex-col">
                <span class="text-base font-medium text-primary group-hover:text-blue-500 transition-colors">
                  {{ entry.title }}
                </span>
                <span class="text-sm text-secondary mt-1">
                    {{ formatDate(entry.updated) }}
                </span>
              </div>
              <Icon icon="chevron-right" class="w-5 h-5 text-gray-300 dark:text-gray-600 mr-4"/>
            </div>
          </div>
        </section>

      </div>
    </div>

    <!-- 全局 Loading (使用 NaiveUI Spin) -->
    <div v-if="loading && rssArticles.length === 0"
         class="fixed inset-0 z-50 flex items-center justify-center bg-white/50 dark:bg-black/50 backdrop-blur-sm">
      <n-spin size="large"/>
    </div>

  </div>
</template>

<script setup lang="ts">
import {onMounted, ref} from 'vue';
import Icon from '@/components/Icon.vue'
import {NSpin} from 'naive-ui';
import PageStart from "../../components/PageStart.vue";
import {loadRssArticles, loadAtomEntries} from '../../services/RssService';
import articleHeaderImg from '/assets/Centre/Article.png';

// --- 类型定义 ---
interface Article {
  title: string;
  url: string;
  image?: string;
  description?: string;
}

interface EntryLink {
  href: string;
}

interface Entry {
  title: string;
  link: EntryLink[];
  updated: string;
}

// --- 状态管理 ---
const isMobile = ref<boolean>(window.innerWidth < 640);
const rssArticles = ref<Article[]>([]);
const entries = ref<Entry[]>([]);
const loading = ref<boolean>(true);

// --- 工具函数 ---
const formatDate = (dateString: string | undefined): string => {
  if (!dateString) return '';
  const date = new Date(dateString);
  return isNaN(date.getTime())
      ? ''
      : new Intl.DateTimeFormat('zh-CN', {year: 'numeric', month: 'short', day: 'numeric'}).format(date);
};

// --- 交互逻辑 ---
const openArticle = (url: string) => {
  if (url) window.open(url, '_blank');
};

const openLink = (url: string | undefined) => {
  if (url) window.open(url, '_blank');
};

const handleImageError = (event: Event) => {
  const target = event.target as HTMLImageElement;
  target.style.display = 'none';
  // 简单的 DOM 操作显示默认图标，实际项目中建议使用 v-if 控制
  const parent = target.parentElement;
  if (parent) {
    parent.classList.add('flex', 'items-center', 'justify-center');
    parent.innerHTML = '<span class="text-gray-300 text-5xl opacity-30"></span>';
  }
};

// --- 生命周期 ---
onMounted(async () => {
  window.addEventListener('resize', () => {
    isMobile.value = window.innerWidth < 640;
  });

  try {
    loading.value = true;
    const [rssResult, atomResult] = await Promise.allSettled([
      loadRssArticles(),
      loadAtomEntries()
    ]);

    if (rssResult.status === 'fulfilled') {
      // 强制类型断言，实际项目中应确保 Service 返回类型安全
      rssArticles.value = rssResult.value as Article[];
    }

    if (atomResult.status === 'fulfilled') {
      entries.value = atomResult.value as Entry[];
    }
  } catch (error) {
    console.error('Failed to fetch blog data:', error);
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
/*
  原生 CSS 区域
  专注于处理 Tailwind 较难实现的苹果风格细节和精细的暗黑模式
*/

/* 1. 页面背景: 浅色是经典的 Apple 浅灰，深色是 iOS 系统级深灰 */
.page-bg {
  background-color: #F5F5F7; /* Apple Website Light Gray */
}

.dark .page-bg {
  background-color: #000000; /* Deep Black for OLED/Contrast */
}

/* 2. 文本颜色系统 */
.section-title {
  color: #1d1d1f;
}

.text-primary {
  color: #1d1d1f;
}

.text-secondary {
  color: #86868b;
}

.dark .section-title {
  color: #f5f5f7;
}

.dark .text-primary {
  color: #f5f5f7;
}

.dark .text-secondary {
  color: #86868b;
}

/* 3. Apple 风格卡片 (Bento Card) */
.apple-card {
  border-radius: 24px; /* iOS 风格的大圆角 */
  border: 1px solid rgba(0, 0, 0, 0.04);
  background: #ffffff;
  box-shadow: 2px 4px 12px rgba(0, 0, 0, 0.04);
  transition: all 0.4s cubic-bezier(0.25, 0.8, 0.25, 1);
}

.apple-card:hover {
  transform: scale(1.02);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.08);
}

.dark .apple-card {
  background: #1c1c1e; /* iOS Secondary System Fill */
  border: 1px solid rgba(255, 255, 255, 0.05);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
}

/* 4. iOS 设置列表风格 (Update List) */
.ios-list-container {
  background: rgba(255, 255, 255, 0.8);
  backdrop-filter: blur(20px);
  border-radius: 18px;
  overflow: hidden;
  border: 1px solid rgba(0, 0, 0, 0.05);
}

.dark .ios-list-container {
  background: rgba(28, 28, 30, 0.8);
  border: 1px solid rgba(255, 255, 255, 0.05);
}

.ios-list-item {
  display: flex;
  align-items: center;
  padding-left: 20px; /* iOS indentation */
  cursor: pointer;
  background: transparent;
  transition: background-color 0.2s ease;
  border-bottom: 1px solid rgba(60, 60, 67, 0.10); /* Separator */
}

.dark .ios-list-item {
  border-bottom: 1px solid rgba(84, 84, 88, 0.35);
}

.ios-list-item:last-child {
  border-bottom: none;
}

.ios-list-item:active {
  background-color: rgba(0, 0, 0, 0.05);
}

.dark .ios-list-item:active {
  background-color: rgba(255, 255, 255, 0.1);
}

/* 5. 动画效果 */
.animate-fade-in-down {
  animation: fadeInDown 0.8s cubic-bezier(0.16, 1, 0.3, 1);
}

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

/* 隐藏滚动条但允许滚动 (Optional clean look) */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}

.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}
</style>