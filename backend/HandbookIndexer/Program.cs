using HandbookCommon.Models;
using HandbookIndexer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI;
using System.ClientModel;

// ---------------------------------------------------------------------------
// Build host (reads appsettings.json + environment variables automatically)
// ---------------------------------------------------------------------------
var host = Host.CreateApplicationBuilder(args).Build();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
var configuration = host.Services.GetRequiredService<IConfiguration>();

logger.LogInformation("=== Handbook Indexer ===");

// ---------------------------------------------------------------------------
// Resolve the shared Data directory (search for Data/Handbook.pdf using relative paths)
// ---------------------------------------------------------------------------
string? FindDataDir()
{
    // Allow overriding via first argument
    if (args.Length > 0)
    {
        var argDir = args[0];
        var pdf = Path.Combine(argDir, "Handbook.pdf");
        if (File.Exists(pdf))
            return argDir;
    }

    // Search upwards from current directory and the base directory for the Data folder
    var roots = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
    foreach (var root in roots)
    {
        var dir = root;
        for (int i = 0; i < 8; i++)
        {
            var dataCandidate = Path.Combine(dir, "Data");
            var pdfCandidate = Path.Combine(dataCandidate, "Handbook.pdf");
            if (File.Exists(pdfCandidate))
            {
                return dataCandidate;
            }

            // move one level up
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
    }

    return null;
}

var dataDir = FindDataDir();
if (dataDir is null)
{
    logger.LogError("Handbook PDF not found (searched relative paths).");
    logger.LogError("Pass the data directory as the first argument, e.g.: dotnet run -- /path/to/Data");
    return 1;
}

var pdfPath = Path.Combine(dataDir, "Handbook.pdf");
var dbPath = Path.Combine(dataDir, "handbook.db");

// ---------------------------------------------------------------------------
// Create Ollama embedding generator (OpenAI-compatible endpoint)
// ---------------------------------------------------------------------------
var ollamaEndpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434/v1";
var embeddingModel = configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";

logger.LogInformation("Ollama endpoint : {Endpoint}", ollamaEndpoint);
logger.LogInformation("Embedding model : {Model}", embeddingModel);

var ollamaClient = new OpenAIClient(
    new ApiKeyCredential("ollama"), // Ollama does not require a real key
    new OpenAIClientOptions { Endpoint = new Uri(ollamaEndpoint) });

IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
    ollamaClient.GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator();

// ---------------------------------------------------------------------------
// Open (or create) the SQLite vector store collection
// ---------------------------------------------------------------------------
var connectionString = $"Data Source={dbPath}";

// Delete the old database if it exists so it can be recreated with the proper schema
if (File.Exists(dbPath))
{
    logger.LogInformation("Removing old database to recreate with proper schema: {DbPath}", dbPath);
    try
    {
        File.Delete(dbPath);
        logger.LogInformation("Old database deleted successfully");

        // Also delete WAL and SHM files if they exist
        var walPath = dbPath + "-wal";
        var shmPath = dbPath + "-shm";
        if (File.Exists(walPath)) File.Delete(walPath);
        if (File.Exists(shmPath)) File.Delete(shmPath);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to delete old database files");
    }
}
else
{
    logger.LogInformation("No existing database found at {DbPath}, creating new one", dbPath);
}

using (var vectorStore = new SqliteVectorStore(connectionString))
{
    var collection = vectorStore.GetCollection<string, HandbookSectionRecord>("HandbookSections");

    logger.LogInformation("Creating vector store collection with proper schema at: {DbPath}", dbPath);
    await collection.EnsureCollectionExistsAsync();

    // ---------------------------------------------------------------------------
    // Run the indexer
    // ---------------------------------------------------------------------------
    var indexerService = new HandbookIndexerService(
        host.Services.GetRequiredService<ILogger<HandbookIndexerService>>(),
        embeddingGenerator,
        collection);

    try
    {
        await indexerService.IndexAsync(pdfPath);
        logger.LogInformation("Done. Verifying records were saved to the database...");

        // Verify records were saved by querying the database directly
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            connection.Open();
            var command = connection.CreateCommand();

            command.CommandText = "SELECT COUNT(*) FROM HandbookSections";
            var recordCount = (long)(command.ExecuteScalar() ?? 0L);
            logger.LogInformation("Database now contains {Count} handbook records.", recordCount);

            if (recordCount == 0)
            {
                logger.LogError("WARNING: No records were saved to the database. Please check the indexing process.");
                return 1;
            }

            // Check vector chunks table
            try
            {
                command.CommandText = "SELECT COUNT(*) as chunk_count FROM vec_HandbookSections_vector_chunks00";
                var vectorChunkCount = (long)(command.ExecuteScalar() ?? 0L);
                logger.LogInformation("Vector chunk rows stored: {Count}", vectorChunkCount);

                if (vectorChunkCount > 0)
                {
                    command.CommandText = "SELECT COUNT(DISTINCT rowid) as unique_rowids FROM vec_HandbookSections_vector_chunks00";
                    var uniqueRowIds = (long)(command.ExecuteScalar() ?? 0L);
                    logger.LogInformation("Unique vector chunk rowids: {Count}", uniqueRowIds);

                    // Get size of vectors column
                    command.CommandText = "SELECT MAX(LENGTH(vectors)) as max_vector_size FROM vec_HandbookSections_vector_chunks00";
                    var maxVectorSize = command.ExecuteScalar();
                    logger.LogInformation("Max vector size in bytes: {Size}", maxVectorSize ?? 0);
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation("Error checking vector chunks details: {Message}", ex.Message);
            }

            // Try to get a sample record
            command.CommandText = "SELECT Id, Title, LENGTH(Content) as ContentLength FROM HandbookSections LIMIT 1";
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                logger.LogInformation("Sample record - Id: {Id}, Title: {Title}, ContentLength: {Length}",
                    reader["Id"], reader["Title"], reader["ContentLength"]);
            }

            logger.LogInformation("Successfully indexed and persisted handbook records. You can now start the backend and query the handbook.");

            // Quick test: Try a semantic search to verify embeddings are working
            logger.LogInformation("=== Testing Semantic Search ===");
            try
            {
                var testQuery = "What is the company leave policy?";
                var queryEmbeddings = await embeddingGenerator.GenerateAsync([testQuery]);
                var queryVector = queryEmbeddings[0].Vector.ToArray();
                logger.LogInformation("Generated test query embedding with {Dimensions} dimensions", queryVector.Length);

                var searchOptions = new VectorSearchOptions<HandbookSectionRecord>();
                var searchResults = collection.SearchAsync(queryVector, top: 2, options: searchOptions);

                int resultCount = 0;
                await foreach (var result in searchResults)
                {
                    resultCount++;
                    logger.LogInformation("Search result {Count}: Id={Id}, Title={Title}, Score={Score}",
                        resultCount, result.Record?.Id, result.Record?.Title, result.Score);
                }

                if (resultCount == 0)
                {
                    logger.LogWarning("WARNING: Semantic search returned no results. Embeddings may not be properly indexed.");
                }
                else
                {
                    logger.LogInformation("✓ Semantic search is working! Found {Count} relevant sections.", resultCount);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during semantic search test");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not verify database content, but indexing may still have succeeded.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Indexing failed");
        return 1;
    }
}
// The 'using' statement ensures the vector store is disposed and all writes are committed to SQLite
return 0;