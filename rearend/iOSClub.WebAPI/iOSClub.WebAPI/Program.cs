using System.Data;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using FluentValidation.AspNetCore;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.Data.Mappers;
using iOSClub.DataApi.Services;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Common.Config;
using iOSClub.WebAPI.Common.Extensions;
using iOSClub.WebAPI.Common.Middleware;
using iOSClub.WebAPI.Common.Security;
using iOSClub.WebAPI.IdentityModels;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using NpgsqlDataProtection;
using ParadeDB.EntityFrameworkCore.Extensions;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using StackExchange.Redis;
using DotEnv.Core;

new EnvLoader().Load();

var builder = WebApplication.CreateBuilder(args);

// Only accept forwarded client IP headers from explicitly configured reverse proxies.
// This keeps rate limiting per real client without allowing arbitrary clients to spoof
// X-Forwarded-For and avoids merging all users behind an untrusted proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    var configuredProxyIps = builder.Configuration.GetSection("Security:TrustedProxies")
        .GetChildren()
        .Select(x => x.Value)
        .Concat((Environment.GetEnvironmentVariable("TRUSTED_PROXY_IPS") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    foreach (var proxyIp in configuredProxyIps.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (IPAddress.TryParse(proxyIp, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }
});

// 配置Mapster全局映射
MapperConfig.Configure();

#region 控制器基本设置

// 配置请求大小限制
builder.Services.Configure<FormOptions>(options =>
{
    // 设置请求体大小上限为10MB
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024;
    options.ValueLengthLimit = 10 * 1024 * 1024;
    options.BufferBodyLengthLimit = 10 * 1024 * 1024;
});

// 配置Kestrel服务器的请求大小限制
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// 注册FluentValidation服务（使用旧版API，抑制警告）
#pragma warning disable CS0618
var mvcBuilder = builder.Services.AddControllers(options =>
{
    options.Filters.Add<GlobalAuthorizationFilter>();
    // 统一让 HTTP 状态码跟随响应体里的 code（详见 ApiResponseResultFilter）
    options.Filters.Add<ApiResponseResultFilter>();
});

// [ApiController] 默认在 DataAnnotations 校验失败时返回 RFC-7807 的 ValidationProblemDetails
// （字段是 type/title/status/errors，没有 code/errorCode/message），前端无法识别。
// 这里改写成统一信封，逐字段错误格式化后放进 detail。
// 注意不要把结构化错误塞进 data——那会破坏 ApiResponse<T> 的泛型契约
// （形如 ApiResponse<string> 的调用方会因 data 变成对象而反序列化失败）。
mvcBuilder.ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        // ModelState 的 key 形如 "dto.UserId"、"$.userId" 或 ""（整个 body 绑定失败）
        static string FieldName(string key)
        {
            var trimmed = key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
            trimmed = trimmed.TrimStart('$', '.');
            return trimmed.Length == 0
                ? "body"
                : char.ToLowerInvariant(trimmed[0]) + trimmed[1..];
        }

        var errors = context.ModelState
            .Where(kv => kv.Value?.Errors.Count > 0)
            .GroupBy(kv => FieldName(kv.Key))
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(kv => kv.Value!.Errors)
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "参数值无效" : e.ErrorMessage)
                    .Distinct()
                    .ToArray());

        var detail = string.Join("; ",
            errors.SelectMany(e => e.Value.Select(m => $"{e.Key}: {m}")));

        var body = ApiResponse<object>.Fail(
            ErrorCode.ParameterValidationFailed,
            "请求参数验证失败",
            detail,
            context.HttpContext.TraceIdentifier);

        return new ObjectResult(body) { StatusCode = body.Code };
    };
});

mvcBuilder.AddFluentValidation(fv =>
{
    fv.RegisterValidatorsFromAssemblyContaining<Program>();
    fv.DisableDataAnnotationsValidation = false;
});
#pragma warning restore CS0618

builder.Services.AddOpenApi(opt => { opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>(); });

#endregion

#region JWT配置和密钥管理

