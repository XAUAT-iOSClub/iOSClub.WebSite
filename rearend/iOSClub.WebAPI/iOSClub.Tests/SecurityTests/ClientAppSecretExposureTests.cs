using System.Text.Json;
using iOSClub.Data.VOs;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// H1 回归：客户端应用列表/详情接口不得回显 ClientSecret。
/// 数据库里存的是 BCrypt 哈希，一旦回显，读到列表的人就能拿哈希冒充客户端。
/// </summary>
public class ClientAppSecretExposureTests
{
    [Fact]
    public void ClientAppVO_HasNoClientSecretProperty()
        => Assert.Null(typeof(ClientAppVO).GetProperty("ClientSecret"));

    [Fact]
    public void ClientAppVO_Serialization_DoesNotContainClientSecret()
    {
        var json = JsonSerializer.Serialize(new ClientAppVO
        {
            ClientId = "demo",
            ApplicationName = "Demo"
        });

        Assert.DoesNotContain("clientSecret", json, StringComparison.OrdinalIgnoreCase);
    }
}
