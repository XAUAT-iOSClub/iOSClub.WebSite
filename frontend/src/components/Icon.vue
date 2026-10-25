<script setup lang="ts">
import { computed } from 'vue'
import { MorphIcon } from 'morphicons/vue'
import type { IconNode } from 'morphicons'
import { resolveIcon } from '../icons'

const props = withDefaults(defineProps<{
  /** Lucide 图标数据，或注册表里的名称（如 'sun'、'lucide:sun'）。 */
  icon: string | IconNode
  /** 默认跟随 font-size（1em），与原来的图标组件保持一致。 */
  size?: number | string
  strokeWidth?: number | string
  absoluteStrokeWidth?: boolean
  /** 有 label 时作为可访问名称，否则标记为装饰性图标。 */
  label?: string
  /** 图标切换时播放 spring 形变动画。仅用于有明确语义的切换点。 */
  morph?: boolean
  /** 加载类图标持续旋转。 */
  spin?: boolean
  spring?: 'snappy' | 'smooth' | 'bouncy'
}>(), {
  size: '1em',
  strokeWidth: 2,
  absoluteStrokeWidth: false,
  morph: false,
  spin: false,
  spring: 'snappy',
})

const data = computed(() => resolveIcon(props.icon))
// 未开启 morph 时使用 "always" 让切换瞬时完成；开启时尊重系统的减少动态效果设置。
const reducedMotion = computed(() => (props.morph ? 'user' : 'always'))
</script>

<template>
  <MorphIcon
      :icon="data"
      :size="size"
      :stroke-width="strokeWidth"
      :absolute-stroke-width="absoluteStrokeWidth"
      :label="label"
      :spring="spring"
      :reduced-motion="reducedMotion"
      :class="{ 'animate-spin': spin }"
  />
</template>
