using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using iOSClub.Data.DataObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace iOSClub.DataApi.Services;

/// <summary>识别结果。字段为空表示未识别到，由人工补录。</summary>
public class FinanceOcrResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? RawText { get; set; }

    public string? Type { get; set; }
    public string? Code { get; set; }
    public string? Number { get; set; }
    public DateTime? IssueDate { get; set; }
    public string? Buyer { get; set; }
    public string? Seller { get; set; }
    public string? Item { get; set; }
    public decimal? Qty { get; set; }
    public decimal? Price { get; set; }
    public string? Unit { get; set; }
    public long AmountCents { get; set; }
    public long TaxCents { get; set; }
    public long TotalCents { get; set; }

    public Dictionary<string, double> Confidence { get; set; } = new();
}

public interface IFinanceOcrService
{
    Task<FinanceOcrResult> RecognizeAsync(Stream stream, string fileName, string contentType,
        CancellationToken ct = default);
}

/// <summary>
/// 可替换的发票识别服务：
/// 1. docx 直接解析 word/document.xml 文本；
/// 2. 带文本层的 PDF 解析内容流文本；
/// 3. 图片与扫描件调用 Finance:OcrEndpoint 配置的外部 OCR；
/// 4. 都不可用时返回失败原因，保留原件并转手工录入，不阻断流程。
/// </summary>
public class FinanceOcrService : IFinanceOcrService
{
    private static readonly Regex SpaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex MoneyRegex = new(@"[-+]?\d[\d,]*\.?\d*", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly ILogger<FinanceOcrService> _logger;
    private readonly FinanceOptions _options;

    public FinanceOcrService(HttpClient httpClient, IOptions<FinanceOptions> options,
        ILogger<FinanceOcrService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<FinanceOcrResult> RecognizeAsync(Stream stream, string fileName, string contentType,
        CancellationToken ct = default)
    {
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.RecognitionTimeoutSeconds, 5, 60)));

        try
        {
            string? text = extension switch
            {
                "docx" => ExtractDocxText(bytes),
                "pdf" => PdfTextExtractor.TryExtract(bytes),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(text) && ContainsInvoiceSignal(text))
            {
                var local = ParseText(text, _options);
                if (local.Success) return local;
            }

            if (!string.IsNullOrWhiteSpace(_options.OcrEndpoint))
            {
                // PDF 先整份交给 OCR：本地服务可直接解析文本层，只有图片型 PDF 才需要逐页/逐图 OCR。
                FinanceOcrResult? bestFailure = null;
                if (extension == "pdf")
                {
                    var wholePdf = await CallRemoteOcrAsync(bytes, fileName, "application/pdf", timeoutCts.Token);
                    if (wholePdf.Success) return wholePdf;
                    bestFailure = wholePdf;
                }

                var images = extension switch
                {
                    "docx" => ExtractDocxImages(bytes),
                    "pdf" => ExtractPdfJpegImages(bytes),
                    _ => new List<ImageCandidate>()
                };

                if (images.Count > 0)
                {
                    var results = new List<FinanceOcrResult>();
                    foreach (var image in images.Take(20))
                    {
                        var result = await CallRemoteOcrAsync(image.Bytes, image.FileName, image.ContentType,
                            timeoutCts.Token);
                        results.Add(result);
                        if (!result.Success && result.Error != null) bestFailure = result;
                    }

                    var best = results
                        .Where(r => r.Success)
                        .OrderByDescending(Score)
                        .FirstOrDefault();
                    if (best != null) return best;
                }

                if (extension != "pdf")
                {
                    var whole = await CallRemoteOcrAsync(bytes, fileName, contentType, timeoutCts.Token);
                    if (whole.Success) return whole;
                    bestFailure ??= whole;
                }

                return bestFailure ?? new FinanceOcrResult
                {
                    Success = false,
                    Error = "OCR 服务未返回可用结果，请手工录入"
                };
            }

            return new FinanceOcrResult
            {
                Success = false,
                RawText = text,
                Error = extension is "docx" or "pdf"
                    ? "文件内没有可解析的文本层（docx/PDF 中可能是发票图片），且未配置 OCR 服务，请手工录入"
                    : "未配置 OCR 服务，请手工录入"
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FinanceOcrResult { Success = false, Error = "识别超时，请重试或手工录入" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "发票识别失败：{FileName}", fileName);
            return new FinanceOcrResult { Success = false, Error = $"识别失败：{ex.Message}" };
        }
    }

    private sealed record ImageCandidate(byte[] Bytes, string FileName, string ContentType);

    private static int Score(FinanceOcrResult result)
    {
        if (!result.Success) return -1;
        var score = result.TotalCents > 0 ? 10 : 0;
        score += result.Confidence.Count(pair => pair.Value > 0.5);
        if (!string.IsNullOrWhiteSpace(result.Number)) score += 5;
        return score;
    }

