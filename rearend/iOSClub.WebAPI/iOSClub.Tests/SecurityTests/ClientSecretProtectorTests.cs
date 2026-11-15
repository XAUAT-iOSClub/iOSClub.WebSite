using iOSClub.WebAPI.Common.Security;
using Microsoft.AspNetCore.DataProtection;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// OAuth 客户端密钥的可恢复存储。
/// </summary>
public class ClientSecretProtectorTests
{
    private readonly ClientSecretProtector _protector = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Protect_RoundTripsWithoutStoringPlaintext()
    {
        const string secret = "very-secret-value";

        var cipher = _protector.Protect(secret);

        Assert.NotEqual(secret, cipher);
        Assert.Equal(secret, _protector.Unprotect(cipher));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-cipher")]
    public void Unprotect_MissingOrInvalidCipher_ReturnsNull(string? cipher)
        => Assert.Null(_protector.Unprotect(cipher));
}
