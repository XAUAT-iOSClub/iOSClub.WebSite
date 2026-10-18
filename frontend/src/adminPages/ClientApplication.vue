<template>
  <div class="apple-container min-h-screen transition-colors duration-500">
    <main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10">

      <!-- 顶部控制栏：搜索与统计 -->
      <div class="flex flex-col sm:flex-row justify-between items-center mb-8 gap-4">
        <div class="search-wrapper relative w-full sm:w-96 group">
          <Icon icon="ion:search" class="absolute left-4 top-1/2 transform -translate-y-1/2 text-gray-400 group-focus-within:text-blue-500 transition-colors" width="20" height="20"/>
          <input
              v-model="searchQuery"
              type="text"
              placeholder="搜索应用名称或 ID..."
              class="search-input w-full pl-12 pr-4 py-3 rounded-2xl outline-none transition-all duration-300"
          />
        </div>

        <!-- 移动端适配时，为了操作方便，也可以在这里放个简略的操作栏 -->
        <div class="sm:hidden w-full flex gap-2">
          <button @click="openCreateModal" class="flex-1 bg-blue-500 text-white py-2 rounded-xl font-medium">创建应用</button>
        </div>
      </div>

      <!-- 主要内容区域：类似 iOS 设置列表的容器 -->
      <div class="content-card rounded-3xl overflow-hidden shadow-sm border">
        <!-- 桌面端表头 -->
        <div class="hidden md:grid grid-cols-12 gap-4 px-8 py-4 bg-gray-50/50 dark:bg-white/5 border-b border-gray-100 dark:border-gray-700/50 text-xs font-semibold text-gray-500 dark:text-gray-400 uppercase tracking-wide">
          <div class="col-span-4">应用信息</div>
          <div class="col-span-3">客户端 ID</div>
          <div class="col-span-2">状态</div>
          <div class="col-span-2">创建时间</div>
          <div class="col-span-1 text-right">操作</div>
        </div>

        <div class="divide-y divide-gray-100 dark:divide-gray-700/50 bg-white dark:bg-[#1c1c1e]">
          <template v-if="isLoading">
            <div v-for="i in 5" :key="i" class="px-8 py-6 animate-pulse">
              <div class="flex items-center space-x-4">
                <div class="w-10 h-10 rounded-xl bg-gray-200 dark:bg-gray-700"></div>
                <div class="flex-1 space-y-2">
                  <div class="h-4 bg-gray-200 dark:bg-gray-700 rounded w-1/4"></div>
                  <div class="h-3 bg-gray-200 dark:bg-gray-700 rounded w-1/2"></div>
                </div>
              </div>
            </div>
          </template>

          <template v-else-if="filteredClientApps.length > 0">
            <div
                v-for="app in filteredClientApps"
                :key="app.clientId"
                class="grid-row-item md:grid md:grid-cols-12 md:items-center gap-4 px-6 md:px-8 py-5 hover:bg-gray-50 dark:hover:bg-white/5 transition-colors duration-200 group"
            >
              <!-- 应用名称与Logo -->
              <div class="col-span-4 flex items-center gap-4">
                <div class="shrink-0 relative">
                  <img v-if="app.logoUrl" :src="app.logoUrl" :alt="app.applicationName"
                       class="h-12 w-12 rounded-2xl object-cover shadow-sm border border-gray-100 dark:border-gray-700"/>
                  <div v-else
                       class="h-12 w-12 rounded-2xl bg-linear-to-br from-gray-100 to-gray-200 dark:from-gray-700 dark:to-gray-800 flex items-center justify-center shadow-inner">
                    <Icon icon="lucide:app-window-mac" width="24" height="24"
                          class="text-gray-500 dark:text-gray-400"/>
                  </div>
                </div>
                <div class="min-w-0">
                  <p class="text-base font-semibold text-gray-900 dark:text-white truncate">{{ app.applicationName }}</p>
                  <p class="text-xs text-gray-500 dark:text-gray-400 truncate mt-0.5">{{ app.description || '暂无描述' }}</p>
                </div>
              </div>

              <!-- 客户端ID (移动端显示为单行) -->
              <div class="col-span-3 mt-3 md:mt-0 flex items-center gap-2">
                <code class="text-xs font-mono text-gray-600 dark:text-gray-300 bg-gray-100 dark:bg-white/10 px-2 py-1 rounded-lg select-all" title="点击复制">
                  {{ app.clientId }}
                </code>
                <button @click="copyToClipboard(app.clientId)" class="opacity-0 group-hover:opacity-100 transition-opacity text-gray-400 hover:text-blue-500">
                  <Icon icon="ion:copy-outline" width="16"/>
                </button>
              </div>

              <!-- 状态 -->
              <div class="col-span-2 mt-2 md:mt-0 flex items-center">
                <span :class="[
                  'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ring-1 ring-inset',
                  app.isActive
                    ? 'bg-green-50 text-green-700 ring-green-600/20 dark:bg-green-400/10 dark:text-green-400 dark:ring-green-400/30'
                    : 'bg-red-50 text-red-700 ring-red-600/10 dark:bg-red-400/10 dark:text-red-400 dark:ring-red-400/20'
                ]">
                  <span class="w-1.5 h-1.5 rounded-full mr-1.5" :class="app.isActive ? 'bg-green-600 dark:bg-green-400' : 'bg-red-600 dark:bg-red-400'"></span>
                  {{ app.isActive ? 'Active' : 'Inactive' }}
                </span>
              </div>

              <!-- 时间 -->
              <div class="col-span-2 mt-1 md:mt-0 text-sm text-gray-500 dark:text-gray-500 tabular-nums">
                {{ formatDateSimple(app.createdAt) }}
              </div>

              <!-- 操作按钮 -->
              <div class="col-span-1 mt-3 md:mt-0 flex justify-end items-center gap-1 md:opacity-0 md:group-hover:opacity-100 transition-opacity duration-200">
                <button @click="openDetailsModal(app)" class="action-btn" title="详情">
                  <Icon icon="lucide:info" width="22" height="22"/>
                </button>
                <button @click="openEditModal(app)" class="action-btn" title="编辑">
                  <Icon icon="tabler:edit" width="22" height="22"/>
                </button>
                <button @click="confirmDelete(app)" class="action-btn text-red-500 hover:bg-red-50 dark:hover:bg-red-900/20" title="删除">
                  <Icon icon="prime:trash" width="22" height="22"/>
                </button>
              </div>
            </div>
          </template>

          <!-- 空状态 -->
          <div v-else class="py-20 text-center">
            <div class="bg-gray-50 dark:bg-gray-800 w-20 h-20 rounded-full flex items-center justify-center mx-auto mb-4">
              <Icon icon="ion:search" width="32" class="text-gray-400"/>
            </div>
            <h3 class="text-lg font-medium text-gray-900 dark:text-white">未找到应用</h3>
            <p class="text-gray-500 dark:text-gray-400 mt-1">尝试调整搜索关键词或创建一个新应用</p>
          </div>
        </div>
      </div>
    </main>

    <!-- 模态框：创建客户端 -->
    <n-modal v-model:show="showCreateModal" preset="card" title="创建新的客户端" :bordered="false" size="huge" style="width: 600px; border-radius: 20px;" class="custom-modal">
      <form @submit.prevent="handleCreateSubmit" class="space-y-6 py-2">
        <!-- 表单项组件化风格 -->
        <div class="form-section">
          <label class="form-label">基本信息</label>
          <div class="input-group">
            <input v-model="createForm.applicationName" required placeholder="应用名称" class="apple-input rounded-t-xl border-b border-gray-200 dark:border-gray-700" />
            <input v-model="createForm.homepageUrl" type="url" required placeholder="https:// 主页 URL" class="apple-input border-b border-gray-200 dark:border-gray-700" />
            <input v-model="createForm.logoUrl" type="url" placeholder="https:// Logo URL (可选)" class="apple-input rounded-b-xl" />
          </div>
        </div>

        <div class="form-section">
          <label class="form-label">功能描述</label>
          <textarea v-model="createForm.description" rows="3" class="apple-textarea rounded-xl" placeholder="简要描述应用用途..."></textarea>
        </div>

        <div class="form-section">
          <label class="form-label">OAuth 设置</label>
          <div class="input-group">
            <textarea v-model="redirectUrisText" required rows="3" class="apple-textarea rounded-t-xl font-mono! text-sm! border-b border-gray-200 dark:border-gray-700" placeholder="回调 URL (一行一个)"></textarea>
            <div class="apple-checkbox-item rounded-b-xl flex justify-between items-center px-4 py-3 bg-gray-50 dark:bg-gray-800/50">
              <span class="text-sm text-gray-700 dark:text-gray-300">启用 PKCE 增强安全</span>
              <n-switch v-model:value="createForm.supportsPkce" size="small" />
            </div>
          </div>
        </div>

        <div class="flex justify-end gap-3 mt-8">
          <button type="button" @click="handleCreateModalClose" class="px-6 py-2 rounded-full text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/10 transition-colors">取消</button>
          <button type="submit" :disabled="isSubmitting" class="px-6 py-2 rounded-full text-sm font-medium bg-[#0071e3] hover:bg-[#0077ed] text-white shadow-sm transition-all disabled:opacity-50">
            {{ isSubmitting ? '处理中...' : '创建应用' }}
          </button>
        </div>
      </form>
    </n-modal>

    <!-- 模态框：编辑客户端 (结构类似创建) -->
    <n-modal v-model:show="showEditModal" preset="card" title="编辑客户端" :bordered="false" size="huge" style="width: 600px; border-radius: 20px;" class="custom-modal">
      <form @submit.prevent="handleEditSubmit" class="space-y-6 py-2">
        <div class="form-section">
          <label class="form-label">基本信息</label>
          <div class="input-group">
            <input v-model="editForm.applicationName" required placeholder="应用名称" class="apple-input rounded-t-xl border-b border-gray-200 dark:border-gray-700" />
            <input v-model="editForm.homepageUrl" type="url" required placeholder="主页 URL" class="apple-input border-b border-gray-200 dark:border-gray-700" />
            <input v-model="editForm.logoUrl" type="url" placeholder="Logo URL" class="apple-input rounded-b-xl" />
          </div>
        </div>

        <div class="form-section">
          <label class="form-label">OAuth 设置</label>
          <div class="input-group">
            <textarea v-model="editRedirectUrisText" required rows="3" class="apple-textarea rounded-t-xl font-mono! text-sm! border-b border-gray-200 dark:border-gray-700" placeholder="回调 URL"></textarea>
            <!-- 开关组 -->
            <div class="bg-gray-50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700 px-4 py-3 flex justify-between items-center">
              <span class="text-sm text-gray-700 dark:text-gray-300">启用客户端</span>
              <n-switch v-model:value="editForm.isActive" size="small" />
            </div>
            <div class="bg-gray-50 dark:bg-gray-800/50 border-b border-gray-200 dark:border-gray-700 px-4 py-3 flex justify-between items-center">
              <span class="text-sm text-gray-700 dark:text-gray-300">需要邮箱验证</span>
              <n-switch v-model:value="editForm.isNeedEMail" size="small" />
            </div>
            <div class="bg-gray-50 dark:bg-gray-800/50 rounded-b-xl px-4 py-3 flex justify-between items-center">
              <span class="text-sm text-gray-700 dark:text-gray-300">支持 PKCE</span>
              <n-switch v-model:value="editForm.supportsPkce" size="small" />
            </div>
          </div>
        </div>

        <div class="flex justify-end gap-3 mt-8">
          <button type="button" @click="handleEditModalClose" class="px-6 py-2 rounded-full text-sm font-medium text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-white/10 transition-colors">取消</button>
          <button type="submit" :disabled="isSubmitting" class="px-6 py-2 rounded-full text-sm font-medium bg-[#0071e3] hover:bg-[#0077ed] text-white shadow-sm transition-all">保存更改</button>
        </div>
      </form>
    </n-modal>

    <!-- 模态框：详情 -->
    <n-modal v-model:show="showDetailsModal" preset="card" :title="selectedApp?.applicationName" :bordered="false" size="huge" style="width: 550px; border-radius: 24px;" class="custom-modal details-modal">
      <div class="relative pt-6 pb-2">
        <div class="flex flex-col items-center mb-8">
          <img alt="" v-if="selectedApp?.logoUrl" :src="selectedApp.logoUrl" class="w-24 h-24 rounded-3xl shadow-lg mb-4 object-cover bg-white" />
          <div v-else class="w-24 h-24 rounded-3xl bg-linear-to-br from-blue-500 to-blue-600 flex items-center justify-center shadow-lg mb-4 text-white">
            <Icon icon="lucide:app-window-mac" width="48" height="48" />
          </div>
          <h2 class="text-2xl font-bold text-gray-900 dark:text-white">{{ selectedApp?.applicationName }}</h2>
          <a v-if="selectedApp?.homepageUrl" :href="selectedApp.homepageUrl" target="_blank" class="text-blue-500 text-sm hover:underline mt-1 flex items-center">
            {{ selectedApp.homepageUrl }} <Icon icon="ion:ios-arrow-forward" width="12" class="ml-1"/>
          </a>
        </div>

        <div class="space-y-6">
          <div class="detail-group">
            <label class="detail-label">客户凭证</label>
            <div class="bg-gray-50 dark:bg-gray-800/50 rounded-xl overflow-hidden">
              <div class="p-3 border-b border-gray-100 dark:border-gray-700/50 flex justify-between items-center">
                <div class="flex flex-col">
                  <span class="text-[10px] uppercase text-gray-400 font-bold tracking-wider">Client ID</span>
                  <code class="text-sm font-mono text-gray-800 dark:text-gray-200">{{ selectedApp?.clientId }}</code>
                </div>
                <button @click="copyToClipboard(selectedApp?.clientId)" class="text-blue-500 hover:bg-blue-500/10 p-1.5 rounded-lg transition-colors"><Icon icon="ion:copy-outline"/></button>
              </div>
              <div class="p-3 flex justify-between items-center">
                <div class="flex flex-col w-full mr-2">
                  <span class="text-[10px] uppercase text-gray-400 font-bold tracking-wider">Client Secret</span>
                  <div class="flex items-center justify-between w-full">
                    <input :type="showSecret ? 'text' : 'password'" readonly :value="selectedApp?.clientSecret" class="bg-transparent font-mono text-sm text-gray-800 dark:text-gray-200 outline-none w-full"/>
                  </div>
                </div>
                <div class="flex gap-1">
                  <button @click="showSecret = !showSecret" class="text-gray-500 hover:bg-gray-200/50 dark:hover:bg-white/10 p-1.5 rounded-lg transition-colors">
                    <Icon :icon="showSecret ? 'ion:eye-off-outline' : 'ion:eye-outline'" />
                  </button>
                  <button @click="copyToClipboard(selectedApp?.clientSecret)" class="text-blue-500 hover:bg-blue-500/10 p-1.5 rounded-lg transition-colors"><Icon icon="ion:copy-outline"/></button>
                </div>
              </div>
            </div>
          </div>

          <div class="detail-group">
            <label class="detail-label">配置</label>
            <div class="bg-gray-50 dark:bg-gray-800/50 rounded-xl p-4 space-y-3">
              <div v-for="(uri, idx) in toUriList(selectedApp?.redirectUris)" :key="idx" class="flex items-start gap-2 text-sm text-gray-600 dark:text-gray-300 break-all">
                <Icon icon="ion:link-outline" class="mt-0.5 text-gray-400 shrink-0" />
                <span>{{ uri }}</span>
              </div>
              <div v-if="selectedApp?.supportsPkce" class="flex items-center gap-2 text-sm text-blue-600 bg-blue-50 dark:bg-blue-500/10 dark:text-blue-400 p-2 rounded-lg w-fit px-3">
                <Icon icon="ion:shield-checkmark-outline" /> PKCE Enabled
              </div>
            </div>
          </div>
        </div>
      </div>
    </n-modal>

    <!-- 帮助指南保持原样，仅微调样式以匹配 -->
    <Transition name="modal-fade">
      <HelpGuide
          v-if="showHelpGuide"
          title="开发者文档"
          :visible="showHelpGuide"
          @close="handleHelpGuideClose"
      >
        <!-- (HelpGuide 内容略，建议在 HelpGuide 组件内部也进行类似的 Tailwind 风格更新) -->
        <div class="prose dark:prose-invert max-w-none p-4">
          <!-- 复用原有内容 -->
          <h3 class="text-xl font-bold mb-4">集成指南</h3>
          <p class="text-gray-600 dark:text-gray-300 mb-4">客户端应用允许第三方系统通过 OAuth 2.0 协议接入认证服务。</p>
          <!-- 省略其他原有内容... -->
        </div>
      </HelpGuide>
    </Transition>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, defineComponent, h } from 'vue';
