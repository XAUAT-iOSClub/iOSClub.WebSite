<script setup lang="ts">
import { nextTick, ref, watch, computed, onBeforeUnmount } from "vue";
import { useRoute } from "vue-router";
import Icon from '@/components/Icon.vue'

// Markdown Libraries
import MarkdownIt from 'markdown-it'
import markdownitFootnote from 'markdown-it-footnote'
import markdownitTaskList from 'markdown-it-task-lists'
import markdownitAttrs from 'markdown-it-attrs'
import mdExpandTabs from 'markdown-it-expand-tabs'
import mdSup from 'markdown-it-sup'
import mdSub from 'markdown-it-sub'
import mdMark from 'markdown-it-mark'
import markdownItAnchor from 'markdown-it-anchor'
import markdownItContainer from 'markdown-it-container'
import markdownItMermaid from '@jsonlee_12138/markdown-it-mermaid';
import markdownItKatex from 'markdown-it-katex';
import DOMPurify from 'dompurify';

// KaTeX CSS
import 'katex/dist/katex.min.css';
import Prism from "prismjs"
import "prismjs/plugins/line-numbers/prism-line-numbers.css"
import "prismjs/plugins/line-numbers/prism-line-numbers"
import "prismjs/plugins/copy-to-clipboard/prism-copy-to-clipboard"

// 图片放大相关状态
const showImageModal = ref(false);
const currentImageSrc = ref('');
const currentImageAlt = ref('');
const scale = ref(1);
const isDragging = ref(false);
const dragStart = ref({ x: 0, y: 0 });
const position = ref({ x: 0, y: 0 });

interface Content {
  title: string
  date: string
  content: string,
  identity?: string
}

const props = withDefaults(defineProps<{
  content?: Content
  showNav?: boolean
}>(), {
  showNav: true
})

const route = useRoute();
const activeHeadingId = ref<string>('');

// --- Markdown Configuration ---
const md = new (MarkdownIt as any)({
  html: true,
  linkify: true,
  typographer: true
})

// Anchor Config: Add scroll-margin-top class to handle sticky headers
md.use(markdownItAnchor, {
  permalink: markdownItAnchor.permalink.linkInsideHeader({
    symbol: '', // No symbol, make the whole header clickable or just invisible
    placement: 'before',
    class: 'absolute -ml-6 opacity-0 hover:opacity-100 text-blue-500 w-6 h-full flex items-center justify-center',
    renderHref: (href: string) => `${route.path}#${href}`,
  }),
  // 重要：slugify 函数确保中文标题也能生成合法的 ID
  slugify: (s: string) => s.trim().toLowerCase().replace(/\s+/g, '-').replace(/[^\w\u4e00-\u9fa5-]/g, '')
})

// ... Other Plugins (Footnote, tasklist, etc - kept same)
md.use(markdownitFootnote)
md.use(markdownitTaskList, { label: false, labelAfter: false })
md.use(markdownitAttrs)
md.use(mdExpandTabs).use(mdSup).use(mdSub).use(mdMark)
// Image size support
// Mermaid might need CSS adjustment in dark mode contexts, relying on plugin defaults for now
md.use(markdownItMermaid({ delay: 200 }))

// KaTeX support for mathematical formulas
md.use(markdownItKatex, {
  throwOnError: false,
  errorColor: '#cc0000'
});

// Custom Containers (Alerts) - macOS Style
const containerOptions = [
  { name: 'warning', icon: 'triangle-alert', color: 'text-amber-500', bg: 'bg-amber-50 dark:bg-amber-500/10', border: 'border-amber-200 dark:border-amber-500/20' },
  { name: 'danger', icon: 'circle-x', color: 'text-red-500', bg: 'bg-red-50 dark:bg-red-500/10', border: 'border-red-200 dark:border-red-500/20' },
  { name: 'tip', icon: 'lightbulb', color: 'text-blue-500', bg: 'bg-blue-50 dark:bg-blue-500/10', border: 'border-blue-200 dark:border-blue-500/20' },
  { name: 'info', icon: 'info', color: 'text-sky-500', bg: 'bg-sky-50 dark:bg-sky-500/10', border: 'border-sky-200 dark:border-sky-500/20' },
  { name: 'note', icon: 'book', color: 'text-emerald-500', bg: 'bg-emerald-50 dark:bg-emerald-500/10', border: 'border-emerald-200 dark:border-emerald-500/20' }
]

