namespace backend.Configuration;

/// <summary>
/// Strongly-typed configuration for the Handbook search feature.
/// Binds to the "Handbook" section in appsettings.json.
/// </summary>
public sealed class HandbookOptions
{
    public const string SectionName = "Handbook";

    public string Mode { get; set; } = "LocalVectorless";
    public PageIndexOptions PageIndex { get; set; } = new();
}

/// <summary>
/// Configuration for the PageIndex Cloud search provider.
/// Nested under <see cref="HandbookOptions"/>.
/// </summary>
public sealed class PageIndexOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string DocId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api.pageindex.ai";
}