var jwtConfig = new JwtConfig
{
    AccessTokenExpiryMinutes =
        int.TryParse(Environment.GetEnvironmentVariable("JWT_ACCESS_TOKEN_EXPIRY_MINUTES"), out var accessTokenExpiry)
            ? accessTokenExpiry
            : 20,
    RefreshTokenExpiryHours =
        int.TryParse(Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_EXPIRY_HOURS"), out var refreshTokenExpiry)
            ? refreshTokenExpiry
            : 72,
    RsaPrivateKeyPath = Environment.GetEnvironmentVariable("JWT_RSA_PRIVATE_KEY_PATH") ?? "./app/keys/rsa_private.pem",
    RsaPublicKeyPath = Environment.GetEnvironmentVariable("JWT_RSA_PUBLIC_KEY_PATH") ?? "./app/keys/rsa_public.pem",
    Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "iOS Club of XAUAT",
    Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "iOS Club of XAUAT",
    KeyRotationDays = int.TryParse(Environment.GetEnvironmentVariable("JWT_KEY_ROTATION_DAYS"), out var keyRotationDays)
        ? keyRotationDays
        : 90
};

builder.Services.AddSingleton(jwtConfig);
builder.Services.AddSingleton<RsaKeyManager>();
builder.Services.AddSingleton<JwtService>();

#endregion

#region 身份验证

builder.Services.AddAuthorizationCore();
builder.Services.AddSingleton<IAuthorizationHandler, FounderAuthorizationHandler>();

// 配置JWT认证 - 注意：在测试环境中，我们将使用服务注入的方式获取RsaKeyManager，
// 而不是直接创建实例，这样可以允许测试代码替换该服务
var rsaKeyManager = new RsaKeyManager(jwtConfig,
    LoggerFactory.Create(loggingBuilder => loggingBuilder.AddConsole()).CreateLogger<RsaKeyManager>());

// 尝试确保密钥有效，但如果失败（例如在测试环境中），我们将使用临时密钥
RSAParameters rsaParams;
try
{
    rsaKeyManager.EnsureKeysValid();
    var publicKey = rsaKeyManager.GetCurrentPublicKey();
    rsaParams = publicKey.ExportParameters(false);
}
catch (Exception)
{
    // 在测试环境或无法访问文件系统的环境中，生成临时密钥
    using var rsa = RSA.Create(2048);
    rsaParams = rsa.ExportParameters(false);
}

// 从RSA密钥中导出公钥的SHA256哈希值作为KeyId，与生成令牌时使用的KeyId保持一致
var publicKeyBytes = RSA.Create(rsaParams).ExportRSAPublicKey();
var keyId = Convert.ToBase64String(SHA256.HashData(publicKeyBytes)).Substring(0, 16);
var rsaSecurityKey = new RsaSecurityKey(rsaParams) { KeyId = keyId };

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = rsaSecurityKey,
            ValidateIssuer = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtConfig.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                // 可以在这里添加额外的验证逻辑
                context.Success();
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                // 只记录日志，不要在这里写响应体：challenge 随后还会设置一次状态码，
                // 而响应一旦开始写入就无法再改，会抛 "response has already started"。
                // 响应统一由下面的 OnChallenge 产出。
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearer");
                logger.LogInformation(context.Exception, "JWT 认证失败: {Path}", context.Request.Path);
                context.NoResult();
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                // 接管框架默认的空 401 响应。OnChallenge 对"完全没带 token"的请求同样会触发，
                // 而 OnAuthenticationFailed 不会，所以这里是唯一能覆盖全部 401 场景的点。
                context.HandleResponse();

                var (errorCode, message) = context.AuthenticateFailure switch
                {
                    SecurityTokenExpiredException => (ErrorCode.LoginExpired, "登录已过期，请重新登录"),
                    SecurityTokenInvalidSignatureException => (ErrorCode.InvalidToken, "令牌签名无效"),
                    SecurityTokenNotYetValidException => (ErrorCode.InvalidToken, "令牌尚未生效"),
                    SecurityTokenInvalidIssuerException => (ErrorCode.InvalidToken, "令牌签发者无效"),
                    SecurityTokenInvalidAudienceException => (ErrorCode.InvalidToken, "令牌受众无效"),
                    _ => (ErrorCode.InvalidToken, "无效的令牌")
                };

                var body = ApiResponse.Fail(errorCode, message,
                    requestId: context.HttpContext.TraceIdentifier);

                context.Response.StatusCode = body.Code;
                context.Response.ContentType = "application/json";
                // 遵循 RFC 6750：即使响应体由我们接管，也保留标准的 WWW-Authenticate 头
                context.Response.Headers.Append("WWW-Authenticate", "Bearer error=\"invalid_token\"");
                await context.Response.WriteAsJsonAsync(body);
            }
        };
    })
    .AddCookie("OAuth2", options =>
    {
        options.LoginPath = "/OAuth/login";
        options.LogoutPath = "/OAuth/logout";
        options.AccessDeniedPath = "/OAuth/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.None;
    });

