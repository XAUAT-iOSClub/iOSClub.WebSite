<template>
  <!-- 页面容器：类 iOS 设置背景 -->
  <div class="min-h-screen bg-[#F2F2F7] dark:bg-[#000000] transition-colors duration-300 pb-20">

    <!-- 内容区域 -->
    <main class="max-w-3xl mx-auto px-4 sm:px-6 pt-12">

      <!-- 骨架屏加载状态 -->
      <div v-if="loading" class="animate-pulse space-y-6">
        <div class="h-32 w-32 bg-gray-300 dark:bg-gray-800 rounded-full mx-auto"></div>
        <div class="h-8 w-48 bg-gray-300 dark:bg-gray-800 rounded mx-auto"></div>
        <div class="bg-white dark:bg-[#1C1C1E] rounded-xl h-64 w-full"></div>
      </div>

      <!-- 真实内容 -->
      <div v-else class="flex flex-col gap-8">

        <!-- 头部：头像与身份 -->
        <div class="flex flex-col items-center space-y-4">
          <div class="relative group">
            <!-- 模拟头像 (Apple ID 风格) -->
            <div
                class="w-28 h-28 rounded-full bg-gradient-to-br from-blue-400 to-blue-600 flex items-center justify-center text-white text-4xl shadow-lg ring-4 ring-white dark:ring-[#1C1C1E] overflow-hidden">
              <span class="font-medium">{{ userInfo.userName ? userInfo.userName[0] : 'U' }}</span>
            </div>
            <!-- 身份徽章 -->
            <div class="absolute bottom-0 right-0 translate-x-1/4 translate-y-1/4">
               <span
                   class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium border-2 border-white dark:border-black shadow-sm"
                   :class="getIdentityBadgeClass(userInfo.identity)">
                {{ identityMap[userInfo.identity] || userInfo.identity || '成员' }}
              </span>
            </div>
          </div>

          <div class="text-center">
            <h1 class="text-2xl font-bold text-gray-900 dark:text-white tracking-tight">
              {{ userInfo.userName || '未命名' }}
            </h1>
            <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">
              {{ userInfo.userId || '无学号' }} · {{ userInfo.academy || '未选择学院' }}
            </p>
          </div>
        </div>

        <!-- 表单区域 -->
        <n-form
            :model="userInfo"
            :rules="rules"
            ref="formRef"
            :show-label="false"
            class="space-y-6"
        >

          <!-- 分组 1: 基本资料 -->
          <section>
            <h3 class="pl-4 mb-2 text-xs font-semibold text-gray-500 uppercase tracking-wider">
              基本资料
            </h3>
            <div
                class="bg-white dark:bg-[#1C1C1E] rounded-xl overflow-hidden shadow-sm border border-gray-200/50 dark:border-gray-800">

              <!-- 姓名 -->
              <div class="group-row">
                <div class="row-label">
                  <Icon icon="lucide:user" class="text-blue-500 mr-2"/>
                  姓名
                </div>
                <div class="row-content">
                  <n-form-item path="userName" class="w-full !m-0 !p-0">
                    <n-input
                        v-model:value="userInfo.userName"
                        placeholder="你的名字"
                        class="apple-input"
                        :bordered="false"
                    />
                  </n-form-item>
                </div>
              </div>
              <div class="separator"></div>

              <!-- 性别 -->
              <div class="group-row">
                <div class="row-label">
                  <Icon icon="lucide:users" class="text-blue-500 mr-2"/>
                  性别
                </div>
                <div class="row-content justify-end">
                  <n-form-item path="gender" class="!m-0 !p-0">
                    <div class="flex bg-gray-100 dark:bg-gray-800 rounded-lg p-0.5">
                      <button
                          v-for="g in genderOptions"
                          :key="g"
                          type="button"
                          @click="userInfo.gender = g"
                          class="px-4 py-1.5 text-sm rounded-md transition-all duration-200"
                          :class="userInfo.gender === g
                           ? 'bg-white dark:bg-gray-600 text-black dark:text-white shadow-sm'
                           : 'text-gray-500 hover:text-gray-700 dark:text-gray-400'"
                      >
                        {{ g }}
                      </button>
                    </div>
                  </n-form-item>
                </div>
              </div>
              <div class="separator"></div>

              <!-- 政治面貌 -->
              <div class="group-row">
                <div class="row-label">
                  <Icon icon="lucide:flag" class="text-blue-500 mr-2"/>
                  政治面貌
                </div>
                <div class="row-content">
                  <n-form-item path="politicalLandscape" class="w-full !m-0 !p-0">
                    <n-select
                        v-model:value="userInfo.politicalLandscape"
                        :options="politicalLandscapeOptions"
                        placeholder="选择面貌"
                        class="apple-select"
                        :bordered="false"
                    />
                  </n-form-item>
                </div>
              </div>
            </div>
            <p class="pl-4 mt-2 text-xs text-gray-400">请保持您的基本信息真实有效。</p>
          </section>

          <!-- 分组 2: 学籍信息 -->
          <section>
            <h3 class="pl-4 mb-2 text-xs font-semibold text-gray-500 uppercase tracking-wider">
              学籍信息
            </h3>
            <div
                class="bg-white dark:bg-[#1C1C1E] rounded-xl overflow-hidden shadow-sm border border-gray-200/50 dark:border-gray-800">

              <!-- 学号 -->
              <div class="group-row">
                <div class="row-label">
                  <Icon icon="iconoir:hashtag" class="text-orange-500 mr-2"/>
                  学号
                </div>
                <div class="row-content">
                  <n-form-item path="userId" class="w-full !m-0 !p-0">
                    <n-input
                        v-model:value="userInfo.userId"
                        placeholder="10位学号"
                        class="apple-input"
                        :bordered="false"
                        :disabled="true"
                    />
                  </n-form-item>
                  <Icon icon="lucide:lock" class="text-gray-400 ml-2 w-4 h-4"/>
                </div>
              </div>
              <div class="separator"></div>

              <!-- 学院 -->
              <div class="group-row">
                <div class="row-label">
                  <Icon icon="iconoir:graduation-cap" class="text-orange-500 mr-2"/>
                  学院
                </div>
                <div class="row-content">
                  <n-form-item path="academy" class="w-full !m-0 !p-0">
                    <n-select
                        v-model:value="userInfo.academy"
                        :options="academyOptions"
                        placeholder="所在学院"
                        filterable
                        class="apple-select"
                        :bordered="false"
                    />
                  </n-form-item>
                </div>
              </div>
              <div class="separator"></div>

              <!-- 班级 -->
              <div class="group-row">
                <div class="row-label">
                  <Icon icon="lucide:book-open" class="text-orange-500 mr-2"/>
                  班级
                </div>
                <div class="row-content">
                  <n-form-item path="className" class="w-full !m-0 !p-0">
                    <n-input
                        v-model:value="userInfo.className"
                        placeholder="例：计科2101"
                        class="apple-input"
                        :bordered="false"
                    />
                  </n-form-item>
                </div>
              </div>

            </div>
          </section>

          <!-- 分组 3: 联系方式 -->
          <section>
            <h3 class="pl-4 mb-2 text-xs font-semibold text-gray-500 uppercase tracking-wider">
              联系方式
            </h3>
            <div
                class="bg-white dark:bg-[#1C1C1E] rounded-xl overflow-hidden shadow-sm border border-gray-200/50 dark:border-gray-800">

              <!-- 手机 -->
              <div class="group-row">
                <div class="row-label">
                  <Icon icon="lucide:phone" class="text-green-500 mr-2"/>
                  手机
                </div>
                <div class="row-content">
                  <n-form-item path="phoneNum" class="w-full !m-0 !p-0">
                    <n-input
                        v-model:value="userInfo.phoneNum"
                        placeholder="联系电话"
                        class="apple-input"
                        :bordered="false"
                    />
                  </n-form-item>
                </div>
              </div>
              <div class="separator"></div>

              <!-- 邮箱 -->
              <div class="group-row">
                <div class="row-label">
                  <Icon icon="lucide:mail" class="text-green-500 mr-2"/>
                  邮箱
                </div>
                <div class="row-content">
                  <n-form-item path="eMail" class="w-full !m-0 !p-0">
                    <n-input
                        v-model:value="userInfo.eMail"
                        placeholder="电子邮箱"
                        class="apple-input"
                        :bordered="false"
                    />
                  </n-form-item>
                </div>
              </div>

            </div>
          </section>

          <!-- 操作按钮组 -->
          <div class="pt-4 space-y-3">
            <button
                type="button"
                @click="handleSubmit"
                :disabled="confirmLoading"
                class="w-full py-3.5 bg-[#007AFF] hover:bg-[#0062cc] active:scale-[0.98] text-white font-medium rounded-xl shadow-sm transition-all flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <span v-if="!confirmLoading">保存更改</span>
              <span v-else>保存中...</span>
            </button>

            <button
                type="button"
                @click="handlePasswordSubmit"
                class="w-full py-3.5 bg-white dark:bg-[#1C1C1E] text-[#007AFF] border border-transparent hover:bg-gray-50 dark:hover:bg-gray-800 active:scale-[0.98] font-medium rounded-xl transition-all flex items-center justify-center gap-2"
            >
              修改密码
            </button>
          </div>

        </n-form>
      </div>
    </main>

    <!-- 确认对话框 - iOS Style -->
    <n-modal v-model:show="showModal" preset="dialog" title="确认更改" positive-text="更新资料" negative-text="取消"
             @positive-click="handleConfirm" :loading="confirmLoading"
             :style="{ width: '90%', maxWidth: '400px', borderRadius: '16px' }">
      <div class="py-2">
        <p class="text-gray-600 dark:text-gray-300">您确定要更新个人资料吗？请确保所有信息准确无误。</p>
      </div>
    </n-modal>

    <!-- 密码修改对话框 -->
    <n-modal v-model:show="showPasswordModal" preset="card" title="修改密码"
             :style="{ width: '90%', maxWidth: '450px', borderRadius: '16px' }">
      <div class="space-y-4 pt-2">
        <n-form :model="passwordForm" :rules="passwordRules" ref="passwordFormRef">
          <div class="bg-gray-50 dark:bg-black/20 rounded-xl p-1 space-y-3">
            <n-form-item path="oldPassword" label="旧密码" :show-label="false">
              <n-input
                  v-model:value="passwordForm.oldPassword"
                  type="password"
                  show-password-on="click"
                  placeholder="当前使用的密码"
                  class="rounded-lg"
              />
            </n-form-item>
            <n-form-item path="newPassword" label="新密码" :show-label="false">
              <n-input
                  v-model:value="passwordForm.newPassword"
                  type="password"
                  show-password-on="click"
                  placeholder="至少 6 位字符"
                  class="rounded-lg"
              />
            </n-form-item>
            <n-form-item path="confirmPassword" label="确认密码" :show-label="false">
              <n-input
                  v-model:value="passwordForm.confirmPassword"
                  type="password"
                  show-password-on="click"
                  placeholder="再次输入新密码"
                  class="rounded-lg"
              />
            </n-form-item>
          </div>
        </n-form>
        <div class="flex justify-end gap-3 mt-4">
          <button @click="showPasswordModal = false" class="px-4 py-2 text-gray-500">取消</button>
          <button @click="confirmPasswordChange"
                  class="px-6 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600 transition-colors">
            确认修改
          </button>
        </div>
      </div>
    </n-modal>
  </div>
