using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sigmap.Application.Abstractions;

namespace Sigmap.Infrastructure.Exports;

/// <summary>Stores generated export files on disk under a configurable directory.</summary>
public sealed class FileExportFileStore(ILogger<FileExportFileStore> logger, string directory) : IExportFileStore
{
    public FileExportFileStore(ILogger<FileExportFileStore> logger, IConfiguration config) : this(logger, Path.Combine(AppContext.BaseDirectory, config.GetValue("Export:Directory", "exports")))
    {
    }

    public async Task<(string FilePath, long SizeBytes)> WriteAsync(Guid exportId, string fileName, byte[] content, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        await System.IO.File.WriteAllBytesAsync(path, content, ct);
        logger.LogInformation("Export {ExportId} written to {Path} ({Bytes} bytes)", exportId, path, content.Length);
        return (fileName, content.Length);
    }

    public async Task<byte[]?> ReadAsync(string filePath, CancellationToken ct)
    {
        var full = Path.IsPathRooted(filePath) ? filePath : Path.Combine(directory, filePath);
        if (!System.IO.File.Exists(full)) return null;
        return await System.IO.File.ReadAllBytesAsync(full, ct);
    }
}
