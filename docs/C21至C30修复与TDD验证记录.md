# C21 至 C30 修复与 TDD 验证记录

日期 2026-10-25。对应 [冲突清单 C21 至 C30](功能冲突与待决策清单.md)。状态为本地修复并通过验证，尚未部署。

## 修复范围

| 编号 | 修复行为 | 回归检查 |
| --- | --- | --- |
| C21 | 移除不可用的 OAuth2 Cookie 入口：删除 `OAuthController`（login/callback/exchange/logout/access-denied）及 `Program.cs` 中 `AddCookie("OAuth2")` 注册。该方案没有外部 OAuth 处理器，Challenge 指回自身 LoginPath 形成循环，logout 又使用未注册的 Cookies 方案；第三方登录统一走 SSO。 | [OAuthEntryRemovalTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/OAuthEntryRemovalTests.cs)：5 条 `/OAuth/*` 路由均 404，`/.well-known/openid-configuration` 仍 200。原 [AuthControllerTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/AuthControllerTests.cs) 中 `/OAuth/exchange` 用例随入口移除一起收敛到 `/Auth/login`，路由移除由前者的 404 断言锁定 |
| C22 | `/SSO/token` 支持 `grant_type=refresh_token`（RFC 6749 §6）。`TokenRequest` 增加 `refresh_token`（System.Text.Json + 表单绑定）；`LoginService` 维护 `refresh-owner:{token} → userId` 反向索引（登录、主站换 SSO 令牌、注册补写、轮换写入；登出与轮换删除旧键），新增 `ILoginService.GetUserIdByRefreshToken`；客户端凭据校验与授权码分支一致，成功返回轮换后的 access/refresh token，未知、已撤销或客户端不匹配返回 `400 invalid_grant`。 | [LoginServiceRefreshTokenIndexTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/LoginServiceRefreshTokenIndexTests.cs) 锁定反向索引的写入、登出删除与轮换迁移；[SsoRefreshGrantTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/SsoRefreshGrantTests.cs) 覆盖 JSON/表单、缺参、客户端不匹配、无效令牌与成功轮换；既有 [SsoTokenRequestBindingTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/SsoTokenRequestBindingTests.cs) 无回归 |
| C23 | 社长（President）为社团层面身份，所有写入路径统一清空部门：`UpdateRoleAsync`、`AssignRoleAsync`、`ChangeStaffDepartmentAsync`（拒绝挂部门、允许清空）、`UpdateStaffAsync`、`CreateStaffAsync`、部门名单导入与数据导入。补齐 `Include(Department)`，否则 EF 识别不到关系变更。Founder 既有保护不变。 | [StaffRepositoryTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/RepositoryTests/StaffRepositoryTests.cs) 覆盖四个仓库入口；[DepartmentImportServiceTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/DepartmentImportServiceTests.cs) 覆盖新增/跨部门社长；[DataCentreServiceTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/DataCentreServiceTests.cs) 覆盖导入文件残留部门；[StaffSecurityRegressionTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/StaffSecurityRegressionTests.cs) 覆盖 HTTP 层 |
| C24 | `IResourceRepository.AddResourceAsync` 改为返回持久化后的 `ResourceDO`（失败返回 `null`），控制器用返回对象组装 `CreatedAtAction`、Location、响应体与日志，不再使用请求 DTO 中始终为空的 ID。 | [ResourceRepositoryTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/RepositoryTests/ResourceRepositoryTests.cs) 断言返回对象含服务端生成的 ID；[ResourceCreationTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/ResourceCreationTests.cs) 走完 POST → GET → PUT → GET → DELETE → GET 全链路 |
| C25 | 文章写入自动创建缺失分类的权限与显式分类写接口对齐：仅 Founder/President 可顺带建分类；Department/Minister 引用不存在分类时返回业务码 3001（HTTP 403）且不创建分类。创建与更新路径共用同一校验。仓库层对管理员的自动创建逻辑保留。 | [ArticlePermissionTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/ArticlePermissionTests.cs) 新增 5 条用例，覆盖 Department/Minister 创建与更新被拒、Founder 成功且分类落库 |
| C26 | `ArticleRepository.GetAll` 投影补上 `CategoryId` 与 `ArticleOrder`，与 `GetFromPath` 的详情投影一致，后台列表不再丢失分类与排序。 | [ArticleRepositoryTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/RepositoryTests/ArticleRepositoryTests.cs) 断言同一文章的列表与详情在分类与排序上相等 |
| C27 | `/Staff/Update` 不再静默忽略 `DepartmentName`：按名称解析并落库，未知部门返回 400 且不写入；缺省 `DepartmentName` 表示“部门不变”，清空部门仍走独立调部门接口；本人修改自己的部门返回 403；社长目标按 C23 清空部门。仓库与调部门接口共用同一 `ResolveDepartmentAsync`。 | [StaffRepositoryTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/RepositoryTests/StaffRepositoryTests.cs) 覆盖改名保留部门、按名称改部门、未知部门失败、社长清空、社长降级回部门；[StaffSecurityRegressionTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/StaffSecurityRegressionTests.cs) 覆盖 HTTP 层本人/他人与缺省部门语义 |
| C28 | 批量成员更新返回逐行结果 `BulkUpdateResultVO { Created, Updated, Skipped, Failed, Issues }`；被过滤的行不再计入成功。零写入且存在跳过/失败时返回业务码 2001（`OperationFailed`），同时 `data` 保留完整计数与原因；空列表返回全 0 成功。前端 JSON 导入据此展示四类数量与逐行原因，零写入提示失败。 | [BulkUpdateResultTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/RepositoryTests/BulkUpdateResultTests.cs)；[MemberBulkUpdateTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/MemberBulkUpdateTests.cs)；前端 [conflicts-c21-c30.spec.ts · C28](../frontend/e2e-ui/conflicts-c21-c30.spec.ts) 两条 |
| C29 | `/SSO/store-session` 在 State 非空且 Redis 写入失败时返回 HTTP 500 `{success:false,message:"会话存储失败"}`，不再吞掉异常后返回成功；前端 `storeSession` 解析响应体，非 2xx 或 `success:false` 均判失败，停留授权登录页并提示“会话创建失败，请稍后重试”，不跳转 SSO/callback。 | [SsoStoreSessionFailureTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/SsoStoreSessionFailureTests.cs)；前端 [conflicts-c21-c30.spec.ts · C29](../frontend/e2e-ui/conflicts-c21-c30.spec.ts) 覆盖 500 与 200+success:false 两种失败形态 |
| C30 | 监控指标改为显式窗口与诚实命名：`/Monitoring/performance` 与 `/Monitoring/http-stats` 均返回 `windowSeconds = 3600`，`requestsPerSecond` 改为 `averageRequestsPerSecond`（累计请求数 ÷ 固定窗口）；`system.cpuUsage` 改为 `system.cpuSecondsTotal`（进程启动以来累计 CPU 秒数）。概览页标签改为“近 1 小时平均每秒请求数”。 | [MonitoringPerformanceMetricsTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/MonitoringPerformanceMetricsTests.cs) 断言两个端点的字段名、窗口值、同响应内自洽与旧字段消失；前端 [conflicts-c21-c30.spec.ts · C30](../frontend/e2e-ui/conflicts-c21-c30.spec.ts) 断言窗口标签与取值 |

