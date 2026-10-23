using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AgentFlow.Api.Services;

namespace AgentFlow.Api.Tests;

// Svarar som respektive agent utifrån vem systemprompten vänder sig till.
public class FakeLlm : ILlm
{
    public List<string> Calls { get; } = [];
    public Queue<string> ReviewAnswers { get; } = new();
    public string? FailFor { get; set; }

    public bool Enabled => true;

    public Task<string> CompleteAsync(string system, string prompt, JsonObject? schema, CancellationToken ct)
    {
        var agent = new[] { "Researcher", "Analyst", "Writer", "Reviewer" }.First(a => system.Contains($"You are {a}"));
        lock (Calls) Calls.Add(agent);
        if (agent == FailFor) throw new LlmException($"{agent} gick sönder");
        return Task.FromResult(agent switch
        {
            "Researcher" => """{"queries":["elbil","electric car"]}""",
            "Analyst" => """{"summary":"Elbilar är bra.","findings":[{"point":"Billiga att köra","sources":[1]}],"gaps":["Pris"]}""",
            "Writer" => prompt.Contains("Reviewer's feedback") ? "# Rapport v2" : "# Rapport v1",
            _ => ReviewAnswers.Count > 0 ? ReviewAnswers.Dequeue() : """{"approved":true,"feedback":""}""",
        });
    }
}

public class FakeSearch(params Source[] results) : ISearchSource
{
    public string Name => "Fake";
    public Task<IReadOnlyList<Source>> SearchAsync(string query, CancellationToken ct) => Task.FromResult<IReadOnlyList<Source>>(results);
}

public class FailingSearch : ISearchSource
{
    public string Name => "Trasig";
    public Task<IReadOnlyList<Source>> SearchAsync(string query, CancellationToken ct) => throw new HttpRequestException("nere");
}

public sealed class FakeHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public string? RequestBody { get; private set; }
    public int Calls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
