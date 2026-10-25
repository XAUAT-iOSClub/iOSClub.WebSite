<script setup lang="ts">
import {ref, computed} from 'vue'
import Icon from '@/components/Icon.vue'
import {NCollapse, NCollapseItem} from 'naive-ui'

// --- 类型定义 ---
interface Category {
  id: string
  title: string
  icon: string
  desc: string
}

interface FaqItem {
  id: string
  question: string
  answer: string
  category: string
}

// --- 数据 Mock ---
const categories = ref<Category[]>([
  {id: 'start', title: '快速开始', icon: 'rocket', desc: '新手指南与入门教程'},
  {id: 'account', title: '账户管理', icon: 'user', desc: '设置、安全与隐私'},
  {id: 'join', title: '加入我们', icon: 'users', desc: '招新信息与会员权益'},
  {id: 'tech', title: '技术支持', icon: 'hammer', desc: '技术问题与开发指导'},
  {id: 'community', title: '社区活动', icon: 'messages-square', desc: '活动交流与项目协作'},
  {id: 'contact', title: '联系我们', icon: 'mail', desc: '获取人工帮助'},
])

const faqs = ref<FaqItem[]>([
  {
    id: '1',
    category: 'start',
    question: '如何重置我的账户密码？',
    answer: '您可以在登录页面点击“忘记密码”，或者进入设置页面中的“安全”选项卡进行修改。我们会向您的注册邮箱发送一封验证邮件。'
  },
  {
    id: '2',
    category: 'join',
    question: '如何加入西安建筑科技大学 iOS Club？',
    answer: '欢迎加入西安建筑科技大学 iOS Club！您可以通过以下方式加入：1. 在网站注册账号并完善个人信息；2. 关注我们的招新公告，参加招新面试；3. 通过现有会员推荐。俱乐部常年开放招新，具体时间请关注网站公告。'
  },
  {
    id: '3',
    category: 'account',
    question: '如何更新我的个人资料信息？',
    answer: '登录后，点击右上角头像进入个人中心，在"个人资料"页面可以修改昵称、联系方式等信息。如需修改绑定邮箱或密码，请在"账户安全"页面操作。'
  },
  {
    id: '4',
    category: 'tech',
    question: '俱乐部提供哪些iOS开发学习资源？',
    answer: '我们提供丰富的学习资源：1. Swift/Objective-C基础教程；2. iOS开发框架学习资料；3. 苹果官方文档翻译；4. WWDC视频解析；5. 开源项目分析；6. 设计规范指南。所有资源均在俱乐部资源库中，会员可免费获取。'
  },
  {
    id: '5',
    category: 'join',
    question: '加入俱乐部有什么要求？',
    answer: '我们欢迎所有对编程数码感兴趣的同学加入，无论你是编程新手还是经验丰富的开发者。基本要求包括：1. 对编程开发或者数码设备有兴趣；2. 有学习热情和团队合作精神；3. 遵守俱乐部规章制度。'
  },
  {
    id: '6',
    category: 'community',
    question: '俱乐部都有哪些活动？',
    answer: '西安建筑科技大学 iOS Club 定期举办多种活动：1. 技术分享会：每周定期举行，分享最新技术动态；2. 项目工作坊：每月1-2次，实际动手开发项目；3. 技术讲座：邀请业界专家进行专题讲座；4. 编程马拉松：每年举办，激发创新思维。'
  },
  {
    id: '7',
    category: 'contact',
    question: '如何联系西安建筑科技大学 iOS Club？',
    answer: '您可以通过以下方式联系我们：1. 发送邮件至 iosclubxauat@163.com；2. 关注我们的官方微信公众号 "西建大iOS Club"；3. 加入官方QQ群。'
  },
  {
    id: '8',
    category: 'start',
    question: '我是编程新手，该如何开始学习iOS开发？',
    answer: '对于编程新手，我们建议：1. 先学习Swift基础语法；2. 了解Xcode开发环境；3. 完成简单的UI界面练习；4. 参加俱乐部的新手训练营；5. 跟着教程制作第一个App。俱乐部有完整的学习路线图和导师指导，不用担心起点低。'
  },
  {
    id: '9',
    category: 'account',
    question: '忘记密码了怎么办？',
    answer: '如果您忘记了密码：1. 在登录页面点击"忘记密码"；2. 输入注册时使用的邮箱；3. 查收邮件中的密码重置链接；4. 设置新密码。如果邮箱也忘记了，请联系管理员验证身份后重置。'
  },
])

// --- 逻辑 ---
const searchQuery = ref('')

// 简单的搜索过滤逻辑
const filteredFaqs = computed(() => {
  if (!searchQuery.value) return faqs.value
  const query = searchQuery.value.toLowerCase()
  return faqs.value.filter(item =>
      item.question.toLowerCase().includes(query) ||
      item.answer.toLowerCase().includes(query)
  )
})
</script>

