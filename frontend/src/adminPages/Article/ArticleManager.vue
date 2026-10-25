<template>
  <!-- 页面容器：添加平滑的背景过渡 -->
  <div class="min-h-screen w-full transition-colors duration-300 ease-in-out bg-gray-50/50 dark:bg-transparent">
    <main class="max-w-[1400px] mx-auto px-4 sm:px-6 lg:px-8 py-8">

      <!-- 网格布局 -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">

        <!-- Loading 骨架屏 (保持原有逻辑，优化样式) -->
        <template v-if="loading">
          <div v-for="i in 8" :key="i" class="ios-card animate-pulse">
            <div class="p-5 h-full flex flex-col">
              <div class="flex justify-between items-start mb-4">
                <div class="w-10 h-10 rounded-xl bg-gray-200 dark:bg-white/10"></div>
                <div class="w-16 h-5 rounded-full bg-gray-200 dark:bg-white/10"></div>
              </div>
              <div class="flex-1 space-y-3">
                <div class="h-5 bg-gray-200 dark:bg-white/10 rounded w-3/4"></div>
                <div class="h-4 bg-gray-200 dark:bg-white/10 rounded w-1/2"></div>
              </div>
              <div class="mt-4 flex justify-end gap-2">
                <div class="w-8 h-8 rounded-full bg-gray-200 dark:bg-white/10"></div>
              </div>
            </div>
          </div>
        </template>

        <!-- 文章列表 -->
        <template v-else>
          <div
              v-for="article in articles"
              :key="article.path"
              class="ios-card group relative flex flex-col transition-all duration-300 ease-out"
              @click="editArticle(article)"
          >
            <!-- 卡片内容 -->
            <div class="p-5 flex flex-col h-full z-10">
              <!-- 顶部：Icon与标签 -->
              <div class="flex justify-between items-start mb-3">
                <!-- 拟物化图标背景 -->
                <div class="w-11 h-11 rounded-xl flex items-center justify-center bg-blue-50 text-blue-600 dark:bg-blue-500/20 dark:text-blue-400 transition-colors">
                  <Icon icon="text-cursor-input" class="w-6 h-6" />
                </div>

                <n-tag
                    :bordered="false"
                    round
                    size="small"
                    :type="getIdentityType(article.identity)"
                    class="!font-medium !text-[11px] px-2"
                >
                  {{ getAccessLabel(article) }}
                </n-tag>
              </div>

              <!-- 中部：标题与信息 -->
              <div class="flex-1 min-h-[80px]">
                <h3 class="text-[17px] font-semibold leading-snug text-gray-900 dark:text-white mb-2 line-clamp-2 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">
                  {{ article.title || '无标题文档' }}
                </h3>

                <div class="flex flex-col gap-1 text-[13px] text-gray-500 dark:text-gray-400/80">
                  <div class="flex items-center gap-1.5">
                    <Icon icon="folder" class="w-3.5 h-3.5 opacity-70" />
                    <span class="truncate max-w-[150px]">{{ article.categoryName || '未分类' }}</span>
                  </div>
                  <div class="flex items-center gap-1.5">
                    <Icon icon="clock" class="w-3.5 h-3.5 opacity-70" />
                    <span>{{ formatDate(article.lastWriteTime) }}</span>
                  </div>
                </div>
              </div>

              <!-- 底部：操作按钮 (Hover显示，移动端常显) -->
              <div class="absolute bottom-4 right-4 flex gap-2 opacity-0 group-hover:opacity-100 transition-opacity duration-200 pt-2">
                <!-- 阻止冒泡，避免触发卡片点击 -->
                <button
                    class="action-btn-icon hover:bg-blue-100 hover:text-blue-600 dark:hover:bg-blue-500/30 dark:hover:text-blue-300"
                    @click.stop="editArticle(article)"
                    title="编辑"
                >
                  <Icon icon="pencil-line" class="w-4 h-4" />
                </button>
                <button
                    class="action-btn-icon hover:bg-red-100 hover:text-red-600 dark:hover:bg-red-500/30 dark:hover:text-red-300"
                    @click.stop="deleteArticle(article)"
                    title="删除"
                >
                  <Icon icon="trash" class="w-4 h-4" />
                </button>
              </div>
            </div>

            <!-- 选中/Hover效果的高亮边框模拟 -->
            <div class="absolute inset-0 rounded-2xl border-2 border-transparent group-hover:border-blue-500/20 dark:group-hover:border-blue-400/30 pointer-events-none transition-colors duration-300"></div>
          </div>

          <!-- 空状态 -->
          <div
              v-if="articles.length === 0 && !loading"
              class="col-span-full min-h-[400px] flex flex-col items-center justify-center rounded-3xl bg-white/50 dark:bg-white/5 border border-dashed border-gray-300 dark:border-white/10 backdrop-blur-sm"
          >
            <div class="w-20 h-20 flex items-center justify-center bg-gray-100 dark:bg-white/10 rounded-full mb-6">
              <Icon icon="inbox" class="text-gray-400 dark:text-white/40 w-10 h-10"/>
            </div>
            <h3 class="text-xl font-semibold text-gray-900 dark:text-white mb-2">在这里存储您的文章</h3>
            <p class="text-sm text-gray-500 dark:text-gray-400 mb-8 max-w-xs text-center">
              点击下方的按钮创建第一篇文章，或通过导入功能添加。
            </p>
            <n-button
                type="primary"
                round
                size="large"
                class="!px-8 !font-medium"
                @click="openCreateModal">
              <template #icon>
                <Icon icon="plus" />
              </template>
              新建文章
            </n-button>
          </div>
        </template>
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, defineComponent, h } from 'vue'
import { useRouter } from 'vue-router'
import { useDialog, useMessage, NButton, NTag } from 'naive-ui'
// 图标统一使用 Lucide
import Icon from '@/components/Icon.vue'
import { ArticleService } from '../../services/ArticleService'
import type { ArticleModel } from '../../models'
import { useLayoutStore } from '../../stores/LayoutStore'
import { useAuthorizationStore } from '../../stores/Authorization'

