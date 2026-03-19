namespace ArchiveAPI.Domain.Entities;

public class ArchiveDocument
{
    // ── Core identity ──────────────────────────────────────────────────────────

    /// <summary>Unique identifier (UUID). Auto-generated if not supplied.</summary>
    public string? Id { get; set; }

    /// <summary>Title or headline of the archived content.</summary>
    public string? Title { get; set; }

    /// <summary>Full text content of the document. Fully searchable.</summary>
    public string? Content { get; set; }

    // ── Provenance ─────────────────────────────────────────────────────────────

    /// <summary>Original URL where the content was captured from.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Platform or origin, e.g. Twitter, Discord, local-news.</summary>
    public string? SourcePlatform { get; set; }

    /// <summary>Original author name or handle, if known.</summary>
    public string? Author { get; set; }

    /// <summary>Person or system that performed the archiving.</summary>
    public string? ArchivedBy { get; set; }

    // ── Timestamps ─────────────────────────────────────────────────────────────

    /// <summary>When the document was archived (UTC).</summary>
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the content was originally published/created, if known.</summary>
    public DateTime? OriginalCreatedAt { get; set; }

    // ── Classification ─────────────────────────────────────────────────────────

    /// <summary>Content type, e.g. social-post, news-article, community-thread, video.</summary>
    public string? ContentType { get; set; }

    /// <summary>File format, e.g. HTML, PDF, MP4, JPEG — relevant for longevity.</summary>
    public string? Format { get; set; }

    /// <summary>ISO 639-1 language code, e.g. "en", "da".</summary>
    public string? Language { get; set; }

    /// <summary>Searchable tags.</summary>
    public List<string> Tags { get; set; } = [];

    // ── Geographic & community ─────────────────────────────────────────────────

    /// <summary>City, region, or free-text location.</summary>
    public string? Location { get; set; }

    /// <summary>Community, group, or forum this content belongs to.</summary>
    public string? Community { get; set; }

    // ── Contextual enrichment ──────────────────────────────────────────────────

    /// <summary>What was happening when this was saved and why it mattered.</summary>
    public string? HistoricalContext { get; set; }

    /// <summary>IDs of related archived documents.</summary>
    public List<string> ConnectedIds { get; set; } = [];

    // ── Integrity & longevity ──────────────────────────────────────────────────

    /// <summary>SHA-256 checksum of the stored content for tamper detection.</summary>
    public string? ChecksumSha256 { get; set; }

    /// <summary>Log of format migrations, e.g. ["JPEG→PNG @ 2035-01-01"].</summary>
    public List<string> MigrationHistory { get; set; } = [];

    // ── Storage ────────────────────────────────────────────────────────────────

    /// <summary>MinIO object name for the associated binary file, if any.</summary>
    public string? ObjectName { get; set; }

    /// <summary>Optional extra key-value metadata for extensibility.</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];
}