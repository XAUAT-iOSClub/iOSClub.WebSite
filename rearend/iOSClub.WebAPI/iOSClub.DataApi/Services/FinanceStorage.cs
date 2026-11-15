using Microsoft.Extensions.Options;

namespace iOSClub.DataApi.Services;

/// <summary>
/// 财务模块文件存储。发票图片可保存到私有 S3 存储桶，文件读取须经过鉴权。
/// </summary>
public interface IFinanceStorage
{
    bool IsAllowedExtension(string fileName);
    Task<string> SaveAsync(Stream stream, string originalFileName, string folder, CancellationToken ct = default);
    Task<string> SaveBytesAsync(byte[] bytes, string extension, string folder, CancellationToken ct = default);
    Stream OpenRead(string relativePath);
    bool Exists(string relativePath);
    Task<Stream?> TryOpenInvoiceAsync(string? filePath, CancellationToken ct = default);
    Task DeleteAsync(string filePath, CancellationToken ct = default);
    static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };
}

public class FinanceStorage(IOptions<FinanceOptions> options, FinanceS3Storage? s3 = null) : IFinanceStorage
{
    private const string S3Scheme = "s3:";
    private readonly FinanceOptions _options = options.Value;
    private readonly HashSet<string> _allowed = new(
        options.Value.SupportedExtensions.Select(e => e.TrimStart('.').ToLowerInvariant()),
        StringComparer.OrdinalIgnoreCase);

    private string Root
    {
        get
        {
            var root = _options.StorageRoot;
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(AppContext.BaseDirectory, "App_Data", "finance");
            Directory.CreateDirectory(root);
            return Path.GetFullPath(root);
        }
    }

    public bool IsAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return ext.Length > 0 && _allowed.Contains(ext);
    }

    public async Task<string> SaveAsync(Stream stream, string originalFileName, string folder,
        CancellationToken ct = default)
    {
        var extension = Path.GetExtension(originalFileName).TrimStart('.').ToLowerInvariant();
        if (!_allowed.Contains(extension))
            throw new InvalidOperationException($"不支持的文件格式：{extension}");

        var relative = BuildRelativePath(folder, extension);
        var contentType = IFinanceStorage.GetContentType("." + extension);
        if (folder == "invoices" && s3 is { Enabled: true } &&
            contentType.StartsWith("image/", StringComparison.Ordinal))
        {
            var key = s3.BuildKey(relative);
            await s3.SaveAsync(key, stream, contentType, ct);
            return S3Scheme + key;
        }

        var physical = GetPhysicalPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);

        await using var target = new FileStream(physical, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, useAsync: true);
        await stream.CopyToAsync(target, ct);
        return relative;
    }

    public async Task<string> SaveBytesAsync(byte[] bytes, string extension, string folder,
        CancellationToken ct = default)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        var relative = BuildRelativePath(folder, ext);
        var physical = GetPhysicalPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
        await File.WriteAllBytesAsync(physical, bytes, ct);
        return relative;
    }

    public Stream OpenRead(string relativePath)
    {
        var physical = GetPhysicalPath(relativePath);
        if (!File.Exists(physical)) throw new FileNotFoundException("文件不存在", relativePath);
        return new FileStream(physical, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public bool Exists(string relativePath) => File.Exists(GetPhysicalPath(relativePath));

    public async Task<Stream?> TryOpenInvoiceAsync(string? filePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        if (filePath.StartsWith(S3Scheme, StringComparison.Ordinal))
            return await ObjectStorage.TryOpenReadAsync(filePath[S3Scheme.Length..], ct);

        try
        {
            return new FileStream(GetPhysicalPath(filePath), FileMode.Open, FileAccess.Read, FileShare.Read,
                81920, useAsync: true);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    public Task DeleteAsync(string filePath, CancellationToken ct = default)
    {
        if (filePath.StartsWith(S3Scheme, StringComparison.Ordinal))
            return ObjectStorage.DeleteAsync(filePath[S3Scheme.Length..], ct);

        File.Delete(GetPhysicalPath(filePath));
        return Task.CompletedTask;
    }

    private FinanceS3Storage ObjectStorage => s3 ??
        throw new InvalidOperationException("未注册财务 S3 存储服务");

    private string GetPhysicalPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var physical = Path.GetFullPath(Path.Combine(Root, normalized));
        var rootWithSeparator = Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!physical.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("非法的文件路径");
        return physical;
    }

    private static string BuildRelativePath(string folder, string extension)
    {
        var now = DateTime.Now;
        return $"{folder}/{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{extension.TrimStart('.')}";
    }
}
