using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using StackExchange.Redis;
using System.Text.Json;
using iOSClub.Data;
using Microsoft.Extensions.Logging;

namespace iOSClub.DataApi.Services;

/// <summary>
/// 令牌生成器接口，提供生成访问令牌和刷新令牌的功能
/// </summary>
public interface ITokenGenerator
{
    /// <summary>
    /// 生成成员令牌
    /// </summary>
    /// <param name="model">成员模型</param>
    /// <param name="rememberMe">是否记住我</param>
    /// <param name="scope">权限范围</param>
    /// <param name="clientId">客户端ID</param>
    /// <returns>访问令牌和刷新令牌的元组</returns>
    public (string AccessToken, string RefreshToken) GetMemberToken(MemberVO model, bool rememberMe = false,
        string scope = "", string clientId = "");
}

public interface ILoginService
{
    /// <summary>
    /// 登录
    /// </summary>
    /// <param name="model">数据</param>
    /// <param name="clientId">客户端 ID</param>
    /// <param name="scope">权限</param>
    /// <returns>凭证</returns>
    public Task<string> Login(LoginDTO model, string clientId = "", string scope = "");

    public Task<string> LoginThirdPartyFromMainJwt(string userId, string clientId, string jwt, string scope = "");

    /// <summary>
    /// 登出
    /// </summary>
    /// <param name="userId">学号</param>
    /// <param name="clientId">客户端 ID</param>
    /// <returns>是否成功登出</returns>
    public Task<bool> Logout(string userId, string clientId = "");

    /// <summary>
    /// 验证token是否有效
    /// </summary>
    /// <param name="userId">学号</param>
    /// <param name="token">token值</param>
    /// <param name="clientId">客户端 ID</param>
    /// <returns>是否有效</returns>
    public Task<bool> ValidateToken(string userId, string token, string clientId);

    /// <summary>
    /// 员工登录
    /// </summary>
    /// <param name="model">数据</param>
    /// <param name="clientId">客户端 ID</param>
    /// <param name="scope">权限</param>
    /// <returns>凭证</returns>
    public Task<string> StaffLogin(LoginDTO model, string clientId = "", string scope = "");

    /// <summary>
    /// 修改用户密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="oldPassword">旧密码</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>是否成功更改密码</returns>
    public Task<bool> ChangePassword(string userId, string oldPassword, string newPassword);

    /// <summary>
    /// 请求重置密码的验证码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>是否成功发送验证码</returns>
    public Task<bool> RequestPasswordResetCode(string userId);

    /// <summary>
    /// 通过验证码重置密码
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="code">验证码</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>是否成功重置密码</returns>
    public Task<bool> ResetPasswordWithCode(string userId, string code, string newPassword);

    public Task<string> GetToken(string userId, string clientId = "");

    /// <summary>
    /// 将已签发的访问/刷新令牌写入 Redis。
    /// 注册接口直接签发令牌但不落库，会导致 /SSO/from_main_jwt 的
    /// ValidateToken（按 Redis 比对）对新注册用户失败。
    /// </summary>
    /// <param name="userId">学号</param>
    /// <param name="accessToken">访问令牌</param>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="clientId">客户端 ID</param>
    public Task StoreMemberToken(string userId, string accessToken, string refreshToken, string clientId = "");

    /// <summary>
    /// 验证token是否有效（简化版）
    /// </summary>
    /// <param name="userId">学号</param>
    /// <param name="token">token值</param>
    /// <returns>是否有效</returns>
    public Task<bool> ValidateToken(string userId, string token);

    /// <summary>
    /// 使用刷新令牌生成新的访问令牌
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="clientId">客户端ID</param>
    /// <param name="scope">权限范围</param>
    /// <returns>新的访问令牌</returns>
    public Task<string> RefreshToken(string userId, string refreshToken, string clientId = "", string scope = "");

    /// <summary>
    /// 获取刷新令牌
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="clientId">客户端ID</param>
    /// <returns>刷新令牌</returns>
    public Task<string> GetRefreshToken(string userId, string clientId = "");
}

