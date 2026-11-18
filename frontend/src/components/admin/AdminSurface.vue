<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, shallowRef, useTemplateRef, useAttrs } from 'vue'

withDefaults(defineProps<{ as?: string }>(), { as: 'div' })
const attrs = useAttrs()
const interactive = computed(() => typeof attrs.onClick === 'function')
function keyboardActivate(event: KeyboardEvent) {
  if (interactive.value && event.target === event.currentTarget && (event.key === 'Enter' || event.key === ' ')) {
    event.preventDefault()
    element.value?.click()
  }
}
const element = useTemplateRef<HTMLElement>('element')
const dimensions = shallowRef({ width: 100, height: 100 })
let observer: ResizeObserver | undefined
const outline = computed(() => {
  const { width: w, height: h } = dimensions.value
  const r = Math.min(24, w / 2, h / 2)
  const a = r * .1, b = r * .22, c = r * .38
  return `M ${r} 0 H ${w-r} C ${w-c} 0 ${w-b} 0 ${w-a} ${a} C ${w} ${b} ${w} ${c} ${w} ${r} V ${h-r} C ${w} ${h-c} ${w} ${h-b} ${w-a} ${h-a} C ${w-b} ${h} ${w-c} ${h} ${w-r} ${h} H ${r} C ${c} ${h} ${b} ${h} ${a} ${h-a} C 0 ${h-b} 0 ${h-c} 0 ${h-r} V ${r} C 0 ${c} 0 ${b} ${a} ${a} C ${b} 0 ${c} 0 ${r} 0 Z`
})
onMounted(() => {
  observer = new ResizeObserver(entries => {
    const entry = entries[0]
    if (entry) dimensions.value = { width: entry.target.clientWidth, height: entry.target.clientHeight }
  })
  if (element.value) observer.observe(element.value)
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <component :is="as" ref="element" class="admin-surface" :role="interactive ? 'button' : undefined" :tabindex="interactive ? 0 : undefined" @keydown="keyboardActivate">
    <svg class="admin-surface-shape" :viewBox="`0 0 ${dimensions.width} ${dimensions.height}`" preserveAspectRatio="none" aria-hidden="true">
      <path :d="outline" />
    </svg>
    <slot />
  </component>
</template>
