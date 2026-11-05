<template>
  <div class="page-container min-h-screen transition-colors duration-500 ease-out">
    <main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 sm:py-12">

      <!-- Header Section: Search & Actions -->
      <div class="flex flex-col sm:flex-row items-center justify-between gap-6 mb-10">
        <!-- Search Bar (iOS Spotlight Style) -->
        <div class="relative w-full sm:max-w-lg group">
          <div class="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none z-10">
            <Icon icon="search" class="text-gray-400 dark:text-gray-500 w-5 h-5 transition-colors group-focus-within:text-blue-500"/>
          </div>
          <input
              v-model="searchTerm"
              placeholder="搜索资源..."
              class="apple-input w-full pl-11 pr-4 py-3 rounded-2xl text-base transition-all duration-300 shadow-sm hover:shadow-md focus:shadow-lg placeholder-gray-400 dark:placeholder-gray-600"
          >
        </div>

        <!-- Admin Action (Visible if Admin) -->
        <button
            v-if="authorizationStore.isAdmin()"
            @click="showAddResourceModal"
            class="apple-button-primary shrink-0 w-full sm:w-auto flex items-center justify-center gap-2"
        >
          <Icon icon="circle-plus" class="w-6 h-6" />
          <span>新建资源</span>
        </button>
      </div>

      <!-- Grid Layout -->
      <div v-if="!loading && filteredResources.length > 0" class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6 xl:gap-8">
        <div
            v-for="resource in filteredResources"
            :key="resource.id"
            class="apple-card group relative flex flex-col h-full p-6 transition-all duration-300 hover:-translate-y-1"
        >
          <!-- Icon Header -->
          <div class="flex items-start justify-between mb-4">
            <div class="w-12 h-12 rounded-2xl bg-blue-50 dark:bg-blue-500/10 flex items-center justify-center text-blue-500 dark:text-blue-400">
              <Icon icon="folder-open" class="w-6 h-6" />
            </div>

            <!-- Context Menu (Admin) -->
            <div v-if="authorizationStore.isAdmin()" class="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity duration-200">
              <button @click.stop="editResource(resource)" class="icon-btn text-blue-500 bg-blue-50 dark:bg-blue-500/20 hover:bg-blue-100 dark:hover:bg-blue-500/30">
                <Icon icon="square-pen" class="w-4 h-4"/>
              </button>
              <button @click.stop="deleteResource(resource)" class="icon-btn text-red-500 bg-red-50 dark:bg-red-500/20 hover:bg-red-100 dark:hover:bg-red-500/30">
                <Icon icon="trash" class="w-4 h-4"/>
              </button>
            </div>
          </div>

          <!-- Content -->
          <div class="flex-1">
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-2 leading-tight tracking-tight">
              {{ resource.name }}
            </h3>
            <p class="text-sm text-gray-500 dark:text-gray-400 line-clamp-3 leading-relaxed">
              {{ resource.description || '暂无描述信息...' }}
            </p>
          </div>

          <!-- Tags Footer -->
          <div class="mt-6 pt-4 border-t border-gray-100 dark:border-white/5">
            <div v-if="getResourceTags(resource).length > 0" class="flex flex-wrap gap-2">
              <span
                  v-for="tag in getResourceTags(resource)"
                  :key="tag"
                  class="px-2.5 py-1 rounded-md text-[11px] font-medium bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-300 tracking-wide uppercase"
              >
                {{ tag }}
              </span>
            </div>
            <div v-else class="text-xs text-gray-400 dark:text-gray-600 italic">
              无标签
            </div>
          </div>
        </div>
      </div>

      <!-- Empty State -->
      <div v-else-if="!loading" class="flex flex-col items-center justify-center py-32 text-center">
        <div class="w-24 h-24 rounded-full bg-gray-100 dark:bg-gray-800 flex items-center justify-center mb-6 shadow-inner">
          <Icon icon="inbox" class="w-10 h-10 text-gray-300 dark:text-gray-600"/>
        </div>
        <h3 class="text-xl font-semibold text-gray-900 dark:text-white mb-2">暂无内容</h3>
        <p class="text-gray-500 dark:text-gray-400 max-w-sm mx-auto">
          {{ searchTerm ? '没有找到匹配的资源' : '这里看起来空空如也，稍后再来看看吧。' }}
        </p>
      </div>

      <!-- Loading State -->
      <div v-else class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
        <SkeletonLoader v-for="i in 6" :key="i" type="card" class="rounded-3xl h-48 opacity-50"/>
      </div>
    </main>

    <!-- iOS Style Modal -->
    <div v-if="showModal" class="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
      <!-- Backdrop -->
      <div class="absolute inset-0 bg-gray-900/30 dark:bg-black/60 backdrop-blur-md transition-opacity" @click="showModal = false"></div>

      <!-- Modal Window -->
      <div class="relative w-full max-w-lg bg-white dark:bg-[#1C1C1E] rounded-[2rem] shadow-2xl overflow-hidden transform transition-all modal-animation flex flex-col max-h-[85vh]">

        <!-- Header -->
        <div class="px-8 py-6 border-b border-gray-100 dark:border-white/10 flex items-center justify-between bg-white/50 dark:bg-white/5 backdrop-blur-xl sticky top-0 z-10">
          <h3 class="text-xl font-bold text-gray-900 dark:text-white">
            {{ editingResource.id ? '编辑资源' : '新建资源' }}
          </h3>
          <button @click="showModal = false" class="w-8 h-8 rounded-full bg-gray-100 dark:bg-gray-700 text-gray-500 hover:bg-gray-200 dark:hover:bg-gray-600 flex items-center justify-center transition-colors">
            <Icon icon="x" class="w-5 h-5"/>
          </button>
        </div>

        <!-- Scrollable Content -->
        <div class="p-8 overflow-y-auto custom-scrollbar">
          <form @submit.prevent="saveResource" class="space-y-6">

            <!-- Name Input -->
            <div class="space-y-2">
              <label class="text-sm font-medium text-gray-700 dark:text-gray-300 ml-1">名称</label>
              <input
                  v-model="editingResource.name"
                  placeholder="资源名称"
                  class="apple-input w-full px-4 py-3 rounded-xl text-base"
                  required
              />
            </div>

            <!-- Description Input -->
            <div class="space-y-2">
              <label class="text-sm font-medium text-gray-700 dark:text-gray-300 ml-1">描述</label>
              <textarea
                  v-model="editingResource.description"
                  placeholder="关于这个资源的详细描述..."
                  class="apple-input w-full px-4 py-3 rounded-xl resize-none h-32 text-base"
              />
            </div>

            <!-- Tags Input -->
            <div class="space-y-3">
              <label class="text-sm font-medium text-gray-700 dark:text-gray-300 ml-1">标签</label>
              <div class="flex flex-wrap gap-2 min-h-[32px]">
                 <span v-for="(tag, index) in resourceTags" :key="index"
                       class="inline-flex items-center px-3 py-1 rounded-full text-sm bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-300">
                  {{ tag }}
                  <button @click="removeTag(index)" type="button" class="ml-1.5 hover:text-blue-900 dark:hover:text-blue-100">
                    <Icon icon="circle-x" class="w-4 h-4"/>
                  </button>
                </span>
              </div>
              <div class="relative">
                <input
                    v-model="newTag"
                    @keyup.enter="addTag"
                    placeholder="输入标签并回车添加"
                    class="apple-input w-full px-4 py-3 pr-12 rounded-xl text-sm"
                />
                <button
                    @click="addTag"
                    type="button"
                    class="absolute right-2 top-1/2 -translate-y-1/2 p-1.5 rounded-lg bg-gray-200 dark:bg-gray-600 text-gray-600 dark:text-gray-300 hover:bg-blue-500 hover:text-white transition-colors"
                >
                  <Icon icon="corner-down-left" class="w-4 h-4"/>
                </button>
              </div>
            </div>

            <!-- Footer Actions -->
            <div class="pt-4 flex items-center justify-end gap-3">
              <button type="button" @click="showModal = false" class="apple-button-secondary">
                取消
              </button>
              <button
                  type="submit"
                  :disabled="saving"
                  class="apple-button-primary px-8 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <Icon v-if="saving" icon="refresh-cw" class="w-5 h-5 animate-spin mr-2"/>
                {{ saving ? '处理中...' : '保存' }}
              </button>
            </div>

          </form>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, defineComponent, h } from 'vue'
