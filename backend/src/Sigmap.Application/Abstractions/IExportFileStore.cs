namespace Sigmap.Application.Abstractions;

/// <summary>Persists generated export files so downloads can serve them later.</summary>
public interface IExportFileStore
{
    /// <summary>Writes the file and returns its relative path + byte size.</summary>
    Task<(string FilePath, long SizeBytes)> WriteAsync(Guid exportId, string fileName, byte[] content, CancellationToken ct);

    /// <summary>Reads a previously written export file.</summary>
    Task<byte[]?> ReadAsync(string filePath, CancellationToken ct);
}
