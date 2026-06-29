using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI;
using System.ClientModel;
using System.ComponentModel;
using HandbookCommon.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Linq;


using Microsoft.Extensions.Options;
using backend.Configuration;

namespace backend.Services;

/// <summary>
/// Provides semantic search over the employee handbook vector database.
/// The database must be populated separately by running the HandbookIndexer console tool.
/// </summary>
public sealed class HandbookService : IHandbookService
{
    private readonly ILogger<HandbookService> _logger;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly string _connectionString;
    private readonly Task _initTask;
    private VectorStoreCollection<string, HandbookSectionRecord>? _collection;

    private readonly IChatClient _chatClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HandbookOptions _options;

    public HandbookService(
        ILogger<HandbookService> logger,
        IOptions<HandbookOptions> handbookOptions,
        IOptions<OllamaOptions> ollamaOptions,
        IChatClient chatClient,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _connectionString = BuildConnectionString();
        _embeddingGenerator = CreateEmbeddingGenerator(ollamaOptions.Value);
        _chatClient = chatClient;
        _httpClientFactory = httpClientFactory;
        _options = handbookOptions.Value;

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
        if (string.IsNullOrWhiteSpace(query))
            return "Please provide a specific query to search the handbook.";

        if (_options.Mode.Equals("PageIndexCloud", StringComparison.OrdinalIgnoreCase))
        {
            return await SearchHandbookPageIndexCloudAsync(query);
        }

        await _initTask;

        if (_collection is null)
            return "The handbook search service is currently unavailable due to an initialization error.";

        if (!IsDatabasePopulated())
        {
            _logger.LogWarning("SearchHandbookAsync called but the handbook database is empty.");
            return "The employee handbook has not been indexed yet. " +
                   "Please run the HandbookIndexer tool first, then restart the backend.";
        }

        if (_options.Mode.Equals("LocalVectorless", StringComparison.OrdinalIgnoreCase))
        {
            return await SearchHandbookLocalVectorlessAsync(query);
        }

        // Default to VectorSearch
        return await SearchHandbookVectorAsync(query);
    }

    private async Task<string> SearchHandbookVectorAsync(string query)
    {
        try
        {
            _logger.LogInformation("Performing semantic search for: '{Query}'", query);

            var queryEmbeddings = await _embeddingGenerator.GenerateAsync([query]);
            var queryVectorMemory = queryEmbeddings[0].Vector;
            var queryVector = queryVectorMemory.ToArray();

            var searchOptions = new VectorSearchOptions<HandbookSectionRecord>();
            var results = _collection!.SearchAsync(queryVector, top: 2, options: searchOptions);

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

    public record PageHeader(string Id, string Title);

    private async Task<List<PageHeader>> FetchPageHeadersAsync()
    {
        var headers = new List<PageHeader>();
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Title FROM HandbookSections";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            headers.Add(new PageHeader(reader.GetString(0), reader.GetString(1)));
        }
        return headers;
    }

    private async Task<List<string>> FetchPageContentsAsync(List<string> pageIds)
    {
        var contents = new List<string>();
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        
        var parameterNames = pageIds.Select((_, index) => $"@id{index}").ToArray();
        var inClause = string.Join(",", parameterNames);
        command.CommandText = $"SELECT Content FROM HandbookSections WHERE Id IN ({inClause})";
        
        for (int i = 0; i < pageIds.Count; i++)
        {
            command.Parameters.AddWithValue(parameterNames[i], pageIds[i]);
        }

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            contents.Add(reader.GetString(0));
        }
        return contents;
    }

    private async Task<string> SearchHandbookLocalVectorlessAsync(string query)
    {
        try
        {
            _logger.LogInformation("Performing local vectorless search for: '{Query}'", query);

            // 1. Fetch all page IDs and titles
            var pages = await FetchPageHeadersAsync();

            // 2. Ask the LLM to select pages
            var jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true };
            var routingPrompt = $@"
You are given a query and a table of contents containing page IDs and titles from the employee handbook.
Your task is to identify which page(s) are most likely to contain the answer to the query.

Query: {query}

Handbook Table of Contents:
{JsonSerializer.Serialize(pages, jsonSerializerOptions)}

Please reply in the exact JSON format below:
{{
    ""thinking"": ""<Your short reasoning process on why these pages are relevant>"",
    ""selected_pages"": [""page_id_1"", ""page_id_2""]
}}
Return ONLY the raw JSON structure. Do not wrap it in markdown code blocks or add any other text.";

            var response = await _chatClient.GetResponseAsync(routingPrompt);
            var responseText = response.Text;

            _logger.LogInformation("LLM Selection Response: {Response}", responseText);

            var cleanedJson = responseText.Replace("```json", "").Replace("```", "").Trim();
            using var jsonDoc = JsonDocument.Parse(cleanedJson);
            var selectedPageIds = jsonDoc.RootElement.GetProperty("selected_pages")
                .EnumerateArray()
                .Select(x => x.GetString()!)
                .ToList();

            if (selectedPageIds.Count == 0)
            {
                return "No relevant handbook sections could be determined for your query.";
            }

            // 3. Fetch text contents
            var contents = await FetchPageContentsAsync(selectedPageIds);
            if (contents.Count == 0)
            {
                return "The selected handbook sections could not be retrieved from the database.";
            }

            return string.Join("\n\n---\n\n", contents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during local vectorless search.");
            return "An error occurred while performing local vectorless search of the employee handbook.";
        }
    }

