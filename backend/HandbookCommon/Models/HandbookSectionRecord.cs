using Microsoft.Extensions.VectorData;

namespace HandbookCommon.Models;

/// <summary>
/// Represents a single page/section of the employee handbook stored in the vector database.
/// Dimensions are set to 768 to match the nomic-embed-text model output via Ollama.
/// </summary>
public sealed class HandbookSectionRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 768, DistanceFunction = DistanceFunction.CosineDistance)]
    public float[] ContentEmbedding { get; set; } = [];
}