import Icon from '@/components/Icon.vue'
import SkeletonLoader from '../components/SkeletonLoader.vue'
import { useAuthorizationStore } from '../stores/Authorization'
import { ResourceService } from '../services/ResourceService'
import { useDialog } from 'naive-ui'
import type { ResourceModel } from '../models'
import { useLayoutStore } from '../stores/LayoutStore'

interface Resource {
  id: string
  name: string
  description: string | null
  tag: string | null | undefined
}

// Stores & Services
const dialog = useDialog()
const authorizationStore = useAuthorizationStore()
const layoutStore = useLayoutStore()

// State
const showModal = ref(false)
const searchTerm = ref('')
const saving = ref(false)
const newTag = ref('')
const loading = ref(false)
const resources = ref<Resource[]>([])

const editingResource = ref<Resource>({
  id: '',
  name: '',
  description: '',
  tag: '',
})

const resourceTags = ref<string[]>([])

// --- Actions ---

const resetForm = () => {
  editingResource.value = { id: '', name: '', description: '', tag: '' }
  resourceTags.value = []
  newTag.value = ''
}

const addTag = () => {
  const tag = newTag.value.trim()
  if (tag && !resourceTags.value.includes(tag)) {
    resourceTags.value.push(tag)
  }
  newTag.value = ''
}