containerOptions.forEach(opt => {
  md.use(markdownItContainer, opt.name, {
    validate: (params: string) => params.trim().match(new RegExp(`^${opt.name}\\s+(.*)$`)),
    render: (tokens: any[], idx: number) => {
      const m = tokens[idx].info.trim().match(new RegExp(`^${opt.name}\\s+(.*)$`))
      if (tokens[idx].nesting === 1) {
        // Use Tailwind utility classes directly in the rendered HTML
        return `<div class="${opt.bg} ${opt.border} border rounded-xl p-4 my-6 flex gap-3 shadow-sm">
                  <div class="min-w-0 flex-1">
                    <p class="font-semibold text-sm ${opt.color} mb-1 opacity-90">${md.utils.escapeHtml(m[1] || opt.name.toUpperCase())}</p>
                    <div class="text-sm opacity-90 leading-relaxed">`
      } else {
        return '</div></div></div>\n'
      }
    }
  })
})

// Details Container for collapsible content
md.use(markdownItContainer, 'details', {
  validate: (params: string) => params.trim().match(new RegExp(`^details\\s+(.*)$`)),
  render: (tokens: any[], idx: number) => {
    const m = tokens[idx].info.trim().match(new RegExp(`^details\\s+(.*)$`))
    if (tokens[idx].nesting === 1) {
      return `<details class="my-6 rounded-lg border border-zinc-200 dark:border-zinc-700 bg-zinc-50 dark:bg-zinc-800/50 overflow-hidden">
                <summary class="px-4 py-3 cursor-pointer text-sm font-medium text-zinc-900 dark:text-zinc-100 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors">
                  ${md.utils.escapeHtml(m ? m[1] || 'Details' : 'Details')}
                </summary>
                <div class="px-4 py-3 pt-0 text-sm text-zinc-700 dark:text-zinc-300">`
    } else {
      return '</div></details>\n'
    }
  }
})

// Obsidian-style Callout Support
// --- Obsidian-style Callout Support ---//
// Add a custom rule to detect and handle callout blockquotes
md.core.ruler.before('linkify', 'callout_detection', function(state: any) {
  const tokens = state.tokens;
  
  for (let i = 0; i < tokens.length; i++) {
    if (tokens[i].type === 'blockquote_open') {
      // Look for the first paragraph in the blockquote
      let j = i + 1;
      while (j < tokens.length && tokens[j].type !== 'blockquote_close') {
        if (tokens[j].type === 'paragraph_open' && 
            j + 1 < tokens.length && tokens[j + 1].type === 'inline') {
          
          const inlineToken = tokens[j + 1];
          const content = inlineToken.content;
          const calloutMatch = content.match(/^\[!(\w+)\](?:\s+(.*))?$/);
          
          if (calloutMatch) {
            const calloutType = calloutMatch[1].toLowerCase();
            const calloutTitle = calloutMatch[2] || calloutType.toUpperCase();
            
            // Find the corresponding container option
            const opt = containerOptions.find(o => o.name === calloutType);
            
            if (opt) {
              // Store callout information in the blockquote open token
              tokens[i].info = calloutType;
              tokens[i].calloutTitle = calloutTitle;
              tokens[i].calloutOptions = opt;
              
              // Remove the callout marker from the content
              if (content.trim() === calloutMatch[0]) {
                inlineToken.content = '';
              } else {
                inlineToken.content = content.replace(/^\[!(\w+)\](?:\s+(.*))?\s*/, '');
              }
              
              // If the paragraph is now empty, remove it
              if (inlineToken.content.trim() === '') {
                // Remove paragraph_open, inline, paragraph_close
                tokens.splice(j, 3);
                j -= 3;
              }
            }
            break;
          }
        }
        j++;
      }
    }
  }
});

// Override blockquote rendering
const defaultRender = md.renderer.rules.blockquote_open || function(tokens: any[], idx: number, options: any, _: any, self: any) {
  return self.renderToken(tokens, idx, options);
};

const defaultCloseRender = md.renderer.rules.blockquote_close || function(tokens: any[], idx: number, options: any, _: any, self: any) {
  return self.renderToken(tokens, idx, options);
};

