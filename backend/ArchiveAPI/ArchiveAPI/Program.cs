using ArchiveAPI.Services;
using OpenSearch.Client;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();