<template>
  <!-- 外层容器：负责背景动画和居中定位 -->
  <div class="login-container relative flex min-h-[calc(100vh-64px)] w-full items-center justify-center overflow-hidden bg-gray-50 transition-colors duration-500 dark:bg-black/30">

    <!-- 背景光斑动画 (原生 CSS 实现) -->
    <div class="aurora-bg absolute inset-0 z-0 opacity-60 dark:opacity-40">
      <div class="blob blob-1"></div>
      <div class="blob blob-2"></div>
      <div class="blob blob-3"></div>
    </div>

    <!-- 登录主卡片 -->
    <div class="login-card relative z-10 w-full max-w-[440px] px-6 py-10 sm:px-10 sm:py-12">

      <!-- 顶部 Logo 与标题 -->
      <div class="mb-10 flex flex-col items-center text-center">
        <div class="mb-6 flex h-16 w-16 items-center justify-center rounded-full bg-white/50 shadow-sm backdrop-blur-md dark:bg-white/10">
          <!-- 使用 Lucide 图标: 这里用指纹图标模拟 ID 概念 -->
          <Icon icon="fingerprint-pattern" class="h-8 w-8 text-gray-900 dark:text-white" />
        </div>
        <h1 class="text-2xl font-semibold tracking-tight text-gray-900 dark:text-white md:text-3xl">
          登录 iMember
        </h1>
        <p class="mt-3 text-[15px] text-gray-500 dark:text-gray-400">
          管理您的数字校园身份
        </p>
      </div>

      <!-- 表单区域 -->
      <n-form
          ref="formRef"
          :model="form"
          :rules="rules"
          :show-label="false"
          class="space-y-5"
      >
        <!-- 学号输入 -->
        <n-form-item path="studentId" class="apple-input-wrapper">
          <n-input
              v-model:value="form.studentId"
              placeholder="学号"
              size="large"
              class="rounded-xl! bg-white/60! focus:bg-white! dark:bg-white/5! dark:focus:bg-black/40!"
              @keyup.enter="handleLogin"
          >
            <template #prefix>
              <Icon icon="graduation-cap" class="text-gray-400 mr-1" width="20" />
            </template>
          </n-input>
        </n-form-item>

        <!-- 密码输入 -->
        <n-form-item path="password" class="apple-input-wrapper">
          <n-input
              type="password"
              v-model:value="form.password"
              placeholder="密码"
              size="large"
              show-password-on="click"
              class="!rounded-xl !bg-white/60 focus:!bg-white dark:!bg-white/5 dark:focus:!bg-black/40"
              @keyup.enter="handleLogin"
          >
            <template #prefix>
              <Icon icon="lock-keyhole" class="text-gray-400 mr-1" width="20" />
            </template>
          </n-input>
        </n-form-item>

        <!-- 辅助功能与错误提示 -->
        <div class="flex items-center justify-between pt-1">
          <div class="flex items-center gap-2">
            <!-- 自定义 Checkbox 样式，这里复用 NaiveUI 但稍微调整颜色 -->
            <n-checkbox v-model:checked="form.rememberMe">
              <span class="text-sm text-gray-600 dark:text-gray-300">记住我</span>
            </n-checkbox>
          </div>
          <router-link
              to="/ForgotPassword"
              class="text-sm font-medium text-[#0071e3] transition-opacity hover:opacity-80 dark:text-[#2997ff]"
          >
            忘记密码？
          </router-link>
        </div>

        <!-- 错误信息提示 (带动画) -->
        <div v-if="errorMsg" class="flex items-center justify-center gap-2 rounded-lg bg-red-50 p-3 text-sm text-red-600 dark:bg-red-900/20 dark:text-red-400">
          <Icon icon="circle-alert" />
          <span>{{ errorMsg }}</span>
        </div>

        <!-- 登录按钮 -->
        <button
            @click="handleLogin"
            :disabled="loading"
            class="apple-btn relative mt-4 flex w-full items-center justify-center gap-2 overflow-hidden rounded-xl bg-[#0071e3] py-3.5 text-[15px] font-medium text-white shadow-lg shadow-blue-500/20 transition-all hover:bg-[#0077ED] hover:shadow-blue-500/30 disabled:cursor-not-allowed disabled:opacity-70 dark:bg-white dark:text-black dark:shadow-none dark:hover:bg-gray-200"
        >
          <Icon v-if="loading" icon="loader" class="animate-spin" width="20" />
          <Icon v-else icon="arrow-right" class="absolute right-4 opacity-0 transition-all group-hover:opacity-100" />
          <span>{{ loading ? '正在验证...' : '登 录' }}</span>
        </button>
      </n-form>

      <!-- 底部注册引导 -->
      <div class="mt-12 text-center text-[14px] text-gray-500 dark:text-gray-400">
        还没有 iMember ID？
        <router-link
            to="/SignUp"
            class="font-medium text-[#0071e3] transition-colors hover:underline dark:text-[#2997ff]"
        >
          立即注册
        </router-link>
      </div>

      <!-- 底部版权/链接 -->
      <div class="mt-8 flex justify-center gap-6 text-xs text-gray-400 dark:text-gray-600">
        <a href="/PrivacyPolicy" class="hover:text-gray-600 dark:hover:text-gray-400">隐私政策</a>
        <span class="text-gray-300 dark:text-gray-700">|</span>
        <a href="/HelpCenter" class="hover:text-gray-600 dark:hover:text-gray-400">帮助中心</a>
      </div>

    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { NInput, NCheckbox, NForm, NFormItem } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import { useAuthorizationStore } from "../../stores/Authorization"