</template>

<script setup lang="ts">
import {ref, reactive, onMounted, onBeforeUnmount} from 'vue';
import {useMessage, NForm, NFormItem, NInput, NSelect, NModal} from 'naive-ui';
import {Icon} from '@iconify/vue';
import {useAuthorizationStore} from '../stores/Authorization';
import {UserService} from '../services/UserService';
import {AuthService} from '../services/AuthService';
import {InfoService} from '../services/InfoService';
import type {MemberVO, StudentUpdateDTO} from '../models';
import {useLayoutStore} from '../stores/LayoutStore';

const message = useMessage();
const authorizationStore = useAuthorizationStore();
const layoutStore = useLayoutStore();

const loading = ref(false);
const confirmLoading = ref(false);
const showModal = ref(false);
const formRef = ref<InstanceType<typeof NForm> | null>(null);

// 身份映射
const identityMap: Record<string, string> = {
  'Founder': '创始人',
  'President': '社长',
  'Minister': '部长',
  'Department': '部员',
  'Member': '成员'
};

const getIdentityBadgeClass = (identity: string) => {
  switch (identity) {
    case 'Founder':
      return 'bg-amber-500 text-white';
    case 'President':
      return 'bg-blue-500 text-white';
    case 'Minister':
      return 'bg-green-500 text-white';
    default:
      return 'bg-gray-500 text-white';
  }
};

