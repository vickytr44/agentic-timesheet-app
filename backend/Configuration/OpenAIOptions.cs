namespace backend.Configuration;

/// <summary>
/// Strongly-typed configuration for the OpenAI / Groq LLM provider.
/// Binds to the "OpenAI" section in appsettings.json.
/// </summary>
public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.groq.com/openai/v1/";
}
