using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TimesheetCopilotApp.Backend.Services;

public class HandbookService
{
    private readonly string _filePath;
    private readonly ILogger<HandbookService> _logger;
    private List<HandbookSection> _sections = new();

    public HandbookService(ILogger<HandbookService> logger)
    {
        _logger = logger;
        // Determine handbook path relative to AppContext or workspace
        _filePath = Path.Combine(AppContext.BaseDirectory, "Data", "employee_handbook.md");
        if (!File.Exists(_filePath))
        {
            // Fallback for development where BaseDirectory might point to bin/Debug/net10.0/
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "employee_handbook.md");
        }
        LoadHandbook();
    }

    private void LoadHandbook()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogWarning($"Handbook file not found at: {_filePath}. RAG capability will be limited.");
                return;
            }

            var content = File.ReadAllText(_filePath);
            
            // Split by "## " (markdown secondary headers) to divide into logical policy sections
            var parts = Regex.Split(content, @"(?=## )");
            
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;

                // Extract title (the first line after ##)
                var lines = part.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var title = lines.Length > 0 ? lines[0].Replace("##", "").Trim() : "General Policies";
                
                _sections.Add(new HandbookSection
                {
                    Title = title,
                    Content = part.Trim()
                });
            }
            _logger.LogInformation($"Successfully loaded {_sections.Count} handbook sections from {_filePath}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load employee handbook.");
        }
    }

    [Description("Searches the employee handbook for policies, rules, and guidelines on leaves, vacation, work hours, wellness stipend, and timesheet submissions.")]
    public string SearchHandbook(
        [Description("The search query detailing what company policy or guideline to look up")] string query)
    {
        if (_sections.Count == 0)
        {
            return "Error: Employee handbook is currently unavailable.";
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return "Please provide a specific query to search the handbook.";
        }

        // Tokenize query into words, ignoring small words
        var keywords = query.Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(w => w.Trim().ToLowerInvariant())
                            .Where(w => w.Length > 2)
                            .ToList();

        if (keywords.Count == 0)
        {
            keywords.Add(query.Trim().ToLowerInvariant());
        }

        var scoredSections = _sections.Select(section =>
        {
            int score = 0;
            string contentLower = section.Content.ToLowerInvariant();
            string titleLower = section.Title.ToLowerInvariant();

            foreach (var keyword in keywords)
            {
                // Title matches get higher weights
                if (titleLower.Contains(keyword))
                {
                    score += 5;
                }
                
                // Content matches
                int index = 0;
                while ((index = contentLower.IndexOf(keyword, index, StringComparison.Ordinal)) != -1)
                {
                    score += 1;
                    index += keyword.Length;
                }
            }

            return new { Section = section, Score = score };
        })
        .Where(s => s.Score > 0)
        .OrderByDescending(s => s.Score)
        .ToList();

        if (scoredSections.Count == 0)
        {
            _logger.LogInformation($"No handbook sections matched the query: '{query}'. Returning basic guidance.");
            return "No specific handbook section matches your query. General company policies require all employees to follow core collaboration hours (10 AM - 3 PM) and standard work hours (9 AM - 5 PM). Please contact HR for more detailed policy questions.";
        }

        // Return top 2 matching sections to keep it concise but relevant
        var topResults = scoredSections.Take(2).Select(s => s.Section.Content);
        string result = string.Join("\n\n---\n\n", topResults);
        
        _logger.LogInformation($"Handbook search for '{query}' returned {scoredSections.Count} matches. Top section: '{scoredSections[0].Section.Title}'");
        return result;
    }
}

public class HandbookSection
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
