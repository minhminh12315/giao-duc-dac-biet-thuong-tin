using Microsoft.Extensions.Options;
using Secms.Application.Interfaces;
using Secms.Application.Options;

namespace Secms.Infrastructure.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly StorageOptions _options;
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "application/pdf"
    };

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        Directory.CreateDirectory(Path.GetFullPath(_options.LocalRoot));
    }

    public async Task<StoredFile> SaveAsync(Stream content, string preferredFileName, string contentType, string folder, CancellationToken ct = default)
    {
        if (!Allowed.Contains(contentType))
            throw new InvalidOperationException("Loại file không được hỗ trợ. Chỉ chấp nhận JPEG/PNG/WebP/PDF.");

        if (content.CanSeek && content.Length > _options.MaxFileBytes)
            throw new InvalidOperationException($"File vượt quá {_options.MaxFileBytes / (1024 * 1024)}MB.");

        var safe = Path.GetFileName(preferredFileName);
        if (safe.Length > 100) safe = safe[..100];
        var relative = Path.Combine(folder, DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"), $"{Guid.NewGuid():N}_{safe}")
            .Replace('\\', '/');
        var full = Path.Combine(_options.LocalRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        var size = fs.Length;
        if (size > _options.MaxFileBytes)
        {
            fs.Close();
            File.Delete(full);
            throw new InvalidOperationException($"File vượt quá {_options.MaxFileBytes / (1024 * 1024)}MB.");
        }

        return new StoredFile(relative, safe, contentType, size, null);
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath)
            || storagePath.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(storagePath))
            throw new UnauthorizedAccessException("Đường dẫn file không hợp lệ.");

        var root = Path.GetFullPath(_options.LocalRoot);
        var full = Path.GetFullPath(Path.Combine(root, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Đường dẫn file không hợp lệ.");
        if (!File.Exists(full)) throw new FileNotFoundException("Không tìm thấy file.", storagePath);
        Stream stream = File.OpenRead(full);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath)
            || storagePath.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(storagePath))
            return Task.CompletedTask;

        var root = Path.GetFullPath(_options.LocalRoot);
        var full = Path.GetFullPath(Path.Combine(root, storagePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }
}
