# P0 修复与 TDD 验证记录

日期 2026-10-25。对应 [冲突清单 C01 至 C05](功能冲突与待决策清单.md)。状态为本地修复并通过验证，尚未部署。

## 修复范围

| 编号 | 修复行为 | 回归检查 |
| --- | --- | --- |
| C01 | 删除主站和 OAuth exchange 的姓名加学号备用登录，删除 StaffLogin 接口与实现。干部仍可通过 Student 密码验证取得 Staff 角色 | [AuthControllerTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/AuthControllerTests.cs) 的姓名加学号拒绝用例和合法干部密码用例 |
| C02 | 常规 Staff 路径不能创建、提升为、修改、删除或移动 Founder。HTTP 接口拒绝修改自己的角色、删除自己或调整自己的部门。Founder 专用调权接口保留 | [StaffSecurityRegressionTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/StaffSecurityRegressionTests.cs)、[StaffRepositoryTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/RepositoryTests/StaffRepositoryTests.cs) |
| C03 | 导入和回滚共用覆盖前检查，按规范化传入学号查找全局 Founder，拒绝覆盖目标部门、其他部门或无部门 Founder | [DepartmentImportFounderSecurityTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/ServiceTests/DepartmentImportFounderSecurityTests.cs)，验证拒绝后名单、学生档案与历史不变 |
| C04 | 签名 JWT 还必须对应有效 Redis 会话。失效或校验异常时拒绝受保护操作。公开接口上的失效登录身份清为空，按匿名继续 | [SessionAuthorizationTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/SessionAuthorizationTests.cs)，覆盖登出、令牌替换、Redis 故障和真实受限文章读取 |
| C05 | 缓存清理仅接受 Founder 的 `POST /clean`。旧 GET 返回 405，未登录 POST 返回 401，其他角色返回 403 | [SessionAuthorizationTests](../rearend/iOSClub.WebAPI/iOSClub.Tests/IntegrationTests/SessionAuthorizationTests.cs)，同时验证拒绝请求不清会话、合法请求不清其他前缀 |

## 修复前失败证据

以下是实际测试输出节选，不是预计结果。每组均先增加测试并得到预期原因的失败，再修改对应生产实现。

| 范围 | 修复前结果 |
| --- | --- |
| C01 | 10 个新增用例中 4 个拒绝用例失败，6 个合法登录用例通过。`Assert.False() Failure`，`Expected: False`，`Actual: True`，说明姓名加学号请求实际返回成功 |
| C02 | HTTP 回归 24 项中 22 项失败。非法角色写入实际返回成功。仓库保护断言同样得到 `Expected: False`，`Actual: True` |
| C03 | 首批 6 项中 4 项失败。其他部门和无部门 Founder 覆盖没有被拒绝，输出 `Assert.Throws() Failure: No exception was thrown`。随后补充空名单保护，总计 7 项 |
| C04、C05 | 首批会话与缓存 HTTP 回归 12 项中 11 项失败。登出和令牌替换后的受保护请求得到 `Expected: Unauthorized`，`Actual: OK`。旧缓存 GET 得到 `Expected: MethodNotAllowed`，`Actual: OK` |
| C04 可选登录 | 独立复核发现公开文章仍复用已撤销 Founder 身份。新增真实文章 HTTP 用例得到 `Expected: NotFound`，`Actual: OK`。AllowAnonymous 身份检查得到 `Expected: False`，`Actual: True`。两项均在扩大过滤器覆盖前失败 |

会话回归使用真实 RSA 签名、JWT 认证中间件、全局过滤器和 LoginService。Redis 通过可读写的内存替身隔离，不连接真实业务缓存。受限文章验证使用真实控制器、仓库和 EF 内存数据库。

测试搭建阶段曾遇到开发环境自动连接 Redis，以及 Redis 新重载未被替身匹配的问题。这些运行不计入失败前证据。完成测试隔离及有效会话前置检查后，重新验证了上述失败。修复前重放仅临时还原本次修改的过滤器和缓存控制器，测试结束后恢复修复文件。

## 修复后验证

最终对整个后端测试项目运行一次完整检查，结果如下。

```text
测试运行成功。
测试总数: 390
     通过数: 390
```

最终全套结果包含全部新增回归、原有认证、文章权限、个人资料、成员仓库、部门导入、数据交换和活动测试。没有弱化原有业务断言。常规 Staff 创建的原正向测试改用 President，并新增 Founder 拒绝断言，以匹配新的安全契约。

可从仓库根目录使用以下命令重新验证。Testing 环境避免启动阶段读取开发 Redis 配置。密钥和数据库路径与业务环境分离。

```sh
DOTNET_ENVIRONMENT=Testing \
ASPNETCORE_ENVIRONMENT=Testing \
REDIS='' \
SQLITE_DB_PATH=/tmp/iosclub-p0-suite/data.db \
JWT_RSA_PRIVATE_KEY_PATH=/tmp/iosclub-p0-suite/private.pem \
JWT_RSA_PUBLIC_KEY_PATH=/tmp/iosclub-p0-suite/public.pem \
dotnet test rearend/iOSClub.WebAPI/iOSClub.Tests/iOSClub.Tests.csproj \
  --artifacts-path /tmp/iosclub-p0-root-artifacts \
  --logger 'console;verbosity=normal'
```

本次临时原始日志保存在本机 `/tmp/iosclub-p0-login-red.log`、`/tmp/iosclub-p0-staff-red.log`、`/tmp/iosclub-p0-staff-repository-red.log`、`/tmp/iosclub-p0-import-red.log`、`/tmp/iosclub-p0-session-red.log`、`/tmp/iosclub-p0-optional-session-red.log` 和 `/tmp/iosclub-p0-suite-final.log`。这些文件不随仓库分发，持久证据为回归测试及本记录。

## 兼容性和边界

- Staff-only 旧账号不能再用姓名加学号登录。继续使用的账号需要建立 Student 档案和真实密码。本轮未自动创建账号、分配密码或迁移生产数据。
- 常规 Staff 维护不再用于 Founder 角色管理，需走现有 Founder 专用权限入口。
- 缓存清理的请求方法和权限改变。当前仓库前端未发现 `/clean` 调用方。外部运维调用需改为有效 Founder 会话下的 POST。
- Redis 无法确认会话时，受保护接口拒绝操作。公开接口继续匿名访问，不能使用原登录职级读取受限内容。
- 本轮修复限定 C01 至 C05。日志权限、统计重置、分类可见性等 P1、P2 项仍保留在冲突清单中。没有执行生产清理、部署或外部数据写入。

采用 Fix Root Causes 原则，将干部登录的两个入口一并删除，并让可选登录遵守会话撤销规则。采用 Test Behavior, Not Implementation 原则，断言 HTTP 结果和数据库状态。采用 Prove It Works 原则，保留失败前与通过后的实际执行证据。
