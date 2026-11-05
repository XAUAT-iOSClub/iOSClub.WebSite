<script setup lang="ts">
import {ref} from 'vue'
import {NImage, NModal} from 'naive-ui'
import Icon from '@/components/Icon.vue'

// 保持原有图片引用路径（这些实际上是二维码）
import xiaomiQr from '/assets/other/xiaomi.jpg'
import huaweiQr from '/assets/other/huawei.jpg'
import aircraftQr from '/assets/other/aircraft.jpg'

// --- 接口定义 ---
interface Organization {
  id: string
  title: string
  subtitle?: string
  qrCode: string      // 图片改名为 qrCode
  description: string[]
  themeColor: string
  icon: string
  bgGradient: string  // 新增背景渐变色
}

interface OffCampusOrg {
  name: string
  url: string
  icon: string
  category: string
  desc: string
}

// --- 数据 ---
const organizations = ref<Organization[]>([
  {
    id: 'xiaomi',
    title: '米粉俱乐部',
    subtitle: '探索科技无限可能',
    qrCode: xiaomiQr,
    themeColor: '#ff6900',
    icon: 'brand-xiaomi',
    bgGradient: 'from-orange-500/10 to-orange-600/5',
    description: [
      '欢迎来到西建大米粉俱乐部。这是一个充满创新和科技热情的社区。',
      '参与最前沿的科技讨论，体验最新的小米产品，甚至参与产品测试和反馈。'
    ]
  },
  {
    id: 'huawei',
    title: '花粉俱乐部',
    subtitle: '构建万物互联的智能世界',
    qrCode: huaweiQr,
    themeColor: '#cf0a2c',
    icon: 'brand-huawei',
    bgGradient: 'from-red-600/10 to-red-700/5',
    description: [
      '西建大花粉俱乐部是由华为公司创办的科技社团。',
      '诚挚邀请每一位对科技充满热情的朋友加入，与志同道合的伙伴交流思想。'
    ]
  },
  {
    id: 'aircraft',
    title: '航模社',
    subtitle: '制霸蓝天，放飞梦想',
    qrCode: aircraftQr,
    themeColor: '#007aff',
    icon: 'plane',
    bgGradient: 'from-blue-500/10 to-blue-600/5',
    description: [
      '致力于航空模型和无人机的设计、制作与飞行。',
      '无论你是新手还是有经验的模型爱好者，让我们一起在蓝天中翱翔。'
    ]
  }
])

const offCampusOrgs = ref<OffCampusOrg[]>([
  {
    name: '仙建协会【MC社】',
    url: 'https://skin.xauatcraft.com/',
    icon: 'brand-minecraft',
    category: 'Gaming',
    desc: 'Minecraft 爱好者聚集地'
  },
  {
    name: '西邮 Linux 小组',
    url: 'https://www.xiyoulinux.com/',
    icon: 'brand-linux',
    category: 'Tech',
    desc: '开源技术与 Linux 学习社区'
  },
  {
    name: '西邮 MC 兴趣团体',
    url: 'https://cop.cooo.site/',
    icon: 'brand-minecraft',
    category: 'Gaming',
    desc: '跨校联谊与游戏构建'
  }
])

// 控制二维码弹窗
const showModal = ref(false)
const currentQrInfo = ref<{ src: string, title: string, color: string } | null>(null)

const handleShowQr = (org: Organization) => {
  currentQrInfo.value = {
    src: org.qrCode,
    title: `加入${org.title}`,
    color: org.themeColor
  }
  showModal.value = true
}
</script>

