using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.WebAPI.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize(Roles = "Founder,President")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ApiKeyController(IDbContextFactory<ClubContext> factory) : ControllerBase
{
    [HttpGet]
    public async Task<ApiResponse<List<ApiKeySummary>>> List(CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var keys = await db.ApiKeys.AsNoTracking().OrderByDescending(key => key.CreatedAt)
            .Select(key => new { key.Id, key.Name, key.Permission, key.CreatedAt, key.ExpiresAt })
            .ToListAsync(cancellationToken);
        return ApiResponse<List<ApiKeySummary>>.Success(keys.Select(key => new ApiKeySummary(
            key.Id.ToString("N"), key.Name, key.Permission, DateTime.SpecifyKind(key.CreatedAt, DateTimeKind.Utc),
            key.ExpiresAt is { } expiry ? DateTime.SpecifyKind(expiry, DateTimeKind.Utc) : null)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ApiKeyCreated>>> Create(
        CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expiresAt = request.ExpiresAt?.UtcDateTime;
        if (string.IsNullOrWhiteSpace(request.Name)
            || request.Permission is not (ApiKeyPermission.ReadOnly or ApiKeyPermission.ReadWrite)
            || expiresAt <= now)
        {
            return BadRequest(ApiResponse<ApiKeyCreated>.Fail(ErrorCode.ParameterValidationFailed,
                "请输入名称和有效的权限等级，过期时间必须晚于当前时间"));
        }

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var key = new ApiKeyDO
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Permission = request.Permission,
            SecretHash = SHA256.HashData(Encoding.ASCII.GetBytes(secret)),
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(cancellationToken);
        return ApiResponse<ApiKeyCreated>.Success(new ApiKeyCreated(key.Id.ToString("N"), secret));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ApiResponse> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await db.ApiKeys.Where(key => key.Id == id).ExecuteDeleteAsync(cancellationToken);
        return ApiResponse.Success("API Key 已撤销");
    }

    public sealed record CreateApiKeyRequest(
        [property: Required, StringLength(100)] string Name, ApiKeyPermission Permission, DateTimeOffset? ExpiresAt);
    public sealed record ApiKeySummary(string Id, string Name, ApiKeyPermission Permission, DateTime CreatedAt, DateTime? ExpiresAt);
    public sealed record ApiKeyCreated(string Id, string Secret);
}
