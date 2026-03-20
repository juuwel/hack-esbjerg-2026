namespace ArchiveAPI.Services;

public interface IGeminiService
{
    Task<GeminiImageAnalysisResult> AnalyzeImageAsync(string base64Image, string mimeType, string prompt);
}

public sealed class GeminiImageAnalysisResult
{
    public string? Description { get; init; }
    public List<string> Tags { get; init; } = [];
}

