# C11 至 C20 修复与 TDD 验证记录

日期 2026-10-25。对应 [冲突清单 C11 至 C20](功能冲突与待决策清单.md)。状态为本地修复并通过验证，尚未部署。

## 修复范围

| 编号 | 修复行为 | 回归检查 |
| --- | --- | --- |
| C11 | 概览页导出不再读取历史键 `localStorage.Authorization`。导出统一使用登录写入的 `accessToken`，清空本地存储后正常登录即可直接导出。 | [conflicts-c11-c16.spec.ts · 数据导出](../frontend/e2e-ui/conflicts-c11-c16.spec.ts)，注入正常登录键后点击导出，断言触发下载、文件名与“部分数据”提示，且不出现“未授权” |
| C12 | 导出 JSON 补齐 `Staffs`（含部门关系）、`Categories`、`Articles`，与导入结构对齐；导入兼容旧 `Presidents` 字段并恢复部员部门归属、分类与文章。活动、参与者、活动操作日志、客户端应用、导入历史仍不导出，界面改称“导出部分数据”并明确不能作为完整还原依据。 | [DataCentreExportTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/DataCentreExportTests.cs) 断言导出文件含部员、部门、分类、文章；[DataCentreServiceTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/DataCentreServiceTests.cs) 新增空库导入断言，核对实体数量、部门关系、分类与文章字段 |
| C13 | 前端以 `localStorage` 为会话事实源：路由守卫判定前从存储同步 Pinia 镜像，自动刷新后继续导航不跳登录。退出请求失败时仍清除本地 `accessToken`、`refreshToken` 与 `UserId`，并单独提示服务端撤销失败。 | [conflicts-c11-c16.spec.ts · 会话状态](../frontend/e2e-ui/conflicts-c11-c16.spec.ts)，覆盖“断网式退出后重载”和“自动刷新后客户端路由跳转”两条链路 |
| C14 | 统计请求失败时 `statistics` 置空，显示“统计数据暂不可用”与重试按钮；不再使用 128/42/24/4 示例值，也不再展示未查询的 `+2.4%`。 | [conflicts-c11-c16.spec.ts · 统计不可用](../frontend/e2e-ui/conflicts-c11-c16.spec.ts)，模拟 `GET /DataCentre` 500，断言不可用提示、重试按钮存在且示例数字不出现 |
| C15 | 资源与文章的菜单入口按接口读取权限降到 Department；分类管理按钮仅 Founder/President 可见；`/Centre/Category` 与 `/Centre/Category/:id` 增加路由级角色守卫。接口仍为最终权限边界。 | [conflicts-c11-c16.spec.ts · 权限入口对齐](../frontend/e2e-ui/conflicts-c11-c16.spec.ts)，覆盖 Department 菜单可见、Minister 看不到分类按钮、Minister 直达分类路由被重定向 |
| C16 | 文章删除成功文案统一为“已永久删除”，不再出现“已移至废纸篓”；移除概览页“清空数据”入口及其硬编码请求外部域名 `Admin/RemoveAllData` 的实现。 | [conflicts-c11-c16.spec.ts · 删除与抹除入口](../frontend/e2e-ui/conflicts-c11-c16.spec.ts)，断言删除后文案且无废纸篓提示，并断言概览页无清空数据入口 |
| C17 | 注册签发令牌时补写 `user:{userId}` 缓存，刷新接口可从缓存重建令牌；注册后不重新登录即可完成首次刷新，旧刷新令牌按既有规则轮换失效。 | [LoginIntegrationTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/LoginIntegrationTests.cs) 新增注册后直接刷新的断言，并核对缓存里的姓名与身份 |
| C18 | 负责人决定不再支持只有 Staff 档案的账号：移除主站 `/User/data` 的 Staff 回退，SSO 授权码交换、ID token 与 userinfo 统一要求 Student 档案。前端遇到 `UserNotFound` 时清理本地会话并回到登录页，不再用 JWT claims 兜底。 | [SsoAccountCompatibilityTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/SsoAccountCompatibilityTests.cs) 断言 Staff-only 账号换令牌返回 `invalid_grant`、userinfo 返回 401；[UserProfileDataTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/UserProfileDataTests.cs) 断言 Staff-only 账号得到 4003；[conflicts-c11-c16.spec.ts · C18](../frontend/e2e-ui/conflicts-c11-c16.spec.ts) 断言主站清理会话并跳转登录 |
| C19 | SSO 请求模型从 Newtonsoft 的 `JsonProperty` 改为 System.Text.Json 的 `JsonPropertyName`，JSON 请求的 snake_case 字段与表单请求得到一致解析；同时修正 `StoreSessionRequest` 的同类问题。 | [SsoTokenRequestBindingTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/SsoTokenRequestBindingTests.cs)，同一请求体分别用 JSON 与表单提交，断言错误字段一致指向 `code` 而非 `grant_type` |
| C20 | 登出接口要求 `userId` 等于当前 JWT 用户，否则返回业务码 3001（HTTP 403）且不删除任何 Redis 会话键；本人登出行为不变。 | [AuthControllerTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/AuthControllerTests.cs)，新增跨用户登出被拒绝且 `KeyDeleteAsync` 从未调用的断言，并修正本人登出用例的测试身份 |