const removeTag = (index: number) => {
  resourceTags.value.splice(index, 1)
}

const getResourceTags = (resource: Resource) => {
  if (!resource.tag) return []
  return resource.tag.split(',').map(t => t.trim()).filter(t => t.length > 0)
}

const filteredResources = computed(() => {
  if (!searchTerm.value) return resources.value
  const term = searchTerm.value.toLowerCase()
  return resources.value.filter(resource =>
      resource.name.toLowerCase().includes(term) ||
      (resource.description && resource.description.toLowerCase().includes(term)) ||
      (resource.tag && resource.tag.toLowerCase().includes(term))
  )
})

const showAddResourceModal = () => {
  resetForm()
  showModal.value = true
}

const editResource = (resource: Resource) => {
  editingResource.value = { ...resource }
  if (resource.tag) {
    resourceTags.value = resource.tag.split(',').map(t => t.trim()).filter(t => t.length > 0)
  } else {
    resourceTags.value = []
  }
  showModal.value = true
}

const deleteResource = (resource: Resource) => {
  // Using NaiveUI Dialog purely for logic/confirmation
  dialog.warning({
    title: '删除确认',
    content: `确定要删除 "${resource.name}" 吗？此操作无法恢复。`,
    positiveText: '确认删除',
    negativeText: '取消',
    onPositiveClick: async () => {
      try {
        await ResourceService.deleteResource(resource.id)
        await fetchResources()
      } catch (error: any) {
        console.error('Delete failed:', error)
        alert('删除失败: ' + error.message)
      }
    }
  })
}

const saveResource = async () => {
  if (!editingResource.value.name.trim()) {
    return // Form required to be handled by browser mostly
  }

  saving.value = true
  try {
    const resourceToSave: ResourceModel = {
      id: editingResource.value.id,
      name: editingResource.value.name,
      description: editingResource.value.description || '',
      tag: resourceTags.value.join(',')
    }

    if (editingResource.value.id) {
      await ResourceService.updateResource(resourceToSave)
    } else {
      await ResourceService.createResource(resourceToSave)
    }

    showModal.value = false
    await fetchResources()
  } catch (error: any) {
    console.error('Save failed:', error)
    alert('保存失败: ' + error.message)
  } finally {
    saving.value = false
  }
}

const fetchResources = async () => {
  try {
    loading.value = true
    const data = await ResourceService.getAllResources()
    resources.value = data.map(resource => ({
      id: resource.id,
      name: resource.name,
      description: resource.description || null,
      tag: resource.tag === undefined ? null : resource.tag
    }))
  } catch (error: any) {
    console.error('Fetch failed:', error)
  } finally {
    loading.value = false
  }
}

// --- Lifecycle ---

