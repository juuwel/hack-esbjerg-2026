using Microsoft.AspNetCore.Mvc;
using ArchiveAPI.Domain.Entities;
using ArchiveAPI.Services;
using ArchiveAPI.Shared.Requests;

namespace ArchiveAPI.Controllers;

/// <summary>
/// API controller for managing and searching archive documents using OpenSearch
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ArchiveController : ControllerBase
{
    private readonly IOpenSearchService _searchService;
    private readonly IMinioService _minioService;
    private readonly ILogger<ArchiveController> _logger;

    public ArchiveController(IOpenSearchService searchService, IMinioService minioService, ILogger<ArchiveController> logger)
    {
        _searchService = searchService;
        _minioService = minioService;
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
    /// If no query is provided, all documents are returned ordered by newest first.
    /// Pass the opaque <c>nextCursor</c> value from a previous response as <c>cursor</c> to retrieve the next page.
    /// </remarks>
    /// <param name="q">The search query string (optional)</param>
    /// <param name="size">Maximum number of results to return (default: 20, max: 100)</param>
    /// <param name="cursor">Opaque pagination cursor returned by the previous page (optional)</param>
    /// <returns>Search results with matching documents, total count, and optional next-page cursor</returns>
    /// <response code="200">Search completed successfully</response>
    [HttpGet("search")]
    [Produces("application/json")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int size = 20, [FromQuery] string? cursor = null)
    {
        if (size > 100) size = 100;
        if (size < 1)  size = 20;

        var results = await _searchService.SearchAsync<ArchiveDocument>("archive", q ?? string.Empty, size, cursor);
        return Ok(results);
    }

    /// <summary>
    /// Delete a stored file from MinIO
    /// </summary>
    /// <remarks>
    /// Removes the raw binary file associated with an archived document.
    /// Called alongside document deletion to clean up storage.
    /// </remarks>
    /// <param name="objectName">The MinIO object name to delete</param>
    /// <returns>No content</returns>
    /// <response code="204">File successfully deleted</response>
    /// <response code="500">Failed to delete file</response>
    [HttpDelete("files")]
    public async Task<IActionResult> DeleteFile([FromQuery] string objectName)
    {
        try
        {
            await _minioService.DeleteFileAsync(objectName);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {ObjectName}", objectName);
            return StatusCode(500, new { error = "Failed to delete file" });
        }
    }

    /// <summary>
    /// Get a pre-signed download URL for a stored file
    /// </summary>
    /// <remarks>
    /// Redirects the caller directly to the pre-signed MinIO URL so the browser
    /// can download or display the file inline (e.g. image previews).
    /// </remarks>
    /// <param name="objectName">The MinIO object name</param>
    /// <param name="expirySeconds">URL validity in seconds (default: 3600)</param>
    /// <returns>302 redirect to the pre-signed URL</returns>
    /// <response code="302">Redirect to pre-signed URL</response>
    /// <response code="500">Failed to generate URL</response>
    [HttpGet("files")]
    public async Task<IActionResult> GetFile([FromQuery] string objectName, [FromQuery] int expirySeconds = 3600)
    {
        try
        {
            var url = await _minioService.GetPresignedUrlAsync(objectName, expirySeconds);
            return Redirect(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pre-signed URL for {ObjectName}", objectName);
            return StatusCode(500, new { error = "Failed to generate file URL" });
        }
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