public class LoginService(
    IStudentRepository studentRepository,
    IStaffRepository staffRepository,
    ITokenGenerator tokenGenerator,
    IConnectionMultiplexer redis,
    IEmailService emailService,
    IClientApplicationRepository clientApplicationRepository,
    ILogger<LoginService> logger)
    : ILoginService
{
    private readonly IDatabase _db = redis.GetDatabase();

    private const string TokenPrefix = "token:";
    private const string RefreshTokenPrefix = "refresh:";
    private const int RefreshTokenExpiryHours = 72; // 刷新令牌过期时间：72小时

    public async Task<string> Login(LoginDTO model, string clientId = "", string scope = "")
    {
        // 一次查询完成密码验证+获取学生数据，消除原先 Login()+GetByIdAsync() 的重复 DB 查询
        var stu = await studentRepository.LoginAndGetStudentAsync(model.UserId, model.Password);
        if (stu == null) return "";

        var staff = await staffRepository.GetStaffByIdWithoutOtherData(model.UserId);
        var identity = "Member";
        if (staff != null)
        {
            identity = staff.Identity;
        }

        var name = stu.UserName;
        var isNotHasEMail = string.IsNullOrEmpty(stu.EMail);

        // 关于查询身份信息的，需要完成 StaffRepository 之后，在这里进行查询，我先随便给个值
        var memberModel = new MemberVO()
        {
            UserName = name,
            UserId = model.UserId,
            Identity = identity
        };

        var s = await GetClientKey(clientId, isNotHasEMail);

        var redisKey = $"{TokenPrefix}{model.UserId}{s}";
        var storedToken = await _db.StringGetAsync(redisKey);
        if (storedToken.HasValue && !string.IsNullOrEmpty(storedToken))
        {
            return storedToken.ToString();
        }

        // 生成访问令牌和刷新令牌
        var (accessToken, refreshToken) = tokenGenerator.GetMemberToken(memberModel, model.RememberMe, scope, clientId);

        // 使用 Redis Batch 合并 3 次 SET 为一次网络往返
        var refreshTokenKey = $"{RefreshTokenPrefix}{model.UserId}{s}";
        var userInfoKey = $"user:{model.UserId}{s}";
        var batch = _db.CreateBatch();
        _ = batch.StringSetAsync(redisKey, accessToken, TimeSpan.FromMinutes(20 * (model.RememberMe ? 24 : 1)));
        _ = batch.StringSetAsync(refreshTokenKey, refreshToken, TimeSpan.FromHours(RefreshTokenExpiryHours));
        _ = batch.StringSetAsync(userInfoKey, JsonSerializer.Serialize(memberModel),
            TimeSpan.FromHours(RefreshTokenExpiryHours * (model.RememberMe ? 24 : 2)));
        batch.Execute();

        return accessToken;
    }

    public async Task<string> LoginThirdPartyFromMainJwt(string userId, string clientId, string jwt, string scope = "")
    {
        if (!await ValidateToken(userId, jwt))
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError("Invalid JWT token");
            }

            return "";
        }

        var s = await GetClientKey(clientId);

        var redisKey = $"{TokenPrefix}{userId}{s}";
        var storedToken = await _db.StringGetAsync(redisKey);
        if (storedToken.HasValue && !string.IsNullOrEmpty(storedToken))
        {
            // logger.LogInformation("Token already exists, get the token");
            return storedToken.ToString();
        }

        var member = await studentRepository.GetByIdAsync(userId);
        var memberModel = new MemberVO()
        {
            UserId = userId
        };

        if (member != null)
        {
            memberModel.UserName = member.UserName;
            memberModel.Identity = "Member";
        }

        var staff = await staffRepository.GetStaffByIdWithoutOtherData(userId);
        if (staff != null)
        {
            memberModel = new MemberVO()
            {
                UserId = userId,
                UserName = staff.Name,
                Identity = staff.Identity
            };
        }

        // 生成访问令牌和刷新令牌
        var (accessToken, refreshToken) = tokenGenerator.GetMemberToken(memberModel, scope: scope, clientId: clientId);

        // 将访问令牌存储到Redis中（可选，因为JWT本身包含过期时间）
        await _db.StringSetAsync(redisKey, accessToken, TimeSpan.FromHours(2));

        // 将刷新令牌存储到Redis中，设置过期时间
        var refreshTokenKey = $"{RefreshTokenPrefix}{userId}{s}";
        await _db.StringSetAsync(refreshTokenKey, refreshToken, TimeSpan.FromHours(RefreshTokenExpiryHours));

        // 存储用户信息，便于后续验证
        var userInfoKey = $"user:{userId}{s}";
        await _db.StringSetAsync(userInfoKey, JsonSerializer.Serialize(memberModel),
            TimeSpan.FromHours(RefreshTokenExpiryHours));

        return accessToken;
    }

    public async Task<bool> Logout(string userId, string clientId = "")
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("用户登出，开始清理令牌，用户ID：{UserId}", userId);
            }

            var s = await GetClientKey(clientId);

            // 同时删除用户信息、访问令牌和刷新令牌
            await _db.KeyDeleteAsync([
                $"user:{userId}{s}",
                $"{TokenPrefix}{userId}{s}",
                $"{RefreshTokenPrefix}{userId}{s}"
            ]);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("用户登出，令牌清理成功，用户ID：{UserId}", userId);
            }

            return true;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "用户登出，令牌清理失败，用户ID：{UserId}", userId);
            }

            return false;
        }
    }

    public async Task<string> GetToken(string userId, string clientId = "")
    {
        if (string.IsNullOrEmpty(userId)) return "";
        var s = await GetClientKey(clientId);

        var storedToken = await _db.StringGetAsync($"{TokenPrefix}{userId}{s}");
        return storedToken.HasValue && !string.IsNullOrEmpty(storedToken) ? storedToken.ToString() : "";
    }

    public async Task StoreMemberToken(string userId, string accessToken, string refreshToken,
        string clientId = "")
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(accessToken)) return;

        var s = await GetClientKey(clientId);
        var accessTokenKey = $"{TokenPrefix}{userId}{s}";
        var refreshTokenKey = $"{RefreshTokenPrefix}{userId}{s}";

        var batch = _db.CreateBatch();
        _ = batch.StringSetAsync(accessTokenKey, accessToken, TimeSpan.FromMinutes(20 * 24));
        if (!string.IsNullOrEmpty(refreshToken))
        {
            _ = batch.StringSetAsync(refreshTokenKey, refreshToken, TimeSpan.FromHours(RefreshTokenExpiryHours));
        }

        batch.Execute();
    }

    /// <summary>
    /// 使用刷新令牌生成新的访问令牌
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="refreshToken">刷新令牌</param>
    /// <param name="clientId">客户端ID</param>
    /// <param name="scope">权限范围</param>
    /// <returns>新的访问令牌</returns>
    public async Task<string> RefreshToken(string userId, string refreshToken, string clientId = "", string scope = "")
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("开始使用刷新令牌生成新的访问令牌，用户ID：{UserId}", userId);
            }

            var s = await GetClientKey(clientId);

            // 验证刷新令牌是否有效
            var refreshTokenKey = $"{RefreshTokenPrefix}{userId}{s}";
            var storedRefreshToken = await _db.StringGetAsync(refreshTokenKey);

            if (!storedRefreshToken.HasValue || string.IsNullOrEmpty(storedRefreshToken) ||
                storedRefreshToken != refreshToken)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("无效的刷新令牌，用户ID：{UserId}", userId);
                }

                return "";
            }

            // 检查刷新令牌是否在黑名单中
            var isBlacklisted = await IsRefreshTokenBlacklisted(refreshToken);
            if (isBlacklisted)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("刷新令牌已被列入黑名单，用户ID：{UserId}", userId);
                }

                return "";
            }

            // 获取用户信息
            var userInfoKey = $"user:{userId}{s}";
            var userInfoJson = await _db.StringGetAsync(userInfoKey);
            if (!userInfoJson.HasValue || string.IsNullOrEmpty(userInfoJson))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("无法获取用户信息，用户ID：{UserId}", userId);
                }

                return "";
            }

            var memberModel = JsonSerializer.Deserialize<MemberVO>(userInfoJson.ToString());
            if (memberModel == null)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("无法解析用户信息，用户ID：{UserId}", userId);
                }

                return "";
            }

            // 生成新的访问令牌和刷新令牌
            var (newAccessToken, newRefreshToken) =
                tokenGenerator.GetMemberToken(memberModel, scope: scope, clientId: clientId);

            // 更新Redis中的令牌信息
            await _db.StringSetAsync($"{TokenPrefix}{userId}{s}", newAccessToken, TimeSpan.FromMinutes(20));
            await _db.StringSetAsync(refreshTokenKey, newRefreshToken, TimeSpan.FromHours(RefreshTokenExpiryHours));

            // 将旧的刷新令牌加入黑名单
            var isAdded = await AddRefreshTokenToBlacklist(refreshToken);

            if (!isAdded)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("将旧的刷新令牌加入黑名单失败，用户ID：{UserId}", userId);
                }
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("使用刷新令牌生成新的访问令牌成功，用户ID：{UserId}", userId);
            }

            return newAccessToken;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "使用刷新令牌生成新的访问令牌失败，用户ID：{UserId}", userId);
            }

            return "";
        }
    }

    /// <summary>
    /// 检查刷新令牌是否在黑名单中
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>是否在黑名单中</returns>
    private async Task<bool> IsRefreshTokenBlacklisted(string refreshToken)
    {
        try
        {
            var blacklistKey = $"blacklist:refresh:{refreshToken}";
            var isBlacklisted = await _db.KeyExistsAsync(blacklistKey);
            return isBlacklisted;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "检查刷新令牌黑名单状态失败");
            }

            // 发生错误时，默认认为令牌无效，返回true
            return true;
        }
    }

    /// <summary>
    /// 将刷新令牌加入黑名单
    /// </summary>
    /// <param name="refreshToken">刷新令牌</param>
    /// <returns>是否成功加入黑名单</returns>
    private async Task<bool> AddRefreshTokenToBlacklist(string refreshToken)
    {
        try
        {
            var blacklistKey = $"blacklist:refresh:{refreshToken}";
            // 设置黑名单过期时间为刷新令牌的过期时间（72小时）
            await _db.StringSetAsync(blacklistKey, "1", TimeSpan.FromHours(RefreshTokenExpiryHours));
            return true;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "将刷新令牌加入黑名单失败");
            }

            return false;
        }
    }

    /// <summary>
    /// 获取刷新令牌
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <param name="clientId">客户端ID</param>
    /// <returns>刷新令牌</returns>
    public async Task<string> GetRefreshToken(string userId, string clientId = "")
    {
        try
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("开始获取刷新令牌，用户ID：{UserId}", userId);
            }

            var s = await GetClientKey(clientId);

            // 从Redis中获取刷新令牌
            var refreshTokenKey = $"{RefreshTokenPrefix}{userId}{s}";
            var storedRefreshToken = await _db.StringGetAsync(refreshTokenKey);

            if (!storedRefreshToken.HasValue || string.IsNullOrEmpty(storedRefreshToken))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("未找到刷新令牌，用户ID：{UserId}", userId);
                }

                return "";
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("获取刷新令牌成功，用户ID：{UserId}", userId);
            }

            return storedRefreshToken.ToString();
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "获取刷新令牌失败，用户ID：{UserId}", userId);
            }

            return "";
        }
    }

    public Task<bool> ValidateToken(string userId, string token)
    {
        return ValidateToken(userId, token, "");
    }

    public async Task<bool> ValidateToken(string userId, string token, string clientId)
    {
        if (string.IsNullOrEmpty(userId)) return false;

        var s = await GetClientKey(clientId);

        var redisKey = $"{TokenPrefix}{userId}{s}";
        var storedToken = await _db.StringGetAsync(redisKey);

        if (!storedToken.HasValue || string.IsNullOrEmpty(storedToken))
            return false;

        return storedToken == token;
    }

    public async Task<string> StaffLogin(LoginDTO model, string clientId = "", string scope = "")
    {
        var staff = await staffRepository.GetStaffByIdWithoutOtherData(model.Password);

        if (staff == null || staff.Name != model.UserId)
            return "";

        var memberModel = new MemberVO()
        {
            UserName = staff.Name,
            UserId = staff.UserId,
            Identity = staff.Identity
        };

        var s = await GetClientKey(clientId);

        var redisKey = $"{TokenPrefix}{model.Password}{s}";
        var storedToken = await _db.StringGetAsync(redisKey);
        if (storedToken.HasValue && !string.IsNullOrEmpty(storedToken))
        {
            return storedToken.ToString();
        }

        // 生成访问令牌和刷新令牌
        var (accessToken, refreshToken) =
            tokenGenerator.GetMemberToken(memberModel, model.RememberMe, scope, clientId: clientId);

        // 将访问令牌存储到Redis中（可选，因为JWT本身包含过期时间）
        await _db.StringSetAsync(redisKey, accessToken, TimeSpan.FromHours(2 * (model.RememberMe ? 12 : 1)));

        // 将刷新令牌存储到Redis中，设置过期时间
        var refreshTokenKey = $"{RefreshTokenPrefix}{model.Password}{s}";
        await _db.StringSetAsync(refreshTokenKey, refreshToken, TimeSpan.FromHours(RefreshTokenExpiryHours));

        // 存储用户信息，便于后续验证
        var userInfoKey = $"user:{model.Password}{s}";
        await _db.StringSetAsync(userInfoKey, JsonSerializer.Serialize(memberModel),
            TimeSpan.FromHours(RefreshTokenExpiryHours * (model.RememberMe ? 12 : 2)));

        return accessToken;
    }

    public async Task<bool> ChangePassword(string userId, string oldPassword, string newPassword)
    {
        // 空密码会让 DataTool.StringToHash 抛 ArgumentException（它把空白输入当作编程错误）。
        // 但这里是用户输入，属于可预期的校验失败，应当返回 false 让上层给出明确的错误提示，
        // 而不是让异常冒泡成 500。
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(oldPassword) ||
            string.IsNullOrWhiteSpace(newPassword))
            return false;

        // 验证旧密码是否正确
        var isLoginSuccess = await studentRepository.Login(userId, oldPassword);
        if (!isLoginSuccess)
            return false;

        // 获取用户信息
        var student = await studentRepository.GetByIdAsync(userId);
        if (student == null)
            return false;

        // 新密码和旧密码相同：没有要改的东西。直接返回 false，而不是白白换一个 BCrypt 哈希
        // —— BCrypt 每次加盐都不同，否则会"成功"地写回一个语义上完全等价的密码。
        if (DataTool.IsOk(newPassword, student.PasswordHash))
            return false;

        return await studentRepository.SetPasswordAsync(userId, newPassword);
    }

    public async Task<bool> RequestPasswordResetCode(string userId)
    {
        // 获取用户信息
        var student = await studentRepository.GetByIdAsync(userId);
        if (student == null)
            return false;

        // 检查是否有邮箱
        if (string.IsNullOrEmpty(student.EMail))
            return false;

        var redisKey = $"password_reset_code:{userId}";
        string code;
        var storedCode = await _db.StringGetAsync(redisKey);
        if (!storedCode.HasValue || string.IsNullOrEmpty(storedCode))
        {
            // 生成6位随机验证码
            var random = new Random();
            code = random.Next(100000, 999999).ToString();
            // 将验证码存储到Redis，设置10分钟过期时间

            await _db.StringSetAsync(redisKey, code, TimeSpan.FromMinutes(10));
        }
        else
        {
            code = storedCode.ToString();
        }

        // 发送邮件
        const string subject = "iOS Club 密码重置验证码";
        var body = GeneratePasswordResetEmailBody(student.UserName, code);

        var result = await emailService.SendEmailAsync(student.EMail, subject, body, true);

        return result;
    }

    public async Task<bool> ResetPasswordWithCode(string userId, string code, string newPassword)
    {
        // 同 ChangePassword：空的新密码属于可预期的校验失败，不应让 StringToHash 抛异常。
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(newPassword))
            return false;

        // 从Redis获取存储的验证码
        var redisKey = $"password_reset_code:{userId}";
        var storedCode = await _db.StringGetAsync(redisKey);

        // 检查验证码是否存在且匹配
        if (!storedCode.HasValue || storedCode != code)
            return false;

        // 密码重置走的是邮箱验证码，用户本来就是忘了密码才来的，所以不校验"新密码是否等于旧密码"。
        var result = await studentRepository.SetPasswordAsync(userId, newPassword);

        // 删除已使用的验证码
        if (result) await _db.KeyDeleteAsync(redisKey);

        return result;
    }

    /// <summary>
    /// 生成密码重置邮件内容
    /// </summary>
    /// <param name="userName">用户名</param>
    /// <param name="code">验证码</param>
    /// <returns>格式化后的邮件内容</returns>
    private static string GeneratePasswordResetEmailBody(string userName, string code)
    {
        return $@"<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f8f8f8;
        }}
        .container {{
            padding: 40px;
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .code {{
            font-size: 32px;
            font-weight: 600;
            text-align: center;
            letter-spacing: 8px;
            padding: 20px;
            background-color: #f1f1f1;
            border-radius: 8px;
            margin: 30px 0;
            color: #111;
        }}
        .footer {{
            margin-top: 30px;
            font-size: 14px;
            color: #888;
            text-align: center;
        }}
        hr {{
            border: none;
            height: 1px;
            background-color: #eee;
            margin: 30px 0;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>iOS Club</h1>
            <p>密码重置请求</p>
        </div>
        
        <p>尊敬的 {userName}，</p>
        
        <p>您正在请求重置您的 iOS Club 账户密码。</p>
        
        <p>请在密码重置页面输入以下验证码：</p>
        
        <div class=""code"">{code}</div>
        
        <p>此验证码将在 10 分钟后过期。</p>
        
        <hr>
        
        <p style=""font-size: 14px; color: #666;"">如果您没有请求密码重置，请忽略此邮件。您的账户安全不会受到影响。</p>
        
        <div class=""footer"">
            <p style=""margin-top: 20px; font-size: 12px;"">© 2025 iOS Club of XAUAT. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }

    private async Task<string> GetClientKey(string clientId, bool isNotHasEMail = false)
    {
        var s = "";
        var app = await clientApplicationRepository.GetByClientIdAsync(clientId);
        if (app == null) return s;
        if (app.IsNeedEMail && isNotHasEMail)
        {
            return "";
        }

        s = $"client_secret:{app.ClientSecret}";
        return s;
    }
}