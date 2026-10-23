using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AgentFlow.Api.Data;
using AgentFlow.Api.Models;
using AgentFlow.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AgentFlow.Api.Endpoints;

public static class RunEndpoints
{
    public const int MinQuestionLength = 5;
    public const int MaxQuestionLength = 500;

    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/status", (ILlm llm) => new { aiEnabled = llm.Enabled });

        var runs = api.MapGroup("/runs");

        runs.MapGet("/", async (AgentFlowDbContext db, CancellationToken ct) =>
            await db.Runs.AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .Select(r => new RunSummaryDto(r.Id, r.Question, r.Status, r.CreatedAt, r.FinishedAt))
                .ToListAsync(ct));

        runs.MapPost("/", async (CreateRunRequest request, AgentFlowDbContext db, RunQueue queue, CancellationToken ct) =>
        {
            var question = request.Question?.Trim() ?? "";
            if (question.Length is < MinQuestionLength or > MaxQuestionLength)
                return Results.BadRequest(new { error = $"Frågan måste vara {MinQuestionLength}–{MaxQuestionLength} tecken." });

            var run = new Run { Question = question };
            db.Runs.Add(run);
            await db.SaveChangesAsync(ct);
            queue.Enqueue(run.Id);
            return Results.Created($"/api/runs/{run.Id}", RunDto.From(run));
        });

        runs.MapGet("/{id:guid}", async (Guid id, AgentFlowDbContext db, CancellationToken ct) =>
            await db.Runs.AsNoTracking().Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == id, ct) is { } run
                ? Results.Ok(RunDto.From(run))
                : Results.NotFound());

        runs.MapDelete("/{id:guid}", async (Guid id, AgentFlowDbContext db, CancellationToken ct) =>
        {
            var run = await db.Runs.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (run is null) return Results.NotFound();
            if (run.Status == RunStatus.Running)
                return Results.Conflict(new { error = "Körningen pågår, vänta tills den är klar." });
            db.Runs.Remove(run);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Server-Sent Events: först allt som redan hänt, sedan nya steg live tills körningen är klar.
        runs.MapGet("/{id:guid}/events", (Guid id, AgentFlowDbContext db, RunEvents events, CancellationToken ct) =>
            TypedResults.ServerSentEvents(StreamAsync(id, db, events, ct)));
    }

    internal static async IAsyncEnumerable<SseItem<string>> StreamAsync(Guid id, AgentFlowDbContext db, RunEvents events,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Prenumerera innan databasen läses, så att inget steg hamnar mellan stolarna.
        using var subscription = events.Subscribe(id);
        var run = await db.Runs.AsNoTracking().Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) yield break;

        var lastSequence = 0;
        foreach (var step in run.Steps.OrderBy(s => s.Sequence))
        {
            lastSequence = step.Sequence;
            yield return Item(RunEvent.ForStep(step));
        }
        yield return Item(RunEvent.ForStatus(run));
        if (IsFinished(run.Status)) yield break;

        await foreach (var e in subscription.Reader.ReadAllAsync(ct))
        {
            if (e.Step is { } s)
            {
                if (s.Sequence <= lastSequence) continue;
                lastSequence = s.Sequence;
            }
            yield return Item(e);
            if (e.Status is { } status && IsFinished(status)) yield break;
        }
    }

    private static bool IsFinished(RunStatus status) => status is RunStatus.Completed or RunStatus.Failed;

    private static SseItem<string> Item(RunEvent e) => new(JsonSerializer.Serialize(e, ApiJson.Options), e.Type);
}
