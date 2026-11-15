namespace iOSClub.Data.DataObjects;

/// <summary>
/// 财务模块统一使用中国标准时间（UTC+8）判断学年、开票日期与展示日期。
/// 部署到 Docker 时服务器通常是 UTC，不能直接用 DateTime.Now 判断 9 月 1 日。
/// </summary>
public static class FinanceTime
{
    public static readonly TimeSpan ChinaOffset = TimeSpan.FromHours(8);

    public static DateTime Now => DateTimeOffset.UtcNow.ToOffset(ChinaOffset).DateTime;

    public static DateTime ToChina(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => DateTime.SpecifyKind(value.AddHours(8), DateTimeKind.Unspecified),
        DateTimeKind.Local => DateTime.SpecifyKind(value.ToUniversalTime().AddHours(8), DateTimeKind.Unspecified),
        _ => value
    };

    public static DateTime? ToChina(DateTime? value) => value.HasValue ? ToChina(value.Value) : null;

    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.AddHours(-8), DateTimeKind.Utc)
    };
}

/// <summary>学年计算：9 月 1 日至次年 8 月 31 日为一个学年。</summary>
public static class FinanceAcademicYear
{
    public const int StartMonth = 9;

    /// <summary>取得某个日期所属的学年，如 2026-09-10 → 2026-2027。</summary>
    public static string Of(DateTime date)
    {
        var startYear = date.Month >= StartMonth ? date.Year : date.Year - 1;
        return $"{startYear}-{startYear + 1}";
    }

    /// <summary>当前学年（中国标准时间，9 月 1 日自动切换）。</summary>
    public static string Current => Of(FinanceTime.Now);

    /// <summary>解析学年字符串，失败返回 false。</summary>
    public static bool TryParse(string? value, out int startYear, out int endYear)
    {
        startYear = 0;
        endYear = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out startYear) || !int.TryParse(parts[1], out endYear)) return false;
        return endYear == startYear + 1 && startYear is >= 2000 and <= 2100;
    }

    public static bool IsValid(string? value) => TryParse(value, out _, out _);

    public static (DateTime Start, DateTime End) Range(string academicYear)
    {
        if (!TryParse(academicYear, out var startYear, out _))
            throw new ArgumentException("学年格式应为 2026-2027", nameof(academicYear));

        var start = new DateTime(startYear, StartMonth, 1, 0, 0, 0, DateTimeKind.Local);
        var end = start.AddYears(1).AddTicks(-1);
        return (start, end);
    }

    /// <summary>返回最近若干学年的列表，按新到旧排列，用于前端学年切换。</summary>
    public static List<string> RecentYears(int count = 5)
    {
        var current = Current;
        TryParse(current, out var startYear, out _);
        return Enumerable.Range(0, Math.Max(1, count))
            .Select(offset => $"{startYear - offset}-{startYear - offset + 1}")
            .ToList();
    }
}

/// <summary>经费计算规则（纯函数，便于单元测试与复算）。</summary>
public static class FinanceFundRules
{
    /// <summary>
    /// 扣减额 D = max(0, 本次开销 C - 活动经费 S)。
    /// 开销小于活动经费时差额不退不补，按 0 扣减。
    /// </summary>
    public static long ComputeDeduction(long costCents, long activityBudgetCents)
        => Math.Max(0, costCents - activityBudgetCents);

    /// <summary>剩余经费 R = 年度经费 - 已扣减合计。</summary>
    public static long ComputeRemaining(long budgetCents, long usedCents) => budgetCents - usedCents;

    /// <summary>
    /// 预警级别：剩余为负 → danger；低于年度经费 20% → warning；否则 normal。
    /// 年度经费为 0 且没有扣减时不算预警。
    /// </summary>
    public static string WarningLevel(long budgetCents, long remainingCents)
    {
        if (remainingCents < 0) return "danger";
        if (budgetCents > 0 && remainingCents < budgetCents * 0.2m) return "warning";
        return "normal";
    }
}
