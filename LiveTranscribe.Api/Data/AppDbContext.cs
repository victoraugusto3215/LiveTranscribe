using LiveTranscribe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LiveTranscribe.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TranscriptionSession> Sessions => Set<TranscriptionSession>();
    public DbSet<TranscriptSegment> Segments => Set<TranscriptSegment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TranscriptionSession>()
            .HasMany(s => s.Segments)
            .WithOne(seg => seg.Session)
            .HasForeignKey(seg => seg.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TranscriptSegment>()
            .HasIndex(s => new { s.SessionId, s.SequenceNumber })
            .IsUnique();

        modelBuilder.Entity<TranscriptionSession>()
            .Property(s => s.Status)
            .HasConversion<string>();
    }
}
