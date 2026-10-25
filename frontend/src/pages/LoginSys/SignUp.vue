<template>
  <!-- 全局容器：使用 Apple 风格的动态模糊背景 -->
  <div
      class="apple-bg relative min-h-[calc(100vh-64px)] w-full flex items-center justify-center p-4 overflow-hidden transition-all duration-500">

    <!-- 背景装饰光斑 (Tailwind 4 CSS 动画) -->
    <div
        class="absolute top-[-10%] left-[-10%] w-[40vw] h-[40vw] rounded-full bg-blue-400/30 blur-[100px] pointer-events-none animate-blob mix-blend-multiply dark:mix-blend-soft-light dark:bg-blue-600/20"></div>
    <div
        class="absolute bottom-[-10%] right-[-10%] w-[40vw] h-[40vw] rounded-full bg-purple-400/30 blur-[100px] pointer-events-none animate-blob animation-delay-2000 mix-blend-multiply dark:mix-blend-soft-light dark:bg-purple-900/20"></div>

    <!-- 主卡片：仿 iCloud 登录框 -->
    <div class="relative z-10 w-full max-w-[440px] apple-card transition-all duration-300">

      <!-- 顶部 Logo 区域 -->
      <div class="pt-10 pb-6 text-center">
        <div
            class="mx-auto w-16 h-16 mb-4 bg-linear-to-b from-gray-100 to-gray-300 dark:from-gray-700 dark:to-gray-800 rounded-[18px] shadow-inner flex items-center justify-center border border-white/20 icon-box">
          <Icon icon="ion:person" class="text-3xl text-gray-600 dark:text-gray-200"/>
        </div>
        <h1 class="text-2xl font-semibold tracking-tight text-gray-900 dark:text-white">
          iMember ID
        </h1>
        <p class="text-gray-500 dark:text-gray-400 text-sm mt-1 font-medium">
          注册以访问所有服务
        </p>
      </div>

      <!-- 精简进度条 -->
      <div v-if="currentStep < 4" class="px-10 mb-6">
        <div class="h-1 w-full bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
          <div
              class="h-full bg-[#0071e3] transition-all duration-500 ease-apple"
              :style="{ width: `${(currentStep) / 3 * 100}%` }"
          ></div>
        </div>
        <div class="flex justify-between mt-2 text-[10px] font-medium uppercase tracking-wider text-gray-400">
          <span>基本</span>
          <span>学校</span>
          <span>账户</span>
        </div>
      </div>

      <!-- 表单内容区域 -->
      <div class="px-8 pb-10">
        <n-form ref="formRef" :model="form" :show-label="false" size="large">

          <!-- 步骤切换动画容器 -->
          <Transition name="slide-fade" mode="out-in">

            <!-- 步骤 1: 基本信息 -->
            <div v-if="currentStep === 1" key="step1" class="space-y-4">
              <n-form-item path="name"
                           :rule="[{ required: true, message: '请输入姓名', trigger: 'blur' }, { pattern: namePattern, message: '姓名应为2-20个汉字或字母，不能包含数字或特殊符号', trigger: 'blur' }]"
                           class="apple-input">
                <n-input v-model:value="form.name" placeholder="姓名" :maxlength="20">
                  <template #prefix>
                    <Icon icon="ion:person-outline" class="input-icon"/>
                  </template>
                </n-input>
              </n-form-item>

              <n-form-item path="studentId"
                           :rule="[{ required: true, message: '请输入学号', trigger: 'blur' }, { pattern: studentIdPattern, message: '学号必须是10位数字', trigger: 'blur' }]"
                           class="apple-input">
                <n-input v-model:value="form.studentId" placeholder="学号" :maxlength="10">
                  <template #prefix>
                    <Icon icon="ion:card-outline" class="input-icon"/>
                  </template>
                </n-input>
              </n-form-item>

              <div class="grid grid-cols-2 gap-4">
                <n-form-item path="gender" :rule="{ required: true, message: '请选择性别', trigger: 'change' }">
                  <n-select v-model:value="form.gender" :options="genderOptions" placeholder="性别"
                            class="apple-select"/>
                </n-form-item>

                <n-form-item path="political" :rule="{ required: true, message: '请选择政治面貌', trigger: 'change' }">
                  <n-select v-model:value="form.political" :options="politicalOptions" placeholder="政治面貌"
                            class="apple-select"/>
                </n-form-item>
              </div>
            </div>

            <!-- 步骤 2: 学校信息 -->
            <div v-else-if="currentStep === 2" key="step2" class="space-y-4">
              <n-form-item path="major" :rule="{ required: true, message: '请选择学院', trigger: 'change' }" class="apple-select">
                <n-select v-model:value="form.major" :options="academyOptions" placeholder="选择学院"
                />
              </n-form-item>

              <n-form-item path="className"
                           :rule="[{ required: true, message: '请输入班级', trigger: 'blur' }, { pattern: classNamePattern, message: '班级名称长度需为2-20个字符', trigger: 'blur' }]"
                           class="apple-input">
                <n-input v-model:value="form.className" placeholder="班级 (如: 机电2401)" :maxlength="20">
                  <template #prefix>
                    <Icon icon="ion:people-outline" class="input-icon"/>
                  </template>
                </n-input>
              </n-form-item>

              <n-form-item path="phone"
                           :rule="[{ required: true, message: '请输入手机号', trigger: 'blur' }, { pattern: phonePattern, message: '请输入正确的手机号', trigger: 'blur' }]"
                           class="apple-input">
                <n-input v-model:value="form.phone" placeholder="手机号码" :maxlength="11">
                  <template #prefix>
                    <Icon icon="ion:call-outline" class="input-icon"/>
                  </template>
                </n-input>
              </n-form-item>

              <n-form-item path="email"
                           :rule="[{ required: true, message: '请输入邮箱' }, { validator: emailValidator, trigger: 'blur' }]"
                           class="apple-input">
                <n-input v-model:value="form.email" placeholder="电子邮箱" :maxlength="256">
                  <template #prefix>
                    <Icon icon="ion:mail-outline" class="input-icon"/>
                  </template>
                </n-input>
              </n-form-item>
            </div>

            <!-- 步骤 3: 账号设置 -->
            <div v-else-if="currentStep === 3" key="step3" class="space-y-4">
              <n-form-item path="password" :rule="[{ required: true, message: '请设置密码', trigger: 'blur' }, { validator: passwordValidator, trigger: ['blur', 'input'] }]">
                <n-input
                    v-model:value="form.password"
                    type="password"
                    show-password-on="click"
                    placeholder="设置密码"
                    class="apple-input"
                >
                  <template #prefix>
                    <Icon icon="ion:lock-closed-outline" class="input-icon"/>
                  </template>
                </n-input>
              </n-form-item>

              <n-form-item
                  path="confirmPassword"
                  :rule="[{ required: true, message: '' }, { validator: (_, v) => v === form.password || new Error('Passwords do not match'), trigger: 'blur' }]"
              >
                <n-input
                    v-model:value="form.confirmPassword"
                    type="password"
                    show-password-on="click"
                    placeholder="确认密码"
                    class="apple-input"
                >
                  <template #prefix>
                    <Icon icon="ion:shield-checkmark-outline" class="input-icon"/>
                  </template>
                </n-input>
              </n-form-item>

              <!-- 信息确认卡片 -->
              <div
                  class="mt-4 p-4 bg-gray-50 dark:bg-white/5 rounded-xl text-sm border border-gray-100 dark:border-white/10">
                <div class="flex items-center gap-2 mb-2 text-gray-500 dark:text-gray-400">
                  <Icon icon="ion:information-circle-outline"/>
                  <span class="font-medium">信息预览</span>
                </div>
                <div class="space-y-1 text-gray-800 dark:text-gray-200 font-medium pl-1">
                  <div class="flex justify-between"><span>姓名</span> <span>{{ form.name }}</span></div>
                  <div class="flex justify-between"><span>学号</span> <span>{{ form.studentId }}</span></div>
                  <div class="flex justify-between"><span>学院</span> <span class="truncate max-w-[150px]">{{
                      form.major
                    }}</span></div>
                </div>
              </div>
            </div>

            <!-- 步骤 4: 成功状态 -->
            <div v-else-if="currentStep === 4" key="step4" class="flex flex-col items-center py-6 text-center">
              <div
                  class="w-20 h-20 rounded-full bg-[#34c759]/10 text-[#34c759] flex items-center justify-center mb-6 animate-scale-in">
                <Icon icon="ion:checkmark-circle" class="text-5xl"/>
              </div>
              <h2 class="text-2xl font-bold text-gray-900 dark:text-white mb-2">欢迎加入</h2>
              <p class="text-gray-500 dark:text-gray-400">账号已创建，即将跳转...</p>
            </div>

          </Transition>
        </n-form>

        <!-- 错误提示 -->
        <div v-if="errorMsg"
             class="mt-4 p-3 bg-red-50 dark:bg-red-900/20 text-red-600 dark:text-red-400 text-sm rounded-lg flex items-center gap-2 animate-shake">
          <Icon icon="ion:alert-circle"/>
          {{ errorMsg }}
        </div>

        <!-- 底部操作栏 -->
        <div v-if="currentStep < 4" class="mt-8 flex items-center gap-4">
          <button
              v-if="currentStep > 1"
              @click="prevStep"
              type="button"
              class="w-12 h-12 rounded-full flex items-center justify-center bg-gray-100 dark:bg-white/10 text-gray-600 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-white/20 transition-colors"
          >
            <Icon icon="ion:arrow-back"/>
          </button>

          <button
              @click="nextStep"
              :disabled="loading"
              type="button"
              class="flex-1 h-12 rounded-full bg-[#0071e3] hover:bg-[#0077ED] active:bg-[#006edb] text-white font-medium text-[15px] transition-all shadow-lg shadow-blue-500/30 hover:shadow-blue-500/40 flex items-center justify-center gap-2 disabled:opacity-70 disabled:cursor-not-allowed"
          >
            <Icon v-if="loading" icon="svg-spinners:180-ring" class="text-xl"/>
            <span v-else>{{ currentStep === 3 ? '完成注册' : '继续' }}</span>
            <Icon v-if="!loading && currentStep !== 3" icon="ion:arrow-forward"/>
          </button>
        </div>

        <!-- 底部链接 -->
        <div v-if="currentStep < 4" class="mt-8 text-center">
          <a href="/Login"
             class="text-sm text-[#0071e3] dark:text-[#2997ff] hover:underline font-medium inline-flex items-center gap-1">
            已有 iMember ID? 立即登录
            <Icon icon="ion:chevron-forward" class="text-xs"/>
          </a>
        </div>

      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {ref, reactive, onMounted} from 'vue'
