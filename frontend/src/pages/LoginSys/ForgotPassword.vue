<template>
  <!-- 页面背景：使用动态网格/渐变背景，模仿 Apple ID 登录页 -->
  <div class="page-container min-h-[calc(100vh-64px)] flex items-center justify-center p-4 transition-all duration-500">

    <!-- 核心卡片：模仿 iOS 弹窗/毛玻璃风格 -->
    <div class="auth-card w-full max-w-[400px] overflow-hidden relative transition-all duration-300">

      <!-- 顶部图标区域 -->
      <div class="pt-10 pb-6 flex flex-col items-center text-center px-8">
        <!-- 仿 Apple ID 风格的图标容器 -->
        <div class="icon-wrapper w-20 h-20 rounded-full flex items-center justify-center mb-6 shadow-sm transition-transform hover:scale-105 duration-300">
          <Icon
              :icon="step === 1 ? 'lock-keyhole-open' : 'shield-check'"
              morph
              class="w-10 h-10 transition-all duration-300 icon-color"
          />
        </div>

        <h1 class="text-2xl font-bold tracking-tight mb-2 title-text">
          {{ step === 1 ? '找回密码' : '设置新密码' }}
        </h1>
        <p class="text-sm subtitle-text">
          {{ step === 1 ? '输入您的学号，我们将向您发送验证码' : '验证身份并创建一个新密码' }}
        </p>
      </div>

      <!-- 表单区域 -->
      <div class="px-8 pb-10">
        <!-- 第一步：请求重置 -->
        <n-form v-if="step === 1" :model="formStep1" :rules="rulesStep1" ref="formStep1Ref" :show-label="false">
          <n-form-item path="studentId">
            <n-input
                v-model:value="formStep1.studentId"
                placeholder="学号"
                size="large"
                class="apple-input"
            >
              <template #prefix>
                <Icon icon="graduation-cap" class="text-gray-400 text-lg mr-1" />
              </template>
            </n-input>
          </n-form-item>
        </n-form>

        <!-- 第二步：重置密码 -->
        <n-form v-else :model="formStep2" :rules="rulesStep2" ref="formStep2Ref" :show-label="false">
          <div class="mb-6 flex justify-center">
            <!-- 学号只读展示，稍微小一点 -->
            <div class="read-only-tag px-3 py-1 rounded-full text-xs font-medium mb-4">
              {{ formStep2.studentId }}
            </div>
          </div>

          <!-- 验证码输入 - 居中 -->
          <n-form-item path="verificationCode" content-class="flex justify-center mb-6">
            <n-input-otp
                v-model:value="formStep2.verificationCode"
                :length="6"
                size="large"
                class="apple-otp"
            />
          </n-form-item>

          <n-form-item path="newPassword">
            <n-input
                v-model:value="formStep2.newPassword"
                type="password"
                show-password-on="click"
                placeholder="新密码"
                size="large"
                class="apple-input"
            >
              <template #prefix>
                <Icon icon="lock-keyhole" class="text-gray-400 text-lg mr-1" />
              </template>
            </n-input>
          </n-form-item>

          <n-form-item path="confirmPassword">
            <n-input
                v-model:value="formStep2.confirmPassword"
                type="password"
                show-password-on="click"
                placeholder="确认新密码"
                size="large"
                class="apple-input"
            >
              <template #prefix>
                <Icon icon="circle-check" class="text-gray-400 text-lg mr-1" />
              </template>
            </n-input>
          </n-form-item>
        </n-form>

        <!-- 状态提示 -->
        <div v-if="errorMsg" class="flex items-center justify-center gap-1.5 text-red-500 mb-4 text-sm bg-red-50 dark:bg-red-900/20 py-2 rounded-lg">
          <Icon icon="circle-alert" />
          <span>{{ errorMsg }}</span>
        </div>

        <div v-if="successMsg" class="flex items-center justify-center gap-1.5 text-green-500 mb-4 text-sm bg-green-50 dark:bg-green-900/20 py-2 rounded-lg">
          <Icon icon="circle-check" />
          <span>{{ successMsg }}</span>
        </div>

        <!-- 按钮组 -->
        <div class="space-y-4">
          <n-button
              @click="handleSubmit"
              :loading="loading"
              type="primary"
              size="large"
              block
              class="apple-btn-primary"
              :disabled="loading"
          >
            {{ loading ? '处理中...' : (step === 1 ? '继续' : '重置密码') }}
          </n-button>

          <div class="flex items-center justify-between text-sm mt-4">
            <router-link
                to="/Login"
                class="flex items-center gap-1 text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-200 transition-colors"
            >
              <Icon icon="arrow-left" />
              返回登录
            </router-link>

            <button
                v-if="step === 2"
                @click="step = 1"
                class="text-blue-500 hover:text-blue-600 dark:text-blue-400 font-medium transition-colors"
            >
              重新发送
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { NForm, NFormItem, NInput, NButton, NInputOtp } from 'naive-ui'
import Icon from '@/components/Icon.vue'