## 修复前失败证据

以下均为实际执行输出节选。后端每组先只加测试并运行，确认失败原因后再改生产代码；前端先加 Playwright 用例，对修复前代码运行得到 8 项全失败，再改实现。

| 范围 | 修复前结果 |
| --- | --- |
| C20 | `AuthControllerTests.Logout_OtherUsersSession_ReturnsForbidden_AndKeepsTargetSession` 得到 `Assert.Equal() Failure ... Expected: Forbidden, Actual: OK`。说明旧实现直接信任查询参数里的 `userId`，任意登录用户都能撤销他人会话 |
| C17 | `LoginIntegrationTests.StoreMemberToken_AfterSignup_WritesUserCacheSoFirstRefreshSucceeds` 得到 `Expected: "new-access-token", Actual: ""`。说明注册只写入 token/refresh，刷新所需 `user:` 缓存缺失 |
| C19 | `SsoTokenRequestBindingTests.Token_JsonSnakeCaseBody_IsBoundLikeFormBody` 得到 JSON 分支 `"缺少必需参数: grant_type"`，表单分支为 `"缺少必需参数: code"`。说明 JSON 请求的 snake_case 字段没有绑定 |
| C18 | 改为“不支持 Staff-only”后先加反向测试：授权码交换期望 `BadRequestObjectResult` 实际仍为 `OkObjectResult`，userinfo 期望 `UnauthorizedResult` 实际仍为 `OkObjectResult`，`UserProfileDataTests.StaffOnlyAccount_ReturnsUserNotFound` 得到 `Expected: 4003, Actual: 0`。说明旧实现三处都在回退返回 Staff 信息 |
| C12 | 导出断言失败于 Staffs/Categories/Articles 为空；导入断言得到 `Expected: 5, Actual: 2`，即只恢复了部门和文章，普通部员与分类未导入 |
| C11/C13/C14/C15/C16/C18 | Playwright 用例在修复前失败：C11 无下载事件；C13 退出后本地令牌仍在、刷新后跳回 `/login`；C14 找不到不可用提示；C15 Department 看不到资源菜单、Minister 能看到分类管理；C16 无“已永久删除”且仍有“清空数据”；C18 Staff-only 账号停留在 `/Centre` 且令牌未清理。原始日志见 `/tmp/frontend-c11-c16-red.log` 与 `/tmp/frontend-c18-red.log` |

## 修复后验证

后端全套测试最终结果。基线 `2c9e006` 为 423 项，本轮新增 8 项（C12 2 项、C17 1 项、C18 3 项、C19 1 项、C20 1 项）。

```text
已通过! - 失败: 0，通过: 431，已跳过: 0，总计: 431，持续时间: 15 s
```

分组过滤结果：C20 2 项、C17 4 项、C19 1 项、C18 3 项、C12 2 项均通过；既有部门导入、Founder 保护和备份合并测试无回归。

前端模拟接口 UI 回归：

```text
9 passed (12.4s)
```

覆盖 C11/C12 导出、C13 退出失败与自动刷新、C14 统计失败、C15 菜单/按钮/直达路由、C16 删除文案与抹除入口、C18 Staff-only 账号拒绝。原始日志见 `/tmp/frontend-c11-c16-green.log`。

前端类型检查与构建：

```text
pnpm run typecheck  # vue-tsc --noEmit，通过
pnpm run build      # vite build，通过；仅既有 chunk 体积警告
```

