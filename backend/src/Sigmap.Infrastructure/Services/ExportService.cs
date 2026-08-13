using Microsoft.EntityFrameworkCore;
using Sigmap.Application.Abstractions;
using Sigmap.Application.Dtos;
using Sigmap.Application.Services;
using Sigmap.Domain.Entities;
using Sigmap.Domain.Enums;
using Sigmap.Infrastructure.Persistence;

namespace Sigmap.Infrastructure.Services;

public sealed class ExportService(SigmapDbContext db, IExportFileStore fileStore) : IExportService
{
    public Task<List<ExportRecordDto>> ListAsync(CancellationToken ct) =>
        db.Exports.AsNoTracking().OrderByDescending(e => e.CreatedAt).Select(e => Mappers.ToDto(e)).ToListAsync(ct);

    public async Task<ExportCreatedDto> CreateAsync(ExportInputDto input, Guid ownerId, CancellationToken ct)
    {
        var sessionName = input.SessionId is null
            ? null
            : await db.Sessions.AsNoTracking().Where(s => s.Id == input.SessionId).Select(s => (string?)s.Name).FirstOrDefaultAsync(ct);

        var record = new Export
        {
            SessionId = input.SessionId,
            SessionName = sessionName,
            Format = input.Format,
            Status = ExportStatus.Queued,
            FileName = null,
            CreatedAt = DateTimeOffset.UtcNow,
            OwnerId = ownerId,
            SizeBytes = null,
            RowCount = null,
        };
        db.Exports.Add(record);
        await db.SaveChangesAsync(ct);
        return new ExportCreatedDto(record.Id, record.Format, record.Status, record.CreatedAt, record.FileName);
    }

    public async Task<(byte[] Content, string FileName)?> DownloadAsync(Guid id, CancellationToken ct)
    {
        var record = await db.Exports.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (record is null || record.Status != ExportStatus.Done || record.FileName is null) return null;
        var bytes = await fileStore.ReadAsync(record.FileName, ct);
        if (bytes is null) return null;
        return (bytes, record.FileName);
    }
}