const genderOptions = ['男', '女'];
const politicalLandscapeOptions = ['群众', '共青团员', '中共党员', '中共预备党员'].map(i => ({label: i, value: i}));
const academyOptions = ref<{ label: string; value: string }[]>([]);

const fetchAcademyOptions = async () => {
  try {
    const academies = await InfoService.getAcademies();
    academyOptions.value = academies.map(a => ({label: a, value: a}));
  } catch (error: any) {
    message.error(error.message || '获取学院列表失败');
  }
};

// 密码相关
const showPasswordModal = ref(false);
const passwordLoading = ref(false);
const passwordFormRef = ref<InstanceType<typeof NForm> | null>(null);
const passwordForm = reactive({oldPassword: '', newPassword: '', confirmPassword: ''});

const passwordRules = {
  oldPassword: {required: true, message: '请输入旧密码', trigger: 'blur'},
  newPassword: [{required: true, message: '请输入新密码', trigger: 'blur'}, {
    min: 6,
    message: '至少6位',
    trigger: 'blur'
  }],
  confirmPassword: [
    {required: true, message: '请确认', trigger: 'blur'},
    {validator: (_: any, v: string) => v === passwordForm.newPassword, message: '密码不一致', trigger: 'blur'}
  ]
};

const userInfo = reactive<MemberVO>({
  userName: '', gender: '男', userId: '', academy: '', politicalLandscape: '群众',
  className: '', phoneNum: '', identity: '', joinTime: '', eMail: null
});