const router = useRouter()
const dialog = useDialog()
const message = useMessage()
const layoutStore = useLayoutStore()
const authorizationStore = useAuthorizationStore()

const articles = ref<ArticleModel[]>([])
const loading = ref(false)

// 分类写操作只允许 Founder / President（后端 CategoryController 同样的边界）。
const canManageCategories = computed(() =>
  ['Founder', 'President'].includes(authorizationStore.getRole || '')
)

// --- 业务逻辑 ---

const getIdentityLabel = (identity: string) => {
  const options: Record<string, string> = {
    'Member': '所有人',
    'Department': '部员',
    'Minister': '部长',
    'President': '社长',
    'Founder': '创始人'
  }
  return options[identity] || '未知'
}

const getAccessLabel = (article: ArticleModel) =>
  article.visibleToDepartment || getIdentityLabel(article.identity || '')

const getIdentityType = (identity: string | null | undefined): "default" | "success" | "info" | "warning" | "error" => {
  switch (identity) {
    case 'Member': return 'success'
    case 'Department': return 'info'
    case 'Minister': return 'warning'
    case 'President': return 'error'
    case 'Founder': return 'default'
    default: return 'default'
  }
}

const formatDate = (dateString: string) => {
  if (!dateString) return '未设置日期'
  try {
    const date = new Date(dateString)
    // 使用更符合中文阅读习惯的短日期格式
    return new Intl.DateTimeFormat('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit'
    }).format(date)
  } catch {
    return dateString
  }
}

const fetchArticles = async () => {
  try {
    loading.value = true
    const data = await ArticleService.getAllArticles()
    articles.value = Array.isArray(data) ? data : []
  } catch (error) {
    console.error(error)
    message.error('无法加载文章: ' + String(error))
  } finally {
    loading.value = false
  }
}

const openCreateModal = () => {
  router.push('/Centre/Article/edit')
}

const editArticle = (article: ArticleModel) => {
  router.push(`/Centre/Article/edit/${article.path}`)
}

const goToCategoryManager = () => {
  router.push('/Centre/Category')
}

