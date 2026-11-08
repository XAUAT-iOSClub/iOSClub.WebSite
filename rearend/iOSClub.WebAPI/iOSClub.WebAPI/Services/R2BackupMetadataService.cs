using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using iOSClub.WebAPI.Common.Config;

namespace iOSClub.WebAPI.Services;

/// <summary>
/// latest.json 中记录的最新成功备份元数据。
/// </summary>
public sealed class BackupManifest
{
    public string? Status { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public string? ObjectKey { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public string? Format { get; set; }
    public string? FileName { get; set; }
}

/// <summary>
/// 备份状态 API 的返回结构（不含凭据与预签名地址）。
/// </summary>
public sealed class BackupStatusVO
{
    public string FileName { get; init; } = "";
    public long SizeBytes { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public string Status { get; init; } = "";
    public string? Sha256 { get; init; }
    public string? Format { get; init; }
}

/// <summary>
/// 最新备份下载信息，downloadUrl 为短时预签名地址。
/// </summary>
public sealed class BackupDownloadVO
{
    public string FileName { get; init; } = "";
    public long SizeBytes { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public string DownloadUrl { get; init; } = "";
}

public interface IR2BackupMetadataService
{
    bool IsConfigured { get; }

    /// <summary>
    /// 读取 R2 中的 latest.json；不存在时返回 null。
    /// </summary>
    Task<BackupManifest?> GetLatestManifestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 为 manifest 指向的单个备份对象生成短时 GET 预签名链接。
    /// </summary>
    Task<string> CreateDownloadUrlAsync(BackupManifest manifest, CancellationToken cancellationToken = default);
}

/// <summary>
/// 只读访问 Cloudflare R2 上的备份元数据，并生成单对象短时下载链接。
/// 不执行 pg_dump、不持有数据库凭据、不需要删除权限。
/// </summary>
public sealed class R2BackupMetadataService : IR2BackupMetadataService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly BackupStorageOptions _options;
    private readonly ILogger<R2BackupMetadataService> _logger;
    private readonly Lazy<IAmazonS3> _client;

    public R2BackupMetadataService(BackupStorageOptions options, ILogger<R2BackupMetadataService> logger)
    {
        _options = options;
        _logger = logger;
        _client = new Lazy<IAmazonS3>(CreateClient);
    }

    public bool IsConfigured => _options.IsConfigured;

    private IAmazonS3 Client => _client.Value;

    public async Task<BackupManifest?> GetLatestManifestAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        try
        {
            using var response = await Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _options.Bucket,
                Key = _options.ManifestKey
            }, cancellationToken);

            using var reader = new StreamReader(response.ResponseStream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync(cancellationToken);
            return JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<string> CreateDownloadUrlAsync(BackupManifest manifest, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var objectKey = manifest.ObjectKey?.Trim();
        if (string.IsNullOrEmpty(objectKey))
        {
            throw new InvalidOperationException("备份元数据缺少 objectKey");
        }

        if (!IsAllowedObjectKey(objectKey))
        {
            throw new InvalidOperationException("备份元数据中的 objectKey 不在允许的备份前缀内");
        }

        var fileName = string.IsNullOrWhiteSpace(manifest.FileName)
            ? Path.GetFileName(objectKey)
            : manifest.FileName.Trim();

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_options.DownloadUrlExpiryMinutes),
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentType = "application/octet-stream",
                ContentDisposition = $"attachment; filename=\"{SanitizeFileName(fileName)}\""
            }
        };

        var url = Client.GetPreSignedURL(request);

        // AWS SDK v4 的预签名 URL 固定输出 https；SigV4 签名不包含 scheme，
        // 因此当配置的是 http endpoint（本地/自建 S3 兼容服务）时安全地改回 http。
        if (_options.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url["https://".Length..];
        }

        return Task.FromResult(url);
    }

    private bool IsAllowedObjectKey(string objectKey)
    {
        if (objectKey.Contains("..", StringComparison.Ordinal)) return false;
        if (objectKey.StartsWith('/')) return false;

        if (string.IsNullOrEmpty(_options.Prefix)) return true;
        return objectKey.StartsWith(_options.Prefix, StringComparison.Ordinal);
    }

    private static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name)) name = "backup.dump";

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }

    private void EnsureConfigured()
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("数据库备份功能已关闭");
        }

        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("数据库备份存储未配置");
        }
    }

    private IAmazonS3 CreateClient()
    {
        var config = new AmazonS3Config
        {
            ServiceURL = _options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = _options.Region,
            // ServiceURL 的 scheme 需要显式映射到 UseHttp，否则 http endpoint 会被签成 https。
            UseHttp = _options.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        };

        _logger.LogInformation(
            "初始化 R2 备份只读客户端，Bucket={Bucket}, Prefix={Prefix}",
            _options.Bucket, _options.Prefix);

        return new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey),
            config);
    }
}