md.renderer.rules.blockquote_open = function(tokens: any[], idx: number, options: any, env: any, self: any) {
  const token = tokens[idx];
  
  if (token.info && token.calloutOptions) {
    const opt = token.calloutOptions;
    const title = token.calloutTitle;
    
    return `<div class="${opt.bg} ${opt.border} border rounded-xl p-4 my-6 flex gap-3 shadow-sm">
              <div class="min-w-0 flex-1">
                <p class="font-semibold text-sm ${opt.color} mb-1 opacity-90">${md.utils.escapeHtml(title)}</p>
                <div class="text-sm opacity-90 leading-relaxed">`;
  }
  
  return defaultRender(tokens, idx, options, env, self);
};

md.renderer.rules.blockquote_close = function(tokens: any[], idx: number, options: any, env: any, self: any) {
  // Find the corresponding blockquote_open token
  let openIdx = idx - 1;
  while (openIdx >= 0) {
    if (tokens[openIdx].type === 'blockquote_open') {
      if (tokens[openIdx].info && tokens[openIdx].calloutOptions) {
        return '</div></div></div>\n';
      }
      break;
    }
    openIdx--;
  }
  
  return defaultCloseRender(tokens, idx, options, env, self);
};

// --- Logic: Headings Extraction ---
// Keep headings reactive
const headings = ref<Array<{ id: string; text: string; level: number; children: any[] }>>([])

const extractHeadings = (markdown: string) => {
  // Use a temporary renderer to parse without applying side effects if needed,
  // currently parsing same doc is fine.
  const tokens = md.parse(markdown, {})
  const result = []

  // Custom slugify logic to match markdown-it-anchor
  const slugify = (s: string) => s.trim().toLowerCase().replace(/\s+/g, '-').replace(/[^\w\u4e00-\u9fa5-]/g, '')

  for (let i = 0; i < tokens.length; i++) {
    if (tokens[i].type === 'heading_open') {
      const level = parseInt(tokens[i].tag.slice(1))
      // Support h2 and h3 for TOC (h1 is usually title)
      if (level > 3 || level < 2) continue;

      const inline = tokens[i + 1]
      if (inline && inline.type === 'inline') {
        const text = inline.content
        const id = slugify(text)
        result.push({ id, text, level, children: [] })
      }
    }
  }
  return result
}

// Flat list for TOC (Since we just want indentation visually)
// Alternatively, build a tree if we want collapsible
const flatHeadings = computed(() => headings.value)

// --- Rendering ---
const html = ref('')

watch(() => props.content, async (newVal) => {
  if (!newVal) {
    html.value = ''
    return
  }

  // Determine content string
  const contentStr = newVal.content

  // Extract Headings first
  headings.value = extractHeadings(contentStr)

  // Render HTML
  let rendered = md.render(contentStr)

  // Post-process: Add scroll-mt to headings for sticky header offset
  // Using a regex replacement for simplicity, though manipulating tokens is cleaner
  // This ensures that when we click a hash link, it doesn't go under the header
  rendered = rendered.replace(/<(h[1-6])/g, '<$1 class="scroll-mt-20 group relative"')

  // 文章正文是 Markdown + 原始 HTML；markdown-it 的 html:true 会原样保留
  // <script>/<img onerror> 等内容。必须在写入 v-html 前统一净化：
  // 否则任何能发文章的人都能在 Founder 的浏览器里执行脚本，
  // 再读走 localStorage 里的 JWT。
  html.value = DOMPurify.sanitize(rendered, {
    USE_PROFILES: { html: true, svg: true, svgFilters: true, mathMl: true },
  })

  await nextTick()
  // Add line numbers and copy button to code blocks
  document.querySelectorAll('pre code').forEach((block) => {
    block.parentElement?.classList.add('line-numbers', 'copy-to-clipboard');
  });
  
  // Add lazy loading to images
  document.querySelectorAll('img').forEach((img) => {
    img.setAttribute('loading', 'lazy');
    // Add zoom capability
    img.classList.add('cursor-zoom-in', 'transition-transform', 'duration-300', 'hover:scale-[1.02]');
    // Add click event for zoom
    img.addEventListener('click', () => {
      currentImageSrc.value = img.src;
      currentImageAlt.value = img.alt || '';
      scale.value = 1;
      position.value = { x: 0, y: 0 };
      showImageModal.value = true;
      // Prevent body scroll when modal is open
      document.body.style.overflow = 'hidden';
    });
  });
  
  Prism.highlightAll()
}, { immediate: true })

