namespace ArchiveAPI.Shared.Requests;

/// <summary>
/// Optional metadata submitted alongside a file upload to populate the <see cref="ArchiveAPI.Domain.Entities.ArchiveDocument"/>.
/// </summary>
public class UploadArchiveRequest
{
    /// <summary>Human-readable title for the archived item.</summary>
    public string? Title { get; set; }

    /// <summary>Original URL the content was captured from.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Platform or origin, e.g. Twitter, Discord, local-news.</summary>
    public string? SourcePlatform { get; set; }

    /// <summary>Original author name or handle.</summary>
    public string? Author { get; set; }

    /// <summary>Person or system performing the archiving.</summary>
    public string? ArchivedBy { get; set; }

    /// <summary>Content type category, e.g. social-post, news-article, community-thread.</summary>
    public string? ContentType { get; set; }

    /// <summary>ISO 639-1 language code, e.g. "en", "da".</summary>
    public string? Language { get; set; }

    /// <summary>Comma-separated tags.</summary>
    public string? Tags { get; set; }

    /// <summary>City, region, or free-text location.</summary>
    public string? Location { get; set; }

    /// <summary>Community, group, or forum this content belongs to.</summary>
    public string? Community { get; set; }

    /// <summary>What was happening when this was saved and why it mattered.</summary>
    public string? HistoricalContext { get; set; }

    /// <summary>When the content was originally published/created (UTC).</summary>
    public DateTime? OriginalCreatedAt { get; set; }
}