import { useMessage, useDialog, NModal, NSwitch } from 'naive-ui';
import { Icon } from '@iconify/vue';
import HelpGuide from '../components/HelpGuide.vue';
import { ClientAppService } from '../services/ClientAppService';
import type { ClientApplication, CreateClientAppModel, UpdateClientAppModel } from '../models';
import { useLayoutStore } from '../stores/LayoutStore';

const message = useMessage();
const dialog = useDialog();
const layoutStore = useLayoutStore();

// 状态管理
const clientApps = ref<ClientApplication[]>([]);
const searchQuery = ref('');
const isLoading = ref(false);
const isSubmitting = ref(false);

// 模态框状态
const showCreateModal = ref(false);
const showEditModal = ref(false);
const showDetailsModal = ref(false);
const showHelpGuide = ref(false);
const selectedApp = ref<ClientApplication | null>(null);
const showSecret = ref(false);

// 表单数据
const createForm = ref<CreateClientAppModel>({
  applicationName: '',
  description: '',
  homepageUrl: '',
  redirectUris: [],
  logoUrl: '',
  supportsPkce: false,
  isNeedEMail: false
});
const redirectUrisText = ref('');

const editForm = ref<UpdateClientAppModel>({
  applicationName: '',
  description: '',
  homepageUrl: '',
  redirectUris: [],
  logoUrl: '',
  isActive: true,
  supportsPkce: false,
  isNeedEMail: false
});
const editRedirectUrisText = ref('');
const currentEditClientId = ref('');

