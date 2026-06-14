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