<template>
  <!--
    整体容器
    Apple 风格背景: 亮色倾向于非常浅的灰色(#F5F5F7)，暗色倾向于深层黑(#000000)或极深灰
  -->
  <div
      class="min-h-screen bg-[#F5F5F7] dark:bg-black text-[#1d1d1f] dark:text-[#f5f5f7] transition-colors duration-300 font-sans pb-20">

    <!-- Hero Section -->
    <div class="relative pt-24 pb-16 px-6 flex flex-col items-center justify-center text-center">
      <h1 class="text-4xl md:text-5xl font-bold tracking-tight mb-4 text-[#1d1d1f] dark:text-white">
        嗨，我们可以帮您什么？
      </h1>
      <p class="text-lg text-gray-500 dark:text-gray-400 mb-10 max-w-2xl">
        搜索热门话题，浏览分类，或者查找常见问题解答。
      </p>

      <!-- 搜索框 (Spotlight Style) -->
      <div class="w-full max-w-2xl relative group">
        <div class="absolute inset-y-0 left-4 flex items-center pointer-events-none z-10">
          <Icon icon="search"
                class="text-gray-400 text-xl group-focus-within:text-[#0071e3] transition-colors"/>
        </div>
        <!-- 使用原生 input 实现更精细的 Apple 风格控制，也可以用 Naive NInput -->
        <input
            v-model="searchQuery"
            type="text"
            placeholder="搜索问题、话题或关键词..."
            class="w-full py-4 pl-12 pr-4 bg-white dark:bg-[#1c1c1e]
                 border border-gray-300/50 dark:border-white/10
                 rounded-2xl shadow-sm
                 text-lg placeholder:text-gray-400
                 focus:outline-none focus:ring-4 focus:ring-[#0071e3]/20 focus:border-[#0071e3]
                 transition-all duration-300"
        />
      </div>
    </div>

    <!-- 主要内容区域 -->
    <div class="container mx-auto px-6">

      <!-- 分类网格 (Bento Grid 风格) -->
      <div v-if="!searchQuery" class="mb-16">
        <h2 class="text-2xl font-semibold mb-6 px-1">浏览分类</h2>
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
          <div
              v-for="cat in categories"
              :key="cat.id"
              class="group cursor-pointer p-6 rounded-2xl bg-white dark:bg-[#1c1c1e]
                   border border-transparent dark:border-white/5
                   shadow-sm hover:shadow-xl hover:scale-[1.02]
                   transition-all duration-300 ease-out"
          >
            <!--Icon Container-->
            <div
                class="w-12 h-12 rounded-xl bg-gray-100 dark:bg-gray-700/50 flex items-center justify-center mb-4 text-[#0071e3] transition-colors group-hover:bg-[#0071e3] group-hover:text-white">
              <Icon :icon="cat.icon" width="24" height="24"/>
            </div>
            <h3 class="text-lg font-semibold mb-1 group-hover:text-[#0071e3] transition-colors">
              {{ cat.title }}
            </h3>
            <p class="text-sm text-gray-500 dark:text-gray-400 leading-relaxed">
              {{ cat.desc }}
            </p>
          </div>
        </div>
      </div>

      <!-- FAQ 区域 -->
      <div class="mx-auto">
        <h2 class="text-2xl font-semibold mb-6 px-1 flex items-center gap-2">
          <Icon v-if="searchQuery" icon="search"/>
          {{ searchQuery ? '搜索结果' : '常见问题' }}
        </h2>

        <!-- Custom Container for List (Replacing NCard) -->
        <div
            class="bg-white dark:bg-[#1c1c1e] rounded-2xl shadow-sm border border-gray-200/50 dark:border-white/10 overflow-hidden">
          <n-collapse
              accordion
              arrow-placement="right"
              display-directive="show"
          >
            <!-- 无结果状态 -->
            <div v-if="filteredFaqs.length === 0" class="p-8 text-center text-gray-500">
              <Icon icon="file-text" class="text-4xl mx-auto mb-2 opacity-30"/>
              <p>没有找到与 "{{ searchQuery }}" 相关的内容</p>
            </div>

            <n-collapse-item
                v-for="faq in filteredFaqs"
                :key="faq.id"
                :title="faq.question"
                :name="faq.id"
                class="group border-b border-gray-100 dark:border-white/5 last:border-0 px-6 py-4 hover:bg-gray-50 dark:hover:bg-white/5 transition-colors mt-0!"
            >
              <template #header>
                <span class="text-base font-medium text-[#1d1d1f] dark:text-gray-200">
                  {{ faq.question }}
                </span>
              </template>

              <!-- 内容区域 -->
              <div class="text-base text-gray-600 dark:text-gray-300 leading-relaxed pt-2 pb-2">
                {{ faq.answer }}
              </div>
            </n-collapse-item>
          </n-collapse>
        </div>
      </div>

      <!-- 底部联系 (Call to Action) -->
      <div class="mt-20 text-center">
        <h3 class="text-xl font-semibold mb-2">找不到答案？</h3>
        <p class="text-gray-500 mb-6">我们的支持团队随时准备为您服务。</p>
        <button
            class="bg-[#0071e3] hover:bg-[#0077ED] text-white px-6 py-3 rounded-full font-medium transition-all hover:shadow-lg active:scale-95 flex items-center justify-center gap-2 mx-auto">
          <Icon icon="message-circle-more"/>
          联系支持团队
        </button>
      </div>

    </div>
  </div>
</template>

<style scoped>
/*
  样式微调：为了适配 Naive UI 的 Collapse 组件到 Apple 风格
  我们在 scoped 中覆盖一些 naive ui 的内部细节，使其更极简。
*/

:deep(.n-collapse .n-collapse-item .n-collapse-item__header) {
  padding-top: 0 !important;
  padding-bottom: 0 !important;
}

:deep(.n-collapse .n-collapse-item__content-inner) {
  padding-top: 0 !important;
}

/* 去除 Naive 默认的 focus 线条，保持 clean */
:deep(.n-collapse-item__header:focus) {
  outline: none !important;
}
</style>