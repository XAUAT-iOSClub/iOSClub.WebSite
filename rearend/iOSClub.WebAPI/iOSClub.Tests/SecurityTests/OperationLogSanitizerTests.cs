using iOSClub.WebAPI.Common.Audit;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// 操作日志脱敏的回归测试：结构化键名脱敏、PII 掩码、先脱敏后截断。
/// </summary>
public class OperationLogSanitizerTests
{
    [Fact]
    public void SanitizeJson_RedactsSensitiveKeys()
    {
        const string json = """
            {
              "userId": "2023000001",
              "userName": "张三",
              "password": "SuperSecret123",
              "new_password": "AnotherSecret1",
              "clientSecret": "client-secret-value",
              "access_token": "access-token-value",
              "verificationCode": "123456"
            }
            """;

        var result = OperationLogSanitizer.SanitizeJson(json);

        Assert.Contains("2023000001", result);
        Assert.Contains("张三", result);
        Assert.DoesNotContain("SuperSecret123", result);
        Assert.DoesNotContain("AnotherSecret1", result);
        Assert.DoesNotContain("client-secret-value", result);
        Assert.DoesNotContain("access-token-value", result);
        Assert.DoesNotContain("123456", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void SanitizeJson_MasksPiiByKeyAndFormat()
    {
        const string json = """
            {
              "phoneNum": "13800138000",
              "eMail": "alice@example.com",
              "idCard": "110101199001011234",
              "password": "PiiSecret1",
              "remark": "联系电话 13900139000，邮箱 bob@example.com"
            }
            """;

        var result = OperationLogSanitizer.SanitizeJson(json);

        Assert.DoesNotContain("13800138000", result);
        Assert.DoesNotContain("alice@example.com", result);
        Assert.DoesNotContain("110101199001011234", result);
        Assert.DoesNotContain("PiiSecret1", result);
        Assert.Contains("138****8000", result);
        Assert.Contains("al****@example.com", result);
        Assert.Contains("[REDACTED]", result);
    }

    [Fact]
    public void SanitizeJson_RecursesIntoNestedObjectsAndArrays()
    {
        const string json = """
            {
              "members": [
                { "userId": "2023000001", "password": "OneSecret1" },
                { "userId": "2023000002", "password": "TwoSecret2" }
              ],
              "meta": { "token": "nested-token" }
            }
            """;

        var result = OperationLogSanitizer.SanitizeJson(json);

        Assert.Contains("2023000001", result);
        Assert.Contains("2023000002", result);
        Assert.DoesNotContain("OneSecret1", result);
        Assert.DoesNotContain("TwoSecret2", result);
        Assert.DoesNotContain("nested-token", result);
    }

    [Fact]
    public void SanitizeJson_NonJson_FallsBackToTextMasking()
    {
        const string text = "password=SuperSecret123&phone=13800138000";

        var result = OperationLogSanitizer.SanitizeJson(text);

        Assert.DoesNotContain("SuperSecret123", result);
        Assert.DoesNotContain("13800138000", result);
        Assert.Contains("REDACTED", result);
    }

    [Fact]
    public void BuildChangeContent_RedactsBeforeTruncating()
    {
        var route = new Dictionary<string, string?> { ["id"] = "2023000001" };
        var query = new Dictionary<string, string?> { ["password"] = "QuerySecret1" };
        var body = $$"""{"userId":"2023000001","password":"Secret1","description":"{{new string('x', 9000)}}"}""";

        var result = OperationLogSanitizer.BuildChangeContent(route, query, body, true, null);

        Assert.NotNull(result);
        Assert.Contains("2023000001", result);
        Assert.DoesNotContain("QuerySecret1", result);
        Assert.DoesNotContain(new string('x', 9000), result);
        Assert.Contains("已截断", result);
        Assert.True(result!.Length <= OperationLogSanitizer.MaxChangeContentLength);
    }

    [Fact]
    public void BuildChangeContent_IncludesBodyOmittedMarker()
    {
        var result = OperationLogSanitizer.BuildChangeContent(
            new Dictionary<string, string?>(),
            new Dictionary<string, string?>(),
            null,
            false,
            "[multipart/form-data, 1024 bytes]");

        Assert.NotNull(result);
        Assert.Contains("multipart/form-data", result);
    }

    [Fact]
    public void Cap_PreservesSuffixWithinLimit()
    {
        var result = OperationLogSanitizer.Cap(new string('a', 100), 20);

        Assert.NotNull(result);
        Assert.True(result!.Length <= 20);
        Assert.EndsWith("…（已截断）", result);
    }
}
