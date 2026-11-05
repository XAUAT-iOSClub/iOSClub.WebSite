using System.Text;
using iOSClub.WebAPI.Common.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// 请求快照必须在模型绑定之前读取并能复位 body；multipart/压缩/超大 body 只记元数据。
/// </summary>
public class OperationLogRequestSnapshotTests
{
    private static DefaultHttpContext CreateContext(
        string method, string? contentType, string? body, string? contentEncoding = null, long? contentLength = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.ContentType = contentType;

        if (body is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bytes);
            context.Request.ContentLength = contentLength ?? bytes.Length;
        }
        else if (contentLength is not null)
        {
            context.Request.ContentLength = contentLength;
        }

        if (!string.IsNullOrEmpty(contentEncoding))
        {
            context.Request.Headers.ContentEncoding = contentEncoding;
        }

        return context;
    }

    [Fact]
    public async Task JsonBody_IsCapturedAndStreamPositionIsReset()
    {
        const string body = """{"userId":"2023000001","name":"张三"}""";
        var context = CreateContext("POST", "application/json", body);

        var snapshot = await OperationLogRequestSnapshot.CaptureAsync(
            context.Request, new RouteValueDictionary { ["controller"] = "MemberManagement", ["action"] = "Create", ["id"] = "2023000001" });

        Assert.Equal(body, snapshot.Body);
        Assert.True(snapshot.BodyIsJson);
        Assert.Null(snapshot.BodyOmitted);
        Assert.True(context.Request.Body.CanSeek);
        Assert.Equal(0, context.Request.Body.Position);

        // 复位后模型绑定仍能读到完整 body。
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
        Assert.Equal(body, await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task RouteAndQuery_AreCapturedAndControllerActionExcluded()
    {
        var context = CreateContext("PUT", "application/json", "{}");
        context.Request.QueryString = new QueryString("?module=成员管理&success=true");

        var snapshot = await OperationLogRequestSnapshot.CaptureAsync(
            context.Request,
            new RouteValueDictionary { ["controller"] = "MemberManagement", ["action"] = "Update", ["id"] = "1" });

        Assert.False(snapshot.RouteValues.ContainsKey("controller"));
        Assert.False(snapshot.RouteValues.ContainsKey("action"));
        Assert.Equal("1", snapshot.RouteValues["id"]);
        Assert.Equal("成员管理", snapshot.Query["module"]);
        Assert.Equal("true", snapshot.Query["success"]);
    }

    [Fact]
    public async Task Multipart_RecordsMetadataOnly()
    {
        var context = CreateContext("POST", "multipart/form-data; boundary=abc", "file-content", contentLength: 1024);

        var snapshot = await OperationLogRequestSnapshot.CaptureAsync(
            context.Request, new RouteValueDictionary());

        Assert.Null(snapshot.Body);
        Assert.Contains("multipart/form-data", snapshot.BodyOmitted);
        Assert.Contains("1024", snapshot.BodyOmitted);
    }

    [Fact]
    public async Task CompressedBody_RecordsMetadataOnly()
    {
        var context = CreateContext("POST", "application/json", "{}", contentEncoding: "gzip");

        var snapshot = await OperationLogRequestSnapshot.CaptureAsync(
            context.Request, new RouteValueDictionary());

        Assert.Null(snapshot.Body);
        Assert.Contains("Content-Encoding", snapshot.BodyOmitted);
    }

    [Fact]
    public async Task OversizedBody_RecordsMetadataOnly()
    {
        var context = CreateContext("POST", "application/json", null, contentLength: 200_000);

        var snapshot = await OperationLogRequestSnapshot.CaptureAsync(
            context.Request, new RouteValueDictionary());

        Assert.Null(snapshot.Body);
        Assert.Contains("请求体过大", snapshot.BodyOmitted);
    }

    [Fact]
    public async Task GetRequest_DoesNotReadBody()
    {
        var context = CreateContext("GET", null, null);

        var snapshot = await OperationLogRequestSnapshot.CaptureAsync(
            context.Request, new RouteValueDictionary { ["id"] = "1" });

        Assert.Null(snapshot.Body);
        Assert.Null(snapshot.BodyOmitted);
        Assert.Equal("GET", snapshot.Method);
    }
}