// --- Metadata Formatting ---
const formattedDate = computed(() => {
  if (!props.content?.date) return '';
  return new Date(props.content.date).toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  });
});

const identityInfo = computed(() => {
  const map: Record<string, { label: string; bg: string; text: string }> = {
    'Department': { label: '部员', bg: 'bg-cyan-100 dark:bg-cyan-500/20', text: 'text-cyan-700 dark:text-cyan-300' },
    'Minister': { label: '部长', bg: 'bg-orange-100 dark:bg-orange-500/20', text: 'text-orange-700 dark:text-orange-300' },
    'President': { label: '社长', bg: 'bg-red-100 dark:bg-red-500/20', text: 'text-red-700 dark:text-red-300' },
    'Founder': { label: '创始人', bg: 'bg-purple-100 dark:bg-purple-500/20', text: 'text-purple-700 dark:text-purple-300' },
  };
  return map[props.content?.identity || ''] || null;
});

// --- TOC Interaction ---
const handleAnchorClick = (e: Event, id: string) => {
  e.preventDefault();
  const el = document.getElementById(id);
  if (el) {
    el.scrollIntoView({ behavior: 'smooth' });
    history.replaceState(null, '', `${route.path}#${id}`);
    activeHeadingId.value = id;
  }
};

// Scroll Spy
let observer: IntersectionObserver | null = null;
const setupObserver = () => {
  if (observer) observer.disconnect();

  observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        activeHeadingId.value = entry.target.id;
      }
    });
  }, { rootMargin: '-100px 0px -60% 0px' }); // Trigger when element is near top

  document.querySelectorAll('h2, h3').forEach(h => {
    observer?.observe(h);
  });
};

watch(html, () => {
  nextTick(() => setTimeout(setupObserver, 200));
});

// 图片放大模态框控制
const closeImageModal = () => {
  showImageModal.value = false;
  // Restore body scroll
  document.body.style.overflow = 'auto';
};

const handleImageClick = (_: MouseEvent) => {
  // Click on image itself closes modal
  closeImageModal();
};

const handleModalClick = (e: MouseEvent) => {
  // Click on modal background closes modal
  if (e.target === e.currentTarget) {
    closeImageModal();
  }
};

const handleZoomIn = (e: MouseEvent) => {
  e.stopPropagation();
  scale.value = Math.min(scale.value + 0.2, 3);
};

const handleZoomOut = (e: MouseEvent) => {
  e.stopPropagation();
  scale.value = Math.max(scale.value - 0.2, 0.5);
};

const handleReset = (e: MouseEvent) => {
  e.stopPropagation();
  scale.value = 1;
  position.value = { x: 0, y: 0 };
};

// 鼠标拖动处理
const handleMouseDown = (e: MouseEvent) => {
  e.stopPropagation();
  if (scale.value > 1) {
    isDragging.value = true;
    dragStart.value = { x: e.clientX - position.value.x, y: e.clientY - position.value.y };
  }
};

const handleMouseMove = (e: MouseEvent) => {
  if (isDragging.value) {
    e.preventDefault();
    position.value = { x: e.clientX - dragStart.value.x, y: e.clientY - dragStart.value.y };
  }
};

const handleMouseUp = () => {
  isDragging.value = false;
};

// 添加全局鼠标事件监听
const setupDragEvents = () => {
  document.addEventListener('mousemove', handleMouseMove);
  document.addEventListener('mouseup', handleMouseUp);
};

const removeDragEvents = () => {
  document.removeEventListener('mousemove', handleMouseMove);
  document.removeEventListener('mouseup', handleMouseUp);
};

// 监听模态框显示状态
watch(showImageModal, (newVal) => {
  if (newVal) {
    setupDragEvents();
  } else {
    removeDragEvents();
  }
});

onBeforeUnmount(() => {
  observer?.disconnect();
  removeDragEvents();
  // Ensure body scroll is restored
  document.body.style.overflow = 'auto';
});
</script>