// 与后端 StudentUpdateDTO 的 DataAnnotations 保持一致
const namePattern = /^[\p{L}· ]{2,20}$/u
const studentIdPattern = /^\d{10}$/
const classNamePattern = /^.{2,20}$/
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

// 邮箱非必填：空值放行，只在填了内容时校验格式
const emailValidator = (_rule: any, value: string | null) => {
  if (!value) return true
  return emailPattern.test(value) || new Error('邮箱格式错误')
}

const rules = {
  userName: [
    {required: true, message: '姓名必填', trigger: 'blur'},
    {pattern: namePattern, message: '姓名应为2-20个汉字或字母，不能包含数字或特殊符号', trigger: 'blur'}
  ],
  userId: [
    {required: true, message: '学号必填', trigger: 'blur'},
    {pattern: studentIdPattern, message: '学号必须是10位数字', trigger: 'blur'}
  ],
  academy: {required: true, message: '必选', trigger: 'blur'},
  className: [
    {required: true, message: '必填', trigger: 'blur'},
    {pattern: classNamePattern, message: '班级名称长度需为2-20个字符', trigger: 'blur'}
  ],
  phoneNum: [{required: true, pattern: /^1[3-9]\d{9}$/, message: '手机号格式错误', trigger: 'blur'}],
  eMail: [{validator: emailValidator, trigger: 'blur'}]
};

const fetchUserInfo = async () => {
  try {
    loading.value = true;
    const data = await UserService.getUserData();
    Object.assign(userInfo, data);
  } catch (error: any) {
    message.error(error.message || '获取失败');
    if (error.message?.includes('认证')) await authorizationStore.logout();
  } finally {
    loading.value = false;
  }
};

const handleSubmit = () => {
  formRef.value?.validate((errors) => {
    if (!errors) showModal.value = true;
    else message.error('请检查红色标记的字段');
  });
};

const handleConfirm = async () => {
  try {
    confirmLoading.value = true;
    // 只提交后端 StudentUpdateDTO 接受的字段，避免把 identity/joinTime 一起发过去。
    const payload: StudentUpdateDTO = {
      userId: userInfo.userId,
      userName: userInfo.userName,
      academy: userInfo.academy,
      politicalLandscape: userInfo.politicalLandscape,
      gender: userInfo.gender,
      className: userInfo.className,
      phoneNum: userInfo.phoneNum,
      eMail: userInfo.eMail
    };
    await UserService.updateProfile(payload);
    message.success('已更新');
    showModal.value = false;
  } catch (error: any) {
    message.error(error.message || '更新失败');
  } finally {
    confirmLoading.value = false;
  }
};