可从仓库根目录复验。

```sh
DOTNET_ENVIRONMENT=Testing \
ASPNETCORE_ENVIRONMENT=Testing \
REDIS='' \
SQLITE_DB_PATH=/tmp/iosclub-c11-c20-suite/data.db \
JWT_RSA_PRIVATE_KEY_PATH=/tmp/iosclub-p0-suite/private.pem \
JWT_RSA_PUBLIC_KEY_PATH=/tmp/iosclub-p0-suite/public.pem \
dotnet test rearend/iOSClub.WebAPI/iOSClub.Tests/iOSClub.Tests.csproj \
  --artifacts-path /tmp/iosclub-c11-c20-root-artifacts \
  --logger 'console;verbosity=minimal'
```

```sh
cd frontend
pnpm exec playwright test -c playwright.ui.config.ts
pnpm run typecheck
pnpm run build
```

模拟接口用例以仓库 `.env` 的 `VITE_API_URL=http://localhost:5052` 和同源 `/api` 两种形态注册路由，不需要启动后端或数据库；若把 `VITE_API_URL` 指向其他地址，需要同步扩展 mock 路由。既有依赖真实后端与 SQLite 的 `pnpm test:e2e` 本轮未运行。

## 产品决策、兼容性和边界

- C12 决策为“部分数据交换”，不是完整备份。导出覆盖 Students、Departments、Staffs（含部门）、Categories、Articles、Resources；明确排除 Activities、ActivityParticipants、ActivityOperationLogs、ClientApplications、ImportHistories。导出文件仍包含学生密码哈希和邮箱，属于敏感文件，需要按凭证管理。导入沿用按主键合并语义，旧版只带 `Presidents` 的备份文件仍可导入；部门名单快照、活动登记与客户端密钥不在本轮恢复范围。
- C13 把 `localStorage` 定为会话事实源，Pinia 只保留响应式镜像，并在每次路由守卫前同步。这样自动刷新不需要知道 Store 实例；代价是若某次刷新后不发生导航，Store 里的角色/过期时间镜像可能短暂落后，实际角色在刷新前后不变。
- C15 只调整入口与直达路由，接口授权没有放宽或收紧：资源读取仍要求 Department 及以上，分类写仍限 Founder/President。菜单隐藏不是授权边界，浏览器直接调用接口仍由后端拒绝。
- C16 删除文章仍是后端硬删除，界面不再暗示存在废纸篓或恢复入口。数据抹除能力没有本地控制器，本轮移除入口而不是伪造一个跨域请求。
- C17 注册时为补写 `user:` 缓存会多读一次学生档案与 Staff 档案；注册不是高频路径，换取首次刷新可用。刷新令牌轮换与黑名单策略未改。
- C18 的最终决定是**不再支持 Staff-only 账号**。主站 `/User/data`、SSO 授权码交换、ID token、userinfo 与改密现在统一要求 Student 档案；前端 `UserNotFound` 视为不受支持账号，清理本地会话并回登录页。原超管若仍只有 Staff 档案，部署前必须补建 Student 档案；开发环境 `Program.cs` 会自动补建，生产环境需管理员显式创建，本轮没有自动写入生产密码。
- C19 只改请求模型绑定，不改变 grant type、PKCE、客户端校验和响应字段。`StoreSessionRequest` 的 `client_id` 同样改用 System.Text.Json 特性，避免同一类静默丢字段。
- C20 只允许撤销本人会话；管理员跨用户撤销属于新的权限设计，本轮未实现。前端退出仍提交本地保存的 `UserId`，与当前 JWT 一致。
- 本轮没有执行部署、生产数据导出/导入、会话撤销或文章删除；`docs/功能入口索引.md` 已按当前源码重新生成。

采用 Fix Root Causes 原则：C12 让导出结构与导入结构对齐，而不是继续用“完整备份”文案掩盖缺口；C17 在签发令牌的统一入口补写缓存，而不是在刷新接口里加兜底查询；C20 在控制器边界比较 JWT 用户，而不是信任查询参数。采用 Test Behavior, Not Implementation 原则：后端断言 HTTP 状态、响应体、Redis 写入与数据库状态；前端用真实路由和组件交互，只替换网络层。采用 Prove It Works 原则：保留修复前失败输出与修复后通过结果，并给出可复验命令。