const deleteArticle = (article: ArticleModel) => {
  // 使用原生样式的确认 (或 NaiveUI 定制)
  dialog.warning({
    title: '删除文章',
    content: `此操作将永久删除“${article.title}”，且不可恢复。`,
    positiveText: '删除',
    negativeText: '取消',
    // 应用一点样式调整
    maskClosable: false,
    onPositiveClick: async () => {
      try {
        await ArticleService.deleteArticle(article.path)
        message.success('已永久删除') // 删除不可恢复，与确认弹窗文案保持一致
        await fetchArticles()
      } catch (error) {
        message.error('删除失败')
      }
    }
  })
}

// --- 生命周期与 Layout ---

onMounted(() => {
  layoutStore.setPageHeader('文章库', '管理所有的社团发布内容')
  layoutStore.setShowPageActions(true)

  // 定制顶栏按钮组件 (Apple 风格)
  const ActionsComponent = defineComponent({
    setup() {
      // 辅助函数：渲染圆形毛玻璃按钮
      const renderCircleBtn = (icon: string, onClick: () => void, title?: string) =>
          h('button', {
            class: 'w-9 h-9 rounded-full flex items-center justify-center transition-all duration-200 bg-gray-100/80 hover:bg-gray-200/80 dark:bg-white/10 dark:hover:bg-white/20 backdrop-blur-md text-gray-700 dark:text-white active:scale-95',
            onClick,
            title
          }, [h(Icon, { icon, class: 'w-5 h-5' })])

      return () => h('div', { class: 'flex items-center gap-3' }, [
        // 创建文章
        renderCircleBtn('plus', openCreateModal, '新建文章'),

        // 分类管理 (胶囊按钮风格) — 仅 Founder/President 可见
        ...(canManageCategories.value
          ? [h('button', {
            class: 'h-9 px-4 rounded-full flex items-center gap-2 text-sm font-medium transition-all duration-200 bg-gray-100/80 hover:bg-gray-200/80 dark:bg-white/10 dark:hover:bg-white/20 backdrop-blur-md text-gray-700 dark:text-white active:scale-95',
            onClick: goToCategoryManager
          }, [
            h(Icon, { icon: 'folder', class: 'w-4 h-4' }),
            h('span', '分类管理')
          ])]
          : []),
      ])
    }
  })

  layoutStore.setActionsComponent(ActionsComponent)
  fetchArticles()
})

onBeforeUnmount(() => {
  layoutStore.clearPageHeader()
})
</script>

<style scoped>
/*
  原生 CSS 区域
  主要处理 Apple 风格的复杂阴影、卡片材质和过渡细节
*/

/* iOS 风格卡片基础 */
.ios-card {
  cursor: pointer;
  background-color: #ffffff;
  border-radius: 1.25rem; /* 20px */
  border: 1px solid rgba(229, 231, 235, 0.8); /* gray-200 */
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  overflow: hidden;
}

.ios-card:hover {
  /* 上浮效果 + 扩散阴影 */
  transform: translateY(-4px);
  box-shadow: 0 12px 24px -8px rgba(0, 0, 0, 0.08), 0 4px 8px -4px rgba(0, 0, 0, 0.04);
}

/* 按钮基础样式 */
.action-btn-icon {
  width: 32px;
  height: 32px;
  border-radius: 9999px;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #f3f4f6; /* gray-100 */
  color: #6b7280; /* gray-500 */
  transition: all 0.2s ease;
  backdrop-filter: blur(4px);
}

/*
  暗黑模式适配 (.dark .class)
*/
.dark .ios-card {
  background-color: #1c1c1e; /* macOS Dark Gray */
  border-color: rgba(255, 255, 255, 0.1);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
}

.dark .ios-card:hover {
  border-color: rgba(255, 255, 255, 0.15);
  box-shadow: 0 12px 24px -8px rgba(0, 0, 0, 0.5), 0 4px 8px -4px rgba(0, 0, 0, 0.3);
}

.dark .action-btn-icon {
  background-color: rgba(255, 255, 255, 0.1);
  color: rgba(255, 255, 255, 0.7);
}

/* Apple SF Fonts Fallback - 确保字体看起来像系统原生 */
.ios-card h3 {
  font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", "Helvetica Neue", sans-serif;
}

/* 响应式断点微调 (可选) */
@media (max-width: 640px) {
  .ios-card:active {
    transform: scale(0.98);
  }
  /* 移动端默认显示操作栏 */
  .ios-card .opacity-0 {
    opacity: 1 !important;
  }
}
</style>
