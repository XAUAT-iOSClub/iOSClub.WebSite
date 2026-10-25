# iOS Club 官网前端

## 项目简介

本项目是西安建筑科技大学iOS Club社团官网的前端部分，采用 Vue 3 + TypeScript + Vite 构建，提供了现代化、响应式的社团展示和管理平台。

## 技术栈

- Vue 3
- TypeScript
- Vite (构建工具)
- Vue Router (路由管理)
- Pinia (状态管理)
- markdown-it (Markdown解析)
- Prism (代码高亮)
- Mermaid (图表渲染)
- Lucide (图标数据)
- morphicons (图标形变动画)

## 图标系统

图标统一使用 [Lucide](https://lucide.dev) 图标数据 + [morphicons](https://www.morphicons.com) 渲染，不再依赖在线图标服务。

- 组件：`src/components/Icon.vue`
- 注册表：`src/icons/lucide.ts`（新增图标需 import 并注册），品牌图标在 `src/icons/brands.ts`
- 基本用法：`<Icon icon="sun" />`
- 切换动效：给语义上会“换图标”的位置加 `morph`，例如 `<Icon :icon="isDark ? 'moon-star' : 'sun'" morph />`
- 加载旋转：`<Icon icon="loader" spin />`
- 尺寸默认跟随 `font-size`（`1em`），也可用 `class="w-4 h-4"`、`width`/`height` 或 `size` 控制

## 项目结构

```
frontend/
├── src/
│   ├── adminPages/          # 后台管理页面
│   │   ├── Article/         # 文章管理
│   │   ├── Category/        # 分类管理
│   │   ├── Project/         # 项目管理
│   │   └── Admin.vue        # 管理首页
│   ├── components/          # 通用组件
│   │   ├── MarkdownComponent.vue  # Markdown渲染组件
│   │   ├── Sidebar.vue             # 侧边栏组件
│   │   └── SkeletonLoader.vue      # 骨架屏组件
│   ├── layouts/             # 布局组件
│   │   ├── MainLayout.vue   # 主布局
│   │   ├── CentreLayout.vue # 个人中心布局
│   │   └── WordLayout.vue   # 文档布局
│   ├── pages/               # 前台页面
│   │   ├── Articles/        # 文章相关
│   │   ├── ClubActivities/  # 社团活动
│   │   └── LoginSys/        # 登录系统
│   ├── services/            # API服务
│   │   ├── ApiService.ts    # 基础API服务
│   │   ├── AuthService.ts   # 认证服务
│   │   └── ArticleService.ts # 文章服务
│   ├── stores/              # 状态管理
│   │   ├── Authorization.ts # 授权状态
│   │   └── theme.ts         # 主题设置
│   └── main.ts              # 入口文件
├── public/                  # 静态资源
├── package.json             # 依赖配置
├── vite.config.js           # Vite配置
└── tsconfig.json            # TypeScript配置
```

## 核心功能

### 前台功能
- 社团介绍与展示
- 活动公告与历史记录
- 成员展示
- 项目作品展示
- 工具资源分享

### 后台管理
- 文章管理（发布、编辑、删除）
- 分类管理
- 成员管理
- 项目管理
- 客户端应用管理

### 技术特性
- 响应式设计，适配各种设备
- Markdown支持，支持代码高亮和图表渲染
- 骨架屏加载，提升用户体验
- 权限控制，不同角色有不同访问权限
- 状态管理，统一管理应用状态

## 开发命令

### 安装依赖
```bash
pnpm install
```

### 启动开发服务器
```bash
pnpm run dev
```

### 构建生产版本
```bash
pnpm run build
```

### 预览生产版本
```bash
pnpm run preview
```

### 类型检查
```bash
pnpm run typecheck
```

## Markdown 功能

本项目使用 markdown-it 进行 Markdown 解析，支持以下特性：
- 基本 Markdown 语法
- 代码高亮（Prism）
- 图表渲染（Mermaid）
- 自定义容器（tip、warning、danger）
- 脚注
- 任务列表
- 上标、下标、标记
- 自动链接
- 锚点链接

## 贡献指南

1. 确保安装了 Node.js 18+ 和 pnpm
2. 克隆仓库并安装依赖
3. 创建功能分支
4. 开发功能
5. 运行类型检查和构建
6. 提交代码并创建 Pull Request

## 许可证

MIT License
