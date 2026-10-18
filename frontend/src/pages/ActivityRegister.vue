<template>
  <div class="min-h-screen flex items-center justify-center px-4 py-10 bg-gray-50 dark:bg-black">
    <div class="w-full max-w-md">
      <div class="rounded-3xl border border-gray-200 dark:border-white/10 bg-white dark:bg-neutral-900 p-6 sm:p-8 shadow-sm">
        <h1 class="text-xl font-bold text-center text-gray-900 dark:text-white mb-1">活动登记</h1>
        <p class="text-center text-sm text-gray-500 dark:text-gray-400 mb-6">请填写本人真实信息（提交后不可修改）</p>

        <!-- 提交成功 -->
        <div v-if="done" class="text-center py-8">
          <div class="text-green-600 text-lg font-semibold mb-2">✓ 登记成功</div>
          <p class="text-sm text-gray-500">感谢参与，请按活动安排准时到场。</p>
        </div>

        <!-- 通道关闭 -->
        <div v-else-if="closed" class="text-center py-8">
          <div class="text-orange-500 text-lg font-semibold mb-2">登记通道已关闭</div>
          <p class="text-sm text-gray-500">{{ closedReason || '如有疑问请联系活动管理员。' }}</p>
        </div>

        <!-- 表单 -->
        <div v-else class="space-y-3">
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
              :disabled="submitting" @click="submit">
            {{ submitting ? '提交中…' : '提交登记' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ActivityService } from '../services/ActivityService'
import { InfoService } from '../services/InfoService'

const route = useRoute()
const activityId = route.params.id as string

const academies = ref<string[]>([])
const form = ref({ name: '', studentId: '', academy: '', className: '' })
const submitting = ref(false)
const done = ref(false)
const closed = ref(false)
const closedReason = ref('')
const error = ref('')

async function submit() {
  if (!form.value.name.trim() || !form.value.studentId.trim() || !form.value.academy || !form.value.className.trim()) {
    error.value = '请填写完整信息'
    return
  }
  if (!/^\d{10}$/.test(form.value.studentId.trim())) {
    error.value = '学号必须是 10 位数字'
    return
  }
  submitting.value = true
  error.value = ''
  try {
    await ActivityService.register(activityId, {
      name: form.value.name.trim(),
      studentId: form.value.studentId.trim(),
      academy: form.value.academy,
      className: form.value.className.trim()
    })
    done.value = true
  } catch (e: any) {
    const msg = e?.message || '登记失败'
    if (msg.includes('关闭')) { closed.value = true; closedReason.value = msg }
    else error.value = msg
  } finally {
    submitting.value = false
  }
}

onMounted(async () => {
  try {
    academies.value = await InfoService.getAcademies()
    form.value.academy = academies.value[0] || ''
  } catch { /* ignore */ }
})
</script>