## 修复前失败证据

以下均为实际执行输出节选。每个条目先加测试并运行，确认失败原因后再改生产代码；前端先加 Playwright 用例，对修复前代码运行得到 4 项失败，再改实现。

| 范围 | 修复前结果 |
| --- | --- |
| C21 | `OAuthEntryRemovalTests.RemovedOAuthEntry_IsNotRouted` 得到 `Expected: NotFound, Actual: Unauthorized`（logout）/ `Actual: BadRequest`（callback）/ `Actual: InternalServerError`（login），说明入口仍由旧控制器应答。原始日志 `/tmp/c21-c30-lead-red.log` |
| C22 | `Login_WritesReverseIndexMappingRefreshTokenToUserId` 等得到 `Expected: True, Actual: False`；`GetUserIdByRefreshToken_AfterLogin_ResolvesUserId` 得到 `Expected: "2024000001", Actual: ""`；refresh 请求得到 `Expected: OkObjectResult, Actual: BadRequestObjectResult`，错误为 `unsupported_grant_type`。原始日志 `/tmp/c21-c30-sso-red.log` |
| C23/C27 | `UpdateStaffAsync_ChangesDepartmentByName` 得到 `Expected: "设计部", Actual: "技术部"`；`UpdateRoleAsync_PromotesToPresident_ClearsDepartment`、`ImportRosterAsync_NewPresidentRow_IsNotAttachedToDepartment` 得到 `Expected: null, Actual: DepartmentDO 技术部`；`Update_OwnDepartmentChange_IsRejected` 得到 `Expected: Forbidden, Actual: OK`。补测 `UpdateStaffAsync_WithoutDepartmentName_KeepsExistingDepartment` 得到 `NullReferenceException`（部门被清空），`Update_OwnWithoutDepartmentName_IsStillAllowed` 得到 `Expected: OK, Actual: Forbidden`，`ImportAllDataAsync_DoesNotAttachPresidentToDepartment` 得到 `Expected: null, Actual: DepartmentDO`。原始日志 `/tmp/c21-c30-org-red.log`、`/tmp/c21-c30-lead-integration-red.log` 与该组 16 项失败一致 |
| C24 | 契约测试对旧 `Task<bool>` 报 `error CS1061: “bool”未包含“Id”的定义`；HTTP 用例得到 `Expected: 0, Actual: 2003`，即空 ID 组装 `CreatedAtAction` 失败被全局异常中间件接管。原始日志 `/tmp/c21-c30-content-red.log` |
| C25 | Department/Minister 创建文章得到 `Expected: OkObjectResult, Actual: CreatedAtActionResult`（旧实现直接建分类并成功），Department 更新得到 `Expected: 3001, Actual: 0`。原始日志同上 |
| C26 | 列表/详情一致性用例得到 `Assert.Equal() Failure: Strings differ Expected: "category-consistency" Actual: null`。原始日志同上 |
| C28 | 测试引用了新契约，旧代码报 `error CS1061: “bool”未包含“Created”的定义`、`error CS0103: 名称“BulkUpdateIssueVO”不存在`；前端用例得到 `Expected substring: "新增 1", Received: "数据更新成功"` 与 `Expected substring: "跳过 3", Received: "导入失败"`。原始日志 `/tmp/c21-c30-data-red.log`、`/tmp/c21-c30-frontend-red.log` |
| C29 | `StoreSession_RedisWriteFails_ReturnsServerError` 得到 `Expected: typeof(ObjectResult), Actual: typeof(OkObjectResult)`；前端 200+success:false 用例找不到“会话创建失败，请稍后重试”。原始日志同上 |
| C30 | `Performance_ReportsFixedWindowAndHonestMetricNames` 得到 `响应缺少 http.windowSeconds，调用方无法知道速率对应的时间窗口`；补测 `HttpStats_ReportsSameFixedWindowAndAverageName` 得到同一断言失败。原始日志 `/tmp/c21-c30-data-red.log`、`/tmp/c21-c30-c30-httpstats-red.log` |

