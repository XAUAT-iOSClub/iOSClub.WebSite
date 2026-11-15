using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Sdcb.SimdPaddleOCR;
using Sdcb.SimdPaddleOCR.Models.ChineseV6Medium;

const int maxFileBytes = 10 * 1024 * 1024;
const int maxRequestBytes = maxFileBytes + 64 * 1024;

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxRequestBytes);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, OcrJsonContext.Default));

await using var app = builder.Build();
using var engine = await PaddleOcrAll.LoadAsync(ChineseV6MediumModels.Default);
using var inferenceSlot = new SemaphoreSlim(1);
var recognizer = new InvoiceRecognizer(engine);

app.MapGet("/health", () => new HealthResponse(true, "paddleocr", "ch", "PP-OCRv6"));

app.MapPost("/ocr", async (IFormFile file, CancellationToken cancellationToken) =>
{
    if (file.Length == 0)
        return Results.BadRequest(new ErrorResponse("empty file"));
    if (file.Length > maxFileBytes)
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

    await inferenceSlot.WaitAsync(cancellationToken);
    try
    {
        await using var input = file.OpenReadStream();
        var isPdf = file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            || file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
        var result = await recognizer.RecognizeAsync(input, isPdf, cancellationToken);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
    finally
    {
        inferenceSlot.Release();
    }
}).DisableAntiforgery();

await app.RunAsync();

internal sealed record HealthResponse(bool Ok, string Engine, string Language, string Version);
internal sealed record OcrResponse(string RawText, OcrLine[]? Lines = null);
internal sealed record OcrLine(string Text, double X0, double Y0, double X1, double Y1);
internal sealed record ErrorResponse(string Detail);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(OcrResponse))]
[JsonSerializable(typeof(ErrorResponse))]
internal partial class OcrJsonContext : JsonSerializerContext;