// 假设这里的 Service 路径保持不变
import { AuthService } from '../../services/AuthService';

const router = useRouter()
const formStep1Ref = ref<InstanceType<typeof NForm> | null>(null)
const formStep2Ref = ref<InstanceType<typeof NForm> | null>(null)
const loading = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const step = ref(1)

// 状态定义
interface FormStep1State {
  studentId: string
}

interface FormStep2State {
  studentId: string
  verificationCode: string[] // 改为 string 类型更符合 Naive UI OTP 默认行为
  newPassword: string
  confirmPassword: string
}

const formStep1 = reactive<FormStep1State>({
  studentId: ''
})

const formStep2 = reactive<FormStep2State>({
  studentId: '',
  verificationCode: ['', '', '', '', '', ''], // 初始化为包含6个空字符串的数组
  newPassword: '',
  confirmPassword: ''
})

// 校验规则
const rulesStep1 = {
  studentId: {
    required: true,
    message: '请输入您的学号',
    trigger: ['input', 'blur']
  }
}

const rulesStep2 = {
  verificationCode: {
    required: true,
    message: '请输入完整验证码',
    trigger: ['input', 'blur'],
    validator: (_: any, value: string[]) => {
      if (value && value.length === 6 && value.every(v => v !== '')) return true
      return new Error('请输入完整验证码')
    }
  },
  newPassword: {
    required: true,
    message: '请输入新密码',
    trigger: ['input', 'blur'],
    validator: (_: any, value: string) => {
      if (!value) return new Error('请输入新密码')
      // 密码至少8个字符，包含一个大写字母和一个数字
      const passwordReg = /^(?=.*[A-Z])(?=.*\d).{8,}$/
      if (passwordReg.test(value)) return true
      return new Error('密码至少8个字符，包含一个大写字母和一个数字')
    }
  },
  confirmPassword: {
    required: true,
    message: '请确认新密码',
    trigger: ['input', 'blur'],
    validator: (_: any, value: string) => {
      if (!value) return new Error('请再次输入密码')
      if (value !== formStep2.newPassword) {
        return new Error('两次输入的密码不一致')
      }
      return true
    }
  }
}

