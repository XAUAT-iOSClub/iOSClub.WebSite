using iOSClub.Data.DataObjects;
using iOSClub.DataApi.Services;

namespace iOSClub.Tests.FinanceTests;

public class FinanceMoneyTests
{
    [Theory]
    [InlineData(0, "零元整")]
    [InlineData(10000, "壹佰元整")]
    [InlineData(420050, "肆仟贰佰元伍角整")]
    [InlineData(10005, "壹佰元零伍分")]
    [InlineData(123456, "壹仟贰佰叁拾肆元伍角陆分")]
    [InlineData(100000000, "壹佰万元整")]
    public void ToChineseUpper_ReturnsRmbUppercase(long cents, string expected)
    {
        Assert.Equal(expected, FinanceMoney.ToChineseUpper(cents));
    }

    [Fact]
    public void FromYuan_RoundsToCents()
    {
        Assert.Equal(123456, FinanceMoney.FromYuan(1234.56m));
        Assert.Equal(101, FinanceMoney.FromYuan(1.005m));
    }
}

public class FinanceFundRulesTests
{
    [Theory]
    [InlineData(420000, 0, 420000)]
    [InlineData(150000, 80000, 70000)]
    [InlineData(40000, 60000, 0)]
    [InlineData(10000, 10000, 0)]
    public void ComputeDeduction_OnlyDeductsOverflow(long cost, long budget, long expected)
    {
        Assert.Equal(expected, FinanceFundRules.ComputeDeduction(cost, budget));
    }

    [Theory]
    [InlineData(3000000, 2510000, "normal")]
    [InlineData(3000000, 590000, "warning")]
    [InlineData(3000000, -1, "danger")]
    [InlineData(0, 0, "normal")]
    public void WarningLevel_FollowsPrdThresholds(long budget, long remaining, string expected)
    {
        Assert.Equal(expected, FinanceFundRules.WarningLevel(budget, remaining));
    }

    [Theory]
    [InlineData("2026-09-01", "2026-2027")]
    [InlineData("2026-08-31", "2025-2026")]
    [InlineData("2027-01-15", "2026-2027")]
    public void AcademicYear_StartsInSeptember(string date, string expected)
    {
        Assert.Equal(expected, FinanceAcademicYear.Of(DateTime.Parse(date)));
    }

    [Theory]
    [InlineData("2026-2027", true)]
    [InlineData("2026-2028", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void AcademicYear_ValidatesFormat(string value, bool expected)
    {
        Assert.Equal(expected, FinanceAcademicYear.IsValid(value));
    }
}

public class FinanceOcrParsingTests
{
    [Fact]
    public void ParseText_PdfTablePreservesColumnsAndWrappedItem()
    {
        const string text = """
            电子发票（普通发票）                       发票号码：12345678
            购 名称：西安建筑科技大学                  销 名称：某某图文快印部
                项目名称     规格型号     单 位    数 量       单 价      金 额     税率/征收率     税 额
            *印刷品*桌上立牌                         个       2 10.005        20.01        1%          0.20
            定制
                    合 计                                          ¥20.01                        ¥0.20
                价税合计（大写）                              （小写）¥20.21
            备注  1234567890123456789
            """;

        var result = FinanceOcrService.ParseText(text, new FinanceOptions());

        Assert.True(result.Success);
        Assert.Equal("*印刷品*桌上立牌定制", result.Item);
        Assert.Equal("某某图文快印部", result.Seller);
        Assert.Equal(2m, result.Qty);
        Assert.Equal(10.005m, result.Price);
        Assert.Equal(2001, result.AmountCents);
        Assert.Equal(20, result.TaxCents);
        Assert.Equal(0.8, result.Confidence["buyer"]);
    }

    [Fact]
    public void ParseText_PdfTableAllowsTaxInclusivePriceWhenTaxPrecedesAmount()
    {
        var result = FinanceOcrService.ParseText("""
            发票号码：12345678
            货物或应税劳务、服务名称      数量 / 单价     税率 / 税额     金额
            *工艺品*钥匙扣                100 2.00       0.01  1.98     198.02
            价税合计（大写）                           （小写）200.00
            """, new FinanceOptions());

        Assert.True(result.Success);
        Assert.Equal(100m, result.Qty);
        Assert.Equal(2m, result.Price);
        Assert.Equal(19802, result.AmountCents);
        Assert.Equal(198, result.TaxCents);
        Assert.Equal(20000, result.TotalCents);
    }

    [Theory]
    [InlineData("1 84.3564356435644", true)]
    [InlineData("184.3564356435644", false)]
    public void ParseText_ImageNumbersRequireASeparatedQuantityAndPrice(string quantityAndPrice, bool separated)
    {
        var result = FinanceOcrService.ParseText($"""
            电子发票（普通发票）
            发票号码：12345678
            *塑料制品*标牌
            块
            {quantityAndPrice}
            84.36
            1%
            0.84
            合
            计
            （小写）¥85.20
            """, new FinanceOptions());

        Assert.True(result.Success);
        Assert.Equal(separated ? 1m : null, result.Qty);
        Assert.Equal(separated ? 84.3564356435644m : null, result.Price);
        Assert.Equal("*塑料制品*标牌", result.Item);
    }

    [Fact]
    public void ParseText_PaymentReceiptIsNotAnInvoice()
    {
        const string text = "支付成功\n收款方：示例有限公司\n支付金额：¥18.00";

        var result = FinanceOcrService.ParseText(text, new FinanceOptions());

        Assert.False(result.Success);
    }

    [Fact]
    public void ParseText_ExtractsLabeledInvoiceFields()
    {
        const string text = """
                            增值税电子普通发票
                            发票代码：044001900111
                            发票号码：12345678
                            开票日期：2026年10月20日
                            购买方名称：西安建筑科技大学 iOS Club
                            销售方名称：西安某某科技有限公司
                            货物或应税劳务、服务名称：活动物料
                            数量：2
                            单价：150.00
                            单位：批
                            金额：300.00
                            税额：18.00
                            价税合计(小写)：¥318.00
                            """;

        var result = FinanceOcrService.ParseText(text, new FinanceOptions());

        Assert.True(result.Success);
        Assert.Equal("044001900111", result.Code);
        Assert.Equal("12345678", result.Number);
        Assert.Equal(new DateTime(2026, 10, 20), result.IssueDate);
        Assert.Equal("西安某某科技有限公司", result.Seller);
        Assert.Equal(30000, result.AmountCents);
        Assert.Equal(1800, result.TaxCents);
        Assert.Equal(31800, result.TotalCents);
        Assert.Equal(2m, result.Qty);
        Assert.True(result.Confidence["number"] >= 0.9);
    }

    [Fact]
    public void ParseText_WithoutKeyFields_FailsGracefully()
    {
        var result = FinanceOcrService.ParseText("这是一张无法识别的图片", new FinanceOptions());

        Assert.False(result.Success);
        Assert.Contains("手工录入", result.Error);
    }
}
