# P1 修复与 TDD 验证记录

日期 2026-10-25。对应 [冲突清单 C06 至 C10](功能冲突与待决策清单.md)。状态为本地修复并通过验证，尚未部署。

## 修复范围

| 编号 | 修复行为 | 回归检查 |
| --- | --- | --- |
| C06 | 日志读取只允许 Founder、President、Minister；清理（POST cleanup）只允许 Founder、President。前端日志页的清理按钮按同一规则隐藏，避免出现点击必然 403 的入口。 | [LogsAuthorizationTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/LogsAuthorizationTests.cs)，覆盖匿名、Member、Department、Minister、President、Founder 的读与清理结果，并验证 President 清理只删除截止日期之前的行 |
| C07 | 监控统计重置改为仅 Founder、President 可用；其余只读监控接口的公开性保持原状，留待部署与产品决策。 | [MonitoringAuthorizationTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/MonitoringAuthorizationTests.cs)，使用 Moq 替身验证未授权调用不会触达 `ResetStatisticsAsync` |
| C08 | 分类文章列表与分类详情复用 `ArticleAccessPolicy` 的可见性判定：匿名与低职级、其他部门用户不再从 `/Category/articles/{id}`、`/Category/{name}` 拿到无权限文章的标题和路径。 | [CategoryVisibilityTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/CategoryVisibilityTests.cs)，对匿名、Member、本部门 Department、Minister、President、Founder 断言标题集合，并与 `GET /Article/{path}` 详情结果逐篇对照 |
| C09 | 新增独立创建接口 `POST /MemberManagement/create`。管理角色可创建学生档案，初始密码由管理员设置（与注册一致：至少 8 位、含大写字母和数字）；学号重复返回业务错误码 2000 且不覆盖原档案；响应不回传密码或哈希。前端新增模式不再调用 update，改为调用创建接口。 | [MemberCreationTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/MemberCreationTests.cs)，覆盖创建、再次查询、初始密码登录、重复学号、非法密码、非管理角色与匿名拒绝 |
| C10 | 编辑保存后按 `userId` 定位列表行替换，不再按 `identity` 找首个同角色成员。新增/编辑模式由显式的 `editingUserId` 区分，不再依赖列表里恒为非空的 `identity`。 | 前端仓库没有可用的单元测试基座（见下文），以 `pnpm run typecheck` 与 `pnpm run build` 作为回归检查，并人工核对保存分支 |

## 修复前失败证据

以下均为实际测试输出节选，不是预计结果。每组都先只加测试、不改生产代码，得到预期原因的失败，再修改对应的生产实现。测试环境为 `Testing`，使用内存数据库、测试认证替身与内存 Redis 替身，不连接真实业务缓存。

| 范围 | 修复前结果 |
| --- | --- |
| C06、C07 | 新增 19 项中 10 项通过、9 项失败。`LogsAuthorizationTests.NonReaderRoles_CannotReadOrCleanup`（Member、Department）与 `NonCleanerRoles_CannotCleanup`（Member、Department、Minister）得到 `Assert.Equal() Failure ... Expected: Forbidden, Actual: OK`；`MonitoringAuthorizationTests.Anonymous_CannotResetDataStats` 得到 `Expected: Unauthorized, Actual: OK`，三个非管理角色重置统计得到 `Expected: Forbidden, Actual: OK`。说明原实现确实允许任意登录角色读写日志、允许匿名重置统计 |
| C08 | 新增 8 项中 2 项通过、6 项失败（仅 Founder、President 两个本应全见的分支通过）。匿名与 Member 的标题断言得到 `Expected: []`、`Actual: ["宣传部文章", "科技部文章", "部长专属"]`；科技部 Department 得到 `Expected: ["科技部文章"]`、`Actual: ["宣传部文章", "科技部文章", "部长专属"]`；泄漏断言得到 `Assert.DoesNotContain() Failure: Sub-string found ... String: ···"ster-only","title":"部长专属",...`。说明分类接口会直接暴露受限文章的路径和标题 |
| C09 | 新增 6 项全部失败。创建成功、重复学号、非法密码、非管理角色、匿名等断言分别得到 `Expected: OK / BadRequest / Forbidden / Unauthorized`，而 `Actual: NotFound`，因为 `POST /MemberManagement/create` 尚不存在；前端此前调用的是必然失败的 update |
| C10 | 未取得修复前的自动化失败证据。原因：前端仓库虽有 `vitest.config.ts`，但未安装 `vitest`、没有测试文件，E2E 需要同时运行前后端和种子数据。按 TDD 约束不为这个单行定位错误引入新测试框架，改用 `pnpm run typecheck`、`pnpm run build` 与保存分支的人工核对作为最接近的回归检查 |

C09 的"再次查询"走真实读取路径：创建后调用 `GET /MemberQuery/all-data/page` 按学号搜索，并对 GZip 压缩响应解压后断言成员存在、身份为 Member。C06 的清理断言直接检查 Testing 环境 Serilog 在进程工作目录创建的 `logs/log.db`，只保留唯一前缀的测试行，并在测试结束时清理。

