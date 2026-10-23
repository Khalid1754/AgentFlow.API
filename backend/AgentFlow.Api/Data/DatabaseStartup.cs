using Microsoft.EntityFrameworkCore;

namespace AgentFlow.Api.Data;

public static class DatabaseStartup
{
    // Skapar/uppdaterar databasen vid start. SQL Server i Docker kan behöva några sekunder extra,
    // så vi försöker igen en stund innan vi ger upp.
    public static async Task MigrateAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AgentFlowDbContext>();
                if (db.Database.IsRelational())
                    await db.Database.MigrateAsync(ct);
                else
                    await db.Database.EnsureCreatedAsync(ct);
                return;
            }
            catch (Exception e) when (attempt < 10 && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Databasen svarar inte än ({Message}), försöker igen om 3 s.", e.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }
}
