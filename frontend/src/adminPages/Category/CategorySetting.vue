<template>
  <div class="min-h-screen bg-[#F5F5F7] dark:bg-black transition-colors duration-300 font-sans">
    <main class="max-w-3xl mx-auto px-4 sm:px-6 py-8 pb-20">

      <!-- 区域 1: 分类基本信息 -->
      <div class="mb-2 px-1">
        <h2 class="text-[13px] text-gray-500 dark:text-gray-400 uppercase tracking-wide mb-2 ml-3">基本信息</h2>
      </div>

      <div class="bg-white dark:bg-[#1C1C1E] rounded-[10px] overflow-hidden mb-6 transition-colors duration-300">
        <!-- 分类名称行 -->
        <div class="flex items-center min-h-[44px] pl-4 pr-0">
          <div class="w-24 shrink-0 text-[17px] text-gray-900 dark:text-white">名称</div>
          <div class="flex-1 border-b border-gray-100 dark:border-[#38383A] py-2 pr-4">
            <n-input
                v-model:value="category.name"
                placeholder="设置分类名称"
                :bordered="false"
                class="bg-transparent text-right font-medium"
                style="--n-caret-color: #007AFF; --n-text-color: inherit; font-size: 17px; text-align: right;"
            />
          </div>
        </div>

        <!-- 分类ID行 (只读) -->
        <div class="flex items-center min-h-[44px] pl-4 pr-0">
          <div class="w-24 shrink-0 text-[17px] text-gray-900 dark:text-white">ID</div>
          <div class="flex-1 py-3 pr-4 flex justify-end items-center">
            <span class="text-[17px] text-gray-500 dark:text-gray-400 font-mono select-all">{{ category.id }}</span>
            <Icon icon="lock" class="ml-2 w-4 h-4 text-gray-400"/>
          </div>
        </div>
      </div>

      <!-- 操作按钮区域 -->
      <div class="mb-8 px-1 flex justify-end">
        <button
            @click="updateCategory"
            :disabled="updatingCategory"
            class="text-[17px] font-medium text-[#007AFF] disabled:opacity-50 hover:opacity-80 transition-opacity bg-transparent px-4 py-1"
        >
          {{ updatingCategory ? '正在保存...' : '保存更改' }}
        </button>
      </div>

      <!-- 区域 2: 文章排序 -->
      <div class="flex justify-between items-end mb-2 px-4">
        <h2 class="text-[13px] text-gray-500 dark:text-gray-400 uppercase tracking-wide">文章排序</h2>
        <button
            @click="updateArticleOrders"
            :disabled="updatingArticles"
            class="text-[15px] text-[#007AFF] font-medium hover:opacity-80 disabled:opacity-50 transition-opacity bg-transparent"
        >
          {{ updatingArticles ? '保存顺序...' : '保存顺序' }}
        </button>
      </div>

      <div
          class="bg-white dark:bg-[#1C1C1E] rounded-[10px] overflow-hidden transition-colors duration-300 min-h-[100px]">

        <!-- 加载骨架 -->
        <div v-if="loadingArticles" class="animate-pulse">
          <div v-for="i in 3" :key="i"
               class="flex items-center pl-4 py-3 border-b border-gray-100 dark:border-[#38383A] last:border-0">
            <div class="w-6 h-6 rounded-full bg-gray-200 dark:bg-gray-700 mr-3"></div>
            <div class="flex-1 h-4 bg-gray-200 dark:bg-gray-700 rounded w-1/2"></div>
          </div>
        </div>

        <!-- 文章列表 -->
        <draggable
            v-else-if="articles.length > 0"
            v-model="articles"
            item-key="path"
            handle=".drag-handle"
            ghost-class="sortable-ghost"
            chosen-class="sortable-chosen"
            drag-class="sortable-drag"
            @start="onDragStart"
            @end="onDragEnd"
            class="divide-y divide-gray-100 dark:divide-[#38383A]"
        >
          <template #item="{ element: article }">
            <div
                class="group flex items-center pl-3 pr-4 py-3 bg-white dark:bg-[#1C1C1E] active:bg-gray-50 dark:active:bg-[#2C2C2E] cursor-default transition-colors">
              <!-- 拖拽手柄 -->
              <div
                  class="drag-handle p-2 cursor-grab active:cursor-grabbing touch-none text-gray-300 dark:text-gray-600 hover:text-gray-500 dark:hover:text-gray-400 transition-colors">
                <Icon icon="menu" class="w-6 h-6"/>
              </div>

              <!-- 文章内容 -->
              <div class="flex-1 min-w-0 ml-1 mr-3">
                <div class="flex items-baseline gap-2">
                  <h3 class="text-[17px] text-gray-900 dark:text-white truncate">{{
                      article.title || '无标题文章'
                    }}</h3>
                </div>
                <p class="text-[13px] text-gray-500 dark:text-gray-400 truncate font-mono mt-0.5">{{ article.path }}</p>
              </div>

              <!-- 标签 -->
              <n-tag
                  size="small"
                  :type="getIdentityType(article.identity)"
                  :bordered="false"
                  round
                  class="shrink-0 opacity-90"
              >
                {{ getIdentityLabel(article.identity || '') }}
              </n-tag>

              <!-- 箭头指示符 (装饰用) -->
              <Icon icon="chevron-right" class="ml-3 text-gray-300 dark:text-gray-600 w-4 h-4"/>
            </div>
          </template>
        </draggable>

        <!-- 空状态 -->
        <div v-else class="flex flex-col items-center justify-center py-12 text-center">
          <div class="w-12 h-12 rounded-full bg-gray-100 dark:bg-[#2C2C2E] flex items-center justify-center mb-3">
            <Icon icon="files" class="text-gray-400 w-6 h-6"/>
          </div>
          <p class="text-gray-500 dark:text-gray-400 text-[15px]">此分类下暂无文章</p>
        </div>
      </div>

      <div class="mt-4 px-4 text-center text-gray-400 dark:text-gray-600 text-xs">
        拖拽左侧图标可调整文章顺序，调整后请点击保存。
      </div>

    </main>
  </div>
