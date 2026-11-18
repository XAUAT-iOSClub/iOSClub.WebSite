import { markRaw } from 'vue';
import type { Component } from 'vue';
import { defineStore } from 'pinia';

export const useLayoutStore = defineStore('layout', {
    state: () => ({
        showSidebar: typeof window !== 'undefined' && window.innerWidth >= 768, // 侧边栏是否显示
        isMobile: typeof window !== 'undefined' ? window.innerWidth < 768 : false, // 是否为移动端
        isSidebarCollapsed: false, // 侧边栏是否折叠
        pageTitle: '', // 页面标题
        pageSubtitle: '', // 页面副标题
        showPageActions: false, // 是否显示页面操作栏
        pageActionsContent: '', // 操作栏HTML内容
        actionsComponent: null as Component | null, // 操作栏组件
    }),
    actions: {
        toggleSidebar() {
            this.showSidebar = !this.showSidebar;
        },
        toggleSidebarCollapse() {
            this.isSidebarCollapsed = !this.isSidebarCollapsed;
        },
        handleResize() {
            const wasMobile = this.isMobile;
            this.isMobile = typeof window !== 'undefined' ? window.innerWidth < 768 : false;
            if (this.isMobile && !wasMobile) {
                this.showSidebar = false;
            } else if (!this.isMobile) {
                this.showSidebar = true;
            }
        },
        setPageHeader(title: string, subtitle: string = '') {
            this.pageTitle = title;
            this.pageSubtitle = subtitle;
        },
        setShowPageActions(show: boolean) {
            this.showPageActions = show;
        },
        setPageActionsContent(content: string) {
            this.pageActionsContent = content;
        },
        clearPageActionsContent() {
            this.pageActionsContent = '';
        },
        setActionsComponent(component: Component | null) {
            this.actionsComponent = component ? markRaw(component) : null;
        },
        clearActionsComponent() {
            this.actionsComponent = null;
        },
        clearPageHeader() {
            this.pageTitle = '';
            this.pageSubtitle = '';
            this.showPageActions = false;
            this.pageActionsContent = '';
            this.actionsComponent = null;
        },
    },
});