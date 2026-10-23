using System.ComponentModel;
using AgentFlow.Api.Services;

namespace AgentFlow.Api.Agents;

public record SearchPlan([property: Description("2 till 4 korta sökfrågor, gärna både på frågans språk och på engelska")] string[] Queries);

public record ResearchResult(IReadOnlyList<string> Queries, IReadOnlyList<Source> Sources);

// Researcher: planerar sökningar, söker i alla källor och samlar de bästa träffarna.
public class Researcher(ILlm llm, IEnumerable<ISearchSource> sources, ILogger<Researcher> logger)
{
    public const string Name = "Researcher";
    public const int MaxSources = 8;
    private const int MaxTextLength = 1500;

    private const string System = """
        You are Researcher in a team of AI agents. Your job is to plan web searches that find facts
        needed to answer the user's question. Searches go to Wikipedia and DuckDuckGo, so use short,
        encyclopedic keywords (names, concepts), not full sentences.
        """;

    public async Task<ResearchResult> ResearchAsync(string question, CancellationToken ct)
    {
        var plan = await llm.CompleteJsonAsync<SearchPlan>(System, $"Question: {question}", ct);
        var queries = plan.Queries.Where(q => !string.IsNullOrWhiteSpace(q)).Distinct().Take(4).ToList();
        if (queries.Count == 0) queries.Add(question);

        var searches = queries.SelectMany(q => sources.Select(s => SafeSearchAsync(s, q, ct)));
        var found = (await Task.WhenAll(searches)).SelectMany(r => r);

        var unique = found
            .GroupBy(s => s.Url, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(MaxSources)
            .Select(s => s with { Text = s.Text.Length > MaxTextLength ? s.Text[..MaxTextLength] + "…" : s.Text })
            .ToList();
        return new ResearchResult(queries, unique);
    }

    // En källa som inte svarar ska inte stoppa hela körningen.
    private async Task<IReadOnlyList<Source>> SafeSearchAsync(ISearchSource source, string query, CancellationToken ct)
    {
        try
        {
            return await source.SearchAsync(query, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Sökning i {Source} efter \"{Query}\" misslyckades: {Message}", source.Name, query, e.Message);
            return [];
        }
    }

    public static string Format(IReadOnlyList<Source> sources) => sources.Count == 0
        ? "(No sources were found.)"
        : string.Join("\n\n", sources.Select((s, i) => $"[{i + 1}] {s.Title} ({s.Url})\n{s.Text}"));
}
