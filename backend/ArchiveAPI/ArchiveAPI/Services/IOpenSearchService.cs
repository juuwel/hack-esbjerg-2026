namespace ArchiveAPI.Services;

public interface IOpenSearchService
{
    Task<bool> HealthCheckAsync();
    Task IndexDocumentAsync<T>(string indexName, string documentId, T document) where T : class;
    Task<T?> GetDocumentAsync<T>(string indexName, string documentId) where T : class;
    Task<SearchResult<T>> SearchAsync<T>(string indexName, string query, int size = 10) where T : class;
    Task DeleteDocumentAsync(string indexName, string documentId);
}

public class SearchResult<T> where T : class
{
    public List<T> Documents { get; set; } = new();
    public long TotalCount { get; set; }
}