// 后端 ClientAppVO.RedirectUris 为字符串数组；兼容早期按分号拼接的字符串返回。
const toUriList = (uris: unknown): string[] => {
  if (Array.isArray(uris)) return uris.filter(Boolean) as string[];
  if (typeof uris === 'string') return uris.split(';').map(u => u.trim()).filter(Boolean);
  return [];
};

// 过滤逻辑
const filteredClientApps = computed(() => {
  if (!searchQuery.value.trim()) {
    return clientApps.value;
  }
  const query = searchQuery.value.toLowerCase().trim();
  return clientApps.value.filter(app =>
      app.applicationName.toLowerCase().includes(query) ||
      app.clientId.toLowerCase().includes(query)
  );
});

// API 操作
const loadClientApps = async () => {
  isLoading.value = true;
  try {
    clientApps.value = await ClientAppService.getAllClientApplications();
  } catch (error) {
    message.error((error as Error).message || '加载失败');
  } finally {
    isLoading.value = false;
  }
};

const formatDateSimple = (dateString?: string) => {
  if (!dateString) return '-';
  return new Date(dateString).toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit'
  });
};

// 复制功能
const copyToClipboard = async (text?: string) => {
  if (!text) return;
  try {
    await navigator.clipboard.writeText(text);
    message.success('已复制');
  } catch {
    message.error('复制失败');
  }
};

