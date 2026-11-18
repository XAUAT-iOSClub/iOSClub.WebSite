<script setup lang="ts">
defineProps<{ title: string; description?: string }>()
</script>
<template>
  <div class="admin-page-heading">
    <div class="min-w-0"><h2>{{ title }}</h2><p v-if="description">{{ description }}</p></div>
    <div v-if="$slots.default" class="admin-actions"><slot /></div>
  </div>
</template>
<style scoped>
.admin-page-heading { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; margin-bottom: 1.5rem; }
h2 { color: var(--admin-text); font-size: 1.625rem; font-weight: 650; line-height: 1.2; letter-spacing: -.035em; }
p { color: var(--admin-secondary); font-size: .8125rem; line-height: 1.6; margin-top: .375rem; }
</style>
