<template>
  <div class="ios-container min-h-screen w-full transition-colors duration-300">
    <main class="max-w-[1600px] mx-auto px-4 sm:px-6 lg:px-8 py-10">

      <!-- 顶部说明 (可选，增加苹果风格的大标题感觉) -->
      <div class="mb-8 pl-1">
        <h2 class="text-sm font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
          分类概览
        </h2>
      </div>

      <!-- 骨架屏加载状态 -->
      <div v-if="loading" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6 w-full">
        <div v-for="i in 8" :key="i" class="ios-card h-48 p-6 flex flex-col justify-between animate-pulse">
          <div class="w-12 h-12 rounded-xl bg-gray-200 dark:bg-gray-700"></div>
          <div class="space-y-3">
            <div class="h-6 bg-gray-200 dark:bg-gray-700 rounded-lg w-2/3"></div>
            <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded-md w-1/3"></div>
          </div>
        </div>
      </div>

      <!-- 实际内容列表 -->
      <draggable
          v-else
          v-model="categories"
          item-key="id"
          handle=".drag-handle"
          ghost-class="drag-ghost"
          chosen-class="drag-chosen"
          @start="onCategoryDragStart"
          @end="onCategoryDragEnd"
          class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6 w-full"
      >
        <template #item="{ element: category }">
          <div class="ios-card group relative h-48 p-6 flex flex-col justify-between select-none transition-all duration-300"
               @click="goToCategorySetting(category)">

            <!-- 顶部：图标与操作 -->
            <div class="flex justify-between items-start z-10">
              <!-- 模拟文件夹图标 -->
              <div class="folder-icon-bg w-12 h-12 rounded-2xl flex items-center justify-center text-blue-500 dark:text-blue-400">
                <Icon icon="folder-open" class="w-7 h-7" />
              </div>

              <!-- 操作区 -->
              <div class="flex items-center space-x-2 opacity-0 group-hover:opacity-100 transition-opacity duration-200">
                <!-- 拖拽手柄 -->
                <div
                    class="drag-handle w-8 h-8 rounded-full bg-gray-100 dark:bg-white/10 flex items-center justify-center cursor-grab active:cursor-grabbing hover:bg-gray-200 dark:hover:bg-white/20 transition-colors"
                    @click.stop
                >
                  <Icon icon="grip" class="w-4 h-4 text-gray-600 dark:text-gray-300" />
                </div>
                <!-- 设置按钮 -->
                <div
                    class="w-8 h-8 rounded-full bg-gray-100 dark:bg-white/10 flex items-center justify-center cursor-pointer hover:bg-blue-100 dark:hover:bg-blue-500/30 transition-colors"
                >
                  <Icon icon="chevron-right" class="w-3 h-3 text-gray-600 dark:text-white" />
                </div>
              </div>
            </div>

            <!-- 底部：信息 -->
            <div class="z-10 mt-4">
              <h3 class="text-xl font-semibold text-gray-900 dark:text-white tracking-tight leading-tight line-clamp-1">
                {{ category.name }}
              </h3>
              <div class="flex items-center mt-1 space-x-2">
                 <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 dark:bg-white/10 text-gray-600 dark:text-gray-300">
                   # {{ category.order + 1 }}
                 </span>
                <span class="text-xs text-gray-400 dark:text-gray-500">ID: {{ category.id.slice(0,4) }}</span>
              </div>
            </div>

            <!-- 背景装饰 (玻璃光泽) -->
            <div class="absolute -right-4 -bottom-4 w-32 h-32 bg-gradient-to-br from-blue-500/5 to-purple-500/5 rounded-full blur-2xl pointer-events-none"></div>
          </div>
        </template>
      </draggable>

      <!-- 空状态 (Apple Style) -->
      <div
          v-if="categories.length === 0 && !loading"
          class="flex flex-col items-center justify-center py-32 text-center"
      >
        <div class="w-24 h-24 bg-gray-100 dark:bg-white/5 rounded-[2rem] flex items-center justify-center mb-6">
          <Icon icon="package" class="w-12 h-12 text-gray-400" />
        </div>
        <h3 class="text-xl font-semibold text-gray-900 dark:text-white mb-2">没有分类</h3>
        <p class="text-gray-500 dark:text-gray-400 max-w-xs mx-auto">
          看起来这里还是空的。
        </p>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue';
