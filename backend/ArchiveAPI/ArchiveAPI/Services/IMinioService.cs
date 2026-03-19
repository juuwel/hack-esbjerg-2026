namespace ArchiveAPI.Services;

public interface IMinioService
{
    /// <summary>
    /// Ensures the given bucket exists, creating it if necessary.
    /// </summary>
    Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct = default);

    /// <summary>
    /// Uploads a stream to the specified bucket and returns the object name.
    /// </summary>
    Task<string> UploadFileAsync(
        string objectName,
        Stream data,
        long size,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a pre-signed URL for downloading the object.
    /// </summary>
    Task<string> GetPresignedUrlAsync(string objectName, int expirySeconds = 3600, CancellationToken ct = default);

    /// <summary>
    /// Deletes an object from the bucket.
    /// </summary>
    Task DeleteFileAsync(string objectName, CancellationToken ct = default);
}

