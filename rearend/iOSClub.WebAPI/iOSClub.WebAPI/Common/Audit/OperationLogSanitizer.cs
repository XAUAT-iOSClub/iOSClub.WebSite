using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using iOSClub.WebAPI.Common.Security;

namespace iOSClub.WebAPI.Common.Audit;

/// <summary>
/// 操作日志脱敏：结构化键名替换 + 值掩码，最后再做长度截断。
/// <para>
/// 注意顺序：必须先完成脱敏再截断。直接截断原始 JSON 可能留下半截密码/token。
/// </para>
/// </summary>
public static class OperationLogSanitizer
{
    public const int MaxChangeContentLength = 8000;
    private const string TruncatedSuffix = "…（已截断）";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 现有 <see cref="SensitiveDataFilter"/> 的 key=value 正则依赖引号回填，遇到
    /// <c>password=xxx&amp;...</c> 这类无引号文本会匹配到空值。这里先做一次结构化替换。
    /// </summary>
    private static readonly Regex KeyValueSecretPattern = new(
        @"(?i)\b(password|passwd|pwd|token|access_token|refresh_token|secret|client_secret|api_key|apikey|authorization|cookie)\b\s*[:=]\s*[^&\s,;]+",
        RegexOptions.Compiled);

    /// <summary>
    /// 组装 { route, query, body } 的脱敏 JSON。body 为空或过大时由 <paramref name="bodyOmitted"/> 提供说明。
    /// </summary>
    public static string? BuildChangeContent(
        IReadOnlyDictionary<string, string?> route,
        IReadOnlyDictionary<string, string?> query,
        string? body,
        bool bodyIsJson,
        string? bodyOmitted)
    {
        var root = new JsonObject();

        if (route.Count > 0)
        {
            root["route"] = SanitizeMap(route);
        }

        if (query.Count > 0)
        {
            root["query"] = SanitizeMap(query);
        }

        if (!string.IsNullOrEmpty(bodyOmitted))
        {
            root["body"] = bodyOmitted;
        }
        else if (!string.IsNullOrEmpty(body))
        {
            if (bodyIsJson)
            {
                try
                {
                    var node = JsonNode.Parse(body);
                    SanitizeNode(node, null);
                    root["body"] = node;
                }
                catch (JsonException)
                {
                    root["body"] = SanitizeText(body);
                }
            }
            else
            {
                root["body"] = SanitizeText(body);
            }
        }

        if (root.Count == 0)
        {
            return null;
        }

        return Cap(root.ToJsonString(JsonOptions), MaxChangeContentLength);
    }

    /// <summary>对 JSON 字符串做递归脱敏；非 JSON 输入按纯文本处理。</summary>
    public static string SanitizeJson(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);

            if (node is JsonValue value && value.TryGetValue<string>(out var text))
            {
                return JsonValue.Create(MaskValue(null, text))!.ToJsonString(JsonOptions);
            }

            SanitizeNode(node, null);
            return node?.ToJsonString(JsonOptions) ?? "null";
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(SanitizeText(json), JsonOptions);
        }
    }

    /// <summary>纯文本脱敏：先替换无引号的 key=value 密钥，再复用系统日志的敏感信息过滤器。</summary>
    public static string SanitizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // 先让系统过滤器处理手机号/邮箱等格式敏感值，再用 key=value 规则兜住
        // 无引号且值非空的密码/token（系统正则对这种情况会匹配到空值）。
        var filtered = SensitiveDataFilter.FilterSensitiveData(text);
        return KeyValueSecretPattern.Replace(filtered, "$1=[REDACTED]");
    }

    /// <summary>按上限截断，保留“已截断”标记。</summary>
    public static string? Cap(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        var keep = maxLength - TruncatedSuffix.Length;
        return keep > 0 ? value[..keep] + TruncatedSuffix : value[..maxLength];
    }

    private static JsonObject SanitizeMap(IReadOnlyDictionary<string, string?> values)
    {
        var result = new JsonObject();
        foreach (var (key, value) in values)
        {
            result[key] = value is null ? null : MaskValue(key, value);
        }

        return result;
    }

    private static void SanitizeNode(JsonNode? node, string? key)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (IsSensitiveKey(property.Key))
                    {
                        obj[property.Key] = "[REDACTED]";
                    }
                    else if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        obj[property.Key] = MaskValue(property.Key, text);
                    }
                    else
                    {
                        SanitizeNode(property.Value, property.Key);
                    }
                }

                break;

            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (key is not null && IsSensitiveKey(key))
                    {
                        array[i] = "[REDACTED]";
                    }
                    else if (array[i] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        array[i] = MaskValue(null, text);
                    }
                    else
                    {
                        SanitizeNode(array[i], key);
                    }
                }

                break;
        }
    }

    private static string MaskValue(string? key, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (!string.IsNullOrEmpty(key))
        {
            if (IsSensitiveKey(key))
            {
                return "[REDACTED]";
            }

            var normalized = NormalizeKey(key);
            if (normalized.Contains("phone") || normalized.Contains("mobile") || normalized.EndsWith("tel"))
            {
                return MaskPhone(value);
            }

            if (normalized.Contains("email") || normalized.Contains("mail"))
            {
                return MaskEmail(value);
            }

            if (normalized.Contains("idcard") || normalized.Contains("identitycard"))
            {
                return MaskIdCard(value);
            }
        }

        return SanitizeText(value);
    }

    private static bool IsSensitiveKey(string key)
    {
        var normalized = NormalizeKey(key);
        return normalized.Contains("password")
               || normalized.Contains("passwd")
               || normalized.Contains("pwd")
               || normalized.EndsWith("token")
               || normalized.EndsWith("secret")
               || normalized is "apikey" or "accesskey" or "privatekey" or "secretkey"
                   or "authorization" or "cookie" or "setcookie" or "credentials"
                   or "verificationcode" or "authcode";
    }

    private static string NormalizeKey(string key)
        => key.ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");

    private static string MaskPhone(string value)
        => value.Length >= 7 ? $"{value[..3]}****{value[^4..]}" : "****";

    private static string MaskEmail(string value)
    {
        var at = value.IndexOf('@');
        if (at <= 0)
        {
            return "****";
        }

        var name = value[..at];
        var domain = value[(at + 1)..];
        var visible = name.Length >= 2 ? name[..2] : name[..1];
        return $"{visible}****@{domain}";
    }

    private static string MaskIdCard(string value)
        => value.Length >= 8 ? $"{value[..6]}***********{value[^1..]}" : "****";
}
