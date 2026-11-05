using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using iOSClub.Data.DataObjects;
using iOSClub.Data.DTOs;
using iOSClub.Data.VOs;
using iOSClub.DataApi.Repositories;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Common.Security;
using System.Security.Cryptography;

namespace iOSClub.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Founder, President, Minister")]
public class ClientAppController(
    IClientApplicationRepository clientAppRepository,
    IClientSecretProtector secretProtector,
    ILogger<ClientAppController> logger)
    : ControllerBase
{
    /// <summary>
    /// 获取所有客户端应用
    /// </summary>
    /// <returns>客户端应用列表</returns>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IEnumerable<ClientAppVO>>>> GetClientApplications()
    {
        var clientApps = await clientAppRepository.GetAllAsync();
        var clientApplications = clientApps as ClientApplicationDO[] ?? clientApps.ToArray();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("获取客户端应用列表成功，应用数量: {Count}", clientApplications.Length);
        }

        return Ok(ApiResponse<IEnumerable<ClientAppVO>>.Success(clientApplications.Adapt<List<ClientAppVO>>()));
    }

    /// <summary>
    /// 根据客户端ID获取应用
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    /// <returns>客户端应用</returns>
    [HttpGet("{clientId}")]
    public async Task<ActionResult<ApiResponse<ClientAppVO>>> GetClientApplication(string clientId)
    {
        var clientApp = await clientAppRepository.GetByClientIdAsync(clientId);
        if (clientApp == null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("获取客户端应用失败，客户端不存在，ID: {ClientId}", clientId);
            }

            return Ok(ApiResponse<ClientAppVO>.Fail(ErrorCode.ResourceNotFound, "客户端应用不存在"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("获取客户端应用成功，ID: {ClientId}, 名称: {Name}", clientId, clientApp.ApplicationName);
        }

        return Ok(ApiResponse<ClientAppVO>.Success(clientApp.Adapt<ClientAppVO>()));
    }

    /// <summary>
    /// 查看客户端应用的可恢复密钥。仅 Founder/President 可用；
    /// 该 GET 会写操作日志（见 OperationLogCatalog 的 MutatingGetActions）。
    /// </summary>
    [Authorize(Roles = "Founder, President")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("{clientId}/secret")]
    public async Task<ActionResult<ApiResponse<ClientSecretVO>>> GetClientSecret(string clientId)
    {
        var clientApp = await clientAppRepository.GetByClientIdAsync(clientId);
        if (clientApp == null)
        {
            return Ok(ApiResponse<ClientSecretVO>.Fail(ErrorCode.ResourceNotFound, "客户端应用不存在"));
        }

        var secret = secretProtector.Unprotect(clientApp.ClientSecretCipher);
        if (string.IsNullOrEmpty(secret))
        {
            // 旧数据只有 BCrypt 哈希，无法还原；让管理员重置一次即可。
            return Ok(ApiResponse<ClientSecretVO>.Fail(
                ErrorCode.InvalidStatusForOperation,
                "该应用密钥不可恢复（创建于旧版本），请重置密钥后查看"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("查看客户端应用密钥，ID: {ClientId}, 操作者: {Operator}",
                clientId, User.Identity?.Name);
        }

        return Ok(ApiResponse<ClientSecretVO>.Success(new ClientSecretVO
        {
            ClientId = clientApp.ClientId,
            ClientSecret = secret
        }));
    }

    /// <summary>
    /// 创建新的客户端应用
    /// </summary>
    /// <param name="dto">客户端应用信息</param>
    /// <returns>创建的客户端应用</returns>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ClientAppResultVO>>> CreateClientApplication(
        [FromBody] ClientAppCreateDTO dto)
    {
        // 生成客户端ID和密钥
        var clientId = GenerateClientId();
        var clientSecret = GenerateClientSecret();

        var clientApp = new ClientApplicationDO
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            // 哈希用于校验，密文用于 Founder/President 后续查看。
            ClientSecretCipher = secretProtector.Protect(clientSecret),
            ApplicationName = dto.ApplicationName,
            Description = dto.Description,
            HomepageUrl = dto.HomepageUrl,
            RedirectUris = string.Join("; ", dto.RedirectUris),
            LogoUrl = dto.LogoUrl,
            IsActive = true,
            IsNeedEMail = dto.IsNeedEMail,
            SupportsPkce = dto.SupportsPkce
        };

        // 保存原始密钥用于返回给客户端
        var originalClientSecret = clientApp.ClientSecret;

        var result = await clientAppRepository.CreateAsync(clientApp);
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("创建客户端应用失败，名称: {Name}", dto.ApplicationName);
            }

            return Ok(ApiResponse<ClientAppResultVO>.Fail(ErrorCode.OperationFailed, "创建客户端应用失败"));
        }

        // 返回包含密钥的信息（只在创建时显示）
        var resultModel = new ClientAppResultVO
        {
            ClientId = clientApp.ClientId,
            // 返回原始密钥而不是哈希值
            ClientSecret = originalClientSecret,
            ApplicationName = clientApp.ApplicationName,
            Description = clientApp.Description,
            HomepageUrl = clientApp.HomepageUrl,
            RedirectUris = clientApp.RedirectUris.Split(';').ToList(),
            LogoUrl = clientApp.LogoUrl,
            IsActive = clientApp.IsActive,
            IsNeedEMail = clientApp.IsNeedEMail,
            SupportsPkce = clientApp.SupportsPkce
        };

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("创建客户端应用成功，ID: {ClientId}, 名称: {Name}", clientId, clientApp.ApplicationName);
        }

        return CreatedAtAction(nameof(GetClientApplication), new { clientId = clientApp.ClientId },
            ApiResponse<ClientAppResultVO>.Success(resultModel, "创建客户端应用成功"));
    }

    /// <summary>
    /// 更新客户端应用
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    /// <param name="dto">客户端应用信息</param>
    /// <returns>更新结果</returns>
    [HttpPut("{clientId}")]
    public async Task<ActionResult<ApiResponse>> UpdateClientApplication(string clientId,
        [FromBody] ClientAppUpdateDTO dto)
    {
        var existingClientApp = await clientAppRepository.GetByClientIdAsync(clientId);
        if (existingClientApp == null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("更新客户端应用失败，客户端不存在，ID: {ClientId}", clientId);
            }

            return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "客户端应用不存在"));
        }

        existingClientApp.ApplicationName = dto.ApplicationName;
        existingClientApp.Description = dto.Description;
        existingClientApp.HomepageUrl = dto.HomepageUrl;
        existingClientApp.RedirectUris = string.Join("; ", dto.RedirectUris);
        existingClientApp.LogoUrl = dto.LogoUrl;
        existingClientApp.IsActive = dto.IsActive;
        existingClientApp.UpdatedAt = DateTime.UtcNow;
        existingClientApp.IsNeedEMail = dto.IsNeedEMail;
        existingClientApp.SupportsPkce = dto.SupportsPkce;

        var result = await clientAppRepository.UpdateAsync(existingClientApp);
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("更新客户端应用失败，ID: {ClientId}, 名称: {Name}", clientId,
                    dto.ApplicationName);
            }

            return Ok(ApiResponse.Fail(ErrorCode.OperationFailed, "更新客户端应用失败"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("更新客户端应用成功，ID: {ClientId}, 名称: {Name}", clientId, dto.ApplicationName);
        }

        return Ok(ApiResponse.Success("更新客户端应用成功"));
    }

    /// <summary>
    /// 删除客户端应用
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    /// <returns>删除结果</returns>
    [HttpDelete("{clientId}")]
    public async Task<ActionResult<ApiResponse>> DeleteClientApplication(string clientId)
    {
        var result = await clientAppRepository.DeleteAsync(clientId);
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("删除客户端应用失败，客户端不存在，ID: {ClientId}", clientId);
            }

            return Ok(ApiResponse.Fail(ErrorCode.ResourceNotFound, "客户端应用不存在"));
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("删除客户端应用成功，ID: {ClientId}", clientId);
        }

        return Ok(ApiResponse.Success("删除客户端应用成功"));
    }

    /// <summary>
    /// 重新生成客户端密钥
    /// </summary>
    /// <param name="clientId">客户端ID</param>
    /// <returns>新的客户端密钥</returns>
    [HttpPost("{clientId}/regenerate-secret")]
    public async Task<ActionResult<ApiResponse<ClientAppResultVO>>> RegenerateClientSecret(string clientId)
    {
        var existingClientApp = await clientAppRepository.GetByClientIdAsync(clientId);
        if (existingClientApp == null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("重新生成客户端密钥失败，客户端不存在，ID: {ClientId}", clientId);
            }

            return Ok(ApiResponse<ClientAppResultVO>.Fail(ErrorCode.ResourceNotFound, "客户端应用不存在"));
        }

        var newSecret = GenerateClientSecret();
        // 保存原始密钥用于返回给客户端，但存储到数据库的是哈希值；
        // 另存一份可恢复密文，供 Founder/President 在管理页查看。
        existingClientApp.ClientSecret = newSecret;
        existingClientApp.ClientSecretCipher = secretProtector.Protect(newSecret);
        existingClientApp.UpdatedAt = DateTime.UtcNow;

        var result = await clientAppRepository.UpdateAsync(existingClientApp);
        if (!result)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("重新生成客户端密钥失败，ID: {ClientId}", clientId);
            }

            return Ok(ApiResponse<ClientAppResultVO>.Fail(ErrorCode.OperationFailed, "重新生成密钥失败"));
        }

        var resultModel = new ClientAppResultVO { ClientId = clientId, ClientSecret = newSecret };
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("重新生成客户端密钥成功，ID: {ClientId}", clientId);
        }

        return Ok(ApiResponse<ClientAppResultVO>.Success(resultModel, "重新生成密钥成功"));
    }

    /// <summary>
    /// 生成客户端ID
    /// </summary>
    /// <returns>客户端ID</returns>
    private static string GenerateClientId()
    {
        return "ca_" + Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>
    /// 生成客户端密钥
    /// </summary>
    /// <returns>客户端密钥</returns>
    private static string GenerateClientSecret()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes);
    }
}