<template>
  <div v-if="content" class="relative flex w-full flex-col lg:flex-row lg:gap-12">

    <!-- Left: Main Content -->
    <article class="min-w-0 flex-1 pb-20">

      <!-- Article Header -->
      <header class="mb-8 border-b border-zinc-100 pb-8 dark:border-zinc-800">
        <h1 class="mb-4 text-3xl font-extrabold tracking-tight text-zinc-900 dark:text-white sm:text-4xl">
          {{ content.title }}
        </h1>

        <div class="flex flex-wrap items-center gap-3 text-sm">
          <!-- Date Pill -->
          <div class="flex items-center gap-1.5 rounded-full bg-zinc-100 px-3 py-1 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400">
            <Icon icon="calendar" class="h-3.5 w-3.5" />
            <time>{{ formattedDate }}</time>
          </div>

          <!-- Identity Pill -->
          <div v-if="identityInfo"
               class="flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold"
               :class="[identityInfo.bg, identityInfo.text]">
            {{ identityInfo.label }}
          </div>
        </div>
      </header>

      <!-- Formatting Content Wrapper -->
      <!--
         prose-zinc: Neutral gray typographic scale
         max-w-none: Allow full width of container
         prose-headings: scroll-mt-20 included via renderer but styling here
      -->
      <div
          class="prose prose-zinc max-w-none dark:prose-invert
          prose-headings:font-bold prose-headings:tracking-tight
          prose-h1:text-3xl prose-h2:text-2xl prose-h3:text-xl
          prose-code:text-xs prose-code:font-mono prose-code:before:content-none prose-code:after:content-none
          prose-code:bg-zinc-100 prose-code:dark:bg-zinc-800 prose-code:px-1.5 prose-code:py-0.5 prose-code:rounded-md
          prose-pre:bg-[#1e1e20] prose-pre:rounded-xl prose-pre:text-sm prose-pre:leading-relaxed
          prose-img:rounded-xl prose-img:shadow-sm"
          v-html="html"
      ></div>
    </article>

    <!-- Right: Table of Contents (Desktop Only) -->
    <!-- Apple Developer Documentation Style: Right thin rail -->
    <aside v-if="showNav && flatHeadings.length > 0" class="hidden lg:block lg:w-64 lg:shrink-0">
      <div class="sticky top-4 overflow-y-auto max-h-[calc(100vh-120px)] pt-2 pr-2">
        <div class="mb-4 text-xs font-bold uppercase tracking-wider text-zinc-400 dark:text-zinc-500">
          本页目录
        </div>

        <nav class="relative">
          <!-- Decorative Line -->
          <div class="absolute left-0 top-0 bottom-0 w-px bg-zinc-200 dark:bg-zinc-800 pointer-events-none"></div>

          <ul class="space-y-0.5 text-sm">
            <li v-for="heading in flatHeadings" :key="heading.id">
              <a
                  :href="`${route.path}#${heading.id}`"
                  @click="handleAnchorClick($event, heading.id)"
                  class="group flex items-start py-1.5 pl-4 transition-colors border-l-2 -ml-px"
                  :class="[
                  activeHeadingId === heading.id
                    ? 'border-blue-500! text-blue-600! dark:text-blue-400! font-medium!'
                    : 'border-transparent! text-zinc-500! hover:text-zinc-900! dark:text-zinc-500! dark:hover:text-zinc-300! hover:border-zinc-300! dark:hover:border-zinc-600!'
                ]"
              >
               <span :class="heading.level === 3 ? 'pl-3' : ''" class="truncate block">
                 {{ heading.text }}
               </span>
              </a>
            </li>
          </ul>
        </nav>
      </div>
    </aside>

  </div>

  <!-- Empty State -->
  <div v-else class="flex h-[60vh] flex-col items-center justify-center text-center">
    <div class="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-zinc-100 dark:bg-zinc-800">
      <Icon icon="book-open" class="h-8 w-8 text-zinc-400" />
    </div>
    <h3 class="text-lg font-medium text-zinc-900 dark:text-white">暂无内容</h3>
    <p class="mt-1 text-zinc-500 dark:text-zinc-400">请从左侧菜单选择要阅读的文档</p>
  </div>

  <!-- 图片放大模态框 -->
  <div
    v-if="showImageModal"
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4 backdrop-blur-sm"
    @click="handleModalClick"
    @wheel="(e) => e.deltaY > 0 ? handleZoomOut(e) : handleZoomIn(e)"
  >
    <div class="relative max-w-full max-h-full">
      <!-- 控制面板 -->
      <div class="absolute top-4 left-1/2 transform -translate-x-1/2 bg-black/70 rounded-full px-4 py-2 flex items-center gap-2 z-10">
        <button @click="handleZoomOut" class="text-white hover:text-blue-300 transition-colors p-1">
          <Icon icon="zoom-out" class="h-5 w-5" />
        </button>
        <button @click="handleReset" class="text-white hover:text-blue-300 transition-colors p-1">
          <Icon icon="refresh-cw" class="h-5 w-5" />
        </button>
        <button @click="handleZoomIn" class="text-white hover:text-blue-300 transition-colors p-1">
          <Icon icon="zoom-in" class="h-5 w-5" />
        </button>
      </div>
      
      <!-- 关闭按钮 -->
      <button 
        @click="closeImageModal" 
        class="absolute top-4 right-4 bg-black/70 text-white hover:text-red-300 transition-colors p-2 rounded-full"
      >
        <Icon icon="x" class="h-6 w-6" />
      </button>
      
      <!-- 放大图片 -->
      <div 
        class="relative inline-block cursor-move"
        @mousedown="handleMouseDown"
        :style="{
          transform: `translate(${position.x}px, ${position.y}px) scale(${scale})`,
          transition: 'transform 0.1s ease'
        }"
      >
        <img 
          :src="currentImageSrc" 
          :alt="currentImageAlt"
          class="max-w-full max-h-[90vh] object-contain rounded-lg"
          @click="handleImageClick"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