#endregion

#region 会话支持

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None; // 允许跨站点发送Cookie
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // 根据请求类型决定是否使用安全Cookie
});

// 使用Redis分布式缓存
builder.Services.AddStackExchangeRedisCache(options =>
{
    var redis = Environment.GetEnvironmentVariable("REDIS", EnvironmentVariableTarget.Process);
    if (string.IsNullOrEmpty(redis) && builder.Environment.IsDevelopment())
    {
        redis = builder.Configuration["Redis"];
    }

    if (!string.IsNullOrEmpty(redis))
    {
        options.Configuration = redis;
        // 设置实例名称为空，避免key前缀导致与IConnectionMultiplexer不一致
        options.InstanceName = null;
    }
});

// 添加内存缓存（用于本地缓存层）
builder.Services.AddMemoryCache();

#endregion

#region 跨域设置

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                origin.EndsWith(".zeabur.app") || // 支持所有 zeabur.app 子域名
                origin.EndsWith(".xauat.site") || // 支持所有 xauat.site 子域名
                origin.StartsWith("http://localhost")) // 支持本地开发环境
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials() // 如果需要发送凭据（如cookies、认证头等）
            .WithExposedHeaders("X-Refresh-Token"); // 允许前端访问X-Refresh-Token响应头
    });
});

#endregion

#region 数据库设置

var sql = Environment.GetEnvironmentVariable("SQL", EnvironmentVariableTarget.Process);

if (string.IsNullOrEmpty(sql))
{
    sql = builder.Configuration["SQL"];
}

if (string.IsNullOrEmpty(sql))
{
    builder.Services.AddDbContextFactory<ClubContext>(opt =>
    {
        opt.UseSqlite("Data Source=Data.db",
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
        opt.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    });

    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("./keys"));
}
else
{
    builder.Services.AddDbContextFactory<ClubContext>(opt =>
    {
        opt.UseNpgsql(sql,
            o =>
            {
                o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                o.UseParadeDb();
            });
        opt.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    });

    builder.Services.AddDataProtection()
        .PersistKeysToPostgres(sql, true);
}

var redis = Environment.GetEnvironmentVariable("REDIS", EnvironmentVariableTarget.Process);
if (string.IsNullOrEmpty(redis) && builder.Environment.IsDevelopment())
{
    redis = builder.Configuration["Redis"];
}

if (!string.IsNullOrEmpty(redis))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redis));
}

#endregion

#region 日志设置

if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddConsole();
}
else
{
    // 定义日志数据库路径
    var logPath = Path.Combine(Environment.CurrentDirectory, "logs", "log.db");

    // 确保日志目录存在
    var logDir = Path.GetDirectoryName(logPath);
    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
    {
        Directory.CreateDirectory(logDir);
    }

    // 统一日志配置，适用于所有环境
    var logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .Enrich.With<SensitiveDataFilter>()
        .WriteTo.Console(
            outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.SQLite(
            sqliteDbPath: logPath,
            tableName: "Logs")
        .WriteTo.File(
            Path.Combine(logDir ?? Environment.CurrentDirectory, "log-.txt"),
            rollingInterval: RollingInterval.Day,
            outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj} {Properties:j}{NewLine}{Exception}")
        .CreateLogger();

    builder.Logging
        .ClearProviders()
        .AddConsole()
        .AddDebug()
        .SetMinimumLevel(LogLevel.Information)
        .AddSerilog(logger);
}

#endregion

#region 仓库和服务的依赖注入

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<GlobalAuthorizationFilter>();
// 注册ITokenGenerator服务
builder.Services.AddScoped<ITokenGenerator, JwtGenerator>();

// 使用扩展方法注册服务
builder.Services.RegisterRepositoriesAndServices();
builder.Services.RegisterSecurityServices();

#endregion

#region 压缩

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest; // 或 CompressionLevel.Optimal
});

builder.Services.Configure<GzipCompressionProviderOptions>(options => { options.Level = CompressionLevel.Fastest; });

#endregion

#region Prometheus 监控

// Prometheus 指标收集不需要 AddMetricServer
// 使用 UseHttpMetrics() 和 MapMetrics() 来配置指标端点

#endregion

var app = builder.Build();

// Must run before RateLimitMiddleware so only trusted proxy headers affect the client IP.
app.UseForwardedHeaders();

// 注册全局异常处理中间件
app.UseMiddleware<GlobalExceptionMiddleware>();

// 注册请求频率限制中间件
app.UseMiddleware<RateLimitMiddleware>();