## 修复后验证

后端全套测试最终结果。基线 `99d2e59` 为 431 项，本轮新增 61 项（含参数化拆分与既有用例调整，分布见上表“回归检查”列）。

```text
已通过! - 失败: 0，通过: 492，已跳过: 0，总计: 492，持续时间: 16 s
```

分组过滤结果：C21 6 项、C22/C29 18 项、C23/C27 131 项、C24/C25/C26 40 项、C28/C30 34 项、监控 9 项均通过；既有登录、会话撤销、部门导入与 Founder 保护测试无回归。

前端模拟接口 UI 回归：

```text
14 passed (15.0s)
```

覆盖 C11/C12 导出、C13 会话、C14 统计、C15 权限入口、C16 删除文案、C18 账号模型，以及本轮 C28 逐行反馈、C29 会话存储失败、C30 监控窗口标签。原始日志 `/tmp/c21-c30-frontend-green-final.log`。

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
SQLITE_DB_PATH=/tmp/iosclub-c21-c30-suite/data.db \
JWT_RSA_PRIVATE_KEY_PATH=/tmp/iosclub-p0-suite/private.pem \
JWT_RSA_PUBLIC_KEY_PATH=/tmp/iosclub-p0-suite/public.pem \
dotnet test rearend/iOSClub.WebAPI/iOSClub.Tests/iOSClub.Tests.csproj \
  --artifacts-path /tmp/iosclub-c21-c30-root-artifacts \
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

