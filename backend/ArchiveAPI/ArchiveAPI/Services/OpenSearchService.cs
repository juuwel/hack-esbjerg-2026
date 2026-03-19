using OpenSearch.Client;

namespace ArchiveAPI.Services;

public class OpenSearchService : IOpenSearchService
{
    private readonly IOpenSearchClient _client;
    private readonly ILogger<OpenSearchService> _logger;

    public OpenSearchService(IOpenSearchClient client, ILogger<OpenSearchService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _client.Cluster.HealthAsync();
            return response.IsValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenSearch health check failed");
            return false;
        }
    }

    public async Task IndexDocumentAsync<T>(string indexName, string documentId, T document) where T : class
    {
        try
        {
            var response = await _client.IndexAsync(document, i => i
                .Index(indexName)
                .Id(documentId)
            );

            if (!response.IsValid)
            {
                _logger.LogError("Failed to index document: {Error}", response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document in {IndexName}", indexName);
            throw;
        }
    }

    public async Task<T?> GetDocumentAsync<T>(string indexName, string documentId) where T : class
    {
        try
        {
            var response = await _client.GetAsync<T>(documentId, g => g.Index(indexName));
            return response.IsValid ? response.Source : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document from {IndexName}", indexName);
            return null;
        }
    }

    public async Task<SearchResult<T>> SearchAsync<T>(string indexName, string query, int size = 10) where T : class
    {
        try
        {
            var response = await _client.SearchAsync<T>(s => s
                .Index(indexName)
                .Query(q => string.IsNullOrWhiteSpace(query)
                    ? q.MatchAll()
                    : q.MultiMatch(mm => mm
                        .Query(query)
                        .Fields(f => f.Field("*"))
                    )
                )
                .Size(size)
            );

            return new SearchResult<T>
            {
                Documents = response.Documents.ToList(),
                TotalCount = response.Total
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching {IndexName}", indexName);
            return new SearchResult<T>();
        }
    }

    public async Task DeleteDocumentAsync(string indexName, string documentId)
    {
        try
        {
            var response = await _client.DeleteAsync(new DeleteRequest(indexName, documentId));
            if (!response.IsValid)
            {
                _logger.LogError("Failed to delete document: {Error}", response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document from {IndexName}", indexName);
            throw;
        }
    }
}

