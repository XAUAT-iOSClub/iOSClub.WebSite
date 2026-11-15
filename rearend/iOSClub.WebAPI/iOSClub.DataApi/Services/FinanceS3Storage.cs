using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace iOSClub.DataApi.Services;

public sealed class FinanceS3Storage : IDisposable
{
    private readonly AmazonS3Client? _client;
    private readonly string _bucket;
    private readonly string _prefix;
    private readonly Uri? _cdnBase;
    private readonly HttpClient? _cdn;

    public FinanceS3Storage(IOptions<FinanceOptions> options)
    {
        var s3 = options.Value.S3;
        _bucket = s3.Bucket.Trim();
        _prefix = s3.BasePrefix.Trim().Trim('/');
        if (!s3.Enabled) return;

        var region = s3.Region.Trim();
        if (_bucket.Length == 0 || region.Length == 0)
            throw new InvalidOperationException("请配置 S3_BUCKET 和 S3_REGION");
        if (_prefix.Length > 400 || Encoding.UTF8.GetByteCount(_prefix) > 900 ||
            _prefix.Split('/').Any(segment => segment is "." or "..") || _prefix.Any(char.IsControl))
            throw new InvalidOperationException("S3_BASE_PREFIX 过长或包含非法路径片段");

        var config = new AmazonS3Config
        {
            ForcePathStyle = s3.ForcePathStyle,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };
        if (string.IsNullOrWhiteSpace(s3.Endpoint))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
        }
        else
        {
            config.ServiceURL = ReadBaseUrl(s3.Endpoint, "S3_ENDPOINT").AbsoluteUri.TrimEnd('/');
            config.AuthenticationRegion = region;
        }

        var hasAccessKey = !string.IsNullOrWhiteSpace(s3.AccessKeyId);
        var hasSecretKey = !string.IsNullOrWhiteSpace(s3.SecretAccessKey);
        var hasSessionToken = !string.IsNullOrWhiteSpace(s3.SessionToken);
        if (hasAccessKey != hasSecretKey || (hasSessionToken && !hasAccessKey))
            throw new InvalidOperationException("S3_ACCESS_KEY_ID 和 S3_SECRET_ACCESS_KEY 必须一起配置");

        if (!string.IsNullOrWhiteSpace(s3.CdnUrl))
        {
            _cdnBase = ReadBaseUrl(s3.CdnUrl, "S3_CDN_URL", requireHttps: true);
            if (string.IsNullOrWhiteSpace(s3.CdnAuthToken) || s3.CdnAuthToken.Any(char.IsWhiteSpace))
                throw new InvalidOperationException("S3_CDN_URL 需要配置供后端访问私有 CDN 的 S3_CDN_AUTH_TOKEN");

            _cdn = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            {
                DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Bearer", s3.CdnAuthToken) }
            };
        }

        _client = hasAccessKey
            ? new AmazonS3Client(hasSessionToken
                ? new SessionAWSCredentials(s3.AccessKeyId, s3.SecretAccessKey, s3.SessionToken)
                : new BasicAWSCredentials(s3.AccessKeyId, s3.SecretAccessKey), config)
            : new AmazonS3Client(config);
    }

    internal bool Enabled => _client != null;

    internal string BuildKey(string relativePath) =>
        _prefix.Length == 0 ? relativePath : $"{_prefix}/{relativePath}";

    private AmazonS3Client Client => _client ??
        throw new InvalidOperationException("请启用 S3 存储以读取已上传的发票图片");

    internal Task SaveAsync(string key, Stream stream, string contentType, CancellationToken ct) =>
        Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
            UseChunkEncoding = false
        }, ct);

    internal async Task<Stream?> TryOpenReadAsync(string key, CancellationToken ct)
    {
        if (_cdn != null)
        {
            var escapedKey = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
            try
            {
                return await _cdn.GetStreamAsync(new Uri(_cdnBase!, escapedKey), ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // 对象尚未进入 CDN 时从 S3 读取。
            }
        }

        try
        {
            var response = await Client.GetObjectAsync(_bucket, key, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey" ||
                                           (ex.StatusCode == HttpStatusCode.NotFound &&
                                            ex.ErrorCode is null or "" or "NotFound"))
        {
            return null;
        }
    }

    internal Task DeleteAsync(string key, CancellationToken ct) =>
        Client.DeleteObjectAsync(_bucket, key, ct);

    private static Uri ReadBaseUrl(string value, string name, bool requireHttps = false)
    {
        if (!Uri.TryCreate(value.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            string.IsNullOrEmpty(uri.Host) ||
            (uri.Scheme != Uri.UriSchemeHttps && (requireHttps || uri.Scheme != Uri.UriSchemeHttp)) ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw new InvalidOperationException($"{name} 必须是无认证信息、查询参数和片段的 HTTP(S) 地址");

        return uri;
    }

    public void Dispose()
    {
        _cdn?.Dispose();
        _client?.Dispose();
    }
}
