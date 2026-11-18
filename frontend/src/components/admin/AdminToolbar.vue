<script setup lang="ts">
import AdminSurface from './AdminSurface.vue'
defineProps<{ label: string }>()
</script>
<template><AdminSurface role="search" :aria-label="label" class="admin-toolbar"><slot /></AdminSurface></template>
<style scoped>
.admin-toolbar { display: flex; flex-wrap: wrap; align-items: center; gap: .75rem; padding: 1rem; margin-bottom: 1rem; }
</style>
