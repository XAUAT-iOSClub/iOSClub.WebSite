# SSO集成指南

本文档详细说明了如何在iOS Club系统中集成OAuth2登录和第三方应用SSO功能。

## 概述


iOS Club系统支持标准OAuth2协议的单点登录(SSO)功能，允许第三方应用通过OAuth2授权码流程集成系统。系统提供了完整的OAuth2授权服务器实现，包括客户端管理、授权码生成、令牌交换和用户信息获取等功能。

## 1. OAuth2登录方法

### 1.1 登录流程（示例）

以下是一个标准的OAuth2授权码流程示例：

1. 用户访问第三方应用，点击"使用iOS Club账户登录"
2. 第三方应用将用户重定向到iOS Club系统的授权端点：
   ```
   GET /SSO/authorize?client_id=CLIENT_ID&redirect_uri=REDIRECT_URI&state=STATE&response_type=code&scope=profile email
   ```
3. 系统显示登录页面，用户输入凭据或使用已有的会话
4. 用户认证成功后，系统将用户重定向回第三方应用的回调地址：
   ```
   REDIRECT_URI?code=AUTHORIZATION_CODE&state=STATE
   ```
5. 第三方应用使用授权码向系统请求访问令牌：
   ```
   POST /SSO/token
   grant_type=authorization_code&code=AUTHORIZATION_CODE&client_id=CLIENT_ID&client_secret=CLIENT_SECRET&redirect_uri=REDIRECT_URI
   ```
6. 系统验证授权码并返回访问令牌：
   ```json
   {
     "access_token": "ACCESS_TOKEN",
     "token_type": "Bearer",
     "expires_in": 7200,
     "scope": "profile email"
   }
   ```
7. 第三方应用使用访问令牌获取用户信息：
   ```
   GET /SSO/userinfo
   Authorization: Bearer ACCESS_TOKEN
   ```

### 1.2 核心代码实现（示例）

以下是一个典型的OAuth2登录实现示例：

```csharp
[HttpGet("login")]
public IActionResult Login(string provider = "OAuth2", string? returnUrl = "/")
{
    // 构建回调URL
    var redirectUrl = Url.Action("Callback", "OAuth", new { returnUrl });
    var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
    
    // 发起认证挑战，将用户重定向到OAuth提供商
    return Challenge(properties, provider);
}

[HttpGet("callback")]
public async Task<IActionResult> Callback(string? returnUrl = "/")
{
    // 处理OAuth提供商的回调
    var authenticateResult = await HttpContext.AuthenticateAsync("OAuth2");

    if (!authenticateResult.Succeeded)
        return BadRequest("OAuth2 认证失败");

    var claims = authenticateResult.Principal?.Claims;
    if (claims == null)
        return BadRequest("无法获取用户信息");

    // 从OAuth提供商返回的声明中提取用户信息
    var userId = claims.FirstOrDefault(c => c.Type == "sub")?.Value;
    var userName = claims.FirstOrDefault(c => c.Type == "name")?.Value;
    var email = claims.FirstOrDefault(c => c.Type == "email")?.Value;

    if (string.IsNullOrEmpty(userId))
        return BadRequest("无法获取用户ID");

    // 使用获取的用户信息生成系统内部令牌
    var token = GenerateInternalToken(userId, userName, email);
    
    if (string.IsNullOrEmpty(token))
        return BadRequest("用户登录失败");

    // 将令牌返回给客户端
    return Ok(new { Token = token, ReturnUrl = returnUrl });
}

private string GenerateInternalToken(string userId, string userName, string email)
{
    // 这里是生成内部JWT令牌的逻辑
    // 实际实现会根据系统需求而有所不同
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.ASCII.GetBytes("your-secret-key");
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Email, email)
        }),
        Expires = DateTime.UtcNow.AddHours(2),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    return tokenHandler.WriteToken(token);
}
```

### 1.3 令牌交换接口（示例）

以下是一个典型的令牌交换接口示例，允许通过用户信息直接获取JWT令牌：

```csharp
[HttpPost("exchange")]
public async Task<IActionResult> ExchangeToken([FromBody] OAuthExchangeRequest request)
{
    // 验证请求参数
    if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.UserName))
        return BadRequest("用户ID和用户名不能为空");

    // 验证外部令牌（如果提供了外部访问令牌）
    if (!string.IsNullOrEmpty(request.AccessToken))
    {
        var isValid = await ValidateExternalToken(request.AccessToken);
        if (!isValid)
            return BadRequest("外部访问令牌无效");
    }

    // 生成内部JWT令牌
    var token = GenerateInternalToken(request.UserId, request.UserName, request.Email);
    
    if (string.IsNullOrEmpty(token))
        return BadRequest("令牌生成失败");

    return Ok(new { Token = token });
}

private async Task<bool> ValidateExternalToken(string accessToken)
{
    // 这里是验证外部OAuth提供商令牌的逻辑
    // 实际实现会根据外部OAuth提供商的API而有所不同
    
    try
    {
        // 调用外部OAuth提供商的API验证令牌
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync($"https://oauth-provider.com/oauth2/userinfo?access_token={accessToken}");
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}
```

