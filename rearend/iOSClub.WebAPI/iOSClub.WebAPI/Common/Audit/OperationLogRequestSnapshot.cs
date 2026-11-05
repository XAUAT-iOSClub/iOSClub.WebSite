using System.Text;
using Microsoft.AspNetCore.Routing;

namespace iOSClub.WebAPI.Common.Audit;

/// <summary>一次请求中可安全写入审计表的元数据。</summary>
public sealed record OperationLogSnapshot(
    string Method,
    string Path,
    string? IpAddress,
    string? UserAgent,
    string TraceId,
    string? ContentType,
    long? ContentLength,
    IReadOnlyDictionary<string, string?> RouteValues,
    IReadOnlyDictionary<string, string?> Query,
    string? Body,
    bool BodyIsJson,
    string? BodyOmitted);

/// <summary>
/// 在模型绑定之前采集请求元数据。
/// <para>
/// 关键点：读取 JSON/form 请求体前必须 <c>EnableBuffering</c>，读取后在 finally 中把
/// <c>Request.Body.Position</c> 复位为 0，否则后续 <c>[FromBody]</c> 会拿到空流。
/// multipart 不读取内容，也不调用 <c>Request.Form</c>，避免提前消费上传流。
/// </para>
/// </summary>
public static class OperationLogRequestSnapshot
{
    public const int MaxBodyChars = 16 * 1024;
    private const long MaxBodyBytes = 64 * 1024;

    public static async Task<OperationLogSnapshot> CaptureAsync(HttpRequest request, RouteValueDictionary routeData)
    {
        var route = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in routeData)
        {
            if (key.Equals("controller", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("action", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            route[key] = value?.ToString();
        }

        var query = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in request.Query)
        {
            query[key] = value.ToString();
        }

        var contentType = request.ContentType;
        var contentLength = request.ContentLength;
        string? body = null;
        var bodyIsJson = IsJson(contentType);
        string? bodyOmitted = null;

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            if (contentType?.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase) == true)
            {
                bodyOmitted = $"[multipart/form-data, {contentLength?.ToString() ?? "unknown"} bytes]";
            }
            else if (!IsIdentityEncoding(request.Headers.ContentEncoding.ToString()))
            {
                bodyOmitted = $"[Content-Encoding: {request.Headers.ContentEncoding}，内容已省略]";
            }
            else if (contentLength is > MaxBodyBytes)
            {
                bodyOmitted = $"[请求体过大（{contentLength} 字节），已省略]";
            }
            else if (contentLength != 0)
            {
                request.EnableBuffering();
                try
                {
                    var buffer = new char[MaxBodyChars + 1];
                    var total = 0;

                    using var reader = new StreamReader(request.Body, Encoding.UTF8, false, 1024, leaveOpen: true);
                    while (total < buffer.Length)
                    {
                        var read = await reader.ReadAsync(buffer.AsMemory(total, buffer.Length - total));
                        if (read == 0) break;
                        total += read;
                    }

                    if (total > MaxBodyChars)
                    {
                        bodyOmitted = $"[请求体过大（{contentLength?.ToString() ?? "unknown"} 字节），已省略]";
                    }
                    else if (total > 0)
                    {
                        body = new string(buffer, 0, total);
                    }
                }
                catch (Exception)
                {
                    bodyOmitted = "[请求体读取失败，已省略]";
                }
                finally
                {
                    if (request.Body.CanSeek)
                    {
                        request.Body.Position = 0;
                    }
                }
            }
        }

        return new OperationLogSnapshot(
            request.Method,
            request.Path.Value ?? "",
            request.HttpContext.Connection.RemoteIpAddress?.ToString(),
            request.Headers.UserAgent.ToString(),
            request.HttpContext.TraceIdentifier,
            contentType,
            contentLength,
            route,
            query,
            body,
            bodyIsJson,
            bodyOmitted);
    }

    private static bool IsJson(string? contentType)
        => contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsIdentityEncoding(string encoding)
        => string.IsNullOrWhiteSpace(encoding)
           || encoding.Equals("identity", StringComparison.OrdinalIgnoreCase);
}
