using HandbookCommon.Models;
using HandbookIndexer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
// Resolve the shared Data directory
// ---------------------------------------------------------------------------
var dataDir = ResolveDataDirectory(args.ElementAtOrDefault(0), logger);
var pdfPath = Path.Combine(dataDir, "Handbook.pdf");
var dbPath = Path.Combine(dataDir, "handbook.db");

if (!File.Exists(pdfPath))
{
    logger.LogError("Handbook PDF not found at: {Path}", pdfPath);
    logger.LogError("Pass the data directory as the first argument, e.g.: dotnet run -- /path/to/Data");
    return 1;
}

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
var vectorStore = new SqliteVectorStore(connectionString);
var collection = vectorStore.GetCollection<string, HandbookSectionRecord>("HandbookSections");

logger.LogInformation("Ensuring vector store collection exists at: {DbPath}", dbPath);
await collection.EnsureCollectionExistsAsync();

// ---------------------------------------------------------------------------
// Run the indexer
// ---------------------------------------------------------------------------
var indexerService = new HandbookIndexerService(
    host.Services.GetRequiredService<ILogger<HandbookIndexerService>>(),
    embeddingGenerator,
    collection);

await indexerService.IndexAsync(pdfPath);

logger.LogInformation("Done. You can now start the backend and query the handbook.");
return 0;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------
static string ResolveDataDirectory(string? argPath, ILogger logger)
{
    if (argPath is not null && Directory.Exists(argPath))
    {
        logger.LogInformation("Using data directory from argument: {Path}", argPath);
        return argPath;
    }

    // Search common locations relative to where the tool is run from
    var candidates = new[]
    {
        Path.Combine(Directory.GetCurrentDirectory(), "Data"),           // run from backend/
        Path.Combine(Directory.GetCurrentDirectory(), "..", "Data"),     // run from backend/HandbookIndexer/
        Path.Combine(AppContext.BaseDirectory, "Data"),                  // run from publish output
    };

    var found = candidates.FirstOrDefault(Directory.Exists);
    if (found is not null)
    {
        logger.LogInformation("Data directory resolved to: {Path}", Path.GetFullPath(found));
        return found;
    }

    throw new DirectoryNotFoundException(
        "Could not locate the Data directory. " +
        "Pass the path as the first argument: dotnet run -- /absolute/path/to/Data");
}