const authorizationStore = useAuthorizationStore()
const router = useRouter()

const formRef = ref()
const form = ref({
  studentId: '',
  password: '',
  rememberMe: false
})
const loading = ref(false)
const errorMsg = ref('')

const rules = {
  studentId: {
    required: true,
    message: '请输入您的学号',
    trigger: ['blur', 'input']
  },
  password: {
    required: true,
    message: '请输入密码',
    trigger: ['blur', 'input']
  }
}

const handleLogin = async () => {
  if (loading.value) return

  formRef.value?.validate(async (errors: any) => {
    if (!errors) {
      loading.value = true
      errorMsg.value = '' // 清空上次的错误
      try {
        await authorizationStore.login({
          userId: form.value.studentId,
          password: form.value.password,
          rememberMe: form.value.rememberMe
        })

        await router.push('/Centre')
      } catch (err: any) {
        console.error(err)
        errorMsg.value = err.message || '账号或密码错误，请重试'
      } finally {
        loading.value = false
      }
    }
  })
}
</script>

<style scoped>
/*
   Apple 风格的极光背景动画
   使用原生 CSS 关键帧动画和滤镜
*/

.aurora-bg {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: transparent;
  filter: blur(80px);
  pointer-events: none;
}

.blob {
  position: absolute;
  border-radius: 50%;
  animation: float 10s infinite ease-in-out alternate;
}

.blob-1 {
  top: -10%;
  left: -10%;
  width: 50vw;
  height: 50vw;
  background: #e0e7ff; /* Light Indigo */
  animation-duration: 12s;
}

.blob-2 {
  bottom: -10%;
  right: -10%;
  width: 40vw;
  height: 40vw;
  background: #dbeafe; /* Light Blue */
  animation-duration: 15s;
  animation-delay: -2s;
}

.blob-3 {
  top: 40%;
  left: 40%;
  width: 30vw;
  height: 30vw;
  background: #f3e8ff; /* Light Purple */
  opacity: 0.6;
  animation-duration: 18s;
}

/* Dark mode color adjustments for blobs via CSS variables or direct media query */
&:where(.dark, .dark *) {
  .login-container .blob-1 { background: #1e1b4b; }
  .login-container .blob-2 { background: #172554; }
  .login-container .blob-3 { background: #312e81; }
}

@keyframes float {
  0% { transform: translate(0, 0) scale(1); }
  50% { transform: translate(20px, 30px) scale(1.1); }
  100% { transform: translate(-20px, -10px) scale(1); }
}

/*
   Apple 风格的卡片去除了明显的边框，使用阴影和毛玻璃
   在 Dark Mode 下更依赖半透明背景
*/
.login-card {
  /* Glassmorphism effect */
  background: rgba(255, 255, 255, 0.65);
  border-radius: 24px; /* Apple 常用的大圆角 */
  box-shadow:
      0 20px 40px -10px rgba(0, 0, 0, 0.08),
      0 0 0 1px rgba(0, 0, 0, 0.02); /* 极细的边框 */
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
}

.dark .login-card {
  background: rgba(28, 28, 30, 0.65);
  box-shadow:
      0 25px 50px -12px rgba(0, 0, 0, 0.5),
      0 0 0 1px rgba(255, 255, 255, 0.08);
}

/*
   按钮点击效果：Apple 风格通常是缩小一点点
*/
.apple-btn:active:not(:disabled) {
  transform: scale(0.98);
}

/*
   覆盖 NaiveUI 的部分默认样式，使其更贴合原生 iOS 质感
   移除 NaiveUI input 默认的 border，改用背景色区分
*/
:deep(.n-input) {
  --n-border: none !important;
  --n-border-hover: none !important;
  --n-border-focus: none !important;
  --n-box-shadow-focus: 0 0 0 2px rgba(0, 113, 227, 0.3) !important; /* Apple Blue Focus Ring */
}

:deep(.n-input__state-border) {
  display: none;
}
</style>