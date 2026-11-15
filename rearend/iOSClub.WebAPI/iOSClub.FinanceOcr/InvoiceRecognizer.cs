using System.Diagnostics;
using Sdcb.SimdCodecs;
using Sdcb.SimdCodecs.Abstractions;
using Sdcb.SimdPaddleOCR;

internal sealed class InvoiceRecognizer(PaddleOcrAll engine)
{
    private const int MaxPdfPages = 20;
    private const int MaxImagePixels = 24_000_000;

    public async Task<OcrResponse> RecognizeAsync(Stream input, bool isPdf, CancellationToken cancellationToken)
    {
        if (!isPdf)
        {
            using var bytes = new MemoryStream();
            await input.CopyToAsync(bytes, cancellationToken);
            return RecognizeImage(bytes.GetBuffer().AsSpan(0, (int)bytes.Length), cancellationToken);
        }

        var directory = Directory.CreateTempSubdirectory("invoice-ocr-");
        try
        {
            var source = Path.Combine(directory.FullName, "invoice.pdf");
            await using (var destination = File.Create(source))
                await input.CopyToAsync(destination, cancellationToken);

            var textPath = Path.Combine(directory.FullName, "text.txt");
            await RunPdfToolAsync("pdftotext", cancellationToken, "-l", MaxPdfPages.ToString(),
                "-layout", source, textPath);
            var text = await File.ReadAllTextAsync(textPath, cancellationToken);
            if (text.Length >= 60 && (text.Contains("发票") || text.Contains("号码")))
                return new OcrResponse(text);

            var pagePrefix = Path.Combine(directory.FullName, "page");
            await RunPdfToolAsync("pdftoppm", cancellationToken, "-scale-to", "2400",
                "-l", (MaxPdfPages + 1).ToString(), "-png", source, pagePrefix);
            var pages = directory.GetFiles("page-*.png").OrderBy(page => page.Name, StringComparer.Ordinal).ToArray();
            if (pages.Length == 0 || pages.Length > MaxPdfPages)
                throw new InvalidDataException($"Scanned PDF must contain 1–{MaxPdfPages} pages.");

            var textPages = new List<string>(pages.Length);
            OcrLine[]? firstPageLines = null;
            foreach (var page in pages)
            {
                var bytes = await File.ReadAllBytesAsync(page.FullName, cancellationToken);
                var recognized = RecognizeImage(bytes, cancellationToken);
                textPages.Add(recognized.RawText);
                firstPageLines ??= recognized.Lines;
            }

            return new OcrResponse(string.Join('\n', textPages), firstPageLines);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private OcrResponse RecognizeImage(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ImageCodecs.TryReadInfo(data, out var info))
            throw new InvalidDataException("Could not read the image.");
        if ((long)info.Width * info.Height > MaxImagePixels)
            throw new InvalidDataException("Image exceeds the 24 megapixel limit.");

        var options = new DecodeOptions { ApplyOrientation = true };
        using var image = ImageCodecs.Decode(data, PixelFormat.Bgr24, options, cancellationToken);
        var recognized = engine.Run(image.Pixels, image.Width, image.Height, image.Stride);
        var lines = recognized.Lines.Select(line =>
        {
            var b = line.Box;
            return new OcrLine(line.Text,
                Math.Min(Math.Min(b.X1, b.X2), Math.Min(b.X3, b.X4)),
                Math.Min(Math.Min(b.Y1, b.Y2), Math.Min(b.Y3, b.Y4)),
                Math.Max(Math.Max(b.X1, b.X2), Math.Max(b.X3, b.X4)),
                Math.Max(Math.Max(b.Y1, b.Y2), Math.Max(b.Y3, b.Y4)));
        }).ToArray();
        return new OcrResponse(recognized.Text, lines);
    }

    private static async Task RunPdfToolAsync(string executable, CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {executable}.");
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await stderr;
            throw;
        }

        await stderr;
        if (process.ExitCode != 0)
            throw new InvalidDataException("Could not read the PDF.");
    }
}
