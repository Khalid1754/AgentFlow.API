using System.Net;
using AgentFlow.Api.Services;

namespace AgentFlow.Api.Tests;

public class SearchTests
{
    [Fact]
    public async Task Wikipedia_ReturnsPagesInRankOrder()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            {"query":{"pages":[
              {"title":"B","index":2,"fullurl":"https://sv.wikipedia.org/wiki/B","extract":"Text B"},
              {"title":"A","index":1,"fullurl":"https://sv.wikipedia.org/wiki/A","extract":"Text A"},
              {"title":"Tom","index":3,"fullurl":"https://sv.wikipedia.org/wiki/Tom","extract":""}
            ]}}
            """);

        var results = await new WikipediaSearch(new HttpClient(handler), "sv").SearchAsync("elbil pris", CancellationToken.None);

        Assert.Equal(["A", "B"], results.Select(r => r.Title));
        Assert.Equal("sv.wikipedia.org", handler.Request!.RequestUri!.Host);
        Assert.Contains("gsrsearch=elbil%20pris", handler.Request.RequestUri.Query);
    }

    [Fact]
    public async Task DuckDuckGo_ReturnsAbstractAndRelatedTopics()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            {"Heading":"Elbil","AbstractText":"En elbil är...","AbstractURL":"https://example.org/elbil",
             "RelatedTopics":[{"Text":"Tesla - en biltillverkare","FirstURL":"https://duckduckgo.com/Tesla"},{"Name":"Grupp","Topics":[]}]}
            """);

        var results = await new DuckDuckGoSearch(new HttpClient(handler)).SearchAsync("elbil", CancellationToken.None);

        Assert.Equal(["Elbil", "Tesla"], results.Select(r => r.Title));
    }
}
