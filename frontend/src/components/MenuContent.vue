<template>
  <div class="menu-container pb-10">
    <!-- Loading State -->
    <div v-if="loading" class="space-y-6 px-4 pt-2">
      <div v-for="i in 3" :key="i" class="space-y-3">
        <n-skeleton text width="40%" class="opacity-50"/>
        <div class="space-y-2 pl-2">
          <n-skeleton text :repeat="3"/>
        </div>
      </div>
    </div>

    <!-- Menu Content -->
    <nav v-else class="space-y-6">
      <div v-for="(group, index) in menuOptions" :key="index" class="menu-group">
        <!-- Group Title (Like macOS Finder Sidebar Headers) -->
        <h3
            v-if="group.label"
            class="mb-2 px-3 text-[11px] font-bold uppercase tracking-wider text-zinc-400 dark:text-zinc-500 select-none group-title"
        >
          {{ group.label }}
        </h3>

        <!-- Group Items -->
        <ul class="space-y-0.5" v-if="group.children && group.children.length">
          <li v-for="item in group.children" :key="item.key">
            <button
                @click="handleSelect(item.key)"
                class="group relative flex w-full items-center gap-2.5 rounded-lg px-3 py-1.5 text-sm transition-colors duration-200 outline-none focus-visible:ring-2 focus-visible:ring-blue-500"
                :class="[
                isActive(item.key)
                  ? 'bg-blue-500/10 text-blue-600 font-semibold dark:bg-blue-500/20 dark:text-blue-400'
                  : 'text-zinc-600 hover:bg-zinc-200/50 dark:text-zinc-400 dark:hover:bg-white/5 hover:text-zinc-900 dark:hover:text-zinc-200 font-medium'
              ]"
            >
              <!-- Optional: Add generic icon if item doesn't specify one -->
              <Icon
                  :icon="isActive(item.key) ? 'file-text' : 'file'"
                  morph
                  class="shrink-0 w-4 h-4 transition-colors"
                  :class="isActive(item.key) ? 'text-blue-500 dark:text-blue-400' : 'text-zinc-400 group-hover:text-zinc-600 dark:text-zinc-600 dark:group-hover:text-zinc-400'"
              />

              <span class="truncate">{{ item.label }}</span>

              <!-- Active Indicator (Subtle dot on right, optional) -->
              <!-- <div v-if="isActive(item.key)" class="ml-auto w-1.5 h-1.5 rounded-full bg-blue-500"></div> -->
            </button>
          </li>
        </ul>
      </div>
    </nav>
  </div>
</template>

<script setup lang="ts">
import {onMounted, ref} from 'vue'
import {useRoute, useRouter} from 'vue-router'
import {NSkeleton} from 'naive-ui'
import Icon from '@/components/Icon.vue'
import {ArticleService} from '../services/ArticleService'
import type {ArticleModel} from '../models'

const emit = defineEmits(['menu-item-click'])
const route = useRoute()
const router = useRouter()

const loading = ref(true)
const menuOptions = ref<any[]>([])

// Check active state
// Handles both exact matches and potential sub-paths if needed
const isActive = (key: string) => {
  return route.path === key || decodeURIComponent(route.path) === key
}

// Fetch Data
const fetchCategoryArticles = async () => {
  // 默认结构：确保 "社团简介" 始终存在
  const specialPagesText = ['关于我们', '社团结构', '其他组织']

  try {
    loading.value = true
    const categoryArticles = await ArticleService.getAllCategoryArticles()

    // 构建菜单树
    const newOptions: any[] = []

    // 1. 处理从 API 获取的分类
    Object.entries(categoryArticles).forEach(([category, articles]) => {
      // 过滤掉特殊页面，防止重复（如果后端也返回了这些）
      const docItems = (articles as ArticleModel[])
          .filter(a => !specialPagesText.includes(a.title))
          .map(article => ({
            label: article.title || '未命名文档',
            key: `/Article/${article.path}`,
            index: article.articleOrder || 0, // 用于排序
          }))

      // 如果是社团简介分类，手动注入特殊页面
      if (category === '社团简介') {
        // 按照特定顺序插入
        const predefinedItems = [
          {label: '关于我们', key: '/About', index: -3},
          {label: '社团结构', key: '/Structure', index: -2},
          {label: '其他组织', key: '/OtherOrg', index: -1}
        ]
        // 合并：预定义在前，API获取的在后（如果有的话）
        docItems.unshift(...predefinedItems)
      }

      newOptions.push({
        label: category,
        children: docItems.sort((a, b) => a.index - b.index) // 按照 articleOrder 排序
      })
    })

    // 处理 API 可能没返回 "社团简介" 的情况（例如该分类下没有普通文章）
    const hasIntroGroup = newOptions.find(g => g.label === '社团简介')
    if (!hasIntroGroup) {
      newOptions.unshift({
        label: '社团简介',
        children: [
          {label: '关于我们', key: '/About'},
          {label: '社团结构', key: '/Structure'},
          {label: '其他组织', key: '/OtherOrg'}
        ]
      })
    }

    menuOptions.value = newOptions

  } catch (error) {
    console.error('Failed to load menu:', error)
    // Fallback menu
    menuOptions.value = [{
      label: '社团简介',
      children: [
        {label: '关于我们', key: '/About'},
        {label: '社团结构', key: '/Structure'},
        {label: '其他组织', key: '/OtherOrg'}
      ]
    }]
  } finally {
    loading.value = false
  }
}

const handleSelect = (key: string) => {
  if (route.path === key) {
    // 如果已经在当前页，可以选择不做任何事，或者滚动到顶部
    window.scrollTo({top: 0, behavior: 'smooth'})
  } else {
    router.push(key)
  }
  emit('menu-item-click', key)
}

onMounted(() => {
  fetchCategoryArticles()
})
</script>

<style scoped>
/*
  Custom styles to ensure smooth rendering and text rendering matches Apple system fonts
  The component mainly relies on Tailwind, but these helpers ensure consistency.
*/
.menu-container {
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

/* 可以在这里添加暗黑模式下的细微调整 */
.dark .group-title {
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.5);
}
</style>