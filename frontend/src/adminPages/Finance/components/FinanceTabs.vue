<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import Icon from '@/components/Icon.vue'

const route = useRoute()

const tabs = [
  { label: '经费看板', to: '/Centre/Finance', icon: 'wallet' },
  { label: '发票库', to: '/Centre/Finance/Invoices', icon: 'file-text' },
  { label: '报销材料', to: '/Centre/Finance/Materials', icon: 'list' },
  { label: '经费流水', to: '/Centre/Finance/Flows', icon: 'refresh-cw' },
  { label: '操作日志', to: '/Centre/Finance/Logs', icon: 'scroll-text' }
]

const activePath = computed(() => route.path)

function isActive(to: string) {
  if (to === '/Centre/Finance') return activePath.value === to
  return activePath.value === to || activePath.value.startsWith(to + '/')
}
</script>

<template>
  <div class="finance-tabs">
    <router-link
        v-for="tab in tabs"
        :key="tab.to"
        :to="tab.to"
        class="finance-tab"
        :class="{ 'is-active': isActive(tab.to) }"
    >
      <Icon :icon="tab.icon" class="finance-tab-icon"/>
      <span>{{ tab.label }}</span>
    </router-link>
  </div>
</template>

<style>
/* 财务模块公共样式。放在 FinanceTabs 里，随财务页面一起加载，不影响现有页面。 */
.finance-page {
  min-height: 100%;
  padding: 26px;
  background: linear-gradient(180deg, #f5f7fa 0%, #f1f4f9 100%);
  transition: background-color .3s ease;
}

.finance-tabs {
  display: flex;
  gap: 4px;
  width: fit-content;
  max-width: 100%;
  padding: 4px;
  margin-bottom: 20px;
  overflow-x: auto;
  background: rgba(0, 0, 0, .05);
  border-radius: 9999px;
}

.finance-tab {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 14px;
  font-size: 13px;
  font-weight: 500;
  color: #6b7280;
  white-space: nowrap;
  border-radius: 9999px;
  cursor: pointer;
  transition: all .2s ease;
}

.finance-tab:hover { color: #111827; }

.finance-tab.is-active {
  color: #007aff;
  background: #fff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, .07);
}

.finance-tab-icon { width: 15px; height: 15px; }

.finance-card {
  padding: 22px;
  background: #fff;
  border: 1px solid rgba(15, 23, 42, .04);
  border-radius: 20px;
  box-shadow: 0 1px 2px rgba(15, 23, 42, .03), 0 8px 24px rgba(15, 23, 42, .04);
}

.finance-card-title {
  font-size: 16px;
  font-weight: 650;
  letter-spacing: -.01em;
  color: #1f2937;
}

.finance-muted { font-size: 12px; color: #8e8e93; }

.finance-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 8px 15px;
  font-size: 13px;
  font-weight: 500;
  color: #1d1d1f;
  background: rgba(0, 0, 0, .05);
  border-radius: 9999px;
  white-space: nowrap;
  flex-shrink: 0;
  cursor: pointer;
  transition: all .2s cubic-bezier(.4, 0, .2, 1);
}

.finance-btn:hover { background: rgba(0, 0, 0, .09); }
.finance-btn:active { transform: scale(.97); }
.finance-btn:disabled { cursor: not-allowed; opacity: .45; }

.finance-btn.primary {
  color: #fff;
  background: #007aff;
  box-shadow: 0 2px 8px rgba(0, 122, 255, .28);
}

.finance-btn.primary:hover { background: #0a6fd8; }

.finance-btn.danger {
  color: #d70015;
  background: rgba(255, 59, 48, .1);
}

.finance-btn.success {
  color: #0b7a3b;
  background: rgba(52, 199, 89, .12);
}

.finance-btn.ghost { background: transparent; color: #007aff; }

.finance-input,
.finance-select,
.finance-textarea {
  width: 100%;
  padding: 10px 12px;
  font-size: 14px;
  color: #1d1d1f;
  background: #f2f2f7;
  border: 1px solid transparent;
  border-radius: 12px;
  outline: none;
  transition: all .2s ease;
}

.finance-input:focus,
.finance-select:focus,
.finance-textarea:focus {
  background: #fff;
  border-color: #007aff;
  box-shadow: 0 0 0 3px rgba(0, 122, 255, .12);
}

/* 学年下拉框单独做成白底描边，避免和页面背景混在一起 */
.finance-select {
  background-color: #fff;
  border-color: rgba(15, 23, 42, .12);
  cursor: pointer;
  box-shadow: 0 1px 2px rgba(15, 23, 42, .03);
}

.finance-select:hover { border-color: rgba(0, 122, 255, .5); }

/* 搜索框内左右留出图标/清空按钮的位置，避免文字与图标重叠 */
.finance-input.pl-9 { padding-left: 2.35rem; }
.finance-input.pr-9 { padding-right: 2.35rem; }

/* 金额、数量、单价等直接输入，不显示上下步进按钮 */
.finance-input[type="number"] {
  appearance: textfield;
  -moz-appearance: textfield;
}

.finance-input[type="number"]::-webkit-inner-spin-button,
.finance-input[type="number"]::-webkit-outer-spin-button {
  -webkit-appearance: none;
  margin: 0;
}

/* 工具栏里的“自动宽度”下拉框：覆盖 .finance-select 的 width:100% */
.finance-select.w-auto { width: auto; }

.finance-input:disabled,
.finance-textarea:disabled,
.finance-select:disabled {
  color: #8e8e93;
  cursor: not-allowed;
  background: #f2f2f7;
  opacity: .75;
}

.finance-label {
  display: block;
  margin-bottom: 6px;
  font-size: 12px;
  font-weight: 500;
  color: #6b7280;
}

.finance-field-error {
  margin-top: 4px;
  font-size: 11px;
  color: #d70015;
}

.finance-alert {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  padding: 12px 14px;
  font-size: 13px;
  border-radius: 14px;
}

.finance-alert.warning { color: #8a5a00; background: #fff8e6; }
.finance-alert.danger { color: #b00020; background: #ffebe9; }
.finance-alert.info { color: #0b4f8a; background: #eef6ff; }

.finance-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 10px;
  padding: 56px 20px;
  color: #8e8e93;
}

.finance-kpi-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}

.finance-kpi {
  position: relative;
  padding: 20px;
  overflow: hidden;
  background: linear-gradient(180deg, #ffffff 0%, #fbfbfd 100%);
  border: 1px solid rgba(15, 23, 42, .04);
  border-radius: 20px;
  box-shadow: 0 1px 2px rgba(15, 23, 42, .03), 0 8px 24px rgba(15, 23, 42, .04);
}

.finance-kpi-label { font-size: 12px; color: #8e8e93; }
.finance-kpi-value { margin-top: 6px; font-size: 26px; font-weight: 700; letter-spacing: -.02em; }
.finance-kpi-value.warning { color: #b26a00; }
.finance-kpi-value.danger { color: #d70015; }

.finance-table .n-data-table-th {
  font-size: 13px;
  font-weight: 600;
  letter-spacing: .01em;
  color: #8e8e93;
  background: transparent;
  border-bottom: 1px solid #eef0f4;
}

.finance-table .n-data-table-td {
  font-size: 13px;
  color: #374151;
  border-bottom: 1px solid #f4f5f7;
}

.finance-table .n-data-table-tr:hover .n-data-table-td { background: rgba(0, 122, 255, .035); }

.finance-table button {
  cursor: pointer;
  border: 1px solid rgba(0, 0, 0, .05);
  box-shadow: 0 1px 2px rgba(0, 0, 0, .04);
}

.finance-table button:hover { box-shadow: 0 2px 6px rgba(0, 0, 0, .1); }
.finance-table button:active { transform: scale(.97); }
.finance-page a { cursor: pointer; }

.finance-table .n-data-table-tr:last-child .n-data-table-td { border-bottom: none; }

.finance-tag {
  display: inline-flex;
  align-items: center;
  padding: 2px 10px;
  font-size: 11px;
  font-weight: 500;
  white-space: nowrap;
  border-radius: 9999px;
}

.finance-tag.pending { color: #0b5cad; background: #e8f2ff; }
.finance-tag.reimbursed { color: #0b7a3b; background: #e8f9ee; }
.finance-tag.deleted { color: #6b7280; background: #f2f2f7; }
.finance-tag.failed { color: #b00020; background: #ffebe9; }

.finance-low-confidence { background: #fff3cd !important; border-color: #ffd666 !important; }

/* Dark mode */
.dark .finance-page { background-color: #000; }
.dark .finance-tabs { background: rgba(255, 255, 255, .1); }
.dark .finance-tab { color: #98989d; }
.dark .finance-tab:hover { color: #f5f5f7; }
.dark .finance-tab.is-active { color: #0a84ff; background: #2c2c2e; box-shadow: none; }
.dark .finance-card,
.dark .finance-kpi { background: #1c1c1e; border-color: rgba(255, 255, 255, .05); box-shadow: 0 4px 14px rgba(0, 0, 0, .25); }
.dark .finance-card-title { color: #f5f5f7; }
.dark .finance-muted { color: #98989d; }
.dark .finance-btn { color: #f5f5f7; background: rgba(255, 255, 255, .1); }
.dark .finance-btn:hover { background: rgba(255, 255, 255, .16); }
.dark .finance-btn.primary { background: #0a84ff; }
.dark .finance-btn.danger { color: #ff6961; background: rgba(255, 69, 58, .16); }
.dark .finance-btn.success { color: #30d158; background: rgba(48, 209, 88, .15); }
.dark .finance-input,
.dark .finance-select,
.dark .finance-textarea { color: #f5f5f7; background: #2c2c2e; }
.dark .finance-select { border-color: rgba(255, 255, 255, .14); }
.dark .finance-input:focus,
.dark .finance-select:focus,
.dark .finance-textarea:focus { background: #3a3a3c; border-color: #0a84ff; }
.dark .finance-label { color: #98989d; }
.dark .finance-alert.warning { color: #ffd60a; background: rgba(255, 214, 10, .12); }
.dark .finance-alert.danger { color: #ff6961; background: rgba(255, 69, 58, .14); }
.dark .finance-alert.info { color: #64d2ff; background: rgba(10, 132, 255, .14); }
.dark .finance-kpi-value { color: #f5f5f7; }
.dark .finance-kpi-label { color: #98989d; }
.dark .finance-table .n-data-table-th { color: #98989d; border-bottom-color: rgba(255, 255, 255, .08); }
.dark .finance-table .n-data-table-td { color: #d1d1d6; border-bottom-color: rgba(255, 255, 255, .06); }
.dark .finance-table button { border-color: rgba(255, 255, 255, .08); box-shadow: none; }
.dark .finance-table button:hover { box-shadow: 0 2px 8px rgba(0, 0, 0, .35); }
.dark .finance-tag.pending { color: #64b5ff; background: rgba(10, 132, 255, .18); }
.dark .finance-tag.reimbursed { color: #30d158; background: rgba(48, 209, 88, .16); }
.dark .finance-tag.deleted { color: #98989d; background: rgba(255, 255, 255, .08); }
.dark .finance-tag.failed { color: #ff6961; background: rgba(255, 69, 58, .16); }
.dark .finance-low-confidence { background: rgba(255, 214, 10, .16) !important; border-color: rgba(255, 214, 10, .45) !important; }

@media (max-width: 1024px) {
  .finance-kpi-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}

@media (max-width: 640px) {
  .finance-page { padding: 0; }
  .finance-card { border-radius: 16px; }
  .finance-kpi-grid { grid-template-columns: 1fr; }
}
</style>