## 修复后验证

最终对整个后端测试项目运行一次完整检查，结果如下。基线 `700f696` 为 390 项，本轮新增 33 项（C06/C07 19 项、C08 8 项、C09 6 项）。

```text
已通过! - 失败: 0，通过: 423，已跳过: 0，总计: 423，持续时间: 13 s
```

分组过滤运行结果：C06/C07 19 项全过；C08 8 项全过，且既有 `CategoryRepositoryTests` 8 项、文章可见性相关 46 项无回归；C09 6 项全过。C06/C07 另过滤运行受影响的 `StaffSecurityRegressionTests`、`SessionAuthorizationTests`、`AuthControllerTests`，59 项全过。

前端在 `frontend/` 下运行 `pnpm run typecheck`（vue-tsc --noEmit）与 `pnpm run build`，两条命令均成功，构建产物正常生成。

可从仓库根目录使用以下命令重新验证。Testing 环境避免启动阶段读取开发 Redis 配置，密钥和数据库路径与业务环境分离。

```sh
DOTNET_ENVIRONMENT=Testing \
ASPNETCORE_ENVIRONMENT=Testing \
REDIS='' \
SQLITE_DB_PATH=/tmp/iosclub-p1-suite/data.db \
JWT_RSA_PRIVATE_KEY_PATH=/tmp/iosclub-p0-suite/private.pem \
JWT_RSA_PUBLIC_KEY_PATH=/tmp/iosclub-p0-suite/public.pem \
dotnet test rearend/iOSClub.WebAPI/iOSClub.Tests/iOSClub.Tests.csproj \
  --artifacts-path /tmp/iosclub-p1-root-artifacts \
  --logger 'console;verbosity=normal'
```

```sh
cd frontend && pnpm run typecheck && pnpm run build
```

本次临时原始日志保存在本机 `/tmp/iosclub-c06-c07-red.log`、`/tmp/iosclub-c06-c07-green.log`、`/tmp/iosclub-c06-c07-existing.log`、`/tmp/iosclub-c08-red.log`、`/tmp/iosclub-c08-green.log`、`/tmp/iosclub-c08-categoryrepo.log`、`/tmp/iosclub-c09-red.log`、`/tmp/iosclub-c09-green.log` 和 `/tmp/iosclub-c06-c10-final.log`。这些文件不随仓库分发，持久证据为回归测试及本记录。

## 兼容性和边界

- C06：日志读取角色由"任意登录用户"收紧为 Founder、President、Minister；清理为 Founder、President。匿名 401，Member 与 Department 403。外部运维脚本如需清理日志，必须使用负责人会话；本轮没有执行生产日志清理，也未改变日志落库格式。
- C07：重置统计需要 Founder 或 President 会话，未授权请求不会再触达统计服务。只读监控接口的匿名可访问性没有改变，因为这属于部署与产品决策；当前前端没有调用重置接口的入口。
- C08：两个公开分类接口改为按访问者过滤文章，响应 JSON 结构不变（仍为标题、路径、排序字段），只减少无权限条目。Founder/President 使用的后台分类设置页仍能看到全部文章。过滤在内存中完成，分类文章数量极大时读取成本略增，与既有 `ArticleRepository` 的过滤风格一致；分类详情接口 `GET /Category/byId/{id}` 不加载文章，因此未改动。
- C09：产品决策为"支持管理员创建账号"。创建只写学生档案，不自动创建 Staff 档案，新成员身份按现有规则为 Member；初始密码由管理员在页面设置，明文只在提交请求中出现，服务端存 BCrypt 哈希，响应不含密码字段。学号重复返回业务错误码 2000（HTTP 400）并保持原档案不变。既有 update、update-many、reset-password 接口行为未变；本轮未迁移或批量创建生产账号。
- C10：编辑保存后仅替换 `userId` 命中行，其他同角色成员不受影响；`identity`、`joinTime` 仍由列表原值保留，因为更新 DTO 不包含这两个字段。前端回归仅覆盖类型检查与构建，未新增浏览器自动化。
- 前端 `LogsViewer.vue` 的清理按钮对非 Founder/President 隐藏，属于 C06 的界面一致性调整；接口本身仍然是权限边界。
- 本轮修复限定 C06 至 C10。C11 至 C30 仍保留在冲突清单中。没有执行部署、生产清理、统计重置或外部数据写入。
- `docs/功能入口索引.md` 已用 `python3 docs/tools/inventory_features.py` 从当前源码重新生成，新增 `POST /MemberManagement/create` 并刷新了行号。

采用 Fix Root Causes 原则：C09 将"创建"从以学号为定位键的更新接口中拆出，而不是在 update 里加隐式插入；C08 复用既有 `ArticleAccessPolicy`，而不是维护第二套可见性规则。采用 Test Behavior, Not Implementation 原则：后端断言 HTTP 结果、响应体与数据库状态，C06 还断言清理的实际删除范围。采用 Prove It Works 原则：保留失败前与通过后的实际执行证据，并给出可复验命令。
