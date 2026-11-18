// Theme preference is shared by the site and the administration workspace.
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { darkTheme } from 'naive-ui'

export type ThemePreference = 'light' | 'dark' | 'system'

function readPreference(): ThemePreference {
    const value = localStorage.getItem('appThemePreference')
    return value === 'light' || value === 'dark' ? value : 'system'
}

export const useThemeStore = defineStore('theme', () => {
    const LOCAL_STORAGE_THEME_KEY = 'appThemePreference'

    const userPreference = ref<ThemePreference>(readPreference())
    const systemPrefersDark = ref(window.matchMedia('(prefers-color-scheme: dark)').matches)

    const isDark = computed({
        get() {
            if (userPreference.value === 'dark') return true
            if (userPreference.value === 'light') return false
            return systemPrefersDark.value
        },
        set(val) {
            userPreference.value = val ? 'dark' : 'light'
            localStorage.setItem(LOCAL_STORAGE_THEME_KEY, userPreference.value)
            updateThemeClass(val)
        }
    })

    const theme = computed(() => (isDark.value ? darkTheme : null))

    function updateThemeClass(dark: boolean) {
        if (dark) {
            document.documentElement.classList.add('dark')
        } else {
            document.documentElement.classList.remove('dark')
        }
    }

    function setThemePreference(preference: ThemePreference) {
        userPreference.value = preference
        localStorage.setItem(LOCAL_STORAGE_THEME_KEY, preference)
        updateThemeClass(isDark.value)
    }

    function toggleTheme() {
        // 循环切换主题模式：light → dark → system → light
        if (userPreference.value === 'light') {
            setThemePreference('dark')
        } else if (userPreference.value === 'dark') {
            setThemePreference('system')
        } else {
            setThemePreference('light')
        }
    }

    // 初始化
    function init() {
        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
        mediaQuery.addEventListener('change', (e) => {
            systemPrefersDark.value = e.matches
            if (userPreference.value === 'system') {
                updateThemeClass(isDark.value)
            }
        })
        updateThemeClass(isDark.value)
    }

    return {
        isDark,
        theme,
        userPreference,
        setThemePreference,
        toggleTheme,
        init
    }
})