onMounted(() => {
  fetchResources()
  layoutStore.setPageHeader('社团资源', '资源文件与链接管理')
  layoutStore.setShowPageActions(true)

  const ActionsComponent = defineComponent({
    setup() {
      return () => h('div', { class: 'flex items-center justify-end' }, [
        // Simple circle button for mobile/header view if needed
        h('button', {
          class: 'flex items-center justify-center w-9 h-9 rounded-full bg-gray-100 dark:bg-gray-800 hover:bg-gray-200 dark:hover:bg-gray-700 text-gray-600 dark:text-gray-300 transition-colors',
          title: '刷新',
          onClick: fetchResources
        }, [
          h(Icon, { icon: 'refresh-cw', class: 'w-5 h-5' })
        ])
      ])
    }
  })

  layoutStore.setActionsComponent(ActionsComponent)
})

onBeforeUnmount(() => {
  layoutStore.clearPageHeader()
  layoutStore.setActionsComponent(null)
})
</script>

<style scoped>
/*
 * Native CSS Styles for Apple/iOS Aesthetics
 * Using .dark .class pattern as requested
 */

/* Global Page Background */
.page-container {
  background-color: #F5F5F7; /* Apple Light Gray */
}

.dark .page-container {
  background-color: #000000; /* Apple Dark / True Black */
}

/* Apple Card Style */
.apple-card {
  background-color: #ffffff;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.02), 0 2px 4px -1px rgba(0, 0, 0, 0.02);
  border-radius: 1.5rem; /* rounded-3xl */
  border: 1px solid rgba(0,0,0,0.03);
}

.dark .apple-card {
  background-color: #1C1C1E; /* Secondary System Fill */
  border: 1px solid rgba(255,255,255,0.08);
  box-shadow: none;
}

/* Inputs: iOS Style */
.apple-input {
  background-color: #FFFFFF;
  border: 1px solid rgba(0,0,0,0.05);
  color: #1d1d1f;
  outline: none;
}

.apple-input:focus {
  border-color: #007AFF;
  box-shadow: 0 0 0 4px rgba(0, 122, 255, 0.1);
}

.dark .apple-input {
  background-color: #2C2C2E; /* Tertiary System Fill */
  border: 1px solid transparent;
  color: #f5f5f7;
}

.dark .apple-input:focus {
  background-color: #3A3A3C;
  box-shadow: 0 0 0 4px rgba(10, 132, 255, 0.15);
}

/* Buttons */
.apple-button-primary {
  background: linear-gradient(180deg, #007AFF 0%, #0062CC 100%);
  color: white;
  font-weight: 500;
  padding: 0.6rem 1.2rem;
  border-radius: 9999px;
  transition: all 0.2s ease;
  box-shadow: 0 2px 5px rgba(0, 122, 255, 0.2);
}

.apple-button-primary:hover {
  box-shadow: 0 4px 12px rgba(0, 122, 255, 0.3);
  transform: translateY(-1px);
}

.apple-button-primary:active {
  transform: scale(0.98);
}

.apple-button-secondary {
  background-color: rgba(0,0,0,0.05);
  color: #333;
  font-weight: 500;
  padding: 0.6rem 1.2rem;
  border-radius: 9999px;
  transition: background-color 0.2s;
}

.apple-button-secondary:hover {
  background-color: rgba(0,0,0,0.1);
}

.dark .apple-button-secondary {
  background-color: rgba(255,255,255,0.1);
  color: #eee;
}

.dark .apple-button-secondary:hover {
  background-color: rgba(255,255,255,0.15);
}

/* Icon Button Helper */
.icon-btn {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

/* Animation for Modal */
@keyframes modalPop {
  0% { opacity: 0; transform: scale(0.95) translateY(10px); }
  100% { opacity: 1; transform: scale(1) translateY(0); }
}

.modal-animation {
  animation: modalPop 0.3s cubic-bezier(0.34, 1.56, 0.64, 1) forwards;
}

/* Custom Scrollbar for Modal Content */
.custom-scrollbar::-webkit-scrollbar {
  width: 6px;
}
.custom-scrollbar::-webkit-scrollbar-track {
  background: transparent;
}
.custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: rgba(0,0,0,0.1);
  border-radius: 3px;
}
.dark .custom-scrollbar::-webkit-scrollbar-thumb {
  background-color: rgba(255,255,255,0.2);
}

/* Line Clamping */
.line-clamp-3 {
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
</style>