- **C21 决策为移除而不是修补。** 仓库中没有注册任何外部 OAuth 处理器，`/OAuth/login` 无法完成授权；保留一个必然失败的入口只会与 SSO 混淆。移除后 `/OAuth/*` 全部 404，属于对外契约的破坏性变更；仓库内前端、文档与测试均无该入口的调用方，外部客户端需要迁移到 `/SSO/authorize`。`docs/功能入口索引.md` 已重新生成，共 181 条声明。
- **C22 决策为让 refresh_token 真正可用，而不是删除该字段。** 刷新令牌仍是不透明 GUID，新增 `refresh-owner:{token}` 反向索引，TTL 与刷新令牌一致（72 小时）。`scope` 不随刷新令牌持久化，刷新签发统一使用 SSO 默认 scope；旧令牌在轮换后同时进入黑名单并失去反向索引。重复登录会覆盖规范键 `refresh:{userId}{client}`，旧反向索引条目会保留到 TTL 自然过期，但旧令牌无法兑换（仍需与规范键比对）。
- **C23 的规则是“社长不隶属任何部门”。** 部长/部员仍必须指定部门，Founder 的既有保护与名单导入拒绝规则未改。该规则不回填历史数据：库中已有的社长若残留部门，会在下一次经过上述任一写入路径时被清空；如需立即统一，需在部署前执行一次性数据清理（本轮未执行任何生产数据操作）。
- **C25 只收紧分类创建权限，不放宽分类写接口。** 显式分类写仍限 Founder/President；文章写入顺带建分类沿用同一角色边界。仓库层仍保留管理员自动创建能力的实现，非管理员路径由控制器统一拒绝。存在“先查后建”的并发窗口，属于既有实现范围，未引入锁。
- **C27 的契约语义是“提供才改，缺省不动”。** `DepartmentName` 缺省时保持原部门，清空部门继续由 `POST /Staff/change-department/{userId}` 承担（与“本人不能调整自己部门”的既有规则一致）。`StaffCreateDTO` 形状未变，前端 `StaffModel` 无需迁移。
- **C28 的失败仍走统一信封。** `ApiResponseResultFilter` 会让 HTTP 状态等于业务码，因此零写入失败实际是 HTTP 400 + `ErrorCode=2001`，`data` 仍带计数与原因；前端直接读取响应体而不是依赖 `apiRequest` 抛错。`SaveChanges` 失败按整批失败处理（关系型数据库下事务回滚），不把未落库的行计入新增/更新。响应类型由布尔改为对象，属于对第三方调用方的破坏性契约变更。
- **C29 只改变失败时的返回。** `State` 为空时保持原有成功语义；`State` 非法（Base64 或 JSON 解析失败）现在也会返回 500，而不再假装成功。前端在 HTTP 200 + `success:false` 时同样按失败处理，避免代理改写状态码时误判。
- **C30 固定窗口为 3600 秒。** `averageRequestsPerSecond` 只是“进程启动以来累计请求数 ÷ 3600”，不代表实时 QPS；`cpuSecondsTotal` 是累计 CPU 秒数，不是占用率。`/Monitoring/http-stats` 同批改名，避免同一指标在另一个端点继续误导；字段改名对外部监控消费者是破坏性变更。概览页本就没有 CPU 卡片，本轮未新增展示。
- 本轮没有执行部署、生产数据导出/导入、真实 OAuth/SSO 授权码兑换或 Redis 故障注入；所有验证均在隔离的 InMemory/SQLite 与 mocked-API 环境完成。`docs/功能入口索引.md` 已按当前源码重新生成。

采用 Fix Root Causes 原则：C21 删除无法完成的入口而不是保留跳转循环；C22 在令牌签发与轮换处维护反向索引，而不是在刷新端点扫描猜测用户；C23 用同一条“社长无部门”规则覆盖所有写路径（含数据导入），而不是只改人事调动；C24 让仓库返回真正持久化的对象；C30 让字段名与单位一致，而不是只改前端文案。采用 Test Behavior, Not Implementation 原则：后端断言 HTTP 状态、响应体、Redis 键生命周期与数据库状态；前端用真实路由与组件交互，只替换网络层。采用 Prove It Works 原则：保留修复前失败输出与修复后通过结果，并给出可复验命令。
