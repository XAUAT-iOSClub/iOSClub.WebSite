using System.Reflection;
using iOSClub.WebAPI.Common.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace iOSClub.Tests.SecurityTests;

/// <summary>
/// 目录覆盖检查：以后新增写接口却忘了补中文模块/操作时，这个测试会失败。
/// </summary>
public class OperationLogCatalogTests
{
    [Fact]
    public void EveryWriteAction_HasExplicitCatalogEntry()
    {
        var missing = new List<string>();

        foreach (var type in typeof(Program).Assembly.GetTypes()
                     .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t)))
        {
            var controller = type.Name.EndsWith("Controller", StringComparison.Ordinal)
                ? type.Name[..^"Controller".Length]
                : type.Name;

            if (controller is "Auth" or "SSO")
            {
                continue;
            }

            var classAnonymous = type.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var isWrite = method.GetCustomAttributes<HttpPostAttribute>().Any()
                              || method.GetCustomAttributes<HttpPutAttribute>().Any()
                              || method.GetCustomAttributes<HttpPatchAttribute>().Any()
                              || method.GetCustomAttributes<HttpDeleteAttribute>().Any();

                if (!isWrite && !OperationLogCatalog.IsMutatingGet(controller, method.Name))
                {
                    continue;
                }

                if (classAnonymous || method.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
                {
                    continue;
                }

                if (!OperationLogCatalog.TryResolve(controller, method.Name, out _, out _))
                {
                    missing.Add($"{controller}.{method.Name}");
                }
            }
        }

        Assert.True(missing.Count == 0,
            "以下写接口缺少中文模块/操作映射：" + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("Category", "DeleteCategory")]
    [InlineData("Department", "DeleteDepartment")]
    [InlineData("Staff", "DeleteStaff")]
    public void MutatingGetActions_AreWhitelistedAndResolved(string controller, string action)
    {
        Assert.True(OperationLogCatalog.IsMutatingGet(controller, action));
        Assert.True(OperationLogCatalog.TryResolve(controller, action, out var module, out var label));
        Assert.False(string.IsNullOrWhiteSpace(module));
        Assert.False(string.IsNullOrWhiteSpace(label));
    }
}
