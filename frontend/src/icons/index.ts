import type { IconNode } from 'morphicons'
import { lucideIcons } from './lucide'
import { brandIcons } from './brands'

/**
 * 项目图标注册表：Lucide 图标 + 内置品牌图标。
 * 键名为 kebab-case 名称，与 lucide.dev 上的图标名一致。
 */
export const iconRegistry: Record<string, IconNode> = {
  ...lucideIcons,
  ...brandIcons,
}

const FALLBACK = lucideIcons['circle-question-mark']

/**
 * 把组件收到的 icon 归一化成 morphicons 需要的 IconNode。
 * 支持直接传 IconNode，也支持 'sun'、'lucide:sun' 这类字符串。
 */
export function resolveIcon(icon: string | IconNode): IconNode {
  if (typeof icon !== 'string') return icon

  const name = icon.startsWith('lucide:') ? icon.slice('lucide:'.length) : icon
  const found = iconRegistry[name]
  if (found) return found

  if (import.meta.env.DEV) {
    console.warn(`[Icon] 未注册的图标 "${icon}"，已回退到 circle-question-mark`)
  }
  return FALLBACK
}