## 2. 第三方应用SSO集成

### 2.1 SSO架构

系统支持OAuth2标准的授权码流程，允许第三方应用通过SSO集成访问系统资源。

### 2.2 核心组件

1. **ClientApplication实体** - 存储第三方应用信息
2. **IClientApplicationRepository** - 管理客户端应用数据
3. **SSOController** - 处理SSO流程
4. **ClientAppController** - 管理客户端应用

### 2.3 SSO流程

1. **应用注册**：管理员通过ClientAppController注册第三方应用
2. **授权请求**：第三方应用将用户重定向到`/SSO/authorize`
3. **用户认证**：系统将用户重定向到OAuth提供商
4. **回调处理**：认证成功后，系统发放授权码
5. **令牌交换**：第三方应用使用授权码换取访问令牌
6. **资源访问**：第三方应用使用令牌访问受保护资源

### 2.4 授权端点

```
GET /SSO/authorize
```

参数：
- `client_id` - 第三方应用的客户端ID
- `redirect_uri` - 回调地址
- `state` - 用于防止CSRF攻击的随机字符串
- `response_type` - 响应类型（默认为code）
- `scope` - 请求的权限范围（支持openid, profile, email, read, phone）
- `code_challenge` - PKCE代码挑战（可选）
- `code_challenge_method` - PKCE代码挑战方法（可选，支持S256或plain）

### 2.5 令牌端点

```
POST /SSO/token
```

参数：
- `grant_type` - 授权类型（必须为authorization_code）
- `code` - 授权码
- `client_id` - 客户端ID
- `client_secret` - 客户端密钥
- `redirect_uri` - 回调地址
- `code_verifier` - PKCE代码验证器（如果使用了PKCE）

### 2.6 用户信息端点

```
GET /SSO/userinfo
```

需要在请求头中包含：
```
Authorization: Bearer ACCESS_TOKEN
```

根据访问令牌中的scope返回不同的用户信息：

| Scope | 返回字段 | 说明 |
|-------|---------|------|
| `openid` | `sub` | 用户ID |
| `profile` | `name`, `nickname`, `academy`, `class` | 用户基本信息 |
| `email` | `email` | 用户邮箱地址 |
| `read` | `role` | 用户角色 |
| `phone` | `phone` | 用户电话号码 |

示例返回：
```json
{
  "sub": "用户ID",
  "name": "用户名",
  "nickname": "昵称",
  "academy": "学院",
  "class": "班级",
  "email": "邮箱",
  "role": "用户角色",
  "phone": "电话号码"
}
```

### 2.7 安全特性

1. **客户端验证**：每个第三方应用都有唯一的ClientId和ClientSecret
2. **回调URL白名单**：防止重定向攻击
3. **角色权限控制**：只有特定角色可以管理客户端应用
4. **令牌管理**：支持令牌的生成、验证和撤销
5. **PKCE支持**：支持Proof Key for Code Exchange增强安全性
6. **Scope控制**：根据请求的权限范围返回相应的用户信息

## 3. 使用示例

### 3.1 第三方应用集成步骤

1. 联系系统管理员注册应用，获取ClientId和ClientSecret
2. 将用户重定向到授权端点：
   ```
   GET /SSO/authorize?client_id=CLIENT_ID&redirect_uri=REDIRECT_URI&state=STATE&response_type=code&scope=profile email
   ```
3. 在回调URL中接收授权码
4. 使用授权码换取访问令牌：
   ```
   POST /SSO/token
   grant_type=authorization_code&code=AUTHORIZATION_CODE&client_id=CLIENT_ID&client_secret=CLIENT_SECRET&redirect_uri=REDIRECT_URI
   ```
5. 使用访问令牌访问用户信息：
   ```
   GET /SSO/userinfo
   Authorization: Bearer ACCESS_TOKEN
   ```

### 3.2 管理员操作

管理员可以使用以下端点管理客户端应用：

1. 获取所有客户端应用：`GET /ClientApp`
2. 获取特定客户端应用：`GET /ClientApp/{clientId}`
3. 创建客户端应用：`POST /ClientApp`
4. 更新客户端应用：`PUT /ClientApp/{clientId}`
5. 删除客户端应用：`DELETE /ClientApp/{clientId}`
6. 重新生成客户端密钥：`POST /ClientApp/{clientId}/regenerate-secret`
7. 获取客户端应用信息：`GET /SSO/client-info?client_id=CLIENT_ID`

## 4. 注意事项

1. 在生产环境中，应使用更安全的密码管理机制替代默认密码
2. 应定期更新客户端密钥以提高安全性
3. 回调URL必须在白名单中，防止重定向攻击
4. 授权码和访问令牌应有适当的过期时间
5. 建议使用PKCE增强安全性，特别是对于公共客户端
6. scope参数允许应用只请求需要的用户信息，遵循最小权限原则