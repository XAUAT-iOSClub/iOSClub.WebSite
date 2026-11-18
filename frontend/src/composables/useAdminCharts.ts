import { nextTick, onBeforeUnmount, watch } from 'vue'
import type { ECharts, EChartsOption } from 'echarts'
import { useThemeStore } from '@/stores/theme'

/** Own chart observers and instances for the current page, including hidden tabs. */
export function useAdminCharts() {
  const theme = useThemeStore()
  const charts = new Map<ECharts, ResizeObserver>()
  const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)')
  function appearance(chart: ECharts) {
    if (chart.isDisposed()) return
    const text = theme.isDark ? '#b6b6bf' : '#65656c'
    const border = theme.isDark ? '#3a3a40' : '#e6e6eb'
    const option: EChartsOption = {
      backgroundColor: 'transparent', animation: !reducedMotion.matches,
      textStyle: { color: text, fontFamily: '-apple-system, BlinkMacSystemFont, sans-serif' },
      legend: { textStyle: { color: text } },
      tooltip: { backgroundColor: theme.isDark ? '#2c2c30' : '#fff', borderColor: border, textStyle: { color: theme.isDark ? '#f5f5f7' : '#1d1d1f' } },
    }
    const current = chart.getOption()
    if (!current) return
    if (current.xAxis) option.xAxis = { axisLabel: { color: text }, axisLine: { lineStyle: { color: border } } }
    if (current.yAxis) option.yAxis = { axisLabel: { color: text }, splitLine: { lineStyle: { color: border } } }
    chart.setOption(option)
  }
  function trackChart(chart: ECharts): ECharts {
    if (!charts.has(chart)) {
      const observer = new ResizeObserver(entries => {
        if (chart.isDisposed()) return
        if (entries.some(entry => entry.contentRect.width > 0 && entry.contentRect.height > 0)) {
          chart.resize()
          appearance(chart)
        }
      })
      observer.observe(chart.getDom())
      charts.set(chart, observer)
    }
    void nextTick(() => appearance(chart))
    return chart
  }
  function refreshAppearance() { charts.forEach((_, chart) => appearance(chart)) }
  watch(() => theme.isDark, refreshAppearance, { flush: 'post' })
  reducedMotion.addEventListener('change', refreshAppearance)
  onBeforeUnmount(() => {
    reducedMotion.removeEventListener('change', refreshAppearance)
    charts.forEach((observer, chart) => {
      observer.disconnect()
      if (!chart.isDisposed()) chart.dispose()
    })
    charts.clear()
  })
  return { trackChart }
}
