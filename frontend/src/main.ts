// @ts-ignore
import {createApp} from 'vue'

import App from './App.vue'
// @ts-ignore
import { createPinia } from 'pinia'
import router from './router'
import { googleAnalyticsService } from './services/GoogleAnalyticsService'
import { getAnalyticsKey, isAnalyticsEnabled } from './lib/analyticsConfig'

// @ts-ignore
import './style.css'

const pinia = createPinia()

const app = createApp(App)

// 初始化谷歌分析服务（如果启用）
if (isAnalyticsEnabled()) {
  // 获取当前应该使用的Key
  const analyticsKey = getAnalyticsKey();
  
  // 初始化谷歌分析
  googleAnalyticsService.initialize(analyticsKey);
  
  // 如果没有配置Key，输出提示信息
  if (!analyticsKey || analyticsKey === 'G-XXXXXXXXXX') {
    console.log('注意：谷歌分析Key未配置或使用默认值。请：')
    console.log('1. 在环境变量(.env.development/.env.production)中设置VITE_GOOGLE_ANALYTICS_KEY')
    console.log('2. 或者在应用代码中使用setAnalyticsConfig({key: "your_key"})设置自定义Key')
  }
}

app.use(pinia)
app.use(router)

// 添加路由守卫跟踪页面浏览（如果谷歌分析已初始化）
if (googleAnalyticsService.getIsInitialized()) {
  router.afterEach((to) => {
    // 跟踪页面浏览
    googleAnalyticsService.trackPageview(to.path, to.meta.title as string || to.name as string)
  })
}

app.mount('#app')