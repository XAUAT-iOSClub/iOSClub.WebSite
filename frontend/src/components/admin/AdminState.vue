<script setup lang="ts">
import Icon from '@/components/Icon.vue'
withDefaults(defineProps<{ title: string; description?: string; kind?: 'empty' | 'error' | 'loading' }>(), { kind: 'empty' })
</script>
<template>
  <div class="admin-state" :role="kind === 'error' ? 'alert' : 'status'">
    <Icon :icon="kind === 'error' ? 'circle-alert' : kind === 'loading' ? 'loader-circle' : 'inbox'" :size="28" :spin="kind === 'loading'" />
    <p class="font-medium">{{ title }}</p><p v-if="description" class="text-sm">{{ description }}</p><slot />
  </div>
</template>
<style scoped>
.admin-state { display: flex; align-items: center; justify-content: center; flex-direction: column; gap: .75rem; padding: 3rem 1.5rem; color: var(--admin-secondary); text-align: center; }
</style>