import {useRouter} from 'vue-router'
import {NInput, NForm, NFormItem, NSelect} from 'naive-ui'
import {Icon} from '@iconify/vue'
import {useAuthorizationStore} from '../../stores/Authorization'
import {ios} from '../../services/AuthService'
import {InfoService} from '../../services/InfoService'
import {isWeiXin, NavigateTo} from "../../lib/site"

const router = useRouter()
const authorizationStore = useAuthorizationStore()
const formRef = ref<InstanceType<typeof NForm>>()

const currentStep = ref(1)
const loading = ref(false)
const errorMsg = ref('')

// 严格类型定义的表单数据
interface RegisterForm {
  name: string;
  studentId: string;
  gender: string | null;
  political: string | null;
  major: string | null;
  className: string;
  phone: string;
  email: string;
  password: string;
  confirmPassword: string;
}

const form = reactive<RegisterForm>({
  name: '',
  studentId: '',
  gender: null,
  political: null,
  major: null,
  className: '',
  phone: '',
  email: '',
  password: '',
  confirmPassword: ''
})

// 选项数据
const genderOptions = [
  {label: '男', value: '男'},
  {label: '女', value: '女'}
]

const politicalOptions = [
  {label: '群众', value: '群众'},
  {label: '共青团员', value: '共青团员'},
  // 与后端 SignRecord.PoliticalLandscapes 保持一致，不能简写成“预备党员”
  {label: '中共预备党员', value: '中共预备党员'},
  {label: '中共党员', value: '中共党员'},
]

