using System.Diagnostics;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iOSClub.Data.DataObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iOSClub.DataApi.Services;

public interface IFinanceDocumentService
{
    bool PdfConfigured { get; }

    /// <summary>学校固定模板路径（随程序发布，不再依赖模板管理页）。</summary>
    string? GetBundledTemplatePath(string kind);

    /// <summary>生成 docx 并落盘，返回相对路径。</summary>
    Task<string> GenerateAsync(FinanceMaterialDO material, IReadOnlyList<FinanceInvoiceDO> invoices, string kind,
        int version, string? templatePath, string operatorName, CancellationToken ct = default);

    byte[] Read(FinanceMaterialDO material, string kind);

    bool HasDocument(FinanceMaterialDO material, string kind);

    Task<byte[]?> TryConvertToPdfAsync(byte[] docx, string baseName, CancellationToken ct = default);

    string BuildFileName(FinanceMaterialDO material, string kind, string extension);
}

/// <summary>
/// 表单生成服务。优先使用模板管理里启用的 docx 模板，按占位符与明细行模板填值；
/// 没有模板时使用内置版式生成，保证功能可用。
/// </summary>
public class FinanceDocumentService(
    IFinanceStorage storage,
    IOptions<FinanceOptions> options,
    ILogger<FinanceDocumentService> logger) : IFinanceDocumentService
{
    private static readonly string[] InvalidFileNameChars = Path.GetInvalidFileNameChars()
        .Select(c => c.ToString()).ToArray();

    private readonly FinanceOptions _options = options.Value;

    public bool PdfConfigured => !string.IsNullOrWhiteSpace(_options.PdfConverterPath);

    public string? GetBundledTemplatePath(string kind)
    {
        var fileName = kind == FinanceFormKind.Apply ? "apply.docx" : "reimburse.docx";
        var path = Path.Combine(AppContext.BaseDirectory, "FinanceTemplates", fileName);
        return File.Exists(path) ? path : null;
    }

    public async Task<string> GenerateAsync(FinanceMaterialDO material, IReadOnlyList<FinanceInvoiceDO> invoices,
        string kind, int version, string? templatePath, string operatorName, CancellationToken ct = default)
    {
        byte[] bytes;
        if (!string.IsNullOrWhiteSpace(templatePath) &&
            (Path.IsPathRooted(templatePath) ? File.Exists(templatePath) : storage.Exists(templatePath)))
        {
            byte[] templateBytes;
            if (Path.IsPathRooted(templatePath))
            {
                templateBytes = await File.ReadAllBytesAsync(templatePath, ct);
            }
            else
            {
                await using var templateStream = storage.OpenRead(templatePath);
                using var templateBuffer = new MemoryStream();
                await templateStream.CopyToAsync(templateBuffer, ct);
                templateBytes = templateBuffer.ToArray();
            }

            try
            {
                bytes = FillTemplate(templateBytes, material, invoices, kind, operatorName);
            }
            catch (Exception ex)
            {
                // 模板损坏不应阻断报销：记录后退回内置版式
                logger.LogWarning(ex, "财务表单模板填充失败，改用内置版式：{Template}", templatePath);
                bytes = kind == FinanceFormKind.Apply
                    ? BuildApplyDocument(material, invoices, operatorName)
                    : BuildReimburseDocument(material, invoices, operatorName);
            }
        }
        else
        {
            bytes = kind == FinanceFormKind.Apply
                ? BuildApplyDocument(material, invoices, operatorName)
                : BuildReimburseDocument(material, invoices, operatorName);
        }

        var relative = await storage.SaveBytesAsync(bytes, "docx", $"materials/{material.Id}", ct);
        return relative;
    }

    public byte[] Read(FinanceMaterialDO material, string kind)
    {
        var path = kind == FinanceFormKind.Apply ? material.ApplyFormPath : material.ReimburseFormPath;
        if (string.IsNullOrWhiteSpace(path) || !storage.Exists(path))
            throw new FileNotFoundException("表单尚未生成，请先生成表单");
        using var stream = storage.OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public bool HasDocument(FinanceMaterialDO material, string kind)
    {
        var path = kind == FinanceFormKind.Apply ? material.ApplyFormPath : material.ReimburseFormPath;
        return !string.IsNullOrWhiteSpace(path) && storage.Exists(path);
    }

    public string BuildFileName(FinanceMaterialDO material, string kind, string extension)
    {
        var activity = string.IsNullOrWhiteSpace(material.ActivityName) ? "未命名活动" : material.ActivityName;
        foreach (var invalid in InvalidFileNameChars) activity = activity.Replace(invalid, "");
        var status = FinanceMaterialStatus.Label(material.Status);
        return $"{activity}_{FinanceFormKind.FileTag(kind)}_{material.Id}_{status}.{extension.TrimStart('.')}";
    }

    public async Task<byte[]?> TryConvertToPdfAsync(byte[] docx, string baseName, CancellationToken ct = default)
    {
        if (!PdfConfigured) return null;

        var converter = _options.PdfConverterPath!;
        var tempDir = Path.Combine(Path.GetTempPath(), "iosclub-finance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var docxPath = Path.Combine(tempDir, Sanitize(baseName) + ".docx");
        var pdfPath = Path.Combine(tempDir, Sanitize(baseName) + ".pdf");

        try
        {
            await File.WriteAllBytesAsync(docxPath, docx, ct);
            var startInfo = new ProcessStartInfo
            {
                FileName = converter,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("--headless");
            startInfo.ArgumentList.Add("--convert-to");
            startInfo.ArgumentList.Add("pdf");
            startInfo.ArgumentList.Add("--outdir");
            startInfo.ArgumentList.Add(tempDir);
            startInfo.ArgumentList.Add(docxPath);

            using var process = Process.Start(startInfo);
            if (process == null) throw new InvalidOperationException("无法启动 PDF 转换器");

            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0 || !File.Exists(pdfPath))
            {
                var error = await process.StandardError.ReadToEndAsync(ct);
                throw new InvalidOperationException($"PDF 转换失败：{error.Trim()}");
            }

            return await File.ReadAllBytesAsync(pdfPath, ct);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
            catch (IOException)
            {
                // 临时目录清理失败不影响导出结果
            }
        }
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value);
        foreach (var invalid in InvalidFileNameChars) builder.Replace(invalid, "");
        return builder.ToString();
    }

    // ===== 内置版式 =====

    private static byte[] BuildApplyDocument(FinanceMaterialDO material, IReadOnlyList<FinanceInvoiceDO> invoices,
        string operatorName)
        => BuildDocument(material, invoices, FinanceFormKind.Apply, operatorName);

    private static byte[] BuildReimburseDocument(FinanceMaterialDO material, IReadOnlyList<FinanceInvoiceDO> invoices,
        string operatorName)
        => BuildDocument(material, invoices, FinanceFormKind.Reimburse, operatorName);

    private static byte[] BuildDocument(FinanceMaterialDO material, IReadOnlyList<FinanceInvoiceDO> invoices,
        string kind, string operatorName)
    {
        var isApply = kind == FinanceFormKind.Apply;
        var items = BuildItemRows(material, invoices, isApply);
        var totalCents = items.Sum(i => i.AmountCents);

        using var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = package.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();
            mainPart.Document.Append(body);

            body.Append(TitleParagraph(isApply ? "社团经费申请表" : "经费报销单"));
            body.Append(SubtitleParagraph($"材料编号：{material.Id}    学年：{material.AcademicYear}"));

            body.Append(BuildInfoTable(material));

            body.Append(SectionTitle("支出清单"));
            AppendItems(body, items, isApply, totalCents);

            body.Append(SpacerParagraph());
            body.Append(TotalsParagraph(isApply ? "申请金额" : "合计", totalCents));
            body.Append(TotalsParagraph("（大写）", 0, FinanceMoney.ToChineseUpper(totalCents)));

            body.Append(SpacerParagraph());
            body.Append(CommitmentParagraph());
            body.Append(SpacerParagraph());
            body.Append(SignatureParagraph("经办人（签字）：________________", "部门负责人（签字）：________________"));
            body.Append(SignatureParagraph("指导老师（签字）：________________", "日期：________________"));

            var sectionProperties = new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin
                {
                    Top = 1440, Bottom = 1440, Left = 1200, Right = 1200, Header = 720U, Footer = 720U, Gutter = 0U
                });
            body.Append(sectionProperties);

            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private sealed record ItemRow(string Item, decimal? Qty, string Unit, decimal? Price, long AmountCents);

    private static List<ItemRow> BuildItemRows(FinanceMaterialDO material, IReadOnlyList<FinanceInvoiceDO> invoices,
        bool isApply)
    {
        // 申请表是“活动前申请经费”，没有单独的预算明细字段，因此用活动经费一行汇总，
        // 保证明细金额与“申请金额”一致；发票明细只用于报销单。
        if (isApply)
        {
            var request = material.ActivityBudgetCents > 0
                ? material.ActivityBudgetCents
                : invoices.Sum(i => i.TotalCents);
            return [new ItemRow("活动经费", 1, "项", FinanceMoney.ToYuan(request), request)];
        }

        return invoices.Select(i =>
        {
            // 报销单的“金额”列使用价税合计；单价按含税金额折算，保证 单价 × 数量 = 金额。
            decimal? price;
            if (i.Qty.HasValue && i.Qty.Value > 0)
                price = Math.Round(FinanceMoney.ToYuan(i.TotalCents) / i.Qty.Value, 2,
                    MidpointRounding.AwayFromZero);
            else if (i.PriceCents.HasValue)
                price = FinanceMoney.ToYuan(i.PriceCents.Value);
            else
                price = null;

            return new ItemRow(
                string.IsNullOrWhiteSpace(i.Item) ? "活动支出" : i.Item!,
                i.Qty,
                string.IsNullOrWhiteSpace(i.Unit) ? "项" : i.Unit!,
                price,
                i.TotalCents);
        }).ToList();
    }

    private static void AppendItems(Body body, IReadOnlyList<ItemRow> items, bool isApply, long totalCents)
    {
        var headers = isApply
            ? new[] { "序号", "支出项目", "单价（元）", "数量", "单位", "金额（元）" }
            : new[] { "序号", "物品名称", "单价（元）", "数量", "单位", "金额（元）" };

        var chunks = items.Chunk(10).ToList();
        if (chunks.Count == 0) chunks.Add([]);

        for (var index = 0; index < chunks.Count; index++)
        {
            if (index > 0) body.Append(PageBreakParagraph());
            var table = CreateItemsTable(headers);
            var number = 0;
            foreach (var item in chunks[index])
            {
                number++;
                table.Append(CreateItemsRow(new[]
                {
                    number.ToString(),
                    item.Item,
                    item.Price?.ToString("0.00") ?? "",
                    item.Qty?.ToString("0.####") ?? "",
                    item.Unit,
                    FinanceMoney.Format(item.AmountCents)
                }));
            }

            // 每页补齐一行合计
            table.Append(CreateItemsRow(new[] { "", "合计", "", "", "", FinanceMoney.Format(totalCents) }, true));
            body.Append(table);
        }
    }

    private static Table CreateItemsTable(string[] headers)
    {
        var table = NewTable();
        var widths = new[] { 700, 3000, 1500, 1100, 1000, 2000 };
        var row = new TableRow(new TableRowProperties(new TableHeader()));
        for (var i = 0; i < headers.Length; i++)
            row.Append(NewCell(headers[i], true, widths[i]));
        table.Append(row);
        return table;
    }

    private static TableRow CreateItemsRow(string[] values, bool bold = false)
    {
        var widths = new[] { 700, 3000, 1500, 1100, 1000, 2000 };
        var row = new TableRow();
        for (var i = 0; i < values.Length; i++)
            row.Append(NewCell(values[i], bold, widths[i]));
        return row;
    }

    private static Table BuildInfoTable(FinanceMaterialDO material)
    {
        var table = NewTable();
        AddInfoRow(table, "活动名称", material.ActivityName ?? "（未填写）");
        AddInfoRow(table, "活动时间", FormatDate(material.ActivityDate));
        AddInfoRow(table, "活动性质", material.ActivityType ?? "");
        AddInfoRow(table, "活动地点", material.Location ?? "");
        AddInfoRow(table, "参加人数", material.Headcount?.ToString() ?? "");
        AddInfoRow(table, "负责人", material.Owner ?? "");
        AddInfoRow(table, "联系电话", material.Phone ?? "");
        AddInfoRow(table, "活动经费", $"¥{FinanceMoney.Format(material.ActivityBudgetCents)}");
        if (!string.IsNullOrWhiteSpace(material.Summary))
            AddInfoRow(table, "活动简介", material.Summary!);
        return table;
    }

    private static void AddInfoRow(Table table, string label, string value)
    {
        var row = new TableRow();
        row.Append(NewCell(label, true, 1800));
        row.Append(NewCell(value, false, 7200));
        table.Append(row);
    }

    private static Table NewTable()
    {
        return new Table(
            new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "999999" })));
    }

    private static TableCell NewCell(string text, bool bold, int width)
    {
        var cell = new TableCell();
        var cellProperties = new TableCellProperties(
            new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });
        if (bold)
            cellProperties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = "E8EEF7" });

        cell.Append(cellProperties);
        cell.Append(new Paragraph(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Left },
                new SpacingBetweenLines { Before = "40", After = "40" }),
            TextRun(text, 20, bold)));
        return cell;
    }

    private static Paragraph TitleParagraph(string text) => new(
        new ParagraphProperties(new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { Before = "0", After = "120" }),
        TextRun(text, 36, true));

    private static Paragraph SubtitleParagraph(string text) => new(
        new ParagraphProperties(new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { After = "160" }),
        TextRun(text, 18, false, "808080"));

    private static Paragraph SectionTitle(string text) => new(
        new ParagraphProperties(new SpacingBetweenLines { Before = "160", After = "80" }),
        TextRun(text, 22, true));

    private static Paragraph SpacerParagraph() => new(new Run(new Text("")));

    private static Paragraph PageBreakParagraph() => new(new Run(new Break { Type = BreakValues.Page }));

    private static Paragraph TotalsParagraph(string label, long cents, string? explicitValue = null) => new(
        new ParagraphProperties(new Justification { Val = JustificationValues.Right },
            new SpacingBetweenLines { Before = "40", After = "40" }),
        TextRun($"{label}：{explicitValue ?? "¥" + FinanceMoney.Format(cents)}", 22, true));

    private static Paragraph CommitmentParagraph() => new(
        new ParagraphProperties(new SpacingBetweenLines { Before = "120", After = "80" }),
        TextRun("本人承诺：以上票据真实有效，费用支出与活动内容相符。如有不实，愿承担相应责任。", 21));

    private static Paragraph SignatureParagraph(string left, string right) => new(
        new ParagraphProperties(new SpacingBetweenLines { Before = "80", After = "80" }),
        TextRun($"{left}        {right}", 21));

    private static Run TextRun(string text, int halfPointSize, bool bold = false, string color = "000000")
    {
        var properties = new RunProperties(
            new RunFonts { Ascii = "宋体", EastAsia = "宋体", HighAnsi = "宋体" },
            new FontSize { Val = halfPointSize.ToString() },
            new Color { Val = color });
        if (bold) properties.Append(new Bold());
        return new Run(properties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static string FormatDate(DateTime? value) =>
        value.HasValue ? FinanceTime.ToChina(value.Value).ToString("yyyy-MM-dd") : "";

    // ===== 模板填充 =====

    private byte[] FillTemplate(byte[] templateBytes, FinanceMaterialDO material,
        IReadOnlyList<FinanceInvoiceDO> invoices, string kind, string operatorName)
    {
        using var stream = new MemoryStream();
        stream.Write(templateBytes);
        stream.Position = 0;

        using (var package = WordprocessingDocument.Open(stream, true))
        {
            var mainPart = package.MainDocumentPart;
            var body = mainPart?.Document?.Body;
            if (mainPart?.Document == null || body == null) return templateBytes;

            var isApply = kind == FinanceFormKind.Apply;
            var hasPlaceholder = body.Descendants<Text>().Any(t =>
                t.Text?.Contains("{{", StringComparison.Ordinal) == true);

            var fixedFilled = !hasPlaceholder && FillFixedTemplate(body, material, invoices, kind);

            if (fixedFilled)
            {
                // 学校固定模板：按真实表头/单元格定位填值，不依赖占位符。
                // 注意不能在这里提前 return：要等 WordprocessingDocument 释放后
                // 内容才会完整刷到 MemoryStream。
                mainPart.Document.Save();
            }
            else
            {
                var items = BuildItemRows(material, invoices, isApply);
                MergePlaceholderParagraphs(body);
                FillItemRows(body, items);

                var totalCents = items.Sum(i => i.AmountCents);

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["activityName"] = material.ActivityName ?? "",
                    ["activityDate"] = FormatDate(material.ActivityDate),
                    ["activityType"] = material.ActivityType ?? "",
                    ["location"] = material.Location ?? "",
                    ["headcount"] = material.Headcount?.ToString() ?? "",
                    ["owner"] = material.Owner ?? "",
                    ["phone"] = material.Phone ?? "",
                    ["summary"] = material.Summary ?? "",
                    ["activityBudget"] = FinanceMoney.Format(material.ActivityBudgetCents),
                    ["totalAmount"] = FinanceMoney.Format(totalCents),
                    ["totalUpper"] = FinanceMoney.ToChineseUpper(totalCents),
                    ["materialNo"] = material.Id,
                    ["operator"] = operatorName,
                    ["exportTime"] = FinanceTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    ["clubName"] = _options.ClubName,
                    ["status"] = FinanceMaterialStatus.Label(material.Status),
                    ["academicYear"] = material.AcademicYear
                };

                ReplacePlaceholders(body, values);
                mainPart.Document.Save();
            }
        }

        return stream.ToArray();
    }

    /// <summary>
    /// 学校固定模板没有占位符时，按真实表头和单元格定位填值。
    /// 目前适配两份模板：
    /// 1. 经费报销单：社团/报销人信息 + 10 行报销清单 + 合计；
    /// 2. 社团经费申请表：活动信息 + 支出清单 + 合计小写/大写。
    /// </summary>
    private bool FillFixedTemplate(Body body, FinanceMaterialDO material,
        IReadOnlyList<FinanceInvoiceDO> invoices, string kind)
    {
        var table = body.Descendants<Table>().FirstOrDefault(t =>
        {
            var text = NormalizeCellText(string.Concat(t.Descendants<Text>().Select(x => x.Text)));
            return kind == FinanceFormKind.Apply
                ? text.Contains("支出项目") && text.Contains("合计（小写）")
                : text.Contains("物品名称") && text.Contains("金额（元）");
        });
        if (table == null)
        {
            logger.LogWarning("财务固定模板：未找到匹配的表格，kind={Kind}", kind);
            return false;
        }


        return kind == FinanceFormKind.Apply
            ? FillApplyFixed(table, material, invoices)
            : FillReimburseFixed(table, material, invoices);
    }

    private bool FillReimburseFixed(Table table, FinanceMaterialDO material,
        IReadOnlyList<FinanceInvoiceDO> invoices)
    {
        SetValueAfterLabel(table, "社团名称", _options.ClubName);
        SetValueAfterLabel(table, "报销人姓名", material.Owner ?? "");
        SetValueAfterLabel(table, "报销人学号", material.CreatedBy ?? "");
        SetValueAfterLabel(table, "报销人电话", material.Phone ?? "");

        var rows = table.Elements<TableRow>().ToList();
        var headerIndex = rows.FindIndex(r =>
            NormalizeCellText(r.InnerText).Contains("物品名称") &&
            NormalizeCellText(r.InnerText).Contains("金额（元）"));
        if (headerIndex < 0) return false;

        var header = ExpandCells(rows[headerIndex]);
        int Column(string label) => header.FindIndex(c => NormalizeCellText(c.InnerText).Contains(label));
        var activityCol = Column("活动名称");
        var itemCol = Column("物品名称");
        var priceCol = Column("单价");
        var qtyCol = Column("数量");
        var unitCol = Column("计数单位");
        if (unitCol < 0) unitCol = Column("单位");
        var amountCol = header.FindLastIndex(c => NormalizeCellText(c.InnerText).Contains("金额"));

        if (itemCol < 0 || amountCol < 0) return false;

        for (var i = 0; i < invoices.Count && i < 10; i++)
        {
            var rowIndex = headerIndex + 1 + i;
            if (rowIndex >= rows.Count) break;
            var cells = ExpandCells(rows[rowIndex]);
            var invoice = invoices[i];

            if (activityCol >= 0) SetCellText(cells[activityCol], material.ActivityName ?? "");
            SetCellText(cells[itemCol], string.IsNullOrWhiteSpace(invoice.Item) ? "活动支出" : invoice.Item!);

            if (priceCol >= 0)
            {
                var price = invoice.Qty is > 0
                    ? Math.Round(FinanceMoney.ToYuan(invoice.TotalCents) / invoice.Qty.Value, 2,
                        MidpointRounding.AwayFromZero)
                    : invoice.PriceCents.HasValue ? FinanceMoney.ToYuan(invoice.PriceCents.Value) : 0;
                SetCellText(cells[priceCol], price.ToString("0.00"));
            }

            if (qtyCol >= 0) SetCellText(cells[qtyCol], invoice.Qty?.ToString("0.####") ?? "");
            if (unitCol >= 0) SetCellText(cells[unitCol], string.IsNullOrWhiteSpace(invoice.Unit) ? "项" : invoice.Unit!);
            SetCellText(cells[amountCol], FinanceMoney.Format(invoice.TotalCents));
        }

        var total = invoices.Sum(i => i.TotalCents);
        SetValueAfterLabel(table, "合计金额", FinanceMoney.Format(total));
        return true;
    }

    private bool FillApplyFixed(Table table, FinanceMaterialDO material, IReadOnlyList<FinanceInvoiceDO> invoices)
    {
        SetValueAfterLabel(table, "申请（主办）单位", _options.ClubName);
        SetValueAfterLabel(table, "承办单位", _options.ClubName);
        SetValueAfterLabel(table, "活动主题", material.ActivityName ?? "");
        SetValueAfterLabel(table, "活动性质", material.ActivityType ?? "");
        SetValueAfterLabel(table, "活动地点", material.Location ?? "");
        SetValueAfterLabel(table, "活动时间", FormatChineseDate(material.ActivityDate));
        SetValueAfterLabel(table, "参加人数", material.Headcount?.ToString() ?? "");
        SetValueAfterLabel(table, "负责人", material.Owner ?? "");
        SetValueAfterLabel(table, "电话", material.Phone ?? "");
        SetValueAfterLabel(table, "活动内容简介", material.Summary ?? "");

        var request = material.ActivityBudgetCents > 0
            ? material.ActivityBudgetCents
            : invoices.Sum(i => i.TotalCents);

        var rows = table.Elements<TableRow>().ToList();
        var headerIndex = rows.FindIndex(r =>
            NormalizeCellText(r.InnerText).Contains("支出项目") &&
            NormalizeCellText(r.InnerText).Contains("金额"));
        if (headerIndex >= 0 && headerIndex + 1 < rows.Count)
        {
            var header = ExpandCells(rows[headerIndex]);
            var itemCol = header.FindIndex(c => NormalizeCellText(c.InnerText).Contains("支出项目"));
            var amountCol = header.FindIndex(c => NormalizeCellText(c.InnerText).Contains("金额"));
            var noteCol = header.FindIndex(c => NormalizeCellText(c.InnerText).Contains("备注"));
            var cells = ExpandCells(rows[headerIndex + 1]);

            if (itemCol >= 0) SetCellText(cells[itemCol], "活动经费");
            if (amountCol >= 0) SetCellText(cells[amountCol], FinanceMoney.Format(request));
            if (noteCol >= 0) SetCellText(cells[noteCol], "活动预算");
        }

        SetValueAfterLabel(table, "合计（小写）", FinanceMoney.Format(request));
        SetValueAfterLabel(table, "合计（大写）", FinanceMoney.ToChineseUpper(request));
        return true;
    }

    private static List<TableCell> ExpandCells(TableRow row)
    {
        var result = new List<TableCell>();
        foreach (var cell in row.Elements<TableCell>())
        {
            var span = cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
            for (var i = 0; i < span; i++) result.Add(cell);
        }

        return result;
    }

    private static bool SetValueAfterLabel(Table table, string label, string value)
    {
        var normalized = NormalizeCellText(label);
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().ToList();
            for (var i = 0; i < cells.Count - 1; i++)
            {
                if (!NormalizeCellText(cells[i].InnerText).Contains(normalized, StringComparison.Ordinal)) continue;
                SetCellText(cells[i + 1], value);
                return true;
            }
        }

        return false;
    }

    private static string NormalizeCellText(string? text) =>
        new string((text ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static string FormatChineseDate(DateTime? value) =>
        value.HasValue ? FinanceTime.ToChina(value.Value).ToString("yyyy年M月d日") : "";

    private const int FilledCellHalfPoints = 16;

    private static void SetCellText(TableCell cell, string value)
    {
        var texts = cell.Descendants<Text>().ToList();
        if (texts.Count == 0)
        {
            cell.RemoveAllChildren<Paragraph>();
            cell.Append(new Paragraph(TextRun(value, FilledCellHalfPoints)));
            return;
        }

        // 模板单元格里常见“空段落 + 有文字的段落”；只保留有文字的那个段落，
        // 否则单元格文本前面会多出一个换行，行高变大后还会把后面的审批栏顶到第二页。
        var textParagraph = texts[0].Ancestors<Paragraph>().FirstOrDefault();
        if (textParagraph == null)
        {
            cell.RemoveAllChildren<Paragraph>();
            cell.Append(new Paragraph(TextRun(value, FilledCellHalfPoints)));
            return;
        }
        foreach (var paragraph in cell.Elements<Paragraph>().ToList())
        {
            if (paragraph != textParagraph)
                paragraph.Remove();
        }

        texts[0].Text = value;
        texts[0].Space = SpaceProcessingModeValues.Preserve;
        SetRunFontSize(texts[0], FilledCellHalfPoints);
        for (var i = 1; i < texts.Count; i++) texts[i].Text = "";
    }

    private static void SetRunFontSize(Text text, int halfPoints)
    {
        var run = text.Ancestors<Run>().FirstOrDefault();
        if (run == null) return;
        var properties = run.RunProperties;
        if (properties == null)
        {
            properties = new RunProperties();
            run.PrependChild(properties);
        }
        var size = properties.GetFirstChild<FontSize>();
        if (size == null)
        {
            properties.Append(new FontSize { Val = halfPoints.ToString() });
        }
        else
        {
            size.Val = halfPoints.ToString();
        }
    }

    private static void ReplacePlaceholders(OpenXmlElement root, IDictionary<string, string> values)
    {
        foreach (var text in root.Descendants<Text>())
        {
            var current = text.Text;
            if (string.IsNullOrEmpty(current) || !current.Contains("{{")) continue;
            foreach (var (key, value) in values)
                current = current.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
            text.Text = current;
            text.Space = SpaceProcessingModeValues.Preserve;
        }
    }

    /// <summary>
    /// Word 经常把一个占位符拆成多个 run（例如 {{activity 与 Name}}）。
    /// 对包含 "{{" 的段落，先把同一段落的文本合并到第一个 run，再做替换，
    /// 避免占位符跨 run 时漏替换。
    /// </summary>
    private static void MergePlaceholderParagraphs(OpenXmlElement root)
    {
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            var texts = paragraph.Descendants<Text>().ToList();
            if (texts.Count <= 1) continue;
            var combined = string.Concat(texts.Select(t => t.Text));
            if (!combined.Contains("{{", StringComparison.Ordinal)) continue;

            texts[0].Text = combined;
            texts[0].Space = SpaceProcessingModeValues.Preserve;
            for (var i = 1; i < texts.Count; i++) texts[i].Text = "";
        }
    }

    private static void FillItemRows(Body body, IReadOnlyList<ItemRow> items)
    {
        var table = body.Descendants<Table>()
            .FirstOrDefault(t => t.Elements<TableRow>().Any(r => r.InnerText.Contains("{{item}}", StringComparison.OrdinalIgnoreCase)));
        if (table == null) return;

        var rows = table.Elements<TableRow>().ToList();
        var templateIndex = rows.FindIndex(r => r.InnerText.Contains("{{item}}", StringComparison.OrdinalIgnoreCase));
        if (templateIndex < 0) return;

        var template = rows[templateIndex];
        var anchor = templateIndex + 1 < rows.Count ? rows[templateIndex + 1] : null;
        template.Remove();

        var number = 0;
        foreach (var item in items)
        {
            number++;
            var clone = (TableRow)template.CloneNode(true);
            var cells = clone.Elements<TableCell>().ToList();
            SetCellText(cells, 0, number.ToString());
            SetCellText(cells, 1, item.Item);
            SetCellText(cells, 2, item.Price?.ToString("0.00") ?? "");
            SetCellText(cells, 3, item.Qty?.ToString("0.####") ?? "");
            SetCellText(cells, 4, item.Unit);
            SetCellText(cells, 5, FinanceMoney.Format(item.AmountCents));

            if (anchor != null) table.InsertBefore(clone, anchor);
            else table.Append(clone);
        }
    }

    private static void SetCellText(IReadOnlyList<TableCell> cells, int index, string value)
    {
        if (index >= cells.Count) return;
        var cell = cells[index];
        var texts = cell.Descendants<Text>().ToList();
        if (texts.Count == 0)
        {
            cell.RemoveAllChildren<Paragraph>();
            cell.Append(new Paragraph(TextRun(value, 20)));
            return;
        }

        texts[0].Text = value;
        texts[0].Space = SpaceProcessingModeValues.Preserve;
        for (var i = 1; i < texts.Count; i++) texts[i].Text = "";
    }

}