using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI;
using UglyToad.PdfPig;

namespace TimesheetCopilotApp.Backend.Services;

public class HandbookSectionRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineDistance)]
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }
}

public class HandbookService
{
    private readonly string _filePath;
    private readonly ILogger<HandbookService> _logger;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly string _connectionString;
    private readonly Task _initTask;
    private VectorStoreCollection<string, HandbookSectionRecord>? _collection;

    public HandbookService(ILogger<HandbookService> logger, IConfiguration configuration)
    {
        _logger = logger;

        // Look for Handbook.pdf in the Data directory
        var dbDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dbDirectory))
        {
            // Fallback for development (bin/Debug/net10.0/ -> project root)
            dbDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        }
        _filePath = Path.Combine(dbDirectory, "Handbook.pdf");

        var dbPath = Path.Combine(dbDirectory, "handbook.db");
        if (!Directory.Exists(Path.GetDirectoryName(dbPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        }
        _connectionString = $"Data Source={dbPath}";

        // Initialize embedding generator using OpenAI backup key or default OpenAI key
        var apiKey = configuration["OpenAI:OpenAI_ApiKey_Backup"] 
                     ?? configuration["OpenAI:ApiKey"] 
                     ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                     
        var endpoint = configuration["OpenAI:Endpoint"] ?? "https://api.openai.com/v1";
        
        var isBackupKey = !string.IsNullOrEmpty(configuration["OpenAI:OpenAI_ApiKey_Backup"]);
        
        IEmbeddingGenerator<string, Embedding<float>>? primaryGenerator = null;
        try
        {
            OpenAIClient openAIClient;
            if (isBackupKey)
            {
                _logger.LogInformation("Using OpenAI Backup Key for embeddings.");
                openAIClient = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey));
                primaryGenerator = openAIClient.GetEmbeddingClient("text-embedding-ada-002").AsIEmbeddingGenerator();
            }
            else
            {
                _logger.LogInformation("Using standard Endpoint for embeddings.");
                var clientOptions = new OpenAIClientOptions
                {
                    Endpoint = new Uri(endpoint)
                };
                openAIClient = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), clientOptions);
                var embedModel = endpoint.Contains("groq.com") ? "nomic-embed-text" : "text-embedding-ada-002";
                primaryGenerator = openAIClient.GetEmbeddingClient(embedModel).AsIEmbeddingGenerator();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize primary OpenAI embedding generator. Will use local fallback.");
        }

        _embeddingGenerator = new FallbackEmbeddingGenerator(primaryGenerator, _logger);

        // Kick off lazy asynchronous initialization to avoid blocking startup
        _initTask = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing Handbook SQLite Vector Store connection...");
            var vectorStore = new SqliteVectorStore(_connectionString);
            var col = vectorStore.GetCollection<string, HandbookSectionRecord>("HandbookSections");
            
            await col.EnsureCollectionExistsAsync();

            if (!IsDatabasePopulated())
            {
                await PopulateDatabaseAsync(col);
            }

            _collection = col;
            _logger.LogInformation("Handbook SQLite Vector Store successfully initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Handbook SQLite Vector Store.");
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if Handbook SQLite database is populated. It may not exist yet.");
            return false;
        }
    }

    private async Task PopulateDatabaseAsync(VectorStoreCollection<string, HandbookSectionRecord> collection)
    {
        _logger.LogInformation("Populating handbook vector database from PDF: {Path}", _filePath);
        
        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("Handbook PDF not found at {Path}. RAG capability will be limited.", _filePath);
            return;
        }

        try
        {
            using var pdf = PdfDocument.Open(_filePath);
            _logger.LogInformation("Loading {PageCount} pages from Employee Handbook PDF...", pdf.NumberOfPages);

            foreach (var page in pdf.GetPages())
            {
                var words = page.GetWords().ToList();
                if (words.Count == 0) continue;

                var pageText = new StringBuilder();
                foreach (var word in words)
                {
                    pageText.Append(word.Text);
                    pageText.Append(' ');
                }

                var content = pageText.ToString().Trim();

                // Skip near-empty pages (less than 40 characters of actual content)
                if (content.Length < 40) continue;

                var firstLine = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                       .FirstOrDefault(l => l.Trim().Length > 3)
                               ?? $"Page {page.Number}";

                var title = firstLine.Trim().Length > 80
                    ? firstLine.Trim()[..77] + "..."
                    : firstLine.Trim();

                var pageContent = $"[Page {page.Number}]\n{content}";

                _logger.LogInformation("Generating semantic embedding for Page {PageNumber}...", page.Number);
                var embeddings = await _embeddingGenerator.GenerateAsync(new[] { pageContent });
                var embedding = embeddings[0];

                var record = new HandbookSectionRecord
                {
                    Id = $"page_{page.Number}",
                    Title = title,
                    Content = pageContent,
                    ContentEmbedding = embedding.Vector
                };

                await collection.UpsertAsync(record);
            }

            _logger.LogInformation("Successfully completed handbook database population.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Handbook PDF and populate vector DB.");
        }
    }

    [Description("Searches the employee handbook PDF for policies, rules, and guidelines on leaves, vacation, work hours, wellness stipend, and timesheet submissions.")]
    public async Task<string> SearchHandbookAsync(
        [Description("The search query detailing what company policy or guideline to look up")] string query)
    {
        await _initTask;

        if (_collection == null)
        {
            return "Error: Employee handbook is currently unavailable.";
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return "Please provide a specific query to search the handbook.";
        }

        try
        {
            _logger.LogInformation("Performing semantic search for: '{Query}'", query);
            
            var queryEmbeddings = await _embeddingGenerator.GenerateAsync(new[] { query });
            var queryEmbedding = queryEmbeddings[0];

            var searchOptions = new VectorSearchOptions<HandbookSectionRecord>();

            var searchResults = _collection.SearchAsync(queryEmbedding.Vector, top: 2, options: searchOptions);
            var matchingContents = new List<string>();

            await foreach (var result in searchResults)
            {
                if (result.Record != null)
                {
                    matchingContents.Add(result.Record.Content);
                }
            }

            if (matchingContents.Count == 0)
            {
                _logger.LogInformation("No handbook sections matched query: '{Query}'", query);
                return "No specific handbook section matches your query. General company policies require all employees to follow core collaboration hours (10 AM - 3 PM) and standard work hours (9 AM - 5 PM). Please contact HR for more detailed policy questions.";
            }

            string resultText = string.Join("\n\n---\n\n", matchingContents);
            _logger.LogInformation("Handbook semantic search completed successfully with {Count} result(s).", matchingContents.Count);
            return resultText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during handbook semantic search query.");
            return "An error occurred while searching the employee handbook. Please contact HR for assistance.";
        }
    }

    private class FallbackEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>>? _primary;
        private readonly ILogger _logger;
        private bool _useFallback;

        public FallbackEmbeddingGenerator(IEmbeddingGenerator<string, Embedding<float>>? primary, ILogger logger)
        {
            _primary = primary;
            _logger = logger;
            _useFallback = primary == null;
        }

        public void Dispose()
        {
            _primary?.Dispose();
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (!_useFallback && _primary != null)
            {
                try
                {
                    return await _primary.GenerateAsync(values, options, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Primary embedding generator failed. Falling back to local deterministic hash vector generator.");
                    _useFallback = true;
                }
            }

            // Fallback: local deterministic hash vector generator (1536 dimensions)
            var result = new List<Embedding<float>>();
            foreach (var text in values)
            {
                var vector = new float[1536];
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var words = text.ToLowerInvariant().Split(new[] { ' ', ',', '.', '?', '!', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var word in words)
                    {
                        int hash = GetDeterministicHashCode(word);
                        int index = Math.Abs(hash) % 1536;
                        vector[index] += 1.0f;
                    }

                    // Normalize vector to unit length
                    float sumSq = 0;
                    for (int i = 0; i < 1536; i++) sumSq += vector[i] * vector[i];
                    if (sumSq > 0)
                    {
                        float norm = (float)Math.Sqrt(sumSq);
                        for (int i = 0; i < 1536; i++) vector[i] /= norm;
                    }
                }

                result.Add(new Embedding<float>(new ReadOnlyMemory<float>(vector)));
            }

            return new GeneratedEmbeddings<Embedding<float>>(result);
        }

        private int GetDeterministicHashCode(string str)
        {
            unchecked
            {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i + 1 == str.Length)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }
}
