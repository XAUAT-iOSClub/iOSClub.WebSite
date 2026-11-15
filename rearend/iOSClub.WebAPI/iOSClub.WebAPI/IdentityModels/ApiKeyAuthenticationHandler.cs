using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using iOSClub.Data;
using iOSClub.Data.DataObjects;
using iOSClub.WebAPI.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace iOSClub.WebAPI.IdentityModels;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDbContextFactory<ClubContext> factory) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKeyBasic";
    public const string ResourceScheme = "ResourceCredentials";
    public const string WritePolicy = "ResourceWrite";
    public const string PermissionClaim = "api_key_permission";
    public const string IdClaim = "api_key_id";

    public static bool IsBasicRequest(HttpContext context) =>
        AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization.ToString(), out var header)
        && header.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        const int credentialLength = 32 + 1 + 64;
        Span<byte> decoded = stackalloc byte[credentialLength];
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var header)
            || !header.Scheme.Equals("Basic", StringComparison.OrdinalIgnoreCase)
            || header.Parameter is not { Length: 132 } encoded
            || !Convert.TryFromBase64String(encoded, decoded, out var length)
            || length != credentialLength || decoded[32] != ':'
            || !Guid.TryParseExact(Encoding.ASCII.GetString(decoded[..32]), "N", out var id))
        {
            return AuthenticateResult.Fail("Invalid API key credentials");
        }

        var hash = SHA256.HashData(decoded[33..]);
        await using var db = await factory.CreateDbContextAsync(Context.RequestAborted);
        var key = await db.ApiKeys.AsNoTracking().SingleOrDefaultAsync(key => key.Id == id, Context.RequestAborted);
        if (key is null || key.ExpiresAt <= DateTime.UtcNow
            || key.Permission is not (ApiKeyPermission.ReadOnly or ApiKeyPermission.ReadWrite)
            || !CryptographicOperations.FixedTimeEquals(hash, key.SecretHash))
        {
            return AuthenticateResult.Fail("Invalid API key credentials");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(IdClaim, id.ToString("N")),
            new Claim(PermissionClaim, key.Permission.ToString())
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Basic realm=\"iOS Club API\"";
        return WriteErrorAsync(ErrorCode.InvalidToken, "API Key 无效、已过期或已撤销");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        WriteErrorAsync(ErrorCode.InsufficientPermission, "API Key 权限不足");

    private Task WriteErrorAsync(int errorCode, string message)
    {
        var body = ApiResponse.Fail(errorCode, message, requestId: Context.TraceIdentifier);
        Response.StatusCode = body.Code;
        return Response.WriteAsJsonAsync(body, Context.RequestAborted);
    }
}
