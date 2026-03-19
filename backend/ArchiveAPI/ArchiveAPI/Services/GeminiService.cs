using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArchiveAPI.Services;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiService> _logger;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";


    public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini:ApiKey is missing in configuration");
        _logger = logger;
    }

    public async Task<string> AnalyzeImageAsync(string base64Image, string mimeType, string prompt)
    {
        // Strip data URL prefix if present
        if (base64Image.Contains(","))
        {
            base64Image = base64Image.Split(',')[1];
        }

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Image
                            }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{BaseUrl}?key={_apiKey}", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Gemini API error: {response.StatusCode} - {errorContent}");
            throw new HttpRequestException($"Gemini API request failed: {response.StatusCode}");
        }

        var responseString = await response.Content.ReadAsStringAsync();
        var jsonResponse = JsonNode.Parse(responseString);
        
        // Extract the text from the response structure
        // Response structure: candidates[0].content.parts[0].text
        try 
        {
            var text = jsonResponse?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
            return text ?? "No description generated.";
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to parse Gemini response");
             return "Failed to parse response.";
        }
    }
}


