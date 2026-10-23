using AgentFlow.Api.Agents;
using AgentFlow.Api.Data;
using AgentFlow.Api.Models;
using AgentFlow.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Api.Tests;

public class OrchestratorTests
{
    private readonly FakeLlm _llm = new();
    private readonly ServiceProvider _services;

    public OrchestratorTests()
    {
        var dbName = Guid.NewGuid().ToString();
        _services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AgentFlowDbContext>(o => o.UseInMemoryDatabase(dbName))
            .AddSingleton<ILlm>(_llm)
            .AddSingleton<ISearchSource>(new FakeSearch(new Source("Elbil", "https://sv.wikipedia.org/wiki/Elbil", "En elbil drivs av el.")))
            .AddSingleton<ISearchSource>(new FailingSearch())
            .AddSingleton<Researcher>().AddSingleton<Analyst>().AddSingleton<Writer>().AddSingleton<Reviewer>()
            .AddSingleton<RunEvents>().AddSingleton<RunOrchestrator>()
            .BuildServiceProvider();
    }

    private async Task<Run> ExecuteAsync(List<RunEvent>? received = null)
    {
        var run = new Run { Question = "Är elbilar bra?" };
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgentFlowDbContext>();
            db.Runs.Add(run);
            await db.SaveChangesAsync();
        }
        using var subscription = _services.GetRequiredService<RunEvents>().Subscribe(run.Id);
        await _services.GetRequiredService<RunOrchestrator>().ExecuteAsync(run.Id, CancellationToken.None);
        while (received is not null && subscription.Reader.TryRead(out var e)) received.Add(e);

        using var readScope = _services.CreateScope();
        return await readScope.ServiceProvider.GetRequiredService<AgentFlowDbContext>().Runs
            .Include(r => r.Steps).SingleAsync(r => r.Id == run.Id);
    }

    private static string[] Handoffs(Run run) => run.Steps.OrderBy(s => s.Sequence)
        .Where(s => s.Kind == StepKind.Handoff).Select(s => $"{s.Agent}->{s.ToAgent}").ToArray();

    [Fact]
    public async Task AgentsHandOverInOrder_AndReportIsSaved()
    {
        var events = new List<RunEvent>();
        var run = await ExecuteAsync(events);

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal("# Rapport v1", run.Report);
        Assert.NotNull(run.FinishedAt);
        Assert.Equal(["Researcher->Analyst", "Analyst->Writer", "Writer->Reviewer"], Handoffs(run));
        Assert.Equal(["Researcher", "Analyst", "Writer", "Reviewer"], _llm.Calls);
        // En trasig sökkälla stoppar inte körningen.
        Assert.Contains(run.Steps, s => s.Agent == "Researcher" && s.Summary.StartsWith("Hittade 1 källor"));

        // Alla steg skickades live, och sist kom statusen Completed.
        Assert.Equal(run.Steps.Count, events.Count(e => e.Type == "step"));
        Assert.Equal(RunStatus.Completed, events.Last().Status);
    }

    [Fact]
    public async Task RejectedReport_GoesBackToWriterOnce()
    {
        _llm.ReviewAnswers.Enqueue("""{"approved":false,"feedback":"Saknar pris."}""");

        var run = await ExecuteAsync();

        Assert.Equal("# Rapport v2", run.Report);
        Assert.Equal(["Researcher->Analyst", "Analyst->Writer", "Writer->Reviewer", "Reviewer->Writer", "Writer->Reviewer"], Handoffs(run));
        Assert.Contains(run.Steps, s => s.Agent == "Reviewer" && s.Detail == "Saknar pris.");
    }

    [Fact]
    public async Task RevisionsAreLimited()
    {
        _llm.ReviewAnswers.Enqueue("""{"approved":false,"feedback":"Fel 1"}""");
        _llm.ReviewAnswers.Enqueue("""{"approved":false,"feedback":"Fel 2"}""");

        var run = await ExecuteAsync();

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(1 + RunOrchestrator.MaxRevisions, _llm.Calls.Count(c => c == "Writer"));
        Assert.Contains("anmärkningar", run.Steps.OrderBy(s => s.Sequence).Last().Summary);
    }

    [Fact]
    public async Task FailingAgent_FailsRunWithMessage()
    {
        _llm.FailFor = "Analyst";

        var run = await ExecuteAsync();

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("Analyst gick sönder", run.Error);
        var last = run.Steps.OrderBy(s => s.Sequence).Last();
        Assert.Equal(("Analyst", StepKind.Failed), (last.Agent, last.Kind));
        Assert.DoesNotContain("Writer", _llm.Calls);
    }
}
