using Minio;
using Minio.DataModel.Args;

namespace ArchiveAPI.Services;

public class MinioService(IMinioClient client, ILogger<MinioService> logger) : IMinioService
{
    public const string BUCKET = "archive";
    
    public async Task EnsureBucketExistsAsync(string bucketName, CancellationToken ct = default)
    {
        var exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(BUCKET), ct);

        if (!exists)
        {
            logger.LogInformation("Creating bucket {Bucket}", BUCKET);
            await client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(BUCKET), ct);
        }
    }

    public async Task<string> UploadFileAsync(
        string objectName,
        Stream data,
        long size,
        string contentType,
        CancellationToken ct = default)
    {

        var args = new PutObjectArgs()
            .WithBucket(BUCKET)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(size)
            .WithContentType(contentType);

        await client.PutObjectAsync(args, ct);
        logger.LogInformation("Uploaded object {Object} to bucket {Bucket}", objectName, BUCKET);

        return objectName;
    }

    public async Task<StoredObject> GetFileAsync(string objectName, CancellationToken ct = default)
    {
        var statArgs = new StatObjectArgs()
            .WithBucket(BUCKET)
            .WithObject(objectName);

        var stat = await client.StatObjectAsync(statArgs, ct);

        var content = new MemoryStream();
        var getArgs = new GetObjectArgs()
            .WithBucket(BUCKET)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyTo(content));

        await client.GetObjectAsync(getArgs, ct);
        content.Position = 0;

        var contentType = string.IsNullOrWhiteSpace(stat.ContentType)
            ? "application/octet-stream"
            : stat.ContentType;

        return new StoredObject(content, contentType, stat.Size);
    }

    public async Task DeleteFileAsync(string objectName, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(BUCKET)
            .WithObject(objectName);

        await client.RemoveObjectAsync(args, ct);
        logger.LogInformation("Deleted object {Object} from bucket {Bucket}", objectName, BUCKET);
    }
}