/* Prism Theme Override for Minimalist Look */
@import "prismjs/themes/prism-tomorrow.min.css"; /* Using tomorrow night as base for dark code blocks */

/* Customize scroll behavior */
:deep(*):focus {
  outline: none;
}

/* Enhancing code block visuals inside typography */
.prose :deep(pre) {
  margin-top: 1.5em;
  margin-bottom: 1.5em;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
}

/* Style for blockquotes to match Apple Notes */
.prose :deep(blockquote) {
  font-style: normal;
  border-left-width: 3px;
  border-color: #e4e4e7; /* zinc-200 */
  color: #71717a; /* zinc-500 */
  background: #f9fafb; /* zinc-50 */
  padding-left: 1rem;
}

.dark .prose :deep(blockquote) {
  border-color: #3f3f46; /* zinc-700 */
  color: #a1a1aa; /* zinc-400 */
  background: #1e1e20 !important; /* zinc-900 */
}

/* Table Styling */
.prose :deep(table) {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
  border-radius: 8px;
  border: 1px solid #e4e4e7;
  overflow: hidden;
  font-size: 0.875rem;
}
.dark .prose :deep(table) {
  border-color: #3f3f46;
}

.prose :deep(thead tr) {
  background-color: #f4f4f5; /* zinc-100 */
}
.dark .prose :deep(thead tr) {
  background-color: #27272a; /* zinc-800 */
}

.prose :deep(th), .prose :deep(td) {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid #e4e4e7;
  text-align: left;
}
.dark .prose :deep(th), .dark .prose :deep(td) {
  border-bottom-color: #3f3f46;
}

.prose :deep(th) {
  font-weight: 600;
  color: #18181b;
}
.dark .prose :deep(th) {
  color: #f4f4f5;
}

.prose :deep(tr:last-child td) {
  border-bottom: none;
}

/* Links */
.prose :deep(a) {
  text-decoration-line: none;
  font-weight: 500;
  color: #3b82f6;
  transition: color 0.15s;
}
.prose :deep(a:hover) {
  color: #2563eb;
  text-decoration-line: underline;
}

/* Mermaid Diagram Sizing */
.prose :deep(.mermaid) {
  display: flex;
  justify-content: center;
  margin: 2rem 0;
  background-color: #f9fafb;
  padding: 1rem;
  border-radius: 0.5rem;
}
.dark .prose :deep(.mermaid) {
  background-color: #1a1a1a;
}

/* Image zoom effect */
.prose :deep(img) {
  cursor: zoom-in;
  transition: transform 0.3s ease;
}

.prose :deep(img:hover) {
  transform: scale(1.02);
}

/* 图片放大模态框样式 */
.modal-backdrop {
  backdrop-filter: blur(4px);
}

.modal-controls {
  backdrop-filter: blur(8px);
}

/* 拖动时的鼠标样式 */
.cursor-move {
  cursor: move;
}

.cursor-move:active {
  cursor: grabbing;
}
</style>