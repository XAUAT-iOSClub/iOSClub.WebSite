# 西建大iOS Club社团官网重制版

## 项目简介

本项目是西安建筑科技大学iOS Club社团官网的重制版，旨在提供一个现代化、功能完善的社团展示和管理平台。项目采用前后端分离架构，前端使用Vue 3 + TypeScript构建，后端使用ASP.NET WebAPI提供数据支持，并集成了监控和可视化工具。

## 技术栈

### 前端
- Vue 3
- TypeScript
- Vite (构建工具)
- Vue Router (路由管理)
- Pinia (状态管理)
- markdown-it (Markdown解析)
- Prism (代码高亮)
- Mermaid (图表渲染)

### 后端
- ASP.NET WebAPI
- Entity Framework Core
- CQRS模式
- PostgreSQL (推荐数据库，需安装中文插件)
- JWT认证

### 监控与可视化
- Prometheus (监控系统)
- Grafana (可视化面板)

## 项目结构

```
iOS-Club-Website/
├── frontend/                    # 前端项目目录
│   ├── src/                     # 前端源码
│   │   ├── adminPages/          # 后台管理页面
│   │   ├── components/          # 通用组件
│   │   ├── layouts/             # 布局组件
│   │   ├── pages/               # 前台页面
│   │   ├── services/            # API服务
│   │   └── stores/              # 状态管理
│   ├── public/                  # 静态资源
│   └── package.json             # 前端依赖配置
├── rearend/                     # 后端项目目录
│   └── iOSClub.WebAPI/          # ASP.NET WebAPI项目
│       ├── iOSClub.Data/        # 数据访问层
│       ├── iOSClub.DataApi/     # 业务逻辑层
│       └── iOSClub.Tests/       # 测试项目
├── grafana/                     # Grafana配置
├── prometheus/                  # Prometheus配置
├── docker-compose.yml           # Docker-compose配置
└── README.md                    # 项目说明文档
```

## 功能特性

### 前端功能
- 社团介绍与展示
- 活动公告与历史记录
- 成员展示与管理
- 项目作品展示
- 工具资源分享
- 后台管理系统
- 响应式设计
- Markdown支持
- 代码高亮
- 图表渲染

### 后端功能
- 用户认证与授权 (JWT)
- 社团成员管理
- 文章与活动管理
- 项目作品管理
- 部门管理
- 资源管理
- API监控
- 数据统计
- 财务管理（发票识别、报销材料、经费看板、操作日志，详见 [财务系统使用与部署说明](docs/财务系统使用与部署说明.md)）

## 开发环境要求

### 前端
- Node.js 18.x 或更高版本
- pnpm 8.x 或更高版本
- VS Code 或其他支持Vue的IDE

### 后端
- .NET 10 SDK 或更高版本
- Visual Studio 2022 或 Rider
- PostgreSQL (推荐，需安装中文插件)

### 监控工具
- Docker (用于运行Prometheus和Grafana)

## 安装与运行

### 前端运行

1. 进入前端目录
```bash
cd frontend
```

2. 安装依赖
```bash
pnpm install
```

3. 启动开发服务器
```bash
pnpm run dev
```

### 后端运行

1. 进入后端目录
```bash
cd rearend/iOSClub.WebAPI
```

2. 还原NuGet包
```bash
dotnet restore
```

3. 启动后端服务
```bash
dotnet run --project iOSClub.WebAPI.csproj
```

### 监控工具运行

使用Docker Compose启动Prometheus和Grafana：

```bash
docker-compose up -d
```

## 部署

### 前端部署

1. 构建生产版本
```bash
cd frontend
pnpm run build
```

2. 将构建产物部署到Web服务器（如Nginx、Apache等）

### 后端部署

1. 发布ASP.NET WebAPI项目
```bash
cd rearend/iOSClub.WebAPI
dotnet publish -c Release
```

2. 部署到IIS或其他支持.NET的Web服务器

3. 配置数据库连接

### 监控工具部署

使用Docker Compose部署：

```bash
docker-compose up -d
```

## 贡献指南

1. Fork本仓库

2. 创建你的功能分支
```bash
git checkout -b feature/amazing-feature
```

3. 提交你的更改
```bash
git commit -m 'Add some amazing feature'
```

4. 推送到分支
```bash
git push origin feature/amazing-feature
```

5. 开启一个Pull Request

## 许可证

本项目采用MIT许可证 - 详情请查看[LICENSE](LICENSE)文件

## 联系方式

如有问题或建议，请通过GitHub Issues提交。

---

© 2025 西建大iOS Club. 保留所有权利。
