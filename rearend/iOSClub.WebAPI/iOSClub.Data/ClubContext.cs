using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using iOSClub.Data.DataObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ParadeDB.EntityFrameworkCore;
using ParadeDB.EntityFrameworkCore.Extensions;

namespace iOSClub.Data;

public sealed class ClubContext(DbContextOptions<ClubContext> options) : DbContext(options)
{
    public DbSet<StudentDO> Students { get; init; }
    public DbSet<StaffDO> Staffs { get; init; }
    public DbSet<ResourceDO> Resources { get; init; }
    public DbSet<DepartmentDO> Departments { get; init; }
    public DbSet<ArticleDO> Articles { get; init; }
    public DbSet<CategoryDO> Categories { get; init; }
    public DbSet<ClientApplicationDO> ClientApplications { get; init; }
    public DbSet<ImportHistoryDO> ImportHistories { get; init; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DepartmentDO>()
            .HasMany(x => x.Staffs)
            .WithOne(x => x.Department)
            .IsRequired(false);

        modelBuilder.Entity<CategoryDO>()
            .HasMany(x => x.Articles)
            .WithOne(x => x.Category)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        modelBuilder.Entity<ArticleDO>()
            .HasOne<DepartmentDO>()
            .WithMany()
            .HasForeignKey(x => x.VisibleToDepartment)
            .HasPrincipalKey(x => x.Name)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // 为频繁用于查询条件的字段添加索引，提高查询效率

        // StudentDO 索引
        modelBuilder.Entity<StudentDO>()
            .HasIndex(s => s.JoinTime); // 用于统计和时间范围查询
        modelBuilder.Entity<StudentDO>()
            .HasIndex(s => s.Academy); // 用于学院统计和过滤
        modelBuilder.Entity<StudentDO>()
            .HasIndex(s => s.UserName); // 用于用户名搜索
        modelBuilder.Entity<StudentDO>()
            .HasIndex(s => s.ClassName); // 用于班级搜索和过滤
        modelBuilder.Entity<StudentDO>()
            .HasIndex(s => s.PhoneNum); // 用于电话号码搜索
        modelBuilder.Entity<StudentDO>()
            .HasIndex(s => s.PoliticalLandscape); // 用于政治面貌统计

        // StaffDO 索引
        modelBuilder.Entity<StaffDO>()
            .HasIndex(s => s.UserId) // 主键索引，同时用于与StudentDO的连接查询
            .IsUnique();
        modelBuilder.Entity<StaffDO>()
            .HasIndex(s => s.Identity); // 用于身份过滤

        // ArticleDO 索引
        modelBuilder.Entity<ArticleDO>()
            .HasIndex(a => a.CategoryId); // 用于按分类查询
        modelBuilder.Entity<ArticleDO>()
            .HasIndex(a => a.VisibleToDepartment); // 用于按部门过滤可见文章

        // ImportHistoryDO 索引：按部门 + 时间倒序拉取导入历史
        modelBuilder.Entity<ImportHistoryDO>()
            .HasIndex(h => new { h.DepartmentName, h.ImportedAt });

        // ParadeDB BM25 全文检索索引（pg_search）
        modelBuilder.Entity<ArticleDO>()
            .HasParadeDbIndex("articles_search_idx", a => a.Path)
            .HasField(a => a.Title, Tokenizer.Jieba())
            .HasField(a => a.Content, Tokenizer.Jieba())
            .HasSearchTokenizer(Tokenizer.Jieba());

        modelBuilder.Entity<StudentDO>()
            .HasParadeDbIndex("students_search_idx", s => s.UserId)
            .HasField(s => s.UserName, Tokenizer.Jieba())
            .HasField(s => s.ClassName, Tokenizer.Jieba())
            .HasField(s => s.Academy, Tokenizer.Jieba())
            .HasSearchTokenizer(Tokenizer.Jieba());
    }
}

[Serializable]
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ClubContext>
{
    public ClubContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ClubContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=paradedb_design", o => o.UseParadeDb());
        return new ClubContext(optionsBuilder.Options);
    }
}

public static class DataTool
{
    /// <summary>
    /// 使用BCrypt进行密码加密
    /// </summary>
    /// <param name="s">密码</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">s 为 null 或者空白时，触发错误</exception>
    public static string StringToHash(string s)
    {
        return string.IsNullOrWhiteSpace(s)
            ? throw new ArgumentException("密码不能为空", nameof(s))
            : BCrypt.Net.BCrypt.HashPassword(s, workFactor: 10); // 工作因子为 10，平衡安全性与性能（原 12 导致登录耗时 200-500ms）
    }

    /// <summary>
    /// 检测密码是否匹配，兼容老的 MD5 加密 和 新的 BCrypt 加密
    /// </summary>
    /// <param name="password">密码原文</param>
    /// <param name="hashPassword">加密之后的密码，一般从数据库中提取出来</param>
    /// <returns></returns>
    public static bool IsOk(string password, string hashPassword)
    {
        // 检测是否为 BCrypt 加密
        // BCrypt 哈希格式: $2a$, $2b$, $2x$, $2y$ 开头，长度通常为 60 字符
        if (!hashPassword.StartsWith("$2") || hashPassword.Length < 59) return ToMd5Hash(password) == hashPassword;
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hashPassword);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // 如果 BCrypt 验证失败，尝试 MD5 验证（兼容性处理）
            return ToMd5Hash(password) == hashPassword;
        }
    }

    /// <summary>
    /// 使用 MD5 进行加密，多使用于一般的Id生成
    /// </summary>
    /// <param name="s"></param>
    /// <returns></returns>
    public static string ToMd5Hash(string s)
    {
        var data = Encoding.UTF8.GetBytes(s);
        var hash = MD5.HashData(data);
        var hashStringBuilder = new StringBuilder();
        foreach (var t in hash)
            hashStringBuilder.Append(t.ToString("x2"));
        return hashStringBuilder.ToString();
    }

    public static string GetProperties<T>(T t)
    {
        var builder = new StringBuilder();
        if (t == null) return builder.ToString();

        var properties = t.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

        if (properties.Length <= 0) return builder.ToString();

        foreach (var item in properties)
        {
            var name = item.Name;
            var value = item.GetValue(t, null);
            if (item.PropertyType.IsValueType || item.PropertyType.Name.StartsWith("String"))
            {
                builder.Append($"{name}:{value ?? "null"},");
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 检测是否为加密之后的数据
    /// </summary>
    /// <param name="modelPasswordHash"></param>
    /// <returns></returns>
    public static bool IsValidHash(string modelPasswordHash)
    {
        return modelPasswordHash.Length >= 32;
    }
}