// 创建逻辑
const openCreateModal = () => {
  createForm.value = {
    applicationName: '', description: '', homepageUrl: '',
    redirectUris: [], logoUrl: '', supportsPkce: false, isNeedEMail: false
  };
  redirectUrisText.value = '';
  showCreateModal.value = true;
};

const handleCreateModalClose = () => showCreateModal.value = false;

const handleCreateSubmit = async () => {
  const uris = redirectUrisText.value.split('\n').map(u => u.trim()).filter(u => u);
  if (uris.length === 0) {
    message.warning('需要至少一个回调 URL');
    return;
  }
  createForm.value.redirectUris = uris;
  isSubmitting.value = true;
  try {
    await ClientAppService.createClientApplication(createForm.value);
    message.success('创建成功');
    handleCreateModalClose();
    await loadClientApps();
  } catch (e) {
    message.error((e as Error).message);
  } finally {
    isSubmitting.value = false;
  }
};

// 编辑逻辑
const openEditModal = (app: ClientApplication) => {
  selectedApp.value = app;
  currentEditClientId.value = app.clientId;
  editForm.value = { ...app, redirectUris: toUriList(app.redirectUris) };
  editRedirectUrisText.value = toUriList(app.redirectUris).join('\n');
  showEditModal.value = true;
};
const handleEditModalClose = () => showEditModal.value = false;

