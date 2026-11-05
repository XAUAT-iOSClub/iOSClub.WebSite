#!/usr/bin/env node
/**
 * 生成离线图标包。
 *
 * @iconify/vue 默认在运行时从 api.iconify.design 拉取图标；在无法访问该域名的
 * 网络里所有图标都会变成空白。本脚本扫描 src 中实际用到的图标，从国内 npm 镜像
 * 下载对应的 @iconify-json 包，只保留用到的图标，生成 src/icons-bundle.json，
 * 由 main.ts 通过 addCollection 注册，从而完全离线渲染。
 *
 * 用法：
 *   node scripts/generate-icon-bundle.mjs
 *   ICONIFY_JSON_REGISTRY=https://registry.npmjs.org node scripts/generate-icon-bundle.mjs
 *
 * 新增图标后重新运行即可。
 */
import {execFileSync} from 'node:child_process'
import {existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync} from 'node:fs'
import {dirname, join, resolve} from 'node:path'
import {fileURLToPath} from 'node:url'

const here = dirname(fileURLToPath(import.meta.url))
const root = resolve(here, '..')
const srcDir = join(root, 'src')
const outFile = join(srcDir, 'icons-bundle.json')
const cacheDir = join(root, 'node_modules', '.iconify-json-cache')
const registry = (process.env.ICONIFY_JSON_REGISTRY || 'https://registry.npmmirror.com').replace(/\/$/, '')

// 有对应 @iconify-json 包的集合。`sf` 没有公开 npm 包，已在代码中替换为 lucide。
const prefixes = [
    'ion', 'solar', 'lucide', 'ph', 'mdi', 'fluent', 'material-symbols', 'heroicons',
    'tabler', 'simple-icons', 'svg-spinners', 'iconoir', 'iconamoon', 'uim', 'prime',
    'mingcute', 'icon-park-twotone', 'icon-park-outline', 'eos-icons', 'basil', 'ant-design'
]

const pattern = new RegExp(`\\b(${prefixes.join('|')}):([a-z0-9-]+)`, 'g')

function* walk(dir) {
    for (const entry of readdirSync(dir, {withFileTypes: true})) {
        const full = join(dir, entry.name)
        if (entry.isDirectory()) {
            if (entry.name === 'node_modules') continue
            yield* walk(full)
        } else if (/\.(vue|ts|tsx|js|mjs)$/.test(entry.name)) {
            yield full
        }
    }
}

const used = new Map()
for (const file of walk(srcDir)) {
    const text = readFileSync(file, 'utf8')
    for (const match of text.matchAll(pattern)) {
        const prefix = match[1]
        const name = match[2]
        if (!used.has(prefix)) used.set(prefix, new Set())
        used.get(prefix).add(name)
    }
}

mkdirSync(cacheDir, {recursive: true})
const bundle = {}
const missing = []
const skipped = []

for (const [prefix, names] of [...used].sort(([a], [b]) => a.localeCompare(b))) {
    const metaUrl = `${registry}/@iconify-json%2f${prefix}/latest`
    const metaRes = await fetch(metaUrl)
    if (!metaRes.ok) {
        skipped.push(`${prefix} (registry ${metaRes.status})`)
        continue
    }
    const meta = await metaRes.json()
    const version = meta.version
    const tarballUrl = meta.dist?.tarball
    if (!tarballUrl) {
        skipped.push(`${prefix} (no tarball)`)
        continue
    }

    const tgz = join(cacheDir, `${prefix}-${version}.tgz`)
    if (!existsSync(tgz)) {
        const res = await fetch(tarballUrl)
        if (!res.ok) {
            skipped.push(`${prefix} (download ${res.status})`)
            continue
        }
        writeFileSync(tgz, Buffer.from(await res.arrayBuffer()))
    }

    const json = JSON.parse(
        execFileSync('tar', ['-xzf', tgz, '-O', 'package/icons.json'], {
            maxBuffer: 256 * 1024 * 1024
        }).toString('utf8')
    )

    const out = {prefix, icons: {}, aliases: {}, width: json.width, height: json.height}
    const visit = (name) => {
        if (out.icons[name] || out.aliases[name]) return
        if (json.icons?.[name]) {
            out.icons[name] = json.icons[name]
            return
        }
        const alias = json.aliases?.[name]
        if (alias) {
            out.aliases[name] = alias
            if (alias.parent) visit(alias.parent)
            return
        }
        missing.push(`${prefix}:${name}`)
    }

    for (const name of [...names].sort()) visit(name)
    if (!Object.keys(out.aliases).length) delete out.aliases

    bundle[prefix] = out
    const count = Object.keys(out.icons).length + Object.keys(out.aliases || {}).length
    console.log(`  ${prefix.padEnd(20)} ${String(count).padStart(3)} icons`)
}

writeFileSync(outFile, JSON.stringify(bundle))

const total = Object.values(bundle).reduce(
    (sum, c) => sum + Object.keys(c.icons).length + Object.keys(c.aliases || {}).length, 0
)
const sizeKb = (Buffer.byteLength(JSON.stringify(bundle)) / 1024).toFixed(1)

console.log(`\n生成 ${outFile}`)
console.log(`共 ${total} 个图标，${sizeKb} KB`)
if (skipped.length) console.warn(`跳过: ${skipped.join(', ')}`)
if (missing.length) console.warn(`未找到: ${[...new Set(missing)].sort().join(', ')}`)
