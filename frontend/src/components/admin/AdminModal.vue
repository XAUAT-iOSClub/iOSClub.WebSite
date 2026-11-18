<script setup lang="ts">
import { useAttrs, useId } from 'vue'
import { NModal } from 'naive-ui'
import Icon from '@/components/Icon.vue'
import AdminSurface from './AdminSurface.vue'

defineOptions({ inheritAttrs: false })
const props = withDefaults(defineProps<{
  show?: boolean
  title?: string
  closable?: boolean
  bordered?: boolean
  size?: string
}>(), { show: false, closable: true })
const emit = defineEmits<{ 'update:show': [value: boolean]; close: [] }>()
const attrs = useAttrs()
const titleId = useId()
function modalAttrs() {
  return Object.fromEntries(Object.entries(attrs).filter(([key]) => key !== 'class' && key !== 'style'))
}
function close() {
  emit('close')
  emit('update:show', false)
}
</script>

<template>
  <NModal v-bind="modalAttrs()" :show="props.show" @update:show="emit('update:show', $event)">
    <AdminSurface class="admin-modal admin-theme" :class="attrs.class" :style="attrs.style" role="dialog" aria-modal="true" :aria-labelledby="titleId" tabindex="-1">
      <header class="admin-modal-header">
        <div :id="titleId" class="admin-modal-title"><slot name="header">{{ title }}</slot></div>
        <button v-if="closable" type="button" class="admin-icon-button" aria-label="关闭弹窗" @click="close"><Icon icon="x" :size="18" /></button>
      </header>
      <div class="admin-modal-content"><slot /></div>
      <footer v-if="$slots.footer || $slots.action" class="admin-modal-footer"><slot name="footer"><slot name="action" /></slot></footer>
    </AdminSurface>
  </NModal>
</template>