import { useRouter } from 'vue-router';
import { useMessage } from 'naive-ui';
import Icon from '@/components/Icon.vue'
import draggable from 'vuedraggable';
import { CategoryService } from '../../services/CategoryService';
import type { CategoryModel } from '../../models';
import { useLayoutStore } from '../../stores/LayoutStore';

const message = useMessage();
const router = useRouter();
const layoutStore = useLayoutStore();
const loading = ref(true);
const categories = ref<CategoryModel[]>([]);

// 加载所有分类
const loadCategories = async () => {
  try {
    loading.value = true;
    categories.value = await CategoryService.getAllCategories();
  } catch (error) {
    message.error((error as Error).message || '加载分类失败');
  } finally {
    loading.value = false;
  }
};

// 跳转到单个分类设置页面
const goToCategorySetting = (category: CategoryModel) => {
  router.push(`/Centre/Category/${category.id}`);
};

// 拖拽开始事件
const onCategoryDragStart = () => {
  document.body.classList.add('dragging-active');
};

// 拖拽结束事件
const onCategoryDragEnd = async () => {
  try {
    const categoryOrders: Record<string, number> = {};
    categories.value.forEach((category, index) => {
      category.order = index;
      categoryOrders[category.id] = index;
    });

    await CategoryService.updateCategoryOrders(categoryOrders);
    message.success('顺序已更新');
  } catch (error) {
    message.error((error as Error).message || '更新失败');
    await loadCategories();
  } finally {
    document.body.classList.remove('dragging-active');
  }
};

onMounted(() => {
  // 与 LayoutStore 交互
  layoutStore.setPageHeader(
      '文章分类',
      '管理内容结构与排序'
  );
  layoutStore.setShowPageActions(false);
  loadCategories();
});

onBeforeUnmount(() => {
  layoutStore.clearPageHeader();
  document.body.classList.remove('dragging-active');
});
</script>

<style scoped>
/* 核心卡片样式 - iOS/macOS 风格 */
.ios-card {
  /* Light Mode */
  background-color: #ffffff;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.02),
  0 2px 4px -1px rgba(0, 0, 0, 0.02),
  0 0 0 1px rgba(0, 0, 0, 0.04); /* 微妙边框 */
  border-radius: 24px; /* 大圆角 */
  cursor: pointer;
  transform: translateZ(0); /* 开启硬件加速 */
  transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1),
  box-shadow 0.3s ease,
  background-color 0.3s ease;
  overflow: hidden;
}

.ios-card:hover {
  /* 悬停时轻微抬起缩放 */
  transform: scale(1.02);
  box-shadow: 0 15px 30px -5px rgba(0, 0, 0, 0.08),
  0 8px 10px -5px rgba(0, 0, 0, 0.02);
  z-index: 10;
}

.folder-icon-bg {
  background-color: #F5F5F7; /* Apple 浅灰色 */
  transition: background-color 0.3s ease;
}

/* 暗黑模式适配 Dark Mode Overrides */
.dark .ios-card {
  background-color: #1C1C1E; /* Apple Dark Background Secondary */
  box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.08); /* 非常淡的内描边 */
}

.dark .ios-card:hover {
  background-color: #2C2C2E; /* 悬停变更亮一点的灰 */
  box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.5);
}

.dark .folder-icon-bg {
  background-color: rgba(255, 255, 255, 0.1);
}

/* 拖拽相关样式 */

/* 拖住时的“幽灵”元素 */
:deep(.drag-ghost) {
  opacity: 0.3 !important;
  background-color: transparent !important;
  box-shadow: none !important;
  border: 2px dashed #3B82F6 !important; /* 蓝色虚线框占位 */
}

/* 拖住时选中的元素 */
:deep(.drag-chosen) {
  cursor: grabbing;
  transform: scale(1.05);
}

/* 全局防止选中 */
:deep(.dragging-active) {
  user-select: none;
  cursor: grabbing;
}

/* 容器背景（如果父级没有设置，这里保底） */
.ios-container {
  /* 通常 LayoutStore 会处理背景，这里作为容器微调 */
}
</style>