<template>
  <div class="w-full font-sans text-slate-900 dark:text-slate-100">

    <!-- 页面头部 -->
    <header class="mb-12 border-b border-zinc-100 dark:border-white/10 pb-8">
      <div class="flex items-center gap-2 mb-3">
        <span class="text-xs font-bold tracking-widest text-blue-600 dark:text-blue-400 uppercase">PARTNERS</span>
      </div>
      <h1 class="text-3xl font-extrabold tracking-tight sm:text-4xl mb-4 text-slate-900 dark:text-white">
        更多精彩社团
      </h1>
      <p class="text-lg text-zinc-500 dark:text-zinc-400 max-w-2xl leading-relaxed">
        联动校内外优秀科技组织，共同构建活跃的校园技术生态圈。
      </p>
    </header>

    <!-- 主要社团展示 -->
    <div class="grid grid-cols-1 gap-6 lg:grid-cols-2">
      <section
          v-for="org in organizations"
          :key="org.id"
          class="group relative flex flex-col justify-between overflow-hidden rounded-3xl border border-zinc-200 bg-white p-6 transition-all hover:border-zinc-300 hover:shadow-lg dark:border-white/10 dark:bg-[#1c1c1e] dark:hover:border-white/20"
      >
        <!-- 装饰背景 -->
        <div class="absolute top-0 right-0 -mt-16 -mr-16 h-48 w-48 rounded-full opacity-20 blur-3xl bg-gradient-to-br"
             :class="org.bgGradient"></div>

        <!-- 顶部：Logo 和 标题 -->
        <div class="relative z-10 flex items-start justify-between mb-6">
          <div class="flex items-center gap-4">
            <!-- Logo 框 -->
            <div
                class="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl border border-zinc-100 bg-zinc-50 shadow-sm dark:border-white/5 dark:bg-[#2c2c2e]"
            >
              <Icon :icon="org.icon" class="text-3xl" :style="{ color: org.themeColor }"/>
            </div>
            <div>
              <h2 class="text-xl font-bold text-slate-900 dark:text-white">
                {{ org.title }}
              </h2>
              <p class="text-xs font-medium text-zinc-500 dark:text-zinc-400 mt-1">
                {{ org.subtitle }}
              </p>
            </div>
          </div>

          <!-- 扫码按钮 (右上角) -->
          <button
              @click="handleShowQr(org)"
              class="flex h-10 w-10 items-center justify-center rounded-full bg-zinc-100 text-zinc-500 transition-colors hover:bg-blue-50 hover:text-blue-600 dark:bg-white/10 dark:text-zinc-400 dark:hover:bg-blue-500/20 dark:hover:text-blue-400"
              title="显示群二维码"
          >
            <Icon icon="qr-code" class="h-5 w-5"/>
          </button>
        </div>

        <!-- 中部：描述 -->
        <div class="relative z-10 mb-6 flex-1">
          <div class="space-y-3 text-sm leading-relaxed text-zinc-600 dark:text-zinc-300">
            <p v-for="(para, i) in org.description" :key="i">
              {{ para }}
            </p>
          </div>
        </div>

        <!-- 底部：行动按钮区 -->
        <div class="relative z-10 mt-auto pt-4 border-t border-zinc-100 dark:border-white/5">
          <button
              @click="handleShowQr(org)"
              class="flex w-full items-center justify-center gap-2 rounded-xl py-2.5 text-sm font-semibold text-white transition-transform active:scale-98"
              :style="{ backgroundColor: org.themeColor }"
          >
            <Icon icon="scan-line" class="h-4 w-4 opacity-80"/>
            扫码加入/了解更多
          </button>
        </div>
      </section>
    </div>

    <!-- 友情链接区域 -->
    <section class="mt-16 pt-10">
      <div class="flex items-end justify-between mb-6">
        <h2 class="text-xl font-bold tracking-tight text-slate-900 dark:text-white">
          友情链接
        </h2>
        <span class="text-xs font-medium text-zinc-400 uppercase tracking-wide">Links</span>
      </div>

      <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <a
            v-for="link in offCampusOrgs"
            :key="link.url"
            :href="link.url"
            target="_blank"
            class="group flex items-start gap-4 rounded-2xl border border-zinc-200 bg-zinc-50/50 p-4 transition-all hover:border-blue-300 hover:bg-white hover:shadow-sm dark:border-white/10 dark:bg-[#2c2c2e]/40 dark:hover:bg-[#2c2c2e] dark:hover:border-blue-500/30"
        >
          <div
              class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-white border border-zinc-100 text-xl text-zinc-500 group-hover:text-blue-600 dark:bg-white/5 dark:border-white/5 dark:text-zinc-400 dark:group-hover:text-blue-400">
            <Icon :icon="link.icon"/>
          </div>
          <div class="min-w-0 flex-1">
            <div class="flex justify-between items-center">
              <h3 class="text-sm font-semibold text-slate-900 group-hover:text-blue-600 dark:text-slate-100 dark:group-hover:text-blue-400">
                {{ link.name }}
              </h3>
              <Icon icon="arrow-up-right"
                    class="w-3 h-3 opacity-0 -translate-x-1 group-hover:translate-x-0 group-hover:opacity-100 transition-all text-zinc-400"/>
            </div>
            <p class="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400 truncate">
              {{ link.desc }}
            </p>
          </div>
        </a>
      </div>
    </section>

    <!-- 二维码弹窗 (Naive UI Modal) -->
    <n-modal v-model:show="showModal" transform-origin="center">
      <div
          class="w-[320px] rounded-2xl bg-white p-6 shadow-2xl dark:bg-[#1c1c1e] text-center"
          v-if="currentQrInfo"
      >
        <h3 class="text-lg font-bold text-slate-900 dark:text-white mb-1">{{ currentQrInfo.title }}</h3>
        <p class="text-xs text-zinc-500 mb-6">请使用对应 APP 扫码</p>

        <div
            class="relative aspect-square w-full overflow-hidden rounded-xl bg-zinc-100 dark:bg-black p-2 mb-6 border border-zinc-100 dark:border-white/10">
          <NImage
              :src="currentQrInfo.src"
              class="w-full h-full"
              object-fit="contain"
              :preview-disabled="true"
          />
        </div>

        <button
            @click="showModal = false"
            class="w-full rounded-xl py-3 text-sm font-semibold text-slate-900 bg-zinc-100 hover:bg-zinc-200 dark:text-white dark:bg-zinc-800 dark:hover:bg-zinc-700 transition-colors"
        >
          关闭
        </button>
      </div>
    </n-modal>

  </div>
</template>

<style scoped>
:deep(.n-image img) {
  width: 100%;
  height: 100%;
}
</style>