// 注册数据脱敏中间件
app.UseDataMasking();

// 配置安全响应头
app.Use(async (context, next) =>
{
    // 添加内容安全策略，防止XSS攻击
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; object-src 'none'; frame-ancestors 'none';");

    // 添加X-XSS-Protection头
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

    // 添加X-Content-Type-Options头，防止MIME嗅探
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

    // 添加X-Frame-Options头，防止点击劫持
    context.Response.Headers.Append("X-Frame-Options", "DENY");

    // 添加Referrer-Policy头
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

    // 添加Permissions-Policy头
    context.Response.Headers.Append("Permissions-Policy",
        "camera=(), microphone=(), geolocation=(), payment=(), fullscreen=*");

    // 添加Strict-Transport-Security头（生产环境建议启用）
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }

    await next();
});

// 优化数据库迁移策略，异步执行迁移
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ClubContext>();

    try
    {
        var pending = context.Database.GetPendingMigrations();
        var enumerable = pending as string[] ?? pending.ToArray();

        if (enumerable.Length != 0)
        {
            Console.WriteLine("Pending migrations: " + string.Join("; ", enumerable));
            await context.Database.MigrateAsync();
            Console.WriteLine("Migrations applied successfully.");
        }
        else
        {
            Console.WriteLine("No pending migrations.");
        }

        // 初始化数据
        if (!await context.Staffs.AnyAsync())
        {
            var user = Environment.GetEnvironmentVariable("USER", EnvironmentVariableTarget.Process);
            Console.WriteLine(user);
            var model = new StaffDO() { Identity = "Founder", Name = "root", UserId = "0000000000" };
            var users = user?.Split(',');
            if (!string.IsNullOrEmpty(user) && users != null)
            {
                if (users.Length > 0)
                    model.Name = users[0];
                if (users.Length > 1)
                    model.UserId = users[1];
            }

            context.Staffs.Add(model);
            // 先落库，下面的 Founder 查询才会命中（FirstOrDefaultAsync 查数据库，不查变更跟踪器）
            await context.SaveChangesAsync();
        }

        // 开发环境：保证 Founder 同时拥有 Student 档案。
        // 系统约定 Founder = Staff + Student；缺 Student 时 /User/data 会返回 404，
        // 前端若把它当成会话失效就会把已登录用户弹回登录页。仅开发环境自动补齐，
        // 生产环境仍由管理员显式创建，避免写入默认密码。
        if (app.Environment.IsDevelopment())
        {
            var founder = await context.Staffs.FirstOrDefaultAsync(x => x.Identity == "Founder");
            if (founder is { UserId: not null and not "" } &&
                !await context.Students.AnyAsync(x => x.UserId == founder.UserId))
            {
                var password = Environment.GetEnvironmentVariable("FOUNDER_PASSWORD", EnvironmentVariableTarget.Process);
                if (string.IsNullOrWhiteSpace(password)) password = "iosclub123";

                Console.WriteLine($"Creating Student profile for Founder: {founder.Name} ({founder.UserId})");
                context.Students.Add(new StudentDO
                {
                    UserId = founder.UserId,
                    UserName = founder.Name,
                    Academy = "计算机科学与技术学院",
                    PoliticalLandscape = "共青团员",
                    Gender = "男",
                    ClassName = "计科2301",
                    PhoneNum = "13800138000",
                    EMail = "iosclub@example.com",
                    PasswordHash = DataTool.StringToHash(password),
                    JoinTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc)
                });
            }
        }

        if (await context.Categories.AnyAsync())
        {
            var categories = await context.Categories.Where(x => string.IsNullOrEmpty(x.Id)).ToListAsync();
            context.Categories.RemoveRange(categories);
        }

        await context.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Migration error: " + ex);
        // 不要抛出异常，避免应用启动失败
    }
    finally
    {
        await context.DisposeAsync();
    }
}

// 别动
app.MapOpenApi();

// 先配置会话中间件
app.UseSession(); // 会话中间件应该在认证和跨域中间件之前

app.UseHttpsRedirection();
// CORS 必须在认证/授权之前运行，才能正确处理携带 Authorization 的跨域预检请求。
app.UseCors();
app.UseAuthentication(); // 添加这行以启用身份验证中间件
app.UseAuthorization();

// 添加 Prometheus HTTP 请求指标收集中间件
app.UseHttpMetrics();

app.MapControllers();
app.MapScalarApiReference();

// 暴露 Prometheus 指标端点
app.MapMetrics();

app.Run();
