using System.Threading.Channels;
using AgentFlow.Api.Data;
using AgentFlow.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentFlow.Api.Services;

public class RunQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid runId) => _channel.Writer.TryWrite(runId);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

// Kör köade körningar en i taget i bakgrunden, så att Groqs gratiskvot räcker längre.
public class RunWorker(IServiceProvider services, RunQueue queue, RunOrchestrator orchestrator, ILogger<RunWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await ResumeAsync(ct);

        await foreach (var runId in queue.ReadAllAsync(ct))
        {
            try
            {
                await orchestrator.ExecuteAsync(runId, ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                logger.LogError(e, "Körning {RunId} kraschade.", runId);
            }
        }
    }

    // Körningar som pågick när servern stängdes av markeras som avbrutna; köade körs igen.
    private async Task ResumeAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentFlowDbContext>();
        foreach (var run in await db.Runs.Where(r => r.Status == RunStatus.Running).ToListAsync(ct))
        {
            run.Status = RunStatus.Failed;
            run.Error = "Avbröts när servern startades om.";
            run.FinishedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        foreach (var id in await db.Runs.Where(r => r.Status == RunStatus.Queued).OrderBy(r => r.CreatedAt).Select(r => r.Id).ToListAsync(ct))
            queue.Enqueue(id);
    }
}
