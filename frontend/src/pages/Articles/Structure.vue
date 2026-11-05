<template>
  <div class="min-h-screen p-6">
    <div v-if="loading" class="flex justify-center items-center h-screen">
      <div class="animate-spin rounded-full h-12 w-12 border-t-2 border-b-2 border-blue-500"></div>
    </div>

    <div v-else-if="article">
      <MarkdownComponent :content="articleContent"/>
    </div>

    <div v-else-if="!loading && !article" class="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-16">
      <div class="bg-white dark:bg-neutral-800 rounded-2xl shadow-lg p-12 text-center">
        <div class="flex justify-center items-center w-full">
          <Icon icon="triangle-alert" width="5rem" height="5rem" style="color: #ea580c" class="dark:text-orange-400"/>
        </div>
        <h2 class="text-2xl font-bold text-gray-900 dark:text-white mb-2">文章未找到</h2>
        <p class="text-gray-600 dark:text-gray-400 mb-6">抱歉，您访问的文章不存在或已被删除。</p>
        <button
            @click="goBack"
            class="px-6 py-3 bg-blue-600 text-white rounded-full hover:bg-blue-700 transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
        >
          返回
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {computed, onMounted, ref} from 'vue';
import {useRouter} from 'vue-router';
import {ArticleService} from '../../services/ArticleService';
import MarkdownComponent from '../../components/MarkdownComponent.vue';
import {ArticleModel} from '../../models';
import Icon from '@/components/Icon.vue'

const router = useRouter();
const article = ref<ArticleModel | null>(null);
const loading = ref(true);

// 返回
const goBack = () => {
  router.go(-1);
};

// 为 MarkdownComponent 准备内容数据
const articleContent = computed(() => {
  if (!article.value) return {
    title: '',
    date: '',
    content: ''
  };

  return {
    title: article.value.title,
    date: article.value.lastWriteTime,
    content: article.value.content,
  };
});

// 加载文章详情
const loadArticle = async (path: string) => {
  try {
    loading.value = true;
    article.value = await ArticleService.getArticle(path);
  } catch (error) {
    article.value = null;
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  loadArticle('Structure');
});
</script>

<style scoped>

</style>