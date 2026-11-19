<template>
  <div class="min-h-screen flex items-center justify-center px-4 py-10 bg-gray-50 dark:bg-black">
    <div class="w-full max-w-md">
      <div class="rounded-3xl border border-gray-200 dark:border-white/10 bg-white dark:bg-neutral-900 p-6 sm:p-8 shadow-sm">
        <h1 class="text-xl font-bold text-center text-gray-900 dark:text-white mb-1">活动登记</h1>

        <p v-if="state.kind === 'checking'" class="text-center py-8 text-sm text-gray-500" role="status">
          正在检查登记状态…
        </p>

        <div v-else-if="state.kind === 'failed'" class="text-center py-8">
          <p class="text-sm text-gray-500" role="alert">{{ state.message }}</p>
          <button type="button" class="mt-3 text-sm font-medium text-blue-600 dark:text-blue-400" @click="checkRegistration">
            重新检查
          </button>
        </div>

        <!-- 提交成功 -->
        <div v-else-if="state.kind === 'done'" class="text-center py-8">
          <div class="text-green-600 text-lg font-semibold mb-2">✓ 登记成功</div>
          <p class="text-sm text-gray-500">感谢参与，请按活动安排准时到场。</p>
        </div>

        <!-- 通道关闭 -->
        <div v-else-if="state.kind === 'closed'" class="text-center py-8">
          <p class="text-orange-500 text-lg font-semibold" role="status">{{ state.message }}</p>
        </div>

        <!-- 表单 -->
        <div v-else-if="state.kind === 'open' || state.kind === 'submitting'" class="space-y-3">
          <p class="text-center text-sm text-gray-500 dark:text-gray-400 mb-6">请填写本人真实信息（提交后不可修改）</p>
          <div>
            <label class="block text-sm text-gray-500 mb-1">姓名 *</label>
            <input v-model="form.name" maxlength="20" class="w-full px-3 py-2.5 rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 text-sm outline-none" />
          </div>
          <div>
            <label class="block text-sm text-gray-500 mb-1">学号 *（10 位）</label>
            <input v-model="form.studentId" maxlength="10" inputmode="numeric" class="w-full px-3 py-2.5 rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 text-sm outline-none" />
          </div>
          <div>
            <label class="block text-sm text-gray-500 mb-1">学院 *</label>
            <select v-model="form.academy" class="w-full px-3 py-2.5 rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 text-sm outline-none">
              <option v-for="a in academies" :key="a" :value="a">{{ a }}</option>
            </select>
          </div>
          <div>
            <label class="block text-sm text-gray-500 mb-1">班级 *</label>
            <input v-model="form.className" maxlength="30" class="w-full px-3 py-2.5 rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 text-sm outline-none" />
          </div>

          <p v-if="error" class="text-sm text-red-500">{{ error }}</p>

          <button class="w-full py-3 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-medium disabled:opacity-50"
              :disabled="state.kind === 'submitting'" @click="submit">
            {{ state.kind === 'submitting' ? '提交中…' : '提交登记' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, shallowRef, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ErrorCode } from '../constants/ErrorCode'
import { ActivityService } from '../services/ActivityService'
import { ApiError } from '../services/ApiService'
import { InfoService } from '../services/InfoService'

const route = useRoute()
type RegistrationState =
  | { kind: 'checking' | 'open' | 'submitting' | 'done' }
  | { kind: 'closed' | 'failed'; message: string }

const academies = ref<string[]>([])
const form = ref({ name: '', studentId: '', academy: '', className: '' })
const state = shallowRef<RegistrationState>({ kind: 'checking' })
const error = ref('')

function isRegistrationClosed(error: unknown): error is ApiError {
  return error instanceof ApiError &&
    (error.errorCode === ErrorCode.InvalidStatusForOperation || error.errorCode === ErrorCode.ResourceNotFound)
}

async function checkRegistration() {
  const activityId = route.params.id
  const checking: RegistrationState = { kind: 'checking' }
  state.value = checking

  if (typeof activityId !== 'string') {
    state.value = { kind: 'closed', message: '活动不存在' }
    return
  }

  try {
    await ActivityService.checkRegistration(activityId)
  } catch (error) {
    if (state.value !== checking) return
    state.value = isRegistrationClosed(error)
      ? { kind: 'closed', message: error.message }
      : { kind: 'failed', message: '暂时无法检查登记状态，请重试。' }
    return
  }

  if (state.value !== checking) return
  try {
    const options = await InfoService.getAcademies()
    if (state.value !== checking) return
    academies.value = options
    form.value.academy = options[0] || ''
    state.value = { kind: 'open' }
  } catch {
    if (state.value === checking) {
      state.value = { kind: 'failed', message: '暂时无法加载学院列表，请重试。' }
    }
  }
}

async function submit() {
  const activityId = route.params.id
  if (state.value.kind !== 'open' || typeof activityId !== 'string') return
  if (!form.value.name.trim() || !form.value.studentId.trim() || !form.value.academy || !form.value.className.trim()) {
    error.value = '请填写完整信息'
    return
  }
  if (!/^\d{10}$/.test(form.value.studentId.trim())) {
    error.value = '学号必须是 10 位数字'
    return
  }
  if (!/^[\p{L}· ]{2,20}$/u.test(form.value.name.trim())) {
    error.value = '姓名应为2-20个汉字或字母，不能包含数字或特殊符号'
    return
  }
  if (form.value.className.trim().length < 2 || form.value.className.trim().length > 30) {
    error.value = '班级名称长度需为2-30个字符'
    return
  }
  const submitting: RegistrationState = { kind: 'submitting' }
  state.value = submitting
  error.value = ''
  try {
    await ActivityService.register(activityId, {
      name: form.value.name.trim(),
      studentId: form.value.studentId.trim(),
      academy: form.value.academy,
      className: form.value.className.trim()
    })
    if (state.value === submitting) state.value = { kind: 'done' }
  } catch (failure) {
    if (state.value !== submitting) return
    if (isRegistrationClosed(failure)) {
      state.value = { kind: 'closed', message: failure.message }
    } else {
      error.value = failure instanceof Error ? failure.message : '登记失败'
      state.value = { kind: 'open' }
    }
  }
}

watch(() => route.params.id, () => {
  form.value = { name: '', studentId: '', academy: '', className: '' }
  error.value = ''
  void checkRegistration()
}, { immediate: true })
</script>
