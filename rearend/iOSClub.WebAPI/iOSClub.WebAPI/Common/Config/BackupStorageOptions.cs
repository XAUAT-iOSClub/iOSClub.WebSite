namespace iOSClub.WebAPI.Common.Config;

/// <summary>
/// 数据库备份存储（Cloudflare R2）配置。
/// 只用于读取 latest.json 元数据并生成单个对象的短时下载链接，
/// 不包含 pg_dump、数据库凭据或写入凭据。
/// </summary>
public sealed class BackupStorageOptions
{
    /// <summary>
    /// 功能开关，未设置时默认启用；未配置 R2 时接口会返回“未配置”。
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// R2 S3 API Endpoint，形如 https://&lt;ACCOUNT_ID&gt;.r2.cloudflarestorage.com
    /// </summary>
    public string Endpoint { get; init; } = "";

    /// <summary>
    /// R2 区域，固定为 auto。
    /// </summary>
    public string Region { get; init; } = "auto";

    /// <summary>
    /// 私有备份桶名称。
    /// </summary>
    public string Bucket { get; init; } = "";

    /// <summary>
    /// 备份对象前缀，用于校验 manifest 中的 objectKey 不会越界。
    /// </summary>
    public string Prefix { get; init; } = "backups/postgresql/";

    /// <summary>
    /// latest.json 的对象 Key。
    /// </summary>
    public string ManifestKey { get; init; } = "metadata/latest.json";

    /// <summary>
    /// WebAPI 使用的只读 R2 凭据。
    /// </summary>
    public string AccessKeyId { get; init; } = "";

    /// <summary>
    /// WebAPI 使用的只读 R2 密钥。
    /// </summary>
    public string SecretAccessKey { get; init; } = "";

    /// <summary>
    /// 预签名下载链接有效期（分钟）。
    /// </summary>
    public int DownloadUrlExpiryMinutes { get; init; } = 5;

    /// <summary>
    /// 是否具备调用 R2 所需的最小配置。
    /// </summary>
    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(Bucket) &&
        !string.IsNullOrWhiteSpace(ManifestKey) &&
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(SecretAccessKey);

    public static BackupStorageOptions FromEnvironment()
    {
        static string Get(string key, string fallback = "")
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        var enabledRaw = Get("BACKUP_FEATURE_ENABLED", "true");
        var enabled = !enabledRaw.Equals("false", StringComparison.OrdinalIgnoreCase) && enabledRaw != "0";

        var expiry = 5;
        if (int.TryParse(Get("BACKUP_DOWNLOAD_URL_EXPIRY_MINUTES", "5"), out var parsedExpiry))
        {
            expiry = Math.Clamp(parsedExpiry, 1, 60);
        }

        return new BackupStorageOptions
        {
            Enabled = enabled,
            Endpoint = Get("R2_ENDPOINT_URL"),
            Region = Get("R2_REGION", "auto"),
            Bucket = Get("R2_BUCKET"),
            Prefix = NormalizePrefix(Get("R2_PREFIX", "backups/postgresql/")),
            ManifestKey = Get("R2_LATEST_MANIFEST_KEY", "metadata/latest.json").TrimStart('/'),
            AccessKeyId = Get("AWS_ACCESS_KEY_ID"),
            SecretAccessKey = Get("AWS_SECRET_ACCESS_KEY"),
            DownloadUrlExpiryMinutes = expiry
        };
    }

    private static string NormalizePrefix(string prefix)
    {
        var trimmed = prefix.Trim().TrimStart('/');
        if (string.IsNullOrEmpty(trimmed)) return "";
        return trimmed.EndsWith('/') ? trimmed : trimmed + "/";
    }
}
