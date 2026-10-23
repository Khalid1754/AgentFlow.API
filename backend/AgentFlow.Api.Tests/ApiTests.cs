using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentFlow.Api.Data;
using AgentFlow.Api.Models;
using AgentFlow.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentFlow.Api.Tests;

public class ApiTests : IClassFixture<ApiTests.App>
{
    // Hela API:t med minnesdatabas, påhittad AI och påhittad sökning.
    public class App : WebApplicationFactory<Program>
    {
        private readonly string _dbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AgentFlowDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AgentFlowDbContext>));
            services.AddDbContext<AgentFlowDbContext>(o => o.UseInMemoryDatabase(_dbName));
            services.RemoveAll<ILlm>();
            services.AddSingleton<ILlm>(new FakeLlm());
            services.RemoveAll<ISearchSource>();
            services.AddSingleton<ISearchSource>(new FakeSearch(new Source("Elbil", "https://example.org", "Text")));
        });
    }

    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = ApiJson.Options;

    public ApiTests(App app) => _client = app.CreateClient();

    [Theory]
    [InlineData("")]
    [InlineData("hej")]
    public async Task TooShortQuestion_IsRejected(string question)
    {
        var response = await _client.PostAsJsonAsync("/api/runs", new { question });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatedRun_IsCompletedAndStreamedAsEvents()
    {
        var response = await _client.PostAsJsonAsync("/api/runs", new { question = "Är elbilar bra?" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;

        // Händelseströmmen tar slut när körningen är klar.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var stream = await _client.GetStringAsync($"/api/runs/{created.Id}/events", cts.Token);
        Assert.Contains("event: step", stream);
        Assert.Contains("\"toAgent\":\"Analyst\"", stream);
        Assert.Contains("\"status\":\"completed\"", stream);

        var run = (await _client.GetFromJsonAsync<RunDto>($"/api/runs/{created.Id}", Json))!;
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal("# Rapport v1", run.Report);
        Assert.Equal(Enumerable.Range(1, run.Steps.Count), run.Steps.Select(s => s.Sequence));

        var list = (await _client.GetFromJsonAsync<List<RunSummaryDto>>("/api/runs", Json))!;
        Assert.Contains(list, r => r.Id == created.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/runs/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/runs/{created.Id}")).StatusCode);
    }
}