// 与后端 StudentCreateDTO 的 DataAnnotations 保持一致
const namePattern = /^[\p{L}· ]{2,20}$/u
const studentIdPattern = /^\d{10}$/
const classNamePattern = /^.{2,20}$/
const phonePattern = /^1[3-9]\d{9}$/

// 学院列表由后端 /Info/academies 提供，保持与 SignRecord.Academies 校验一致
const academyOptions = ref<{ label: string; value: string }[]>([])

// 验证逻辑
const emailValidator = (_rule: any, value: string) => {
  if (!value) return true
  const emailReg = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  if (emailReg.test(value)) return true
  return new Error('请输入有效的邮箱地址')
}

const passwordValidator = (_rule: any, value: string) => {
  if (!value) return true
  // 密码至少8个字符，包含一个大写字母和一个数字
  const passwordReg = /^(?=.*[A-Z])(?=.*\d).{8,}$/
  if (passwordReg.test(value)) return true
  return new Error('密码至少8个字符，包含一个大写字母和一个数字')
}

const validateCurrentStep = async (): Promise<boolean> => {
  errorMsg.value = ''

  // 先去掉首尾空白，避免用户无意输入的空格被当成"非法字符"
  form.name = form.name.trim()
  form.studentId = form.studentId.trim()
  form.className = form.className.trim()
  form.phone = form.phone.trim()
  form.email = form.email.trim()

  // 手动简单检查，提升 UX (直接反馈为空)
  let emptyFields: string[] = []
  if (currentStep.value === 1) {
    if (!form.name) emptyFields.push('姓名')
    if (!form.studentId) emptyFields.push('学号')
    if (!form.gender) emptyFields.push('性别')
    if (!form.political) emptyFields.push('政治面貌')
  } else if (currentStep.value === 2) {
    if (!form.major) emptyFields.push('学院')
    if (!form.className) emptyFields.push('班级')
    if (!form.phone) emptyFields.push('电话')
    if (!form.email) emptyFields.push('邮箱')
  } else if (currentStep.value === 3) {
    if (!form.password) emptyFields.push('密码')
    if (!form.confirmPassword) emptyFields.push('确认密码')
  }

  if (emptyFields.length > 0) {
    errorMsg.value = `请完善：${emptyFields.join(', ')}`
    return false
  }

  // Naive UI 深度检查 (Regex 等)
  try {
    await formRef.value?.validate(
        (errors) => {
          if (!errors) return
          // 过滤掉非当前步骤的错误
          // 这里简单处理：如果 validate 抛出 throw，catch 会捕获
        },
        (_) => {
          // 简单判断 key 是否在当前 step 范围内，这里简化处理，依靠 try catch
          return true
        }
    )
    return true
  } catch (e: any) {
    // 检查错误是否属于当前 View
    // 实际项目中可根据 err.fields 判断
    return false
  }
}

