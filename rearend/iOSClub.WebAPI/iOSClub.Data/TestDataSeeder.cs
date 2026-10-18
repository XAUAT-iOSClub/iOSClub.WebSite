using iOSClub.Data.DataObjects;
using Microsoft.EntityFrameworkCore;

namespace iOSClub.Data;

/// <summary>
/// 开发环境用的测试账号种子：为「成员 / 部员 / 部长 / 社长」各造一个可登录账号，
/// 便于验证权限与状态流转。仅在 Development 下、且账号不存在时创建（幂等）。
/// 登录方式与普通学生一致：用户名 = UserId，密码 = Test1234。
/// </summary>
public static class TestDataSeeder
{
    public const string DefaultPassword = "Test1234";

    private sealed record TestAccount(string UserId, string Name, string Identity, string ClassName, string Phone, string Gender);

    private static readonly TestAccount[] Accounts =
    [
        new("2023000001", "测试成员", "Member",     "计科2301", "13800000001", "男"),
        new("2023000002", "测试部员", "Department", "计科2302", "13800000002", "女"),
        new("2023000003", "测试部长", "Minister",   "计科2303", "13800000003", "男"),
        new("2023000004", "测试社长", "President",  "计科2304", "13800000004", "女"),
    ];

    public static async Task SeedAsync(ClubContext context)
    {
        var created = new List<string>();

        foreach (var a in Accounts)
        {
            if (!await context.Students.AnyAsync(s => s.UserId == a.UserId))
            {
                context.Students.Add(new StudentDO
                {
                    UserId = a.UserId,
                    UserName = a.Name,
                    Academy = "计算机和信息工程学院",
                    PoliticalLandscape = "共青团员",
                    Gender = a.Gender,
                    ClassName = a.ClassName,
                    PhoneNum = a.Phone,
                    PasswordHash = DataTool.StringToHash(DefaultPassword),
                    EMail = null,
                    JoinTime = DateTime.UtcNow
                });
                created.Add($"{a.Name}({a.UserId})");
            }

            // Member 不写入 Staff（身份即普通成员）；其余写入对应角色
            if (a.Identity != "Member" && !await context.Staffs.AnyAsync(s => s.UserId == a.UserId))
            {
                context.Staffs.Add(new StaffDO { UserId = a.UserId, Name = a.Name, Identity = a.Identity });
            }
        }

        await context.SaveChangesAsync();

        if (created.Count > 0)
        {
            Console.WriteLine($"已创建测试账号（密码 {DefaultPassword}）：{string.Join("、", created)}");
        }
    }
}