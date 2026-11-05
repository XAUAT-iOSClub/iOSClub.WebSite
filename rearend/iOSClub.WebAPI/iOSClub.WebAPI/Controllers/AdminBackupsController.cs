using System.Security.Claims;
using iOSClub.WebAPI.Common;
using iOSClub.WebAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iOSClub.WebAPI.Controllers;

/// <summary>
/// 数据库备份只读接口：查询最近一次成功备份、获取短时下载链接。
/// 不执行 pg_dump，不接触数据库备份凭据。
/// </summary>
[ApiController]
[Route("AdminBackups")]
[Authorize(Roles = "Founder, President, Minister")]
public class AdminBackupsController(
    IR2BackupMetadataService backups,
    ILogger<AdminBackupsController> logger) : ControllerBase
{
    /// <summary>
    /// 查询最近一次成功备份的非敏感元数据。
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<BackupStatusVO>>> GetStatus(CancellationToken cancellationToken)
    {
        if (!backups.IsConfigured)
        {
            return Ok(ApiResponse<BackupStatusVO>.Fail(
                ErrorCode.ExternalServiceNotConfigured, "数据库备份服务未配置"));
        }

        try
        {
            var manifest = await backups.GetLatestManifestAsync(cancellationToken);
            if (manifest is null || !IsSuccessManifest(manifest))
            {
                return Ok(ApiResponse<BackupStatusVO>.Fail(
                    ErrorCode.ResourceNotFound, "暂无可用的数据库备份"));
            }

            return Ok(ApiResponse<BackupStatusVO>.Success(new BackupStatusVO
            {
                FileName = ResolveFileName(manifest),
                SizeBytes = manifest.SizeBytes,
                CreatedAt = manifest.CreatedAt,
                Status = manifest.Status ?? "success",
                Sha256 = manifest.Sha256,
                Format = manifest.Format
            }));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "数据库备份服务未就绪");
            return Ok(ApiResponse<BackupStatusVO>.Fail(
                ErrorCode.ExternalServiceNotConfigured, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "读取数据库备份状态失败");
            return Ok(ApiResponse<BackupStatusVO>.Fail(
                ErrorCode.ExternalServiceFailed, "读取数据库备份状态失败"));
        }
    }

    /// <summary>
    /// 为最近一次成功备份生成短时 GET 预签名下载链接。
    /// </summary>
    [HttpGet("latest/download-url")]
    public async Task<ActionResult<ApiResponse<BackupDownloadVO>>> GetLatestDownloadUrl(
        CancellationToken cancellationToken)
    {
        if (!backups.IsConfigured)
        {
            return Ok(ApiResponse<BackupDownloadVO>.Fail(
                ErrorCode.ExternalServiceNotConfigured, "数据库备份服务未配置"));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.Identity?.Name
                     ?? "unknown";

        try
        {
            var manifest = await backups.GetLatestManifestAsync(cancellationToken);
            if (manifest is null || !IsSuccessManifest(manifest))
            {
                logger.LogInformation("用户 {UserId} 请求下载数据库备份，但没有可用备份", userId);
                return Ok(ApiResponse<BackupDownloadVO>.Fail(
                    ErrorCode.ResourceNotFound, "暂无可下载的数据库备份"));
            }

            var downloadUrl = await backups.CreateDownloadUrlAsync(manifest, cancellationToken);
            var fileName = ResolveFileName(manifest);

            logger.LogInformation(
                "用户 {UserId} 请求下载数据库备份，文件={FileName}, 大小={SizeBytes}, 对象={ObjectKey}",
                userId, fileName, manifest.SizeBytes, manifest.ObjectKey);

            return Ok(ApiResponse<BackupDownloadVO>.Success(new BackupDownloadVO
            {
                FileName = fileName,
                SizeBytes = manifest.SizeBytes,
                CreatedAt = manifest.CreatedAt,
                DownloadUrl = downloadUrl
            }));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "用户 {UserId} 请求下载数据库备份失败：配置或元数据无效", userId);
            return Ok(ApiResponse<BackupDownloadVO>.Fail(
                ErrorCode.ExternalServiceFailed, "备份元数据无效，无法生成下载链接"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "用户 {UserId} 获取数据库备份下载链接失败", userId);
            return Ok(ApiResponse<BackupDownloadVO>.Fail(
                ErrorCode.ExternalServiceFailed, "获取数据库备份下载链接失败"));
        }
    }

    private static bool IsSuccessManifest(BackupManifest manifest) =>
        string.Equals(manifest.Status, "success", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(manifest.ObjectKey);

    private static string ResolveFileName(BackupManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.FileName))
        {
            return Path.GetFileName(manifest.FileName);
        }

        return string.IsNullOrWhiteSpace(manifest.ObjectKey)
            ? "backup.dump"
            : Path.GetFileName(manifest.ObjectKey);
    }
}
