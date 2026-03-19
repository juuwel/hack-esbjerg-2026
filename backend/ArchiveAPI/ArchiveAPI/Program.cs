using ArchiveAPI.Services;
using Minio;
using OpenSearch.Client;
using Scalar.AspNetCore;
using ArchiveAPI.Presentation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

// Add OpenAPI
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new()
        {
            Title = "ArchiveAPI",
            Version = "v1",
            Description = "API for managing and searching archive documents with OpenSearch"
        };
        return Task.CompletedTask;
    });
});

// Configure OpenSearch
var openSearchUrl = builder.Configuration["OPENSEARCH__URL"] ?? "http://localhost:9200";
var settings = new ConnectionSettings(new Uri(openSearchUrl))
    .DefaultIndex("archive");

builder.Services.AddSingleton<IOpenSearchClient>(new OpenSearchClient(settings));
builder.Services.AddScoped<IOpenSearchService, OpenSearchService>();

// Configure MinIO
var minioUrl = new Uri(builder.Configuration["Minio:URL"] ?? "http://localhost:9000");
var minioEndpoint = minioUrl.Authority;
var minioAccessKey = builder.Configuration["Minio:AccessKey"] ?? "minioadmin";
var minioSecretKey = builder.Configuration["Minio:SecretKey"] ?? "minioadmin";
var minioUseSsl = minioUrl.Scheme == "https";

builder.Services.AddMinio(configureClient => configureClient
    .WithEndpoint(minioEndpoint)
    .WithCredentials(minioAccessKey, minioSecretKey)
    .WithSSL(minioUseSsl)
    .Build());

builder.Services.AddScoped<IMinioService, MinioService>();

var app = builder.Build();

// Ensure the MinIO bucket exists once at startup
using (var scope = app.Services.CreateScope())
{
    var minio = scope.ServiceProvider.GetRequiredService<IMinioService>();
    await minio.EnsureBucketExistsAsync(MinioService.BUCKET);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}


app.MapControllers();

app.Run();