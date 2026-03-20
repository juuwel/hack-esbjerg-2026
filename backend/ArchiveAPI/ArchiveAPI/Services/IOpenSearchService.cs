namespace ArchiveAPI.Services;

public interface IOpenSearchService
{
    Task<bool> HealthCheckAsync();
    Task IndexDocumentAsync<T>(string indexName, string documentId, T document) where T : class;
    Task<T?> GetDocumentAsync<T>(string indexName, string documentId) where T : class;
    Task<SearchResult<T>> SearchAsync<T>(string indexName, string query, int size = 10, string? searchAfterCursor = null) where T : class;
    Task DeleteDocumentAsync(string indexName, string documentId);
}

public class SearchResult<T> where T : class
{
    public List<T> Hits { get; set; } = new();
    public long Total { get; set; }
    /// <summary>Opaque base64 cursor — pass as `cursor` on the next page request. Null when no further pages.</summary>
    public string? NextCursor { get; set; }
}

