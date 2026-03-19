using Microsoft.AspNetCore.Mvc;
using ArchiveAPI.Services;

namespace ArchiveAPI.Controllers;

/// <summary>
/// API controller for managing and searching archive documents using OpenSearch
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ArchiveController : ControllerBase
{
    private readonly IOpenSearchService _searchService;
    private readonly ILogger<ArchiveController> _logger;

    public ArchiveController(IOpenSearchService searchService, ILogger<ArchiveController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Health check for OpenSearch connection
    /// </summary>
    /// <remarks>
    /// Returns the health status of the OpenSearch cluster connection.
    /// Use this endpoint to verify that the archive service is operational.
    /// </remarks>
    /// <returns>Health status of OpenSearch</returns>
    /// <response code="200">OpenSearch is healthy and ready to use</response>
    /// <response code="503">OpenSearch is unhealthy or unreachable</response>
    [HttpGet("health")]
    public async Task<IActionResult> Health()
    {
        var isHealthy = await _searchService.HealthCheckAsync();
        if (isHealthy)
        {
            return Ok(new { status = "healthy" });
        }
        return StatusCode(503, new { status = "unhealthy" });
    }

    /// <summary>
    /// Index a new archive document
    /// </summary>
    /// <remarks>
    /// Creates or updates an archive document in OpenSearch. If no ID is provided, 
    /// a UUID will be automatically generated. The document will be searchable immediately 
    /// after indexing.
    /// </remarks>
    /// <param name="document">The archive document to index</param>
    /// <returns>The ID of the indexed document</returns>
    /// <response code="201">Document successfully indexed</response>
    /// <response code="400">Invalid or null document provided</response>
    /// <response code="500">Failed to index document</response>
    [HttpPost("documents")]
    public async Task<IActionResult> IndexDocument([FromBody] ArchiveDocument document)
    {
        if (document == null)
        {
            return BadRequest(new { error = "Document cannot be null" });
        }

        var documentId = document.Id ?? Guid.NewGuid().ToString();
        
        try
        {
            await _searchService.IndexDocumentAsync("archive", documentId, document);
            return CreatedAtAction(nameof(GetDocument), new { id = documentId }, new { id = documentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document");
            return StatusCode(500, new { error = "Failed to index document" });
        }
    }

    /// <summary>
    /// Get a specific document by ID
    /// </summary>
    /// <remarks>
    /// Retrieves the full details of an archive document by its unique identifier.
    /// </remarks>
    /// <param name="id">The unique identifier of the document</param>
    /// <returns>The archive document</returns>
    /// <response code="200">Document found and returned</response>
    /// <response code="404">Document not found</response>
    [HttpGet("documents/{id}")]
    [Produces("application/json")]
    public async Task<IActionResult> GetDocument(string id)
    {
        var document = await _searchService.GetDocumentAsync<ArchiveDocument>("archive", id);
        
        if (document == null)
        {
            return NotFound(new { error = "Document not found" });
        }

        return Ok(document);
    }

    /// <summary>
    /// Search across all archive documents
    /// </summary>
    /// <remarks>
    /// Performs a full-text search across all indexed archive documents. 
    /// The search query is matched against the document title, content, and source fields.
    /// Results are limited by the size parameter (default: 10, max: 100).
    /// </remarks>
    /// <param name="q">The search query string (required)</param>
    /// <param name="size">Maximum number of results to return (default: 10)</param>
    /// <returns>Search results with matching documents and total count</returns>
    /// <response code="200">Search completed successfully</response>
    /// <response code="400">Search query parameter missing</response>
    [HttpGet("search")]
    [Produces("application/json")]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int size = 10)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error = "Query parameter 'q' is required" });
        }

        // Limit size to prevent resource exhaustion
        if (size > 100)
            size = 100;
        if (size < 1)
            size = 10;

        var results = await _searchService.SearchAsync<ArchiveDocument>("archive", q, size);
        return Ok(results);
    }

    /// <summary>
    /// Delete an archive document
    /// </summary>
    /// <remarks>
    /// Permanently removes an archive document from the index. 
    /// This action cannot be undone.
    /// </remarks>
    /// <param name="id">The unique identifier of the document to delete</param>
    /// <returns>No content</returns>
    /// <response code="204">Document successfully deleted</response>
    /// <response code="500">Failed to delete document</response>
    [HttpDelete("documents/{id}")]
    public async Task<IActionResult> DeleteDocument(string id)
    {
        try
        {
            await _searchService.DeleteDocumentAsync("archive", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { error = "Failed to delete document" });
        }
    }
}

/// <summary>
/// Represents an archived document in the system
/// </summary>
public class ArchiveDocument
{
    /// <summary>
    /// Unique identifier for the document. If not provided, a UUID will be generated.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Title or headline of the archived content
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Full text content of the document. This field is fully searchable.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Original source URL or origin of the archived content
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Timestamp when the document was archived (UTC)
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional custom metadata as key-value pairs for additional context
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