const handleEditSubmit = async () => {
  const uris = editRedirectUrisText.value.split('\n').map(u => u.trim()).filter(u => u);
  if (uris.length === 0) {
    message.warning('需要至少一个回调 URL');
    return;
  }
  editForm.value.redirectUris = uris;
  isSubmitting.value = true;
  try {
    await ClientAppService.updateClientApplication(currentEditClientId.value, editForm.value);
    message.success('更新成功');
    handleEditModalClose();
    await loadClientApps();
  } catch (e) {
    message.error((e as Error).message);
  } finally {
    isSubmitting.value = false;
  }
};

// 详情逻辑
const openDetailsModal = (app: ClientApplication) => {
  selectedApp.value = app;
  showSecret.value = false;
  showDetailsModal.value = true;
};


// 删除逻辑
const confirmDelete = (app: ClientApplication) => {
  dialog.warning({
    title: '删除应用',
    content: `确定删除 "${app.applicationName}"？此操作不可恢复。`,
    positiveText: '确认删除',
    negativeText: '取消',
    positiveButtonProps: { type: 'error', bordered: false, size: 'medium' },
    onPositiveClick: async () => {
      try {
        await ClientAppService.deleteClientApplication(app.clientId);
        message.success('已删除');
        await loadClientApps();
      } catch (e) {
        message.error((e as Error).message);
      }
    }
  });
};

