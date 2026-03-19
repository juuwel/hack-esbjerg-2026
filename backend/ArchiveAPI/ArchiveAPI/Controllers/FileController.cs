using Microsoft.AspNetCore.Mvc;
using ArchiveAPI.Services;

namespace ArchiveAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileController : ControllerBase
{
    private readonly IMinioService _minioService;
    private readonly ILogger<FileController> _logger;

    public FileController(IMinioService minioService, ILogger<FileController> logger)
    {
        _minioService = minioService;
        _logger = logger;
    }
    
    [HttpPost()]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided or file is empty" });

        // Build a unique object name preserving the original filename
        var objectName = $"{Guid.NewGuid()}/{file.FileName}";

        try
        {
            await using var stream = file.OpenReadStream();
            await _minioService.UploadFileAsync(
                objectName,
                stream,
                file.Length,
                file.ContentType ?? "application/octet-stream");

            var url = await _minioService.GetPresignedUrlAsync(objectName);

            return CreatedAtAction(nameof(GetFileUrl), new { objectName = Uri.EscapeDataString(objectName) },
                new
                {
                    objectName,
                    fileName = file.FileName,
                    contentType = file.ContentType,
                    size = file.Length,
                    url
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName}", file.FileName);
            return StatusCode(500, new { error = "Failed to upload file" });
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

