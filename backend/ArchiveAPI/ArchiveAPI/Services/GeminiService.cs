using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ArchiveAPI.Services;

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiService> _logger;
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
    private const string DefaultJsonPrompt = """
        Describe this picture shortly and generate search tags.
        Return JSON only with this exact shape:
        {
          "description": "short description",
          "tags": ["tag-one", "tag-two"]
        }
        Rules:
        - description must be a short single sentence
        - tags must contain 3 to 10 concise search tags
        - tags must be lowercase strings
        - do not include markdown, explanations, or extra keys
        """;


    public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini:ApiKey is missing in configuration");
        _logger = logger;
    }

    public async Task<GeminiImageAnalysisResult> AnalyzeImageAsync(string base64Image, string mimeType, string prompt)
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
                        new { text = string.IsNullOrWhiteSpace(prompt) ? DefaultJsonPrompt : prompt },
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
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
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
            return ParseAnalysisResult(text);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Failed to parse Gemini response");
             return new GeminiImageAnalysisResult();
        }
    }

    private GeminiImageAnalysisResult ParseAnalysisResult(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new GeminiImageAnalysisResult();
        }

        var normalized = text.Trim();
        normalized = Regex.Replace(normalized, "^```(?:json)?\\s*|\\s*```$", string.Empty, RegexOptions.IgnoreCase);

        try
        {
            var response = JsonSerializer.Deserialize<GeminiJsonResponse>(normalized, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return new GeminiImageAnalysisResult
            {
                Description = string.IsNullOrWhiteSpace(response?.Description) ? null : response.Description.Trim(),
                Tags = response?.Tags?
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? []
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Gemini returned non-JSON or malformed JSON payload: {Payload}", normalized);
            return new GeminiImageAnalysisResult();
        }
    }

    private sealed class GeminiJsonResponse
    {
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
    }
}