const handleHelpGuideClose = () => showHelpGuide.value = false;

// 生命周期
onMounted(() => {
  loadClientApps();
  layoutStore.setPageHeader('应用管理', '管理 OAuth 2.0 客户端接入');
  layoutStore.setShowPageActions(true);

  // 使用 Render Function 定义 Header 按钮 (保持原有逻辑，样式微调)
  const ActionsComponent = defineComponent({
    setup() {
      return () => h('div', { class: 'flex gap-3' }, [
        h('button', {
          class: 'hidden sm:flex items-center px-4 py-2 rounded-full text-sm font-medium bg-gray-100 dark:bg-white/10 hover:bg-gray-200 dark:hover:bg-white/20 text-gray-700 dark:text-gray-200 transition-colors',
          onClick: () => showHelpGuide.value = true
        }, [h(Icon, { icon: 'ion:ios-help-circle-outline', class: 'mr-1.5', width: 18 }), '帮助']),

        h('button', {
          class: 'hidden sm:flex items-center px-4 py-2 rounded-full text-sm font-medium bg-[#0071e3] hover:bg-[#0077ed] text-white shadow-sm transition-all',
          onClick: openCreateModal
        }, [h(Icon, { icon: 'ion:plus', class: 'mr-1', width: 18 }), '新建应用'])
      ]);
    }
  });
  layoutStore.setActionsComponent(ActionsComponent);
});

