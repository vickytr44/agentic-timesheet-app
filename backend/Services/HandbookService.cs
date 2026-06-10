using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace TimesheetCopilotApp.Backend.Services;

public class HandbookService
{
    private readonly string _filePath;
    private readonly ILogger<HandbookService> _logger;
    private List<HandbookSection> _sections = new();

    public HandbookService(ILogger<HandbookService> logger)
    {
        _logger = logger;

        // Look for Handbook.pdf in the Data directory
        _filePath = Path.Combine(AppContext.BaseDirectory, "Data", "Handbook.pdf");
        if (!File.Exists(_filePath))
        {
            // Fallback for development (bin/Debug/net10.0/ → project root)
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Handbook.pdf");
        }

        LoadHandbook();
    }

    private void LoadHandbook()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogWarning("Handbook PDF not found at: {Path}. RAG capability will be limited.", _filePath);
                return;
            }

            using var pdf = PdfDocument.Open(_filePath);

            // Build one section per page so each chunk is a focused, searchable unit.
            // Pages with very little text (e.g. cover images) are skipped.
            foreach (Page page in pdf.GetPages())
            {
                // Extract text from all words on the page, preserving reading order.
                var words = page.GetWords().ToList();
                if (words.Count == 0) continue;

                var pageText = new StringBuilder();
                string? prevLine = null;

                foreach (var word in words)
                {
                    // PdfPig word bounding boxes: use Y to detect line breaks (simplified).
                    pageText.Append(word.Text);
                    pageText.Append(' ');
                }

                var content = pageText.ToString().Trim();

                // Skip near-empty pages (less than 40 characters of actual content)
                if (content.Length < 40) continue;

                // Use the first non-trivial line as the section title
                var firstLine = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                       .FirstOrDefault(l => l.Trim().Length > 3)
                               ?? $"Page {page.Number}";

                _sections.Add(new HandbookSection
                {
                    Title = firstLine.Trim().Length > 80
                        ? firstLine.Trim()[..77] + "..."
                        : firstLine.Trim(),
                    Content = $"[Page {page.Number}]\n{content}"
                });
            }

            _logger.LogInformation(
                "Successfully loaded {Count} handbook sections from PDF: {Path}",
                _sections.Count, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load employee handbook PDF.");
        }
    }

    [Description("Searches the employee handbook PDF for policies, rules, and guidelines on leaves, vacation, work hours, wellness stipend, and timesheet submissions.")]
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

        // Tokenize query into meaningful keywords
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
                // Title matches carry higher weight
                if (titleLower.Contains(keyword)) score += 5;

                // Count every occurrence in content
                int index = 0;
                while ((index = contentLower.IndexOf(keyword, index, StringComparison.Ordinal)) != -1)
                {
                    score++;
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
            _logger.LogInformation("No handbook sections matched query: '{Query}'.", query);
            return "No specific handbook section matches your query. General company policies require all employees to follow core collaboration hours (10 AM - 3 PM) and standard work hours (9 AM - 5 PM). Please contact HR for more detailed policy questions.";
        }

        // Return top 2 matching sections
        var topResults = scoredSections.Take(2).Select(s => s.Section.Content);
        string result = string.Join("\n\n---\n\n", topResults);

        _logger.LogInformation(
            "Handbook search for '{Query}' → {Count} match(es). Top: '{Title}'",
            query, scoredSections.Count, scoredSections[0].Section.Title);

        return result;
    }
}

public class HandbookSection
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
