using System.Text;
using System.Text.Json;
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

    public async Task<SearchResult<T>> SearchAsync<T>(string indexName, string query, int size = 10, string? searchAfterCursor = null) where T : class
    {
        try
        {
            var trimmedQuery = query?.Trim() ?? string.Empty;

            // Decode the opaque cursor into sort values for search_after
            object[]? searchAfterValues = null;
            if (!string.IsNullOrEmpty(searchAfterCursor))
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(searchAfterCursor));
                searchAfterValues = JsonSerializer.Deserialize<object[]>(json);
            }

            var response = await _client.SearchAsync<T>(s =>
            {
                s.Index(indexName)
                 .TrackTotalHits()
                 .Query(q => string.IsNullOrWhiteSpace(trimmedQuery)
                     ? q.MatchAll()
                     : q.Bool(b => b
                         .Should(
                             sh => sh.MultiMatch(mm => mm
                                 .Query(trimmedQuery)
                                 .Type(TextQueryType.PhrasePrefix)
                                 .Fields(f => f
                                     .Field("title^8")
                                     .Field("tags^7")
                                     .Field("aiTags^7")
                                     .Field("aiDescription^6")
                                     .Field("historicalContext^3")
                                     .Field("content^2")
                                 )
                             ),
                             sh => sh.MultiMatch(mm => mm
                                 .Query(trimmedQuery)
                                 .Type(TextQueryType.BestFields)
                                 .Operator(Operator.And)
                                 .Fields(f => f
                                     .Field("title^7")
                                     .Field("tags^6")
                                     .Field("aiTags^6")
                                     .Field("aiDescription^5")
                                     .Field("historicalContext^2.5")
                                     .Field("content^2")
                                     .Field("sourcePlatform^2")
                                     .Field("community^2")
                                     .Field("location^1.5")
                                 )
                             ),
                             sh => sh.MultiMatch(mm => mm
                                 .Query(trimmedQuery)
                                 .Type(TextQueryType.BestFields)
                                 .Fuzziness(Fuzziness.Auto)
                                 .MinimumShouldMatch("75%")
                                 .Fields(f => f
                                     .Field("title^5")
                                     .Field("tags^5")
                                     .Field("aiTags^5")
                                     .Field("aiDescription^4")
                                     .Field("historicalContext^2")
                                     .Field("content^1.5")
                                     .Field("sourcePlatform^1.5")
                                     .Field("community^1.5")
                                     .Field("location")
                                 )
                             )
                     )
                         .MinimumShouldMatch(1)
                     )
                 )
                 // Sort deterministically: newest first, _id as tiebreaker (always keyword-sortable)
                 .Sort(so => so
                     .Field(f => f.Field("capturedAt").Descending())
                     .Field(f => f.Field("_id").Ascending())
                 )
                 .Size(size);

                if (searchAfterValues != null)
                    s.SearchAfter(searchAfterValues);

                return s;
            });

            if (!response.IsValid)
            {
                _logger.LogError("Search failed: {Error}", response.DebugInformation);
                return new SearchResult<T>();
            }

            // Encode the sort values of the last hit as the next cursor
            string? nextCursor = null;
            var lastHit = response.Hits.LastOrDefault();
            if (lastHit?.Sorts != null && response.Hits.Count == size)
            {
                var sortValues = lastHit.Sorts.ToArray();
                var json = JsonSerializer.Serialize(sortValues);
                nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
            }

            return new SearchResult<T>
            {
                Hits = response.Documents.ToList(),
                Total = response.Total,
                NextCursor = nextCursor
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

