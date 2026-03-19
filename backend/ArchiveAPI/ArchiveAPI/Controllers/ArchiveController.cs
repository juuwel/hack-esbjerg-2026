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
    /// Upload a file and archive it
    /// </summary>
    /// <remarks>
    /// Uploads a file to MinIO and indexes a corresponding <see cref="ArchiveDocument"/> in OpenSearch.
    /// All metadata fields are optional — only the file itself is required.
    /// Returns the new document ID and a pre-signed download URL valid for 1 hour.
    /// </remarks>
    /// <param name="file">The file to upload (multipart/form-data)</param>
    /// <param name="request">Optional metadata to attach to the archived document</param>
    /// <returns>The created archive document ID and a pre-signed download URL</returns>
    /// <response code="201">File uploaded and document indexed successfully</response>
    /// <response code="400">No file provided or file is empty</response>
    /// <response code="500">Failed to upload file or index document</response>
    [HttpPost("files")]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] UploadArchiveRequest request)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided or file is empty" });

        var documentId = Guid.NewGuid().ToString();
        var objectName = $"{documentId}/{file.FileName}";

        try
        {
            await using var stream = file.OpenReadStream();
            await _minioService.UploadFileAsync(
                objectName,
                stream,
                file.Length,
                file.ContentType ?? "application/octet-stream");

            var url = await _minioService.GetPresignedUrlAsync(objectName);

            var document = new ArchiveDocument
            {
                Id          = documentId,
                Title       = request.Title ?? file.FileName,
                Format      = file.ContentType,
                ObjectName  = objectName,
                CapturedAt  = DateTime.UtcNow,
                SourceUrl           = request.SourceUrl,
                SourcePlatform      = request.SourcePlatform,
                Author              = request.Author,
                ArchivedBy          = request.ArchivedBy,
                ContentType         = request.ContentType,
                Language            = request.Language,
                Location            = request.Location,
                Community           = request.Community,
                HistoricalContext   = request.HistoricalContext,
                OriginalCreatedAt   = request.OriginalCreatedAt,
                Tags = string.IsNullOrWhiteSpace(request.Tags)
                    ? []
                    : [..request.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            };

            await _searchService.IndexDocumentAsync("archive", documentId, document);

            return CreatedAtAction(nameof(GetDocument), new { id = documentId }, new
            {
                id = documentId,
                objectName,
                fileName = file.FileName,
                url
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to upload file" });
        }
    }

    /// <summary>
    /// Get a pre-signed download URL for a stored file
    /// </summary>
    /// <remarks>
    /// Returns a pre-signed URL that allows direct download of the file for 1 hour.
    /// </remarks>
    /// <param name="bucket">The bucket that contains the file</param>
    /// <param name="objectName">The object name returned when the file was uploaded</param>
    /// <param name="expirySeconds">URL validity in seconds (default: 3600)</param>
    /// <returns>Pre-signed download URL</returns>
    /// <response code="200">URL generated successfully</response>
    /// <response code="500">Failed to generate URL</response>
    [HttpGet("files")]
    [Produces("application/json")]
    public async Task<IActionResult> GetFileUrl(
        [FromQuery] string bucket,
        [FromQuery] string objectName,
        [FromQuery] int expirySeconds = 3600)
    {
        try
        {
            var url = await _minioService.GetPresignedUrlAsync(objectName, expirySeconds);
            return Ok(new { bucket, objectName, url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pre-signed URL for {ObjectName}", objectName);
            return StatusCode(500, new { error = "Failed to generate download URL" });
        }
    }

    /// <summary>
    /// Delete a file from MinIO object storage
    /// </summary>
    /// <remarks>
    /// Permanently removes a file from the specified MinIO bucket. This action cannot be undone.
    /// </remarks>
    /// <param name="bucket">The bucket that contains the file</param>
    /// <param name="objectName">The object name to delete</param>
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