</template>

<script setup lang="ts">
import {ref, onMounted, h, defineComponent} from 'vue';
import {useRoute, useRouter} from 'vue-router';
import {useMessage, NInput, NTag} from 'naive-ui';
import Icon from '@/components/Icon.vue'
import draggable from 'vuedraggable';
import {CategoryService} from '../../services/CategoryService';
import {ArticleService} from '../../services/ArticleService';
import type {CategoryModel} from '../../models';
import type {ArticleModel} from '../../models';
import {useLayoutStore} from '../../stores/LayoutStore';

const route = useRoute();
const router = useRouter();
const message = useMessage();
const layoutStore = useLayoutStore();

const categoryId = ref<string>(route.params.id as string);
const category = ref<CategoryModel>({id: '', name: '', order: 0});
const articles = ref<ArticleModel[]>([]);
const loadingArticles = ref(false);
const updatingCategory = ref(false);
const updatingArticles = ref(false);

// 权限标签逻辑
const getIdentityLabel = (identity: string) => {
  const options: Record<string, string> = {
    'Member': '所有人',
    'Department': '部员',
    'Minister': '部长',
    'President': '社长',
    'Founder': '创始人'
  };
  return options[identity] || '未知';
};

const getIdentityType = (identity: string | null | undefined): 'success' | 'info' | 'warning' | 'error' | 'default' => {
  switch (identity) {
    case 'Member':
      return 'success';
    case 'Department':
      return 'info';
    case 'Minister':
      return 'warning';
    case 'President':
      return 'error';
    default:
      return 'default';
  }
};

// API逻辑
const loadCategory = async () => {
  try {
    const foundCategory = await CategoryService.getCategoryById(categoryId.value);
    if (foundCategory) {
      category.value = foundCategory;
      articles.value = await CategoryService.getCategoryArticles(categoryId.value);
    } else {
      message.error('分类不存在');
      goBack();
    }
  } catch (error) {
    console.error('加载分类失败:', error);
    message.error('加载分类失败');
    goBack();
  }
};

const updateCategory = async () => {
  try {
    updatingCategory.value = true;
    await CategoryService.createOrUpdateCategory(category.value);
    message.success('已更新分类信息');
  } catch (error) {
    console.error('更新分类失败:', error);
    message.error('更新失败');
  } finally {
    updatingCategory.value = false;
  }
};

const updateArticleOrders = async () => {
  try {
    updatingArticles.value = true;
    const articleOrders: Record<string, number> = {};
    articles.value.forEach((article, index) => {
      articleOrders[article.path] = index;
    });
    await ArticleService.updateArticleOrders(articleOrders);
    message.success('文章顺序已保存');
  } catch (error) {
    console.error('更新文章顺序失败:', error);
    message.error('保存顺序失败');
  } finally {
    updatingArticles.value = false;
  }
};

// 拖拽相关
const onDragStart = () => {
  document.body.style.cursor = 'grabbing';
};

const onDragEnd = () => {
  document.body.style.cursor = '';
};

const goBack = () => {
  router.push('/Centre/Category');
};

// 生命周期
onMounted(async () => {
  await loadCategory();

  // 因为我们自定义了页面 Header，这里可以隐藏全局 Header 或者保持一致
  // 如果你的 layout store 支持隐藏原有的 header 最好
  layoutStore.setPageHeader(
      category.value.name || '设置',
      '管理分类信息，并设置分类顺序'
  )

  layoutStore.setShowPageActions(true)

  const ActionsComponent = defineComponent({
    setup() {
      return () => h('button', {
        class: 'p-2.5 rounded-full bg-blue-600 hover:bg-blue-700 text-white transition-transform active:scale-95 flex items-center text-sm font-medium shadow-lg shadow-blue-500/30',
        onClick: () => goBack()
      }, [h(Icon, {icon: 'arrow-left', class: ''})])
    }
  })
  layoutStore.setActionsComponent(ActionsComponent);
});
</script>

<style scoped>
/* 自定义 n-input 样式以匹配 iOS 风格 */
:deep(.n-input) {
  background-color: transparent !important;
}

:deep(.n-input--state-focused),
:deep(.n-input:hover) {
  box-shadow: none !important;
}

:deep(.n-input-wrapper) {
  padding-right: 0 !important;
}

/* 拖拽时的样式控制 - 使用原生 CSS */
.sortable-ghost {
  background-color: #F2F2F7;
  opacity: 0.8;
}

.sortable-drag {
  background-color: #FFFFFF;
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.15);
  z-index: 50;
  transform: scale(1.02);
  border-radius: 0;
}

/* 深色模式下的拖拽样式 */
.dark .sortable-ghost {
  background-color: #2C2C2E;
  opacity: 0.6;
}

.dark .sortable-drag {
  background-color: #1C1C1E;
  box-shadow: 0 8px 20px rgba(0, 0, 0, 0.5);
}

/* 平滑过渡 */
.list-enter-active,
.list-leave-active {
  transition: all 0.3s ease;
}

.list-enter-from,
.list-leave-to {
  opacity: 0;
  transform: translateX(-20px);
}
</style>