const handleSubmit = () => {
  if (loading.value) return
  errorMsg.value = ''
  successMsg.value = ''

  if (step.value === 1) {
    formStep1Ref.value?.validate(async (errors) => {
      if (!errors) {
        loading.value = true
        try {
          await AuthService.requestPasswordReset(formStep1.studentId);
          // 后端不再区分"用户不存在/没有邮箱"，前端也保持同样的模糊提示，
          // 避免通过界面反馈枚举学号。
          successMsg.value = '如果该账号已绑定邮箱，验证码已发送，请查收'

          // 迁移数据并切换步骤
          formStep2.studentId = formStep1.studentId
          formStep2.verificationCode = []

          setTimeout(() => {
            step.value = 2
            successMsg.value = ''
          }, 1500)
        } catch (err: any) {
          errorMsg.value = err.message || '请求失败，请稍后重试'
        } finally {
          loading.value = false
        }
      }
    })
  } else {
    formStep2Ref.value?.validate(async (errors) => {
      if (!errors) {
        loading.value = true
        try {
          // Join the verificationCode array into a string for the API call
          const verificationCodeString = formStep2.verificationCode.join('')
          
          await AuthService.resetPassword(
              formStep2.studentId,
              verificationCodeString,
              formStep2.newPassword
          );

          successMsg.value = '密码重置成功，准备跳转...'
          setTimeout(() => {
            router.push('/Login')
          }, 2000)
        } catch (err: any) {
          errorMsg.value = err.message || '重置失败，请检查验证码'
        } finally {
          loading.value = false
        }
      }
    })
  }
}
</script>

<style scoped>
/*
  Apple/iCloud 风格配置
  使用原生 CSS 变量来处理深色模式，而不是混用 tailwind utilities
*/
.page-container {
  background-color: #f5f5f7; /* Apple 浅灰色背景 */
}

.auth-card {
  background-color: rgba(255, 255, 255, 0.8);
  backdrop-filter: blur(20px);
  -webkit-backdrop-filter: blur(20px);
  border-radius: 24px; /* 更大的圆角 */
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.6);
}

.icon-wrapper {
  background: linear-gradient(135deg, #e0eafc 0%, #cfdef3 100%);
}

.icon-color {
  color: #007AFF; /* iOS Blue */
}

.title-text {
  color: #1d1d1f;
}

.subtitle-text {
  color: #86868b;
}

.read-only-tag {
  background-color: #f5f5f7;
  color: #86868b;
}

/* 暗黑模式适配 */
.dark .page-container {
  background-color: #000000;
}

.dark .auth-card {
  background-color: rgba(28, 28, 30, 0.7);
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.4);
}

.dark .icon-wrapper {
  background: linear-gradient(135deg, #1c1c1e 0%, #2c2c2e 100%);
  border: 1px solid rgba(255, 255, 255, 0.1);
}

.dark .icon-color {
  color: #0A84FF; /* iOS Dark Mode Blue */
}

.dark .title-text {
  color: #f5f5f7;
}

.dark .subtitle-text {
  color: #a1a1a6;
}

.dark .read-only-tag {
  background-color: rgba(255,255,255,0.1);
  color: #a1a1a6;
}

/* 深度定制 Naive UI 组件以符合 Apple 风格 */

/* Input 样式覆写 */
:deep(.apple-input .n-input__border),
:deep(.apple-input .n-input__state-border) {
  border-radius: 12px;
}

:deep(.apple-input) {
  background-color: transparent;
}

:deep(.apple-input .n-input-wrapper) {
  padding-left: 12px;
  padding-right: 12px;
}

/* 亮色模式下 Input 背景 */
:deep(.n-input .n-input__input-el) {
  height: 44px; /* 此高度更符合 iOS 触控标准 */
}

/* 主按钮样式 - iOS 风格 */
:deep(.apple-btn-primary) {
  height: 48px;
  border-radius: 12px;
  font-size: 16px;
  font-weight: 500;
  background-color: #007AFF;
  border: none;
}

:deep(.apple-btn-primary:hover) {
  background-color: #0071e3; /* Apple hover blue */
}

:deep(.apple-btn-primary:focus) {
  box-shadow: 0 0 0 4px rgba(0, 122, 255, 0.2);
}

/* Dark mode button */
.dark :deep(.apple-btn-primary) {
  background-color: #0A84FF;
}
.dark :deep(.apple-btn-primary:hover) {
  background-color: #409cff;
}

/* OTP 输入框样式微调 */
:deep(.apple-otp .n-input) {
  border-radius: 12px !important;
  height: 48px;
  width: 40px;
}
</style>