using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI;
using System.ClientModel;
using System.ComponentModel;

namespace backend.Services;

/// <summary>
/// Provides semantic search over the employee handbook vector database.
/// The database must be populated separately by running the HandbookIndexer console tool.
/// </summary>
public sealed class HandbookService
{
    private readonly ILogger<HandbookService> _logger;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly string _connectionString;
    private readonly Task _initTask;
    private VectorStoreCollection<string, HandbookSectionRecord>? _collection;

    public HandbookService(ILogger<HandbookService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = BuildConnectionString();
        _embeddingGenerator = CreateEmbeddingGenerator(configuration);
        _initTask = InitializeCollectionAsync();
    }

    // -------------------------------------------------------------------------
    // Tool method (exposed to the AI agent)
    // -------------------------------------------------------------------------

    [Description("Searches the employee handbook PDF for policies, rules, and guidelines on " +
                 "leaves, vacation, work hours, wellness stipend, and timesheet submissions.")]
    public async Task<string> SearchHandbookAsync(
        [Description("The search query detailing what company policy or guideline to look up")]
        string query)
    {
        await _initTask;

        if (_collection is null)
            return "The handbook search service is currently unavailable due to an initialization error.";

        if (!IsDatabasePopulated())
        {
            _logger.LogWarning("SearchHandbookAsync called but the handbook database is empty.");
            return "The employee handbook has not been indexed yet. " +
                   "Please run the HandbookIndexer tool first, then restart the backend.";
        }

        if (string.IsNullOrWhiteSpace(query))
            return "Please provide a specific query to search the handbook.";

        try
        {
            _logger.LogInformation("Performing semantic search for: '{Query}'", query);

            var queryEmbeddings = await _embeddingGenerator.GenerateAsync([query]);
            var queryVector = queryEmbeddings[0].Vector;

            var searchOptions = new VectorSearchOptions<HandbookSectionRecord>();
            var results = _collection.SearchAsync(queryVector, top: 2, options: searchOptions);

            var matchingContents = new List<string>();
            await foreach (var result in results)
            {
                if (result.Record?.Content is not null)
                    matchingContents.Add(result.Record.Content);
            }

            if (matchingContents.Count == 0)
            {
                _logger.LogInformation("No results for query: '{Query}'", query);
                return "No relevant section was found in the employee handbook for your query. " +
                       "Please contact HR for assistance.";
            }

            _logger.LogInformation("Search returned {Count} result(s).", matchingContents.Count);
            return string.Join("\n\n---\n\n", matchingContents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during handbook semantic search.");
            return "An error occurred while searching the employee handbook. Please try again or contact HR.";
        }
    }

    // -------------------------------------------------------------------------
    // Private initialisation helpers
    // -------------------------------------------------------------------------

    private async Task InitializeCollectionAsync()
    {
        try
        {
            _logger.LogInformation("Opening handbook vector store...");
            var vectorStore = new SqliteVectorStore(_connectionString);
            _collection = vectorStore.GetCollection<string, HandbookSectionRecord>("HandbookSections");

            if (!IsDatabasePopulated())
            {
                _logger.LogWarning(
                    "Handbook vector database is empty or does not exist. " +
                    "Run HandbookIndexer to index Handbook.pdf before starting the backend.");
            }
            else
            {
                _logger.LogInformation("Handbook vector store is ready.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open the handbook vector store.");
        }
    }

    private bool IsDatabasePopulated()
    {
        try
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM HandbookSections";
            var count = (long)(command.ExecuteScalar() ?? 0L);
            return count > 0;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Static factory helpers
    // -------------------------------------------------------------------------

    private static string BuildConnectionString()
    {
        var dbDirectory = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Data"))
            ? Path.Combine(AppContext.BaseDirectory, "Data")
            : Path.Combine(Directory.GetCurrentDirectory(), "Data");

        var dbPath = Path.Combine(dbDirectory, "handbook.db");
        return $"Data Source={dbPath}";
    }

    private static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator(
        IConfiguration configuration)
    {
        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434/v1";
        var model = configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";

        var client = new OpenAIClient(
            new ApiKeyCredential("ollama"), // Ollama does not require a real API key
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

        return client.GetEmbeddingClient(model).AsIEmbeddingGenerator();
    }
}
