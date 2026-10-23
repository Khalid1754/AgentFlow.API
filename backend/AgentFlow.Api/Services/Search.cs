using System.Text.Json.Nodes;

namespace AgentFlow.Api.Services;

public record Source(string Title, string Url, string Text);

// En plats där Researcher kan söka. Båda källorna nedan är gratis och kräver ingen nyckel.
public interface ISearchSource
{
    string Name { get; }
    Task<IReadOnlyList<Source>> SearchAsync(string query, CancellationToken ct);
}

// Wikipedias sök-API: hämtar inledningen av de bäst matchande artiklarna.
public class WikipediaSearch(HttpClient http, string language) : ISearchSource
{
    public string Name => $"Wikipedia ({language})";

    public async Task<IReadOnlyList<Source>> SearchAsync(string query, CancellationToken ct)
    {
        var url = $"https://{language}.wikipedia.org/w/api.php?action=query&format=json&formatversion=2" +
                  "&generator=search&gsrlimit=3&prop=extracts|info&inprop=url&exintro=1&explaintext=1" +
                  $"&gsrsearch={Uri.EscapeDataString(query)}";
        var json = JsonNode.Parse(await http.GetStringAsync(url, ct));
        var pages = json?["query"]?["pages"]?.AsArray() ?? [];
        return pages
            .OrderBy(p => p?["index"]?.GetValue<int>() ?? 0)
            .Select(p => new Source(
                p?["title"]?.GetValue<string>() ?? "",
                p?["fullurl"]?.GetValue<string>() ?? "",
                p?["extract"]?.GetValue<string>() ?? ""))
            .Where(s => s.Url.Length > 0 && s.Text.Length > 0)
            .ToList();
    }
}

// DuckDuckGos Instant Answer-API: ger en kort sammanfattning och relaterade ämnen.
public class DuckDuckGoSearch(HttpClient http) : ISearchSource
{
    public string Name => "DuckDuckGo";

    public async Task<IReadOnlyList<Source>> SearchAsync(string query, CancellationToken ct)
    {
        var url = $"https://api.duckduckgo.com/?format=json&no_html=1&skip_disambig=1&q={Uri.EscapeDataString(query)}";
        var json = JsonNode.Parse(await http.GetStringAsync(url, ct));
        var results = new List<Source>();

        var abstractText = json?["AbstractText"]?.GetValue<string>() ?? "";
        var abstractUrl = json?["AbstractURL"]?.GetValue<string>() ?? "";
        if (abstractText.Length > 0 && abstractUrl.Length > 0)
            results.Add(new Source(json?["Heading"]?.GetValue<string>() ?? query, abstractUrl, abstractText));

        foreach (var topic in json?["RelatedTopics"]?.AsArray() ?? [])
        {
            var text = topic?["Text"]?.GetValue<string>();
            var link = topic?["FirstURL"]?.GetValue<string>();
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(link)) continue;
            results.Add(new Source(text.Split(" - ")[0], link, text));
            if (results.Count >= 3) break;
        }
        return results;
    }
}