const nextStep = async () => {
  if (loading.value) return;

  const isValid = await validateCurrentStep()
  if (!isValid) return

  if (currentStep.value === 3) {
    await submitRegistration()
  } else {
    currentStep.value++
  }
}

const prevStep = () => {
  if (currentStep.value > 1) {
    currentStep.value--
    errorMsg.value = ''
  }
}

const submitRegistration = async () => {
  loading.value = true
  errorMsg.value = ''

  try {
    await authorizationStore.signup({
      userName: form.name,
      userId: form.studentId,
      academy: form.major || '',
      politicalLandscape: form.political || '',
      gender: form.gender || '',
      className: form.className,
      phoneNum: form.phone,
      // 字段名对应后端 StudentCreateDTO.Password（明文，由后端 BCrypt 哈希）
      password: form.password,
      eMail: form.email
    })

    currentStep.value = 4

    setTimeout(async () => {
      const w = isWeiXin()
      if (w) {
        await router.push('QrCode')
      } else {
        NavigateTo(ios.url1, ios.url2)
      }
    }, 2000)

  } catch (err: any) {
    errorMsg.value = err.message || '注册失败，请连接校园网或稍后再试'
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  try {
    const academies = await InfoService.getAcademies()
    academyOptions.value = academies.map(a => ({label: a, value: a}))
  } catch {
    // 拉不到列表时表单仍有校验兜底，这里只提示，不打断页面渲染
    errorMsg.value = '学院列表加载失败，请刷新页面重试'
  }
})
</script>

<style scoped>
/*
  原生 CSS 覆盖 NaiveUI 样式以实现 Apple 风格
  这里不使用 scope，以便穿透 Naive UI 的 Shadow DOM 结构
*/