    /// <summary>docx 的发票常常是插入的图片，从 word/media 中抽出图片再交给 OCR。</summary>
    private static List<ImageCandidate> ExtractDocxImages(byte[] bytes)
    {
        var images = new List<ImageCandidate>();
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, false);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith("word/media/", StringComparison.OrdinalIgnoreCase)) continue;
                var extension = Path.GetExtension(entry.Name).TrimStart('.').ToLowerInvariant();
                if (extension is not ("png" or "jpg" or "jpeg" or "bmp" or "webp")) continue;
                if (entry.Length is <= 0 or > 10 * 1024 * 1024) continue;

                using var stream = entry.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                if (buffer.Length < 1024) continue;

                images.Add(new ImageCandidate(
                    buffer.ToArray(),
                    $"invoice-image-{images.Count + 1}.{(extension == "jpg" ? "jpeg" : extension)}",
                    $"image/{(extension == "jpg" ? "jpeg" : extension)}"));
            }
        }
        catch
        {
            // 损坏的 docx 交给后续整文件 OCR 或手工录入
        }

        return images;
    }

    /// <summary>
    /// 扫描 PDF 中的 JPEG 图片流（DCTDecode 常见于扫描件）。
    /// 只能抽 JPEG，其他压缩方式交给整文件 OCR 服务处理。
    /// </summary>
    private static List<ImageCandidate> ExtractPdfJpegImages(byte[] bytes)
    {
        var images = new List<ImageCandidate>();
        for (var i = 0; i < bytes.Length - 3 && images.Count < 20; i++)
        {
            if (bytes[i] != 0xFF || bytes[i + 1] != 0xD8 || bytes[i + 2] != 0xFF) continue;

            var end = -1;
            for (var j = i + 2; j < bytes.Length - 1; j++)
            {
                if (bytes[j] != 0xFF || bytes[j + 1] != 0xD9) continue;
                end = j + 2;
                break;
            }

            if (end <= i + 1024) continue;
            var image = new byte[end - i];
            Array.Copy(bytes, i, image, 0, image.Length);
            images.Add(new ImageCandidate(image, $"invoice-page-{images.Count + 1}.jpeg", "image/jpeg"));
            i = end - 1;
        }

        return images;
    }

    private async Task<FinanceOcrResult> CallRemoteOcrAsync(byte[] bytes, string fileName, string contentType,
        CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(fileContent, "file", fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.OcrEndpoint) { Content = form };
        if (!string.IsNullOrWhiteSpace(_options.OcrApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OcrApiKey);

        using var response = await _httpClient.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return new FinanceOcrResult { Success = false, Error = $"OCR 服务返回 {(int)response.StatusCode}" };

        try
        {
            using var document = JsonDocument.Parse(payload);
            return ParseRemoteJson(document.RootElement, _options);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "OCR 服务返回内容不是合法 JSON");
            return new FinanceOcrResult { Success = false, Error = "OCR 服务返回格式不正确" };
        }
    }

    private static FinanceOcrResult ParseRemoteJson(JsonElement root, FinanceOptions options)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return new FinanceOcrResult { Success = false, Error = "OCR 服务返回格式不正确" };

        // 兼容 { data: {...} } 与直接返回字段两种结构
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            root = data;

        var lines = ReadLines(root);

        // 有坐标行时优先按表格版面解析；rawText 继续作为无坐标时的兜底。
        foreach (var name in new[] { "rawText", "text" })
        {
            if (root.TryGetProperty(name, out var raw) && raw.ValueKind == JsonValueKind.String)
            {
                var rawText = raw.GetString();
                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    var parsed = ParseText(rawText, options);
                    if (lines.Count > 0) ApplyLayout(parsed, lines);
                    return parsed;
                }
            }
        }

        var result = new FinanceOcrResult { Success = true };


        string? Str(string name)
        {
            if (!root.TryGetProperty(name, out var element)) return null;
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                _ => null
            };
        }

        result.Type = Str("type");
        result.Code = Str("code");
        result.Number = Str("number");
        result.Buyer = Str("buyer");
        result.Seller = Str("seller");
        result.Item = Str("item");
        result.Unit = Str("unit");
        result.RawText = Str("rawText");

        var issue = Str("issueDate");
        if (DateTime.TryParse(issue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var issueDate))
            result.IssueDate = issueDate;

        decimal? Dec(string name)
        {
            var value = Str(name);
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        result.Qty = Dec("qty");
        result.Price = Dec("price");
        var amount = Dec("amount");
        var tax = Dec("tax");
        var total = Dec("total");
        if (amount.HasValue) result.AmountCents = FinanceMoney.FromYuan(amount.Value);
        if (tax.HasValue) result.TaxCents = FinanceMoney.FromYuan(tax.Value);
        if (total.HasValue) result.TotalCents = FinanceMoney.FromYuan(total.Value);
        else if (amount.HasValue) result.TotalCents = result.AmountCents + result.TaxCents;

        if (root.TryGetProperty("confidence", out var confidence) && confidence.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in confidence.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number)
                    result.Confidence[property.Name] = property.Value.GetDouble();
            }
        }
        else
        {
            foreach (var field in FieldNames)
                if (!string.IsNullOrWhiteSpace(GetField(result, field)))
                    result.Confidence[field] = 0.9;
        }

        if (string.IsNullOrWhiteSpace(result.Number) && result.TotalCents <= 0)
        {
            return new FinanceOcrResult
            {
                Success = false,
                RawText = result.RawText,
                Error = "OCR 服务未返回可用的发票字段，请手工录入"
            };
        }

        return result;
    }

    private static readonly string[] FieldNames =
        ["type", "code", "number", "issueDate", "buyer", "seller", "item", "qty", "price", "unit", "amount", "tax", "total"];

    private static string? GetField(FinanceOcrResult result, string field) => field switch
    {
        "type" => result.Type,
        "code" => result.Code,
        "number" => result.Number,
        "issueDate" => result.IssueDate?.ToString("yyyy-MM-dd"),
        "buyer" => result.Buyer,
        "seller" => result.Seller,
        "item" => result.Item,
        "qty" => result.Qty?.ToString(CultureInfo.InvariantCulture),
        "price" => result.Price?.ToString(CultureInfo.InvariantCulture),
        "unit" => result.Unit,
        "amount" => result.AmountCents.ToString(),
        "tax" => result.TaxCents.ToString(),
        "total" => result.TotalCents.ToString(),
        _ => null
    };

    /// <summary>解析 docx 的正文文本（仅取文本，不依赖 Word 组件）。</summary>
    internal static string? ExtractDocxText(byte[] bytes)
    {
        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, false);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null) return null;

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

            var lines = document.Descendants(w + "p")
                .Select(paragraph => string.Concat(paragraph.Descendants(w + "t").Select(t => t.Value)))
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join("\n", lines);
        }
        catch
        {
            return null;
        }
    }

    private static bool ContainsInvoiceSignal(string text) =>
        text.Contains('发') && (text.Contains('票') || text.Contains('号'));

    private sealed record OcrLine(string Text, double X0, double Y0, double X1, double Y1);

    private static List<OcrLine> ReadLines(JsonElement root)
    {
        var lines = new List<OcrLine>();
        if (!root.TryGetProperty("lines", out var array) || array.ValueKind != JsonValueKind.Array)
            return lines;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var text = item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(text)) continue;

            double Num(string name) =>
                item.TryGetProperty(name, out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetDouble(out var parsed) ? parsed : 0;

            var line = new OcrLine(text, Num("x0"), Num("y0"), Num("x1"), Num("y1"));
            var key = $"{line.Text}|{line.X0:0.##}|{line.Y0:0.##}|{line.X1:0.##}|{line.Y1:0.##}";
            if (seen.Add(key)) lines.Add(line);
        }

        return lines;
    }

    /// <summary>
    /// 标准发票表格的按列解析：PDF 文本层和 OCR 框都带坐标，
    /// 用表头中心定位列，避免把“合计/大写金额/开票人”误拼进项目名。
    /// </summary>
    private static void ApplyLayout(FinanceOcrResult result, IReadOnlyList<OcrLine> lines)
    {
        static string Norm(string text) => SpaceRegex.Replace(text.Replace('：', ':'), "");

        OcrLine? Header(params string[] patterns) => lines
            .Where(line => patterns.Any(pattern => Regex.IsMatch(Norm(line.Text), pattern)))
            .OrderBy(line => line.Y0)
            .FirstOrDefault();

        var itemHeader = Header(@"项目名称", @"货物或应税劳务.*名称", @"货物名称", @"服务名称");
        var specHeader = Header(@"规格型号");
        var unitHeader = Header(@"^单位$");
        var qtyHeader = Header(@"^数量$");
        var priceHeader = Header(@"^单价$");
        var amountHeader = Header(@"^金额$");
        var taxRateHeader = Header(@"税率", @"征收率");
        var taxHeader = Header(@"^税额$");

        var headers = new List<(string Role, OcrLine Line)>();
        void AddHeader(string role, OcrLine? line)
        {
            if (line != null) headers.Add((role, line));
        }
        AddHeader("item", itemHeader);
        AddHeader("spec", specHeader);
        AddHeader("unit", unitHeader);
        AddHeader("qty", qtyHeader);
        AddHeader("price", priceHeader);
        AddHeader("amount", amountHeader);
        AddHeader("taxRate", taxRateHeader);
        AddHeader("tax", taxHeader);
        if (headers.Count < 5) return;

        var headerY = headers.Min(x => x.Line.Y0);
        var totalsY = lines
            .Where(line => Norm(line.Text) is "合" or "计" or "合计" or "价税合计(大写)" or "价税合计")
            .Select(line => (double?)line.Y0)
            .Min();
        var lowerBound = totalsY ?? double.MaxValue;

        var itemParts = new List<(double Y, double X, string Text)>();
        string? unit = null;
        decimal? qty = null;
        decimal? price = null;
        decimal? amount = null;
        decimal? tax = null;

        foreach (var line in lines.OrderBy(line => line.Y0).ThenBy(line => line.X0))
        {
            if (line.Y0 <= headerY + 2 || line.Y1 >= lowerBound) continue;
            var text = Norm(line.Text);
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (headers.Any(h => ReferenceEquals(h.Line, line))) continue;
            if (Regex.IsMatch(text, @"^(合计|价税合计|备注|开票人)")) break;

            // 规格型号列经常是尺寸/型号，不应当拼进商品名称。
            static bool IsSpecText(string value) =>
                Regex.IsMatch(value, @"^\d+(?:\.\d+)?(?:cm|mm|CM|MM)?$") ||
                value.Contains("cm", StringComparison.OrdinalIgnoreCase) ||
                value.Contains("mm", StringComparison.OrdinalIgnoreCase);

            // 规格列里的型号/容量/接口通常没有汉字，不并入项目名称。
            static bool IsModelLikeSpec(string value) =>
                !value.Any(c => c >= '\u4e00' && c <= '\u9fff') ||
                Regex.IsMatch(value, @"(USB|Type-?C|\d+\s*(GB|TB)|[A-Z]{2,}\d)", RegexOptions.IgnoreCase);

            // Paddle 有时把“数量+单价”识别成一行；这一行跨越两列时交给 rawText 解析，
            // 否则会把 15 / 6.36 误读成单价 156.36。
            if (qtyHeader != null && priceHeader != null &&
                line.X0 < qtyHeader.X1 && line.X1 > priceHeader.X0 &&
                Regex.IsMatch(text, @"^\d+(?:\.\d+)?$"))
            {
                continue;
            }

            var center = (line.X0 + line.X1) / 2;
            var role = headers
                .OrderBy(h => Math.Abs((h.Line.X0 + h.Line.X1) / 2 - center))
                .First().Role;

            switch (role)
            {
                case "item":
                    if (!IsSpecText(text) && !text.StartsWith("规格型号") && !text.StartsWith("单位") &&
                        !itemParts.Any(part => part.Text == text))
                        itemParts.Add((line.Y0, line.X0, text));
                    break;
                case "spec":
                    if (!IsSpecText(text) && !IsModelLikeSpec(text) && !text.StartsWith("单位") &&
                        !itemParts.Any(part => part.Text == text))
                        itemParts.Add((line.Y0, line.X0, text));
                    break;
                case "unit":
                    if (Regex.IsMatch(text, @"^(个|批|项|套|张|份|箱|台|本|块|支|盒|包|件|米|千克|kg|把|只|条|双|组|卷|袋|瓶|罐|枚|片|粒|副|辆|艘|架|部)$"))
                        unit ??= text;
                    break;
                case "qty":
                    if (qty == null && decimal.TryParse(OnlyNumber(text), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out var parsedQty))
                        qty = parsedQty;
                    break;
                case "price":
                    if (price == null && decimal.TryParse(OnlyNumber(text), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out var parsedPrice))
                        price = parsedPrice;
                    break;
                case "amount":
                    if (amount == null && decimal.TryParse(OnlyNumber(text), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out var parsedAmount))
                        amount = parsedAmount;
                    break;
                case "tax":
                    if (tax == null && decimal.TryParse(OnlyNumber(text), NumberStyles.Any,
                            CultureInfo.InvariantCulture, out var parsedTax))
                        tax = parsedTax;
                    break;
            }
        }

        var orderedNames = itemParts.OrderBy(part => part.Y).ThenBy(part => part.X).Select(part => part.Text).ToList();
        var itemText = "";
        foreach (var part in orderedNames)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            if (itemText.Length == 0)
            {
                itemText = part;
                continue;
            }
            // 单字（如“本”）是上一段规格的续行；其余片段之间补一个顿号。
            var needsSeparator = part.Length > 1 &&
                                 !itemText.EndsWith('、') && !itemText.EndsWith('，') &&
                                 !itemText.EndsWith(',') && !itemText.EndsWith('；');
            itemText += (needsSeparator ? "、" : "") + part;
        }
        if (!string.IsNullOrWhiteSpace(itemText)) result.Item = itemText.Trim();

        if (!string.IsNullOrWhiteSpace(unit)) result.Unit = unit;
        if (qty is > 0) result.Qty = qty;
        if (price is > 0) result.Price = price;
        if (amount is > 0) result.AmountCents = FinanceMoney.FromYuan(amount.Value);
        if (tax is >= 0) result.TaxCents = FinanceMoney.FromYuan(tax.Value);

        if (result.Qty is not > 0 && result.Price is > 0 && result.AmountCents > 0)
        {
            var ratio = FinanceMoney.ToYuan(result.AmountCents) / result.Price.Value;
            var rounded = Math.Round(ratio);
            if (rounded >= 1 && Math.Abs(ratio - rounded) <= 0.05m) result.Qty = rounded;
        }

        // 发票抬头区域：购买方在左、销售方在右；用坐标兜底，避免把金额/税号当成销售方。
        var partyLines = lines
            .Where(line => line.Y1 < headerY - 2 && line.Y0 > 40)
            .Select(line =>
            {
                var raw = Norm(line.Text);
                var name = Regex.Match(raw, @"^名称\s*[:：]?\s*(.*)$");
                return new { Line = line, Raw = raw, Text = name.Success ? name.Groups[1].Value : raw };
            })
            .Where(item =>
            {
                var text = item.Text;
                return text.Length is >= 4 and <= 80 &&
                       !Regex.IsMatch(text, @"^[0-9A-Z]{15,20}$") &&
                       !Regex.IsMatch(text, @"^(统一社会信用|发票号码|开票日期|电子发票|购买方|销售方|购|买|方|信|息|销|售)") &&
                       !text.Contains("税务局") && !text.Contains("税务") && !text.Contains("总局") &&
                       !text.Contains("查验") && !text.Contains("打印") && !text.Contains("次数") &&
                       !text.Contains("时间") &&
                       !Regex.IsMatch(text, @"^\d{4}\s*年") && !text.Contains("年") &&
                       !text.Contains("社会信用代码") && !text.Contains("纳税人识别号") &&
                       !text.Contains("识别号") && !text.Contains("信用代码") &&
                       !text.Contains("地址") && !text.Contains("电话") &&
                       !text.Contains("银行") && !text.Contains("账号") &&
                       !text.Contains("下载次数") && !text.Contains("开票日期");
            })
            .OrderBy(item => (item.Line.X0 + item.Line.X1) / 2)
            .ToList();

        if (partyLines.Count >= 2)
        {
            var buyer = partyLines[0].Text;
            if (buyer.Contains("建筑科技")) buyer = "西安建筑科技大学";
            result.Buyer = buyer;
            result.Seller = partyLines[^1].Text;
        }
    }

    private static string OnlyNumber(string text)
    {
        var match = Regex.Match(text.Replace(",", ""), @"[-+]?\d+(?:\.\d+)?");
        return match.Success ? match.Value : "";
    }

    /// <summary>从文本中按标签抽取发票字段。纯函数，便于单测。</summary>
    public static FinanceOcrResult ParseText(string text, FinanceOptions options)
    {
        var normalized = Regex.Replace(
            text.Replace('：', ':').Replace('（', '(').Replace('）', ')'),
            @"[ \t\u00A0]+", "");
        var result = new FinanceOcrResult { Success = true, RawText = text };

        string? Capture(string pattern, int group = 1)
        {
            // 标签和值可能分两行；这里只允许同一行内的空格/制表符，避免把下一行表头当成值。
            var sameLinePattern = pattern.Replace(@"\s*", @"[ \t]*");
            var match = Regex.Match(normalized, sameLinePattern, RegexOptions.Multiline);
            return match.Success ? match.Groups[group].Value.Trim() : null;
        }

        void Set(string field, string? value, double confidence)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            result.Confidence[field] = confidence;
        }

        result.Type = Capture(@"(增值税(?:专用|普通)?(?:电子)?发票|增值税电子(?:专用|普通)?发票|电子发票(?:\(普通发票\))?|机动车销售统一发票|通用机打发票)", 1);
        if (string.IsNullOrWhiteSpace(result.Type))
        {
            var flat = SpaceRegex.Replace(text, "");
            if (flat.Contains("电子发") && flat.Contains("普通"))
                result.Type = "电子发票(普通发票)";
            else if (flat.Contains("增值税") && flat.Contains("普通"))
                result.Type = "增值税电子普通发票";
            else if (flat.Contains("电子发"))
                result.Type = "电子发票";
        }
        Set("type", result.Type, 0.95);

        result.Code = Capture(@"(?:发票代码|代码)\s*[:：]?\s*(\d{10,12})");
        Set("code", result.Code, 0.95);

        result.Number = Capture(@"(?:发票号码|号码)\s*[:：]?\s*(\d{8,20})");
        if (string.IsNullOrWhiteSpace(result.Number))
        {
            // OCR 常把“发票号码”标签读错；取最长的 8-20 位数字作为发票号兜底
            var candidate = Regex.Matches(normalized, @"(?<!\d)\d{8,20}(?!\d)")
                .Cast<Match>()
                .Select(m => m.Value)
                .OrderByDescending(v => v.Length)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                result.Number = candidate;
                Set("number", candidate, 0.55);
            }
        }
        Set("number", result.Number, 0.95);

        var dateText = Capture(@"(?:开票日期|日期)\s*[:：]?\s*(\d{4}\s*[年\-/.]\s*\d{1,2}\s*[月\-/.]\s*\d{1,2}\s*日?)");
        if (string.IsNullOrWhiteSpace(dateText))
        {
            var dateMatch = Regex.Match(normalized, @"(20\d{2})\s*年\s*(\d{1,2})\s*月\s*(\d{1,2})");
            if (!dateMatch.Success)
                dateMatch = Regex.Match(normalized, @"(20\d{2})\s*[年\-/.]\s*(\d{1,2})\s*[月\-/.]\s*(\d{1,2})");
            if (dateMatch.Success) dateText = dateMatch.Value;
        }
        if (!string.IsNullOrWhiteSpace(dateText))
        {
            var cleaned = dateText.Replace("年", "-").Replace("月", "-").Replace("日", "")
                .Replace("/", "-").Replace(".", "-").Replace(" ", "");
            if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                result.IssueDate = date;
                Set("issueDate", dateText, 0.95);
            }
        }

        result.Buyer = Capture(@"(?:购买方名称|购方名称|购买方|付款方)\s*[:：]?\s*([^\r\n]{2,80})");
        if (!string.IsNullOrWhiteSpace(result.Buyer) &&
            (result.Buyer.Length < 3 ||
             Regex.IsMatch(result.Buyer, @"^(名称|地址|电话|信息|购买方|购方)")))
        {
            result.Buyer = null;
        }
        if (string.IsNullOrWhiteSpace(result.Buyer) &&
            (normalized.Contains("西安建筑科技大学") || normalized.Contains("建筑科技")))
        {
            result.Buyer = "西安建筑科技大学";
            Set("buyer", result.Buyer, 0.55);
        }
        Set("buyer", result.Buyer, 0.8);

        result.Seller = Capture(@"(?:销售方名称|销方名称|销售方(?!地址|电话|开户|账号|信息)|收款方)\s*[:：]?\s*([^\r\n]{2,80})");
        if (!string.IsNullOrWhiteSpace(result.Seller) && result.Seller.Contains(':'))
        {
            result.Seller = result.Seller[(result.Seller.IndexOf(':') + 1)..].Trim();
        }
        if (!string.IsNullOrWhiteSpace(result.Seller) &&
            (result.Seller.Length < 3 ||
             Regex.IsMatch(result.Seller, @"^(信息|名称|地址|电话|开户|账号|银行|销|售|方)") ||
             Regex.IsMatch(result.Seller, @"地址|电话|开户|账号|银行")))
        {
            result.Seller = null;
        }
        if (string.IsNullOrWhiteSpace(result.Seller))
        {
            // OCR 可能把“名称”读成别的字；用公司/厂/店后缀兜底，排除购买方学校和地址/银行行
            var company = Regex.Matches(normalized, @"(?m)^[^\r\n]*(?:有限公司|有限责任公司|厂|店)[^\r\n]*$")
                .Cast<Match>()
                .Select(m =>
                {
                    var value = m.Value;
                    var colon = value.IndexOf(':');
                    return (colon >= 0 ? value[(colon + 1)..] : value).Trim();
                })
                .FirstOrDefault(v => v.Length >= 3 && !v.Contains("西安建筑科技大学") &&
                                     !Regex.IsMatch(v, @"银行|地址|电话|开户|账号"));
            if (!string.IsNullOrWhiteSpace(company))
            {
                result.Seller = company;
                Set("seller", company, 0.55);
            }
        }
        if (string.IsNullOrWhiteSpace(result.Seller))
        {
            var companyMatch = Regex.Match(normalized,
                @"[\u4e00-\u9fa5A-Za-z0-9（）()]{2,40}(?:有限公司|有限责任公司|厂|店)");
            if (companyMatch.Success &&
                !Regex.IsMatch(companyMatch.Value, @"西安建筑科技大学|银行|地址|电话|开户|账号"))
            {
                result.Seller = companyMatch.Value;
                Set("seller", result.Seller, 0.5);
            }
        }
        if (!string.IsNullOrWhiteSpace(result.Seller))
        {
            result.Seller = Regex.Replace(result.Seller, @"^.*?[:：]\s*", "").Trim();
        }
        Set("seller", result.Seller, 0.8);

        result.Item = Capture(@"(?:项目名称|货物或应税劳务、服务名称|货物或应税劳务名称|货物名称|服务名称)\s*[:：]?\s*([^\r\n]{2,80})");
        Set("item", result.Item, 0.75);

        var qty = Capture(@"(?:数量)\s*[:：]?\s*(\d+(?:\.\d+)?)");
        if (decimal.TryParse(qty, NumberStyles.Any, CultureInfo.InvariantCulture, out var qtyValue))
        {
            result.Qty = qtyValue;
            Set("qty", qty, 0.75);
        }

        var unit = Capture(@"(?:单位)\s*[:：]?\s*([^\s\r\n\d]{1,10})");
        result.Unit = unit;
        Set("unit", unit, 0.7);

        var price = Capture(@"(?:单价)\s*[:：]?\s*(?:¥|￥)?\s*([-+]?\d[\d,]*\.?\d*)");
        if (TryParseMoney(price, out var priceCents))
        {
            result.Price = FinanceMoney.ToYuan(priceCents);
            Set("price", price, 0.8);
        }

        // "合计金额"优先；避免把"价税合计"当成不含税金额
        var amountText = Capture(@"(?<!价税)(?:合计金额|金额)\s*[:：]?\s*(?:¥|￥)?\s*([-+]?\d[\d,]*\.?\d*)");
        if (TryParseMoney(amountText, out var amountCents))
        {
            result.AmountCents = amountCents;
            Set("amount", amountText, 0.8);
        }

        var taxText = Capture(@"(?:税额|税款)\s*[:：]?\s*(?:¥|￥)?\s*([-+]?\d[\d,]*\.?\d*)");
        if (TryParseMoney(taxText, out var taxCents))
        {
            result.TaxCents = taxCents;
            Set("tax", taxText, 0.8);
        }

        var totalText = Capture(@"(?:价税合计|合计\(小写\)|小写)\s*(?:\(大写\)|\(小写\))?\s*[:：]?\s*(?:¥|￥)?\s*([-+]?\d[\d,]*\.?\d*)");
        if (!TryParseMoney(totalText, out var totalCents))
        {
            // 有些 PDF 把金额拆成"金额 + 税额"，只有金额时用两者相加兜底
            totalCents = result.AmountCents + result.TaxCents;
            if (totalCents > 0) Set("total", FinanceMoney.Format(totalCents), 0.5);
        }
        else
        {
            Set("total", totalText, 0.9);
        }

        if (totalCents <= 0)
        {
            // 标签识别失败时，取票面最大的两位小数金额作为价税合计兜底
            var money = Regex.Matches(normalized, @"(?<![\d.])(\d{1,9}\.\d{2})(?![\d.])")
                .Cast<Match>()
                .Select(m => m.Value)
                .Select(v => decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m)
                .Where(v => v > 0)
                .DefaultIfEmpty(0m)
                .Max();
            if (money > 0)
            {
                totalCents = FinanceMoney.FromYuan(money);
                Set("total", money.ToString("0.00"), 0.5);
            }
        }

        // PaddleOCR 的表格行经常把标签和值分到不同行；用行序兜底项目/单位/数量/单价/金额/税额。
        var lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        var itemIndex = lines.FindIndex(line => line.StartsWith('*'));
        var unitIndex = lines.FindIndex(line =>
            Regex.IsMatch(line, @"^(个|批|项|套|张|份|箱|台|本|块|支|盒|包|件|米|千克|kg|把|只|条|双|组|卷|袋|瓶|罐|枚|片|粒|副|辆|艘|架|部)$"));
        if (itemIndex >= 0)
        {
            var firstItemLine = lines[itemIndex];
            var itemParts = new List<string> { firstItemLine };
            for (var j = itemIndex + 1; j < lines.Count; j++)
            {
                var next = lines[j];
                if (Regex.IsMatch(next, @"^(合计|价税合计|备注|开票人|销售方地址|销售方信息|购买方信息)"))
                {
                    break;
                }
                if (Regex.IsMatch(next, @"^(项目名称|规格型号|单位|数量|单价|金额|税率/征收率|税额)") ||
                    Regex.IsMatch(next, @"^(个|批|项|套|张|份|箱|台|本|块|支|盒|包|件|米|千克|kg|把|只|条|双|组|卷|袋|瓶|罐|枚|片|粒|副|辆|艘|架|部)$") ||
                    Regex.IsMatch(next, @"^\d") ||
                    next.Contains('%') ||
                    next.Contains("cm", StringComparison.OrdinalIgnoreCase) ||
                    next.Contains("mm", StringComparison.OrdinalIgnoreCase) ||
                    next.Contains("地址") || next.Contains("电话") ||
                    next.Contains("银行") || next.Contains("账号"))
                {
                    continue;
                }

                // 明细名称可能被 OCR 拆到数量/金额前后；只收集至少两个汉字、非标签的片段。
                if (next.Length >= 2 && next.Length <= 80 &&
                    next.Any(c => c >= '\u4e00' && c <= '\u9fff') &&
                    !itemParts.Contains(next))
                {
                    var previous = itemParts[^1];
                    var separator = previous.EndsWith('、') || previous.EndsWith('，') ||
                                    previous.EndsWith(',') || previous.EndsWith('；') ? "" : "、";
                    itemParts.Add(separator + next);
                }
            }

            var itemLine = string.Join("", itemParts).Trim();
            if (!string.IsNullOrWhiteSpace(itemLine) &&
                (string.IsNullOrWhiteSpace(result.Item) || result.Item is "规格型号" or "项目名称"))
            {
                result.Item = itemLine;
                Set("item", itemLine, 0.55);
            }

            if (unitIndex >= 0 &&
                (string.IsNullOrWhiteSpace(result.Unit) || result.Unit is "数量" or "单位" or "单价"))
            {
                result.Unit = lines[unitIndex];
                Set("unit", result.Unit, 0.55);
            }

            if (result.Qty is not > 0 && result.Price is not > 0)
            {
                var numericLine = lines.Skip(itemIndex + 1)
                    .FirstOrDefault(line => Regex.IsMatch(line, @"^\d{2}\d+\.\d{3,}$"));
                if (numericLine != null)
                {
                    var parts = Regex.Match(numericLine, @"^(\d{2})(\d+\.\d{3,})$");
                    if (parts.Success &&
                        decimal.TryParse(parts.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedQty) &&
                        decimal.TryParse(parts.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedPrice))
                    {
                        result.Qty = parsedQty;
                        result.Price = parsedPrice;
                        Set("qty", parsedQty.ToString("0.##"), 0.45);
                        Set("price", parsedPrice.ToString("0.00"), 0.45);
                    }
                }
            }

            if (result.Qty is not > 0 || result.Price is not > 0)
            {
                var numeric = lines.Skip((unitIndex >= 0 ? unitIndex : itemIndex) + 1)
                    .Where(line => Regex.IsMatch(line, @"^\d+(?:\.\d+)?$"))
                    .Take(4)
                    .ToList();
                if (result.Qty is not > 0 && numeric.Count >= 1 &&
                    Regex.IsMatch(numeric[0], @"^\d+$") &&
                    decimal.TryParse(numeric[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var separateQty))
                {
                    result.Qty = separateQty;
                    Set("qty", separateQty.ToString("0.##"), 0.45);
                }
                if (result.Price is not > 0 && numeric.Count >= 2 &&
                    decimal.TryParse(numeric[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var separatePrice))
                {
                    result.Price = separatePrice;
                    Set("price", separatePrice.ToString("0.00"), 0.45);
                }
            }
        }

        // 没有 *分类*项目名 的发票：从“单位”往上找第一行有效项目名。
        if (string.IsNullOrWhiteSpace(result.Item) && unitIndex > 0)
        {
            for (var j = unitIndex - 1; j >= 0; j--)
            {
                var candidate = lines[j];
                if (Regex.IsMatch(candidate,
                        @"^(项目名称|规格型号|单位|数量|单价|金额|税率/征收率|税额|备注|合计|购买方|销售方|名称)") ||
                    Regex.IsMatch(candidate, @"^\d") ||
                    candidate.Contains("cm", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Contains("mm", StringComparison.OrdinalIgnoreCase) ||
                    candidate.Contains("地址") || candidate.Contains("电话") ||
                    candidate.Contains("银行") || candidate.Contains("账号"))
                {
                    continue;
                }
                if (candidate.Length is >= 2 and <= 100)
                {
                    result.Item = candidate;
                    Set("item", candidate, 0.45);
                    break;
                }
            }
        }

        // 没有 *项目名 时，数量/单价也按“单位”后面的数字行兜底。
        if ((result.Qty is not > 0 || result.Price is not > 0) && unitIndex >= 0)
        {
            var numeric = lines.Skip(unitIndex + 1)
                .Where(line => Regex.IsMatch(line, @"^\d+(?:\.\d+)?$"))
                .Take(4)
                .ToList();
            if (result.Qty is not > 0 && numeric.Count >= 1 &&
                Regex.IsMatch(numeric[0], @"^\d+$") &&
                decimal.TryParse(numeric[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var genericQty))
            {
                result.Qty = genericQty;
                Set("qty", genericQty.ToString("0.##"), 0.4);
            }
            if (result.Price is not > 0 && numeric.Count >= 2 &&
                decimal.TryParse(numeric[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var genericPrice))
            {
                result.Price = genericPrice;
                Set("price", genericPrice.ToString("0.00"), 0.4);
            }
        }

        // 有些发票不印数量，只印单价和金额；金额 / 单价为整数时反推数量。
        if (result.Qty is not > 0 && result.Price is > 0 && result.AmountCents > 0)
        {
            var amountYuan = FinanceMoney.ToYuan(result.AmountCents);
            var ratio = amountYuan / result.Price.Value;
            var rounded = Math.Round(ratio);
            if (rounded >= 1 && Math.Abs(ratio - rounded) <= 0.05m)
            {
                result.Qty = rounded;
                Set("qty", rounded.ToString("0.##"), 0.35);
            }
        }

        // 购买方缺失时，从“购买方信息”后面的第一个“名称：”行兜底。
        if (string.IsNullOrWhiteSpace(result.Buyer))
        {
            var buyerLabel = lines.FindIndex(line => line.Contains("购买方") || line.Contains("购方"));
            if (buyerLabel >= 0)
            {
                var nameLine = lines.Skip(buyerLabel + 1)
                    .FirstOrDefault(line => Regex.IsMatch(line, @"^名称\s*[:：]"));
                if (!string.IsNullOrWhiteSpace(nameLine))
                {
                    var name = nameLine[(nameLine.IndexOf(':') + 1)..].Trim();
                    if (name.Contains("建筑科技")) name = "西安建筑科技大学";
                    if (name.Length >= 3 && !name.Contains("销售方") && !name.Contains("地址") &&
                        !name.Contains("电话") && !name.Contains("银行") && !name.Contains("账号"))
                    {
                        result.Buyer = name;
                        Set("buyer", name, 0.45);
                    }
                }
            }
        }

        if (result.AmountCents <= 0 || result.TaxCents <= 0)
        {
            var totalYuan = FinanceMoney.ToYuan(totalCents);
            var moneyValues = Regex.Matches(normalized, @"[¥￥]\s*([-+]?\d[\d,]*\.?\d*)")
                .Cast<Match>()
                .Select(m => decimal.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var value) ? value : 0m)
                .Where(value => value > 0 && (totalYuan <= 0 || value < totalYuan))
                .Take(2)
                .ToList();
            if (result.AmountCents <= 0 && moneyValues.Count >= 1)
            {
                result.AmountCents = FinanceMoney.FromYuan(moneyValues[0]);
                Set("amount", moneyValues[0].ToString("0.00"), 0.55);
            }
            if (result.TaxCents <= 0 && moneyValues.Count >= 2)
            {
                result.TaxCents = FinanceMoney.FromYuan(moneyValues[1]);
                Set("tax", moneyValues[1].ToString("0.00"), 0.55);
            }
        }

        if (result.Qty is not > 0 && result.Price is > 0 && result.AmountCents > 0)
        {
            var amountYuan = FinanceMoney.ToYuan(result.AmountCents);
            var ratio = amountYuan / result.Price.Value;
            var rounded = Math.Round(ratio);
            if (rounded >= 1 && Math.Abs(ratio - rounded) <= 0.05m)
            {
                result.Qty = rounded;
                Set("qty", rounded.ToString("0.##"), 0.35);
            }
        }

        result.TotalCents = totalCents;

        var hasKeyField = !string.IsNullOrWhiteSpace(result.Number) || result.TotalCents > 0 ||
                          !string.IsNullOrWhiteSpace(result.Seller);
        if (!hasKeyField)
        {
            result.Success = false;
            result.Error = "未识别到发票号码、销售方或价税合计，请手工录入";
        }

        if (!string.IsNullOrWhiteSpace(options.ClubName) && !string.IsNullOrWhiteSpace(result.Buyer) &&
            !SpaceRegex.Replace(result.Buyer, "").Contains(SpaceRegex.Replace(options.ClubName, "")))
        {
            result.Confidence["buyer"] = 0.4;
        }

        return result;
    }

    private static bool TryParseMoney(string? text, out long cents)
    {
        cents = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var match = MoneyRegex.Match(text.Replace(",", ""));
        if (!match.Success) return false;
        if (!decimal.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return false;
        cents = FinanceMoney.FromYuan(value);
        return true;
    }
}

/// <summary>
/// 极简 PDF 文本层提取：只处理 FlateDecode 内容流中的 Tj / TJ 文本。
/// 扫描件没有文本层，会返回空字符串并交给外部 OCR。
/// </summary>
internal static class PdfTextExtractor
{
    public static string? TryExtract(byte[] bytes)
    {
        try
        {
            var latin = Encoding.Latin1.GetString(bytes);
            var builder = new StringBuilder();

            foreach (Match match in Regex.Matches(latin, @"stream\r?\n(.*?)\r?\nendstream",
                         RegexOptions.Singleline))
            {
                var start = match.Index - 300;
                if (start < 0) start = 0;
                var dictionary = latin.Substring(start, match.Index - start);
                if (!dictionary.Contains("FlateDecode", StringComparison.OrdinalIgnoreCase)) continue;

                var data = Encoding.Latin1.GetBytes(match.Groups[1].Value);
                string content;
                try
                {
                    using var input = new MemoryStream(data);
                    using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                    using var reader = new StreamReader(zlib, Encoding.Latin1);
                    content = reader.ReadToEnd();
                }
                catch
                {
                    continue;
                }

                ExtractTextOperators(content, builder);
            }

            var text = builder.ToString();
            return text.Length >= 10 ? text : null;
        }
        catch
        {
            return null;
        }
    }

    private static void ExtractTextOperators(string content, StringBuilder builder)
    {
        foreach (Match match in Regex.Matches(content, @"\((?:\\.|[^\\()])*\)\s*Tj"))
        {
            var value = match.Value;
            var start = value.IndexOf('(');
            var end = value.LastIndexOf(')');
            if (end > start) builder.AppendLine(Unescape(value[(start + 1)..end]));
        }

        foreach (Match match in Regex.Matches(content, @"\[(.*?)\]\s*TJ", RegexOptions.Singleline))
        {
            foreach (Match part in Regex.Matches(match.Groups[1].Value, @"\((?:\\.|[^\\()])*\)"))
            {
                var value = part.Value;
                builder.Append(Unescape(value[1..^1]));
            }

            builder.AppendLine();
        }
    }

    private static string Unescape(string value) => Regex.Replace(value, @"\\([nrtbf()\\]|[0-7]{1,3})", m =>
    {
        var token = m.Groups[1].Value;
        if (token.Length is 1 or 2 && token.All(char.IsDigit))
            return ((char)Convert.ToInt32(token, 8)).ToString();
        return token switch
        {
            "n" => "\n",
            "r" => "\r",
            "t" => "\t",
            "b" => "\b",
            "f" => "\f",
            "(" => "(",
            ")" => ")",
            "\\" => "\\",
            _ => token
        };
    });
}