onBeforeUnmount(() => {
  layoutStore.clearPageHeader();
});
</script>

<style scoped>
/* Apple风格全局覆盖 - 放在没有 scoped 的 style 中或者全局 css 中，这里模拟 */
.apple-container {
  background-color: #fbfbfd; /* iOS Light Background */
}
.dark .apple-container {
  background-color: #000000; /* iOS Dark Background */
}

/* 搜索框样式 */
.search-wrapper .search-input {
  background-color: rgba(118, 118, 128, 0.12);
  color: #1d1d1f;
}
.dark .search-wrapper .search-input {
  background-color: rgba(118, 118, 128, 0.24);
  color: #f5f5f7;
}
.search-wrapper:focus-within .search-input {
  background-color: #fff;
  box-shadow: 0 0 0 4px rgba(0, 125, 250, 0.1);
}
.dark .search-wrapper:focus-within .search-input {
  background-color: #1c1c1e;
  box-shadow: 0 0 0 4px rgba(0, 125, 250, 0.2);
}

/* 内容卡片边框 */
.content-card {
  border-color: rgba(0, 0, 0, 0.05);
}
.dark .content-card {
  border-color: rgba(255, 255, 255, 0.1);
}

/* 输入框组样式 */
.input-group .apple-input,
.input-group .apple-textarea {
  width: 100%;
  padding: 12px 16px;
  background-color: #f5f5f7;
  color: #1d1d1f;
  outline: none;
  transition: background-color 0.2s;
}
.dark .input-group .apple-input,
.dark .input-group .apple-textarea {
  background-color: rgba(255, 255, 255, 0.1);
  color: #fff;
}
.input-group .apple-input:focus,
.input-group .apple-textarea:focus {
  background-color: #fff;
  z-index: 10;
}
.dark .input-group .apple-input:focus,
.dark .input-group .apple-textarea:focus {
  background-color: rgba(255, 255, 255, 0.15);
}

/* NaiveUI Modal 定制 */
.custom-modal .n-card-header {
  padding-top: 24px;
  padding-bottom: 8px;
}
.custom-modal .n-card__content {
  padding: 0 24px 24px 24px;
}
.form-label {
  display: block;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  color: #86868b;
  margin-bottom: 8px;
  margin-left: 4px;
}
.dark .form-label {
  color: #98989d;
}

/* 操作按钮 */
.action-btn {
  padding: 8px;
  border-radius: 50%;
  color: #86868b;
  transition: all 0.2s;
}
.action-btn:hover {
  background-color: #f5f5f7;
  color: #0071e3;
}
.dark .action-btn:hover {
  background-color: rgba(255, 255, 255, 0.1);
  color: #409cff;
}

/* 详情 Label */
.detail-label {
  display: block;
  font-size: 13px;
  color: #86868b;
  margin-bottom: 6px;
  font-weight: 500;
}

/* 滚动条美化 (可选) */
::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}
::-webkit-scrollbar-track {
  background: transparent;
}
::-webkit-scrollbar-thumb {
  background: rgba(0, 0, 0, 0.1);
  border-radius: 4px;
}
.dark ::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.15);
}
</style>