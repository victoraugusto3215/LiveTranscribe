using LiveTranscribe.Api.Data;
using LiveTranscribe.Api.Hubs;
using LiveTranscribe.Api.Models;
using LiveTranscribe.Api.Services.Audio;
using LiveTranscribe.Api.Services.Processing;
using LiveTranscribe.Api.Services.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace LiveTranscribe.Api.Controllers;

public sealed record UploadSessionResponse(Guid SessionId);

public sealed record SessionStatusResponse(Guid SessionId, string Status, string? ErrorMessage);

[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(
    IDbContextFactory<AppDbContext> dbContextFactory,
    AudioChunkStore chunkStore,
    AudioNormalizer normalizer,
    SessionProcessingQueue queue) : ControllerBase
{
    private const long MaxUploadBytes = 50 * 1024 * 1024; // 50 MB — espelhado em audioFormats.ts no front

    [HttpPost("upload")]
    [RequestSizeLimit(MaxUploadBytes)]
    [EnableRateLimiting(RateLimitingSetup.UploadPolicy)]
    public async Task<ActionResult<UploadSessionResponse>> Upload(
        IFormFile file, [FromForm] string languageCode = "pt", CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Envie um arquivo de áudio.");
        }

        if (!AudioFormats.IsAllowed(file.FileName))
        {
            string formats = string.Join(", ", AudioFormats.AllowedExtensions);
            return BadRequest($"Formato não suportado. Formatos aceitos: {formats}.");
        }

        var session = new TranscriptionSession
        {
            Id = Guid.NewGuid(),
            OriginalFileName = file.FileName,
            LanguageCode = languageCode,
            Status = SessionStatus.Processing,
        };

        string extension = Path.GetExtension(file.FileName);
        await using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        string uploadPath = await chunkStore.SaveUploadAsync(session.Id, extension, memoryStream.ToArray(), cancellationToken);

        if (!await normalizer.IsReadableAudioAsync(uploadPath, cancellationToken))
        {
            return BadRequest("Não consegui ler esse arquivo como áudio — ele pode estar corrompido ou em um formato inesperado.");
        }

        await using (var db = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            db.Sessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
        }

        await queue.EnqueueAsync(session.Id, cancellationToken);

        return Accepted(new UploadSessionResponse(session.Id));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SessionStatusResponse>> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        return new SessionStatusResponse(session.Id, session.Status.ToString(), session.ErrorMessage);
    }

    [HttpGet("{id:guid}/segments")]
    public async Task<ActionResult<IReadOnlyList<TranscriptSegmentDto>>> GetSegments(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool exists = await db.Sessions.AnyAsync(s => s.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        List<TranscriptSegmentDto> segments = await db.Segments
            .Where(s => s.SessionId == id)
            .OrderBy(s => s.SequenceNumber)
            .Select(s => new TranscriptSegmentDto(s.Id, s.SequenceNumber, s.StartMs, s.EndMs, s.Text, s.IsFinal))
            .ToListAsync(cancellationToken);

        return segments;
    }
}
