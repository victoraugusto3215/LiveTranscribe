namespace LiveTranscribe.Api.Services.Processing;

public sealed class SessionCleanupOptions
{
    public const string SectionName = "SessionCleanup";

    /// <summary>Por quanto tempo o áudio de uma sessão fica em disco antes de ser excluído.</summary>
    public int RetentionHours { get; set; } = 24;

    /// <summary>De quanto em quanto tempo a varredura de limpeza roda.</summary>
    public int IntervalHours { get; set; } = 1;

    public TimeSpan RetentionPeriod => TimeSpan.FromHours(RetentionHours);
    public TimeSpan Interval => TimeSpan.FromHours(IntervalHours);
}
