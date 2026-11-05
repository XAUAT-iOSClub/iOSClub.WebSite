<template>
  <div class="min-h-screen bg-gradient-to-br from-slate-100 to-slate-200 dark:from-neutral-900 dark:to-neutral-800 flex items-center justify-center p-4 transition-colors duration-300">
    <div class="w-full max-w-md bg-white/80 dark:bg-neutral-900/80 backdrop-blur-lg rounded-2xl shadow-xl p-8 transition-all duration-300">
      <div class="text-center mb-8">
        <div class="w-20 h-20 mx-auto bg-red-100 dark:bg-red-900/30 rounded-full flex items-center justify-center mb-4">
          <n-icon size="48" color="#ff4d4f">
            <Icon icon="circle-x" />
          </n-icon>
        </div>
        <h1 class="text-xl md:text-3xl font-semibold bg-gradient-to-r from-red-500 to-red-600 bg-clip-text text-transparent">
          访问被拒绝
        </h1>
        <p class="text-gray-500 dark:text-gray-400 mt-2">
          您没有权限访问此资源或操作
        </p>
        <p v-if="errorMessage" class="text-red-500 dark:text-red-400 mt-2">{{ errorMessage }}</p>
      </div>

      <div class="flex flex-col items-center gap-4">
        <n-button type="primary" @click="handleGoBack" class="w-full">
          返回上一页
        </n-button>
        <n-button @click="handleGoHome">
          返回首页
        </n-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {NButton, NIcon} from 'naive-ui'
import {useRouter, useRoute} from 'vue-router'
import Icon from '@/components/Icon.vue'

const router = useRouter()
const route = useRoute()

// 可以从路由参数中获取错误信息
const errorMessage = route.query.error as string || ''

const handleGoBack = () => {
  router.back()
}

const handleGoHome = () => {
  router.push('/')
}
</script>

<style scoped>
.btn {
  width: 100%;
  padding: 8px 0;
  background: linear-gradient(90deg, #FF99C8 0%, #646EF8 100%);
  border: none;
  border-radius: 12px;
  color: #FFFFFF;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn:hover:not(:disabled) {
  transform: scale(1.03);
  box-shadow: 0 6px 20px rgba(255, 153, 200, 0.3);
}

.btn:active:not(:disabled) {
  transform: scale(0.98);
  box-shadow: 0 4px 16px rgba(255, 153, 200, 0.2);
}

.btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
  transform: none;
  box-shadow: none;
}
</style>