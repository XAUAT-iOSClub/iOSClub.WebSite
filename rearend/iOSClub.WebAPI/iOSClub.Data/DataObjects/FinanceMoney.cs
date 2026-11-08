using System.Globalization;
using System.Text;

namespace iOSClub.Data.DataObjects;

/// <summary>
/// 金额换算工具。
/// <para>
/// 数据库统一存"分"（long），所有加减与经费公式都用整数运算，
/// 只在接口边界与展示层换算成"元"（decimal），避免浮点误差。
/// </para>
/// </summary>
public static class FinanceMoney
{
    public const long CentsPerYuan = 100;

    private static readonly string[] Digits = ["零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖"];
    private static readonly string[] SmallUnits = ["", "拾", "佰", "仟"];
    private static readonly string[] GroupUnits = ["", "万", "亿", "万亿"];

    /// <summary>元 → 分，四舍五入到分。</summary>
    public static long FromYuan(decimal yuan) =>
        (long)Math.Round(yuan * CentsPerYuan, 0, MidpointRounding.AwayFromZero);

    /// <summary>分 → 元。</summary>
    public static decimal ToYuan(long cents) => cents / (decimal)CentsPerYuan;

    /// <summary>格式化为两位小数字符串（不带千分位）。</summary>
    public static string Format(long cents) => ToYuan(cents).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>人民币金额转大写，用于经费申请表 / 报销单的"合计（大写）"栏。</summary>
    public static string ToChineseUpper(long cents)
    {
        if (cents == 0) return "零元整";

        var negative = cents < 0;
        var value = Math.Abs(cents);
        var yuan = value / CentsPerYuan;
        var jiao = value / 10 % 10;
        var fen = value % 10;

        var builder = new StringBuilder();
        if (negative) builder.Append('负');

        if (yuan > 0)
        {
            builder.Append(ConvertInteger(yuan)).Append('元');
        }
        else if (jiao > 0 || fen > 0)
        {
            builder.Append("零元");
        }

        if (jiao == 0 && fen == 0)
        {
            builder.Append('整');
            return builder.ToString();
        }

        if (jiao > 0)
        {
            builder.Append(Digits[jiao]).Append('角');
        }
        else if (yuan > 0)
        {
            // 元后面直接跟"分"时补一个零，例如 100.05 → 壹佰元零伍分
            builder.Append('零');
        }

        if (fen > 0)
        {
            builder.Append(Digits[fen]).Append('分');
        }
        else
        {
            builder.Append('整');
        }

        return builder.ToString();
    }

    private static string ConvertInteger(long value)
    {
        var groups = new List<int>();
        var rest = value;
        while (rest > 0)
        {
            groups.Add((int)(rest % 10000));
            rest /= 10000;
        }

        var builder = new StringBuilder();
        var pendingZero = false;

        for (var i = groups.Count - 1; i >= 0; i--)
        {
            var group = groups[i];
            if (group == 0)
            {
                if (builder.Length > 0) pendingZero = true;
                continue;
            }

            // 高位组与当前组之间出现空位，或当前组小于 1000 时需要补"零"
            if (builder.Length > 0 && (pendingZero || group < 1000))
            {
                builder.Append('零');
            }

            builder.Append(ConvertGroup(group)).Append(GroupUnits[i]);
            pendingZero = false;
        }

        return builder.ToString();
    }

    private static string ConvertGroup(int group)
    {
        var builder = new StringBuilder();
        var pendingZero = false;

        for (var position = 3; position >= 0; position--)
        {
            var power = (int)Math.Pow(10, position);
            var digit = group / power % 10;

            if (digit == 0)
            {
                if (builder.Length > 0 && group % power > 0) pendingZero = true;
                continue;
            }

            if (pendingZero)
            {
                builder.Append('零');
                pendingZero = false;
            }

            builder.Append(Digits[digit]).Append(SmallUnits[position]);
        }

        return builder.ToString();
    }
}
