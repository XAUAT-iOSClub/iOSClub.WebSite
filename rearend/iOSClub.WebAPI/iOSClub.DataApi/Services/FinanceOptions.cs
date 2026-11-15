namespace iOSClub.DataApi.Services;

/// <summary>
/// 财务模块配置。默认值保证不配置也能启动；生产环境可用
/// Finance__StorageRoot / Finance__OcrEndpoint 等环境变量覆盖。
/// </summary>
public class FinanceOptions
{
    /// <summary>发票与生成文档的落盘根目录（绝对路径，由 Program 解析）。</summary>
    public string StorageRoot { get; set; } = "";

    /// <summary>购买方抬头，用于抬头一致性提示与表单默认值。</summary>
    public string ClubName { get; set; } = "西安建筑科技大学 iOS Club";

    /// <summary>低于该置信度的字段前端标黄，要求人工确认。</summary>
    public double LowConfidenceThreshold { get; set; } = 0.8;

    /// <summary>可选的外部 OCR 服务地址。为空时图片/扫描件识别失败并转手工录入。</summary>
    public string? OcrEndpoint { get; set; }

    public string? OcrApiKey { get; set; }

    /// <summary>可选的 LibreOffice/soffice 路径，用于 docx 转 pdf。</summary>
    public string? PdfConverterPath { get; set; }

    public int RecognitionTimeoutSeconds { get; set; } = 15;

    public int MaxFileSizeMb { get; set; } = 10;

    public int MaxFilesPerUpload { get; set; } = 20;

    public string[] SupportedExtensions { get; set; } =
        ["jpg", "jpeg", "png", "webp", "bmp", "docx", "pdf"];

    public string[] ActivityTypes { get; set; } =
        ["校级活动", "内部活动"];
}
