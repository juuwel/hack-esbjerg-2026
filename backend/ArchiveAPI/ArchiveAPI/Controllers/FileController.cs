using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ArchiveAPI.Domain.Entities;
using ArchiveAPI.Services;
using ArchiveAPI.Shared.Requests;

namespace ArchiveAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IMinioService _minioService;
    private readonly IOpenSearchService _searchService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FileController> _logger;

    // Extensions/MIME types we can attempt OCR on
    private static readonly HashSet<string> OcrContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/jpg", "image/tiff", "image/bmp", "image/gif", "image/webp"
    };

    public FileController(IMinioService minioService, IOpenSearchService searchService, IHttpClientFactory httpClientFactory, ILogger<FileController> logger)
    {
        _minioService = minioService;
        _searchService = searchService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }
    
    [HttpPost()]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    public async Task<IActionResult> UploadFile([FromForm] IFormFile file, [FromForm] UploadArchiveRequest? metadata)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided or file is empty" });

        var documentId = Guid.NewGuid().ToString();
        // Build a unique object name preserving the original filename
        var objectName = $"{documentId}/{file.FileName}";

        try
        {
            // --- Read file bytes once for checksum + OCR ---
            byte[] fileBytes;
            await using (var readStream = file.OpenReadStream())
            {
                fileBytes = new byte[file.Length];
                _ = await readStream.ReadAsync(fileBytes);
            }

            // --- SHA-256 checksum ---
            var checksumBytes = SHA256.HashData(fileBytes);
            var checksum = Convert.ToHexString(checksumBytes).ToLowerInvariant();

            // --- Upload to MinIO ---
            await using (var uploadStream = new MemoryStream(fileBytes))
            {
                await _minioService.UploadFileAsync(
                    objectName,
                    uploadStream,
                    file.Length,
                    file.ContentType);
            }

            var url = await _minioService.GetPresignedUrlAsync(objectName);

            // --- Derive metadata ---
            var extension = Path.GetExtension(file.FileName);
            var format = extension.TrimStart('.').ToUpperInvariant();
            var contentType = file.ContentType;
            // --- OCR for image types ---
            string? extractedText = null;
            if (OcrContentTypes.Contains(contentType))
            {
                extractedText = await TryExtractTextViaOcrAsync(fileBytes, file.FileName, contentType);
            }

            // --- Build ArchiveDocument ---
            var tags = metadata?.Tags?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList() ?? [];

            var document = new ArchiveDocument
            {
                Id           = documentId,
                Title        = metadata?.Title ?? Path.GetFileNameWithoutExtension(file.FileName),
                Content      = extractedText,
                SourceUrl    = metadata?.SourceUrl,
                SourcePlatform = metadata?.SourcePlatform,
                Author       = metadata?.Author,
                ArchivedBy   = metadata?.ArchivedBy,
                CapturedAt   = DateTime.UtcNow,
                OriginalCreatedAt = metadata?.OriginalCreatedAt,
                ContentType  = metadata?.ContentType,
                Format       = format,
                Language     = metadata?.Language,
                Tags         = tags,
                Location     = metadata?.Location,
                Community    = metadata?.Community,
                HistoricalContext = metadata?.HistoricalContext,
                ChecksumSha256 = checksum,
                ObjectName   = objectName,
                Metadata     = new Dictionary<string, string>
                {
                    ["fileName"]    = file.FileName,
                    ["mimeType"]    = contentType,
                    ["sizeBytes"]   = file.Length.ToString(),
                    ["extension"]   = extension,
                }
            };

            // --- Index into OpenSearch ---
            await _searchService.IndexDocumentAsync("archive", documentId, document);
            _logger.LogInformation("Indexed document {DocumentId} for file {FileName}", documentId, file.FileName);

            return CreatedAtAction(nameof(GetFileUrl), new { objectName = Uri.EscapeDataString(objectName) },
                new
                {
                    documentId,
                    objectName,
                    fileName  = file.FileName,
                    contentType,
                    size      = file.Length,
                    format,
                    checksum,
                    hasExtractedText = extractedText != null,
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
    /// Attempts to extract text from image bytes by calling the tesseract-server HTTP service.
    /// Returns null if the service is unavailable or extraction fails.
    /// </summary>
    private async Task<string?> TryExtractTextViaOcrAsync(byte[] imageBytes, string fileName, string contentType)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("tesseract");

            using var form = new MultipartFormDataContent();

            var options = JsonSerializer.Serialize(new { languages = new[] { "eng", "dan" } });
            form.Add(new StringContent(options), "options");

            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "file", fileName);

            var response = await client.PostAsync("/tesseract", form);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("tesseract-server returned {Status}, skipping OCR", response.StatusCode);
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(responseStream);
            var text = doc.RootElement
                .GetProperty("data")
                .GetProperty("stdout")
                .GetString()
                ?.Trim();

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR extraction failed, proceeding without text content");
            return null;
        }
    }

    [HttpGet()]
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
    
    [HttpDelete()]
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
}