    public class PageIndexNode
    {
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("page_index")]
        public int PageIndex { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("children")]
        public List<PageIndexNode>? Children { get; set; }
    }

    public class PageIndexLightweightNode
    {
        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("page_index")]
        public int PageIndex { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("children")]
        public List<PageIndexLightweightNode>? Children { get; set; }
    }

    private PageIndexLightweightNode ToLightweight(PageIndexNode node)
    {
        return new PageIndexLightweightNode
        {
            NodeId = node.NodeId,
            Title = node.Title,
            PageIndex = node.PageIndex,
            Summary = node.Summary,
            Children = node.Children?.Select(ToLightweight).ToList()
        };
    }

    private void MapNodeTexts(PageIndexNode node, Dictionary<string, string> lookup)
    {
        if (!string.IsNullOrEmpty(node.Text))
        {
            lookup[node.NodeId] = node.Text;
        }
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                MapNodeTexts(child, lookup);
            }
        }
    }

    private async Task<string> SearchHandbookPageIndexCloudAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(_options.PageIndex.ApiKey) || _options.PageIndex.ApiKey == "YOUR_PAGEINDEX_API_KEY")
        {
            return "PageIndex Cloud configuration error: ApiKey is not set in appsettings.json.";
        }

        if (string.IsNullOrWhiteSpace(_options.PageIndex.DocId) || _options.PageIndex.DocId == "YOUR_PAGEINDEX_DOC_ID")
        {
            return "PageIndex Cloud configuration error: DocId is not set in appsettings.json. Please run the HandbookIndexer console app first.";
        }

        try
        {
            _logger.LogInformation("Performing PageIndex Cloud search for: '{Query}'", query);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("api_key", _options.PageIndex.ApiKey);

            var url = $"{_options.PageIndex.Endpoint.TrimEnd('/')}/doc/{_options.PageIndex.DocId}/?type=tree&node_summary=true";
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to fetch tree from PageIndex: {Status} - {Error}", response.StatusCode, err);
                return "Failed to fetch document index from PageIndex Cloud. Please verify your ApiKey and DocId.";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseJson);
            var resultElement = jsonDoc.RootElement.GetProperty("result");
            
            var fullTree = JsonSerializer.Deserialize<PageIndexNode>(resultElement.GetRawText());
            if (fullTree == null)
            {
                return "Failed to parse tree structure returned from PageIndex Cloud.";
            }

            var lightweightTree = ToLightweight(fullTree);

            var reasoningPrompt = $@"
You are given a query and a hierarchical tree structure of a document.
Each node contains a node id, node title, and a corresponding summary.
Your task is to identify which node(s) are most likely to contain the answer to the query.

Query: {query}

Document tree structure:
{JsonSerializer.Serialize(lightweightTree, new JsonSerializerOptions { WriteIndented = true })}

Please reply in the exact JSON format below:
{{
    ""thinking"": ""<Your short reasoning process on why these nodes are relevant>"",
    ""node_list"": [""node_id_1"", ""node_id_2""]
}}
Return ONLY the raw JSON structure. Do not wrap it in markdown code blocks or add any other text.";

            var llmResponse = await _chatClient.GetResponseAsync(reasoningPrompt);
            var responseText = llmResponse.Text;

            _logger.LogInformation("LLM PageIndex Cloud Selection Response: {Response}", responseText);

            var cleanedJson = responseText.Replace("```json", "").Replace("```", "").Trim();
            using var selectDoc = JsonDocument.Parse(cleanedJson);
            var selectedNodeIds = selectDoc.RootElement.GetProperty("node_list")
                .EnumerateArray()
                .Select(x => x.GetString()!)
                .ToList();

            if (selectedNodeIds.Count == 0)
            {
                return "No relevant handbook nodes could be determined for your query by the reasoning agent.";
            }

            var lookup = new Dictionary<string, string>();
            MapNodeTexts(fullTree, lookup);

            var selectedTexts = selectedNodeIds
                .Where(lookup.ContainsKey)
                .Select(id => lookup[id]);

            return string.Join("\n\n---\n\n", selectedTexts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PageIndex Cloud search.");
            return "An error occurred while performing PageIndex Cloud search of the employee handbook.";
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
        OllamaOptions options)
    {
        var endpoint = options.Endpoint;
        var model = options.EmbeddingModel;

        var client = new OpenAIClient(
            new ApiKeyCredential("ollama"), // Ollama does not require a real API key
            new OpenAIClientOptions { Endpoint = new Uri(endpoint) });

        return client.GetEmbeddingClient(model).AsIEmbeddingGenerator();
    }
}
