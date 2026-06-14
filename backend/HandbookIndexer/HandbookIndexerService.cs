using HandbookCommon.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using System.Text;
using UglyToad.PdfPig;

namespace HandbookIndexer;

/// <summary>
/// Orchestrates reading the employee handbook PDF, generating embeddings via Ollama,
/// and upserting each page as a vector record into the SQLite vector store.
/// </summary>
public sealed class HandbookIndexerService(
    ILogger<HandbookIndexerService> logger,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    VectorStoreCollection<string, HandbookSectionRecord> collection)
{
    public async Task IndexAsync(string pdfPath, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Opening PDF: {Path}", pdfPath);

        using var pdf = PdfDocument.Open(pdfPath);

        logger.LogInformation("Handbook has {PageCount} pages. Starting indexing...", pdf.NumberOfPages);

        int indexed = 0;
        int skipped = 0;

        foreach (var page in pdf.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = ExtractPageText(page);
                if (content is null)
                {
                    logger.LogDebug("Skipping page {PageNumber} (insufficient content).", page.Number);
                    skipped++;
                    continue;
                }

                logger.LogInformation("[{Current}/{Total}] Embedding page {PageNumber}...",
                    page.Number, pdf.NumberOfPages, page.Number);

                var embeddings = await embeddingGenerator.GenerateAsync(
                    [content], cancellationToken: cancellationToken);

                if (embeddings == null || embeddings.Count == 0)
                {
                    logger.LogError("Embedding generator returned null or empty result for page {PageNumber}", page.Number);
                    skipped++;
                    continue;
                }

                var embedding = embeddings[0].Vector;
                if (embedding.IsEmpty)
                {
                    logger.LogError("Generated embedding is empty for page {PageNumber}", page.Number);
                    skipped++;
                    continue;
                }

                logger.LogDebug("Generated embedding with {Dimensions} dimensions", embedding.Length);

                var record = BuildRecord(page.Number, content, embedding);
                var embeddingArray = record.ContentEmbedding;
                logger.LogDebug("Record built: Id={Id}, EmbeddingLength={Length}, EmbeddingNull={IsNull}, EmbeddingEmpty={IsEmpty}", 
                    record.Id, embeddingArray?.Length ?? 0, embeddingArray == null, embeddingArray?.Length == 0);

                await collection.UpsertAsync(record, cancellationToken);
                logger.LogDebug("Successfully upserted record {Id}", record.Id);

                indexed++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing page {PageNumber}", page.Number);
                skipped++;
            }
        }

        logger.LogInformation(
            "Indexing complete. Pages indexed: {Indexed}, skipped: {Skipped}.",
            indexed, skipped);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static string? ExtractPageText(UglyToad.PdfPig.Content.Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0)
            return null;

        var sb = new StringBuilder(capacity: words.Count * 6);
        foreach (var word in words)
        {
            sb.Append(word.Text);
            sb.Append(' ');
        }

        var text = sb.ToString().Trim();

        // Skip near-empty pages (e.g. cover images, blank pages)
        return text.Length < 40 ? null : $"[Page {page.Number}]\n{text}";
    }

    private static HandbookSectionRecord BuildRecord(
        int pageNumber,
        string content,
        ReadOnlyMemory<float> embedding)
    {
        return new HandbookSectionRecord
        {
            Id = $"page_{pageNumber}",
            Title = ExtractTitle(content, pageNumber),
            Content = content,
            ContentEmbedding = embedding.ToArray()
        };
    }

    private static string ExtractTitle(string content, int pageNumber)
    {
        var firstMeaningfulLine = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(l => l.Trim().Length > 3)
            ?? $"Page {pageNumber}";

        var trimmed = firstMeaningfulLine.Trim();
        return trimmed.Length > 80 ? trimmed[..77] + "..." : trimmed;
    }
}