/* 背景与字体 */
.apple-bg {
  background-color: #fbfbfd; /* Apple off-white */
  font-family: -apple-system, BlinkMacSystemFont, "SF Pro Text", "Helvetica Neue", sans-serif;
}

/* 暗黑模式背景 */
.dark .apple-bg {
  background-color: #000000;
}

/* 卡片样式 */
.apple-card {
  background: rgba(255, 255, 255, 0.72);
  backdrop-filter: saturate(180%) blur(20px);
  -webkit-backdrop-filter: saturate(180%) blur(20px);
  border-radius: 24px;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.6);
}

.dark .apple-card {
  background: rgba(28, 28, 30, 0.72);
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
}

/* 输入框样式重写 */
.apple-input .n-input,
.apple-select .n-base-selection {
  --n-border: 1px solid #d2d2d7 !important;
  --n-border-hover: 1px solid #86868b !important;
  --n-border-focus: 1px solid #0071e3 !important;
  --n-box-shadow-focus: 0 0 0 4px rgba(0, 113, 227, 0.2) !important;
  background-color: transparent !important; /* 让 inputs 半透明 */
  border-radius: 12px !important;
  min-height: 46px !important;
  font-size: 15px !important;
}

.apple-input .n-input-wrapper,
.apple-select .n-base-selection-label {
  min-height: 46px !important;
  display: flex;
  align-items: center;
}

/* 输入框背景 - 亮色模式 */
.apple-input .n-input,
.apple-select .n-base-selection {
  background-color: rgba(255, 255, 255, 0.8) !important;
}

/* 输入框背景 - 暗黑模式 */
.dark .apple-input .n-input,
.dark .apple-select .n-base-selection {
  --n-border: 1px solid #424245 !important;
  --n-border-hover: 1px solid #6e6e73 !important;
  --n-border-focus: 1px solid #2997ff !important;
  --n-box-shadow-focus: 0 0 0 4px rgba(41, 151, 255, 0.2) !important;
  background-color: rgba(28, 28, 30, 0.6) !important;
  color: white !important;
}

.dark .n-input__input-el,
.dark .n-base-selection-label {
  color: white !important;
}

.dark .n-base-selection-placeholder {
  color: #86868b !important;
}

/* Icon 颜色 */
.input-icon {
  color: #86868b;
  font-size: 20px;
  margin-right: 4px;
}

.dark .input-icon {
  color: #98989d;
}

/* 动画 */
.ease-apple {
  transition-timing-function: cubic-bezier(0.25, 0.1, 0.25, 1);
}

/* Vue Transition: Slide Fade */
.slide-fade-enter-active {
  transition: all 0.3s ease-out;
}

.slide-fade-leave-active {
  transition: all 0.2s cubic-bezier(1, 0.5, 0.8, 1);
}

.slide-fade-enter-from {
  transform: translateX(20px);
  opacity: 0;
}

.slide-fade-leave-to {
  transform: translateX(-20px);
  opacity: 0;
}

/* 自定义关键帧动画 */
@keyframes blob {
  0% {
    transform: translate(0px, 0px) scale(1);
  }
  33% {
    transform: translate(30px, -50px) scale(1.1);
  }
  66% {
    transform: translate(-20px, 20px) scale(0.9);
  }
  100% {
    transform: translate(0px, 0px) scale(1);
  }
}

.animate-blob {
  animation: blob 7s infinite;
}

.animation-delay-2000 {
  animation-delay: 2s;
}

@keyframes shake {
  0%, 100% {
    transform: translateX(0);
  }
  10%, 30%, 50%, 70%, 90% {
    transform: translateX(-4px);
  }
  20%, 40%, 60%, 80% {
    transform: translateX(4px);
  }
}

.animate-shake {
  animation: shake 0.4s cubic-bezier(.36, .07, .19, .97) both;
}

@keyframes scaleIn {
  0% {
    transform: scale(0);
    opacity: 0;
  }
  60% {
    transform: scale(1.2);
    opacity: 1;
  }
  100% {
    transform: scale(1);
  }
}

.animate-scale-in {
  animation: scaleIn 0.5s ease-out forwards;
}
</style>