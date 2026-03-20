namespace ArchiveAPI.Services;

public interface IGeminiService
{
    Task<string> AnalyzeImageAsync(string base64Image, string mimeType, string prompt);
}

