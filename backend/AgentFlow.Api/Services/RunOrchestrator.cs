using AgentFlow.Api.Agents;
using AgentFlow.Api.Data;
using AgentFlow.Api.Models;

namespace AgentFlow.Api.Services;

// Styr körningen: Researcher → Analyst → Writer → Reviewer, och tillbaka till Writer om Reviewer
// underkänner rapporten (högst MaxRevisions gånger). Varje steg sparas och skickas live till webbläsaren.
public class RunOrchestrator(
    IServiceScopeFactory scopes, RunEvents events,
    Researcher researcher, Analyst analyst, Writer writer, Reviewer reviewer,
    ILogger<RunOrchestrator> logger)
{
    public const int MaxRevisions = 1;

    public async Task ExecuteAsync(Guid runId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentFlowDbContext>();
        var run = await db.Runs.FindAsync([runId], ct);
        if (run is null || run.Status != RunStatus.Queued) return;

        var log = new StepLog(db, run, events);
        await log.SetStatusAsync(RunStatus.Running, ct);
        var current = Researcher.Name;
        try
        {
            await log.AddAsync(Researcher.Name, StepKind.Started, "Planerar sökningar och letar information", ct: ct);
            var research = await researcher.ResearchAsync(run.Question, ct);
            await log.AddAsync(Researcher.Name, StepKind.Completed,
                $"Hittade {research.Sources.Count} källor med {research.Queries.Count} sökningar",
                DescribeResearch(research), ct: ct);
            await log.AddAsync(Researcher.Name, StepKind.Handoff, $"Lämnar över {research.Sources.Count} källor", to: Analyst.Name, ct: ct);

            current = Analyst.Name;
            await log.AddAsync(Analyst.Name, StepKind.Started, "Läser källorna och plockar ut fakta", ct: ct);
            var analysis = await analyst.AnalyzeAsync(run.Question, research.Sources, ct);
            await log.AddAsync(Analyst.Name, StepKind.Completed,
                $"{analysis.Findings.Length} slutsatser och {analysis.Gaps.Length} luckor", Analyst.Format(analysis), ct: ct);
            await log.AddAsync(Analyst.Name, StepKind.Handoff, "Lämnar över analysen", to: Writer.Name, ct: ct);

            string? report = null;
            string? feedback = null;
            for (var revision = 0; ; revision++)
            {
                current = Writer.Name;
                await log.AddAsync(Writer.Name, StepKind.Started,
                    feedback is null ? "Skriver rapporten" : "Skriver om rapporten efter granskningen", ct: ct);
                report = await writer.WriteAsync(run.Question, analysis, research.Sources, report, feedback, ct);
                await log.AddAsync(Writer.Name, StepKind.Completed,
                    feedback is null ? "Första utkastet är klart" : "Ny version är klar", report, ct: ct);
                await log.AddAsync(Writer.Name, StepKind.Handoff, "Lämnar över rapporten för granskning", to: Reviewer.Name, ct: ct);

                current = Reviewer.Name;
                await log.AddAsync(Reviewer.Name, StepKind.Started, "Granskar rapporten", ct: ct);
                var review = await reviewer.ReviewAsync(run.Question, analysis, report, ct);
                if (review.Approved || revision >= MaxRevisions)
                {
                    await log.AddAsync(Reviewer.Name, StepKind.Completed,
                        review.Approved ? "Rapporten är godkänd" : "Godkänd med anmärkningar (max antal omskrivningar nått)",
                        string.IsNullOrWhiteSpace(review.Feedback) ? null : review.Feedback, ct: ct);
                    break;
                }
                feedback = review.Feedback;
                await log.AddAsync(Reviewer.Name, StepKind.Completed, "Rapporten behöver förbättras", feedback, ct: ct);
                await log.AddAsync(Reviewer.Name, StepKind.Handoff, "Skickar tillbaka till Writer med synpunkter", to: Writer.Name, ct: ct);
            }

            run.Report = report;
            await log.SetStatusAsync(RunStatus.Completed, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogError(e, "{Agent} misslyckades i körning {RunId}.", current, runId);
            var message = e is LlmException ? e.Message : "Något gick fel på servern.";
            if (message.Length > 500) message = message[..500];
            await log.AddAsync(current, StepKind.Failed, message, ct: CancellationToken.None);
            run.Error = message;
            await log.SetStatusAsync(RunStatus.Failed, CancellationToken.None);
        }
    }

    private static string DescribeResearch(ResearchResult r) => $"""
        **Sökningar:** {string.Join(", ", r.Queries.Select(q => $"\"{q}\""))}

        **Källor**
        {(r.Sources.Count > 0 ? string.Join("\n", r.Sources.Select((s, i) => $"{i + 1}. [{s.Title}]({s.Url})")) : "Inga träffar.")}
        """;

    private sealed class StepLog(AgentFlowDbContext db, Run run, RunEvents events)
    {
        private int _sequence = run.Steps.Count;

        public async Task AddAsync(string agent, StepKind kind, string summary, string? detail = null, string? to = null,
            CancellationToken ct = default)
        {
            var step = new AgentStep
            {
                RunId = run.Id, Sequence = ++_sequence, Agent = agent, ToAgent = to, Kind = kind,
                Summary = summary.Length > 500 ? summary[..500] : summary, Detail = detail,
            };
            db.Steps.Add(step);
            await db.SaveChangesAsync(ct);
            events.Publish(run.Id, RunEvent.ForStep(step));
        }

        public async Task SetStatusAsync(RunStatus status, CancellationToken ct)
        {
            run.Status = status;
            if (status is RunStatus.Completed or RunStatus.Failed) run.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            events.Publish(run.Id, RunEvent.ForStatus(run));
        }
    }
}