const handlePasswordSubmit = () => showPasswordModal.value = true;

const confirmPasswordChange = async () => {
  passwordFormRef.value?.validate(async (errors) => {
    if (!errors) {
      try {
        passwordLoading.value = true;
        await AuthService.changePassword(userInfo.userId, passwordForm.oldPassword, passwordForm.newPassword);
        message.success('密码已修改');
        showPasswordModal.value = false;
        passwordForm.oldPassword = '';
        passwordForm.newPassword = '';
        passwordForm.confirmPassword = '';
      } catch (e: any) {
        message.error(e.message || '修改失败');
      } finally {
        passwordLoading.value = false;
      }
    }
  });
};

onMounted(() => {
  fetchUserInfo();
  fetchAcademyOptions();
  layoutStore.setPageHeader('个人信息', '管理您的个人资料');
  layoutStore.setShowPageActions(false);
});

onBeforeUnmount(() => {
  layoutStore.clearPageHeader();
});
</script>

<style scoped>

/* 核心布局样式 - Apple Settings List Style */
.group-row {
  display: flex;
  padding: 0.75rem 1rem;
  min-height: 50px;
  transition: colors 0.2s ease;
  position: relative;
  flex-direction: row;
  align-items: center;
}

/* 标签样式 */
.row-label {
  font-size: 1rem;
  font-weight: 500;
  color: #111827;
  display: flex;
  align-items: center;
  min-width: 120px;
  margin-bottom: 0.25rem;
}

.dark .row-label {
  color: #ffffff;
}

@media (min-width: 640px) {
  .row-label {
    margin-bottom: 0;
  }
}

/* 内容区域样式 */
.row-content {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  color: #6b7280;
}

.dark .row-content {
  color: #9ca3af;
}

/* 分割线 - 只有中间有，最后一行没有 */
.separator {
  border-bottom: 1px solid #e5e7eb;
  height: 1px;
  width: 100%;
  margin-left: 1rem;
}

.dark .separator {
  border-color: #1f2937;
}

.separator:last-child {
  display: none;
}

/* 定制 Naive UI Input 以适应 iOS 风格 */
:deep(.apple-input .n-input__input-el),
:deep(.apple-input .n-input__textarea-el) {
  text-align: right !important;
  font-size: 1rem !important;
  color: #2563eb !important;
  caret-color: #3b82f6 !important;
}

:deep(.apple-input .n-input__input-el).dark,
:deep(.apple-input .n-input__textarea-el).dark {
  color: #60a5fa !important;
}

/* 占位符样式 */
:deep(.apple-input .n-input__placeholder) {
  text-align: right !important;
  color: #9ca3af !important;
}

/* Select 样式定制 */
:deep(.apple-select .n-base-selection-label) {
  background-color: transparent !important;
  text-align: right !important;
  justify-content: flex-end !important;
}

:deep(.apple-select .n-base-selection-input__content) {
  text-align: right !important;
  color: #2563eb !important;
  padding-right: 1.5rem !important;
}

:deep(.apple-select .n-base-selection-input__content).dark {
  color: #60a5fa !important;
}

/* 消除输入框默认背景和焦点边框 */
:deep(.n-input), :deep(.n-input:hover), :deep(.n-input:focus) {
  background-color: transparent !important;
}

:deep(.n-input .n-input__state-border), :deep(.n-input .n-input__border) {
  display: none !important;
}

/* 消除 Select 默认背景 */
:deep(.n-base-selection) {
  --n-border: none !important;
  --n-box-shadow-focus: none !important;
  --n-box-shadow-active: none !important;
  --n-box-shadow-hover: none !important;
  background-color: transparent !important;
}

/* 修复 Naive Form Item 的默认间距，使其紧凑 */
:deep(.n-form-item-feedback-wrapper) {
  position: absolute;
  right: 0;
  top: 100%;
  padding-top: 2px;
  font-size: 12px;
  text-align: right;
  z-index: 10;
}
</style>