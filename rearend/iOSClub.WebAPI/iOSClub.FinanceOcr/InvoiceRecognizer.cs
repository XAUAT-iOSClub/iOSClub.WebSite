using System.Diagnostics;
using Sdcb.SimdCodecs;
using Sdcb.SimdCodecs.Abstractions;
using Sdcb.SimdPaddleOCR;

internal sealed class InvoiceRecognizer(PaddleOcrAll engine)
{
    private const int MaxPdfPages = 20;
    private const int MaxImagePixels = 24_000_000;

    public async Task<string> RecognizeAsync(Stream input, bool isPdf, CancellationToken cancellationToken)
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
                return text;

            var pagePrefix = Path.Combine(directory.FullName, "page");
            await RunPdfToolAsync("pdftoppm", cancellationToken, "-scale-to", "2400",
                "-l", (MaxPdfPages + 1).ToString(), "-png", source, pagePrefix);
            var pages = directory.GetFiles("page-*.png").OrderBy(page => page.Name, StringComparer.Ordinal).ToArray();
            if (pages.Length == 0 || pages.Length > MaxPdfPages)
                throw new InvalidDataException($"Scanned PDF must contain 1–{MaxPdfPages} pages.");

            var lines = new List<string>(pages.Length);
            foreach (var page in pages)
            {
                var bytes = await File.ReadAllBytesAsync(page.FullName, cancellationToken);
                lines.Add(RecognizeImage(bytes, cancellationToken));
            }

            return string.Join('\n', lines);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private string RecognizeImage(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ImageCodecs.TryReadInfo(data, out var info))
            throw new InvalidDataException("Could not read the image.");
        if ((long)info.Width * info.Height > MaxImagePixels)
            throw new InvalidDataException("Image exceeds the 24 megapixel limit.");

        var options = new DecodeOptions { ApplyOrientation = true };
        using var image = ImageCodecs.Decode(data, PixelFormat.Bgr24, options, cancellationToken);
        return engine.Run(image.Pixels, image.Width, image.Height, image.Stride).Text;
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
