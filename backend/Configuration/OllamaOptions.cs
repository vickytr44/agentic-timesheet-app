namespace backend.Configuration;

/// <summary>
/// Strongly-typed configuration for the Ollama LLM and embedding provider.
/// Binds to the "Ollama" section in appsettings.json.
/// </summary>
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string Endpoint { get; set; } = "http://localhost:11434/v1";
    public string ModelId { get; set; } = "qwen3